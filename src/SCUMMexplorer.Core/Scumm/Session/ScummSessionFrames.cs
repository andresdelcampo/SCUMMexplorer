using System.Globalization;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    /// <summary>
    /// Jiffies (1/60 s) per frame: ScummVM's main loop waits <c>VAR_TIMER_NEXT</c>
    /// (<c>var[0x013]</c>) jiffies, at least 1. The boot sets 6 (script 001 +0x0017).
    /// </summary>
    public int FrameJiffies => Math.Max(1, _runtime.GlobalVariables.TryGetValue(TimerNextVariable, out var next) ? next : 0);

    protected const string TimerNextVariable = ScummVars.TimerNext;

    /// <summary>ScummVM v3 <c>VAR_TMR_1</c>..<c>VAR_TMR_3</c> (vars 11–13): jiffies the frame loop adds every frame.</summary>
    protected static readonly string[] EngineTimerVariables = [ScummVars.Timer1, ScummVars.Timer2, ScummVars.Timer3];

    /// <summary>ScummVM v3 <c>VAR_TIMER</c> (var 46): the jiffies of the last frame.</summary>
    protected const string TimerVariable = ScummVars.Timer;

    /// <summary>ScummVM v3 <c>VAR_TIMER_TOTAL</c> (var 47): jiffies since the start.</summary>
    protected const string TimerTotalVariable = ScummVars.TimerTotal;

    /// <summary>ScummVM <c>scummLoop</c> clamps the jiffies it counts script delays and talk down by to 15.</summary>
    protected const int MaxFrameDelta = 15;

    /// <summary>
    /// The start of ScummVM's <c>scummLoop(delta)</c> for v3: <c>VAR_TIMER</c> = delta, <c>VAR_TIMER_TOTAL</c> and
    /// <c>VAR_TMR_1..3</c> += delta, then <c>decreaseScriptDelay(min(delta, 15))</c>: each paused slot's delay goes down,
    /// and a slot whose delay falls below 0 runs again this frame.
    /// </summary>
    protected void UpdateTimersAndDelays(int delta)
    {
        _runtime.SetGlobalVariable(TimerVariable, (ushort)delta);
        foreach (var timer in EngineTimerVariables.Append(TimerTotalVariable))
            if (_runtime.GlobalVariables.TryGetValue(timer, out var value))
                _runtime.SetGlobalVariable(timer, (ushort)(value + delta));
        var counted = Math.Min(delta, MaxFrameDelta);
        foreach (var item in _pending)
        {
            if (item.Frame.Delay is not int delay) continue;
            delay -= counted;
            item.Frame.Delay = delay < 0 ? null : delay;
            if (delay < 0) _visitTrace.Add($"{item.Frame.Label}'s delay ends; it runs again at +0x{item.Offset:X4}");
        }
    }

    /// <summary>Frames a continued cutscene may run (about 33 minutes at 10 frames a second) before the preview stops following it.</summary>
    public const int CutsceneFrameLimit = 20000;

    protected int _musicTimer;

    /// <summary>
    /// scummLoop's <c>updateMusicTimer</c>: <c>VAR_MUSIC_TIMER</c> (var[0x00E]) follows the markers of the playing Roland music, which the opening
    /// (script 044) waits on to time its fades. Cards whose sounds carry no markers leave the variable alone.
    /// </summary>
    protected virtual void UpdateMusicTimer()
    {
        var timer = _soundClock.MusicTimer();
        if (timer == 0 && _musicTimer == 0) return;
        _musicTimer = timer;
        _runtime.SetGlobalVariable(MusicTimerVariable, (ushort)timer);
    }

    protected const string MusicTimerVariable = ScummVars.MusicTimer;

    protected ScummSessionSnapshot AdvanceOneFrame()
    {
        // scummLoop (v3): the talk delay goes down by the frame's jiffies and displayDialog
        // runs before the scripts; VAR_HAVE_MSG takes _haveMsg.
        // processActors ends the previous frame: animateCostume steps the limbs of the actors in the room.
        _runtime.Actors.AnimateCostumes(CurrentPlacement());
        UpdateTimersAndDelays(FrameJiffies);
        foreach (var sound in _soundClock.Advance(FrameJiffies))
        {
            _runtime.ActiveSounds.Remove(sound);
            _visitTrace.Add($"Sound {sound:00} completed on its native AdLib resource clock.");
        }
        UpdateMusicTimer();
        _runtime.Messages.StartFrame(FrameJiffies, _runtime);
        PlaceVirtualMouse();
        var beforeEvents = _visitEvents.Count;
        var runnable = _pending.ToArray();
        _pending.Clear();
        _frameScripts.AddRange(runnable);
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            foreach (var item in runnable)
            {
                _frameScripts.Remove(item);
                // A room load earlier in this frame ended the room's scripts.
                if (item.RoomId.HasValue && item.RoomId != _currentRoom?.ResourceId) continue;
                // An earlier script in this frame may have stopped this one.
                if (item.Frame.Stopped) continue;
                // runAllScripts skips frozen and paused (delayed) slots; they keep their place.
                if (item.Frame.FreezeCount > 0 || item.Frame.Delay is not null)
                {
                    _pending.Add(item);
                    continue;
                }
                // Every script works on the current room's objects (ScummVM _objs): a global
                // script pended in an earlier room must not see that room's objects.
                RunScript(item.Script, _currentObjects, item.Offset, item.Locals, 0, item.ScriptNumber,
                    item.EstimatedLocals, item.PathEstimated, item.Frame);
                DispatchRequestedLoads("after a frame advance");
            }
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
            _frameScripts.Clear();
        }
        StepWalksAndCamera();
        var roomLoad = _visitEvents.Skip(beforeEvents).FirstOrDefault(item =>
            IsExecutedRoomLoad(item) &&
            !_dispatchedLoads.Contains(item));
        if (roomLoad is null || _currentRoom is null) return Snapshot;
        var target = roomLoad.TargetId is int id ? _game.Rooms.FirstOrDefault(item => item.ResourceId == id) : null;
        if (target is null)
        {
            _uncertainties.Add($"Reached room load has no indexed destination ({roomLoad.Operands}).");
            return Snapshot;
        }
        EnterReachedLoad(roomLoad, target, null, "after a frame advance");
        return Snapshot;
    }

    /// <summary>
    /// The end of ScummVM's <c>scummLoop</c> after the scripts: <c>walkActors</c> steps every walking or turning actor
    /// in the room (<see cref="ScummActorTable.StepWalks"/>), then <c>moveCamera</c> moves the camera up to 8 pixels
    /// (<see cref="ScummCamera.Move"/>); the next frame's <c>processActors</c> draws and animates them.
    /// </summary>
    protected void StepWalksAndCamera()
    {
        if (_currentRoom is null) return;
        var place = CurrentPlacement();
        _runtime.Actors.StepWalks(place);
        if (!_runtime.GlobalVariables.TryGetValue(CameraVariable, out var camera) || _runtime.RoomCameraBounds is not { } bounds) return;
        var (min, max) = _runtime.CameraRange ?? bounds;
        var cameraState = _runtime.Camera;
        ScummActorSnapshot? followed = null;
        if (cameraState.Mode == ScummCamera.FollowActor || cameraState.MovingToActor)
            followed = ScummActorTable.IsValid(cameraState.Follows) ? _runtime.Actors[cameraState.Follows] : null;
        var moved = cameraState.Move(camera, min, max, bounds, followed?.X);
        if (moved == camera) return;
        _runtime.SetGlobalVariable(CameraVariable, (ushort)moved);
        if (followed?.Estimate is not null && _runtime.EstimatedVariables.Add(CameraVariable))
            _uncertainties.Add($"Camera x (var[0x002]) = {moved} is an estimate: it follows actor {followed.Number}, whose position is an estimate ({followed.Estimate}).");
    }

    /// <summary>True while an actor in the current room walks or turns (ScummVM <c>_moving</c>).</summary>
    public bool ActorsMoving => _runtime.Actors.Snapshot().Any(actor => actor.Moving != 0 && actor.Room == _currentRoom?.ResourceId);

    /// <summary>
    /// True while the ego (<c>VAR_EGO</c>) walks or turns. Playback waits for this, not for every actor: ambient
    /// actors (script 042's swan speck in Room 02) walk from point to point without end.
    /// </summary>
    public bool EgoMoving => _runtime.GlobalVariables.TryGetValue(EgoVariable, out var ego) && ScummActorTable.IsValid(ego) &&
                             _runtime.Actors[ego] is { Moving: not 0 } actor && actor.Room == _currentRoom?.ResourceId;

    /// <summary>
    /// True while <c>moveCamera</c> would still move the camera: it is not on the strip of its target (the followed
    /// actor while moving to it, else its destination), with the target kept inside the camera's limits. A camera held
    /// at a room edge short of its actor does not count, although ScummVM keeps <c>_movingToActor</c> set then.
    /// </summary>
    public bool CameraMoving
    {
        get
        {
            if (CameraX is not int x || _runtime.RoomCameraBounds is not { } bounds) return false;
            var (min, max) = _runtime.CameraRange ?? bounds;
            var camera = _runtime.Camera;
            var target = camera.DestX;
            if (camera.MovingToActor && ScummActorTable.IsValid(camera.Follows) && _runtime.Actors[camera.Follows].X is int actorX)
                target = actorX;
            target = Math.Clamp(Math.Clamp(target, min, Math.Max(min, max)), bounds.Min, bounds.Max);
            return x / 8 != target / 8;
        }
    }

    /// <summary>
    /// Advances frames while scripts are pending, until the session has left
    /// the room and no cutscene is open. Frames outside a cutscene count
    /// against <paramref name="maxFrames"/>; frames inside one count against
    /// <paramref name="maxCutsceneFrames"/>, because the player has no control
    /// until the cutscene ends. Returns true when the session is in another room.
    /// Since messages last their talk delay (<see cref="ScummMessageTable"/>),
    /// cutscenes take the game's number of frames: the Elders' trial (script 046,
    /// from Room 08's door) runs 2,120 frames, about 3.5 minutes at 10 frames a
    /// second, so the default limit is <see cref="CutsceneFrameLimit"/>.
    /// </summary>
    public bool ContinueUntilRoomChanges(int maxFrames, int maxCutsceneFrames = CutsceneFrameLimit)
    {
        _uncertainties.StartStep();
        var room = _currentRoom?.ResourceId;
        var frames = 0;
        var cutsceneFrames = 0;
        while (_pending.Count > 0)
        {
            var inCutscene = InCutscene;
            if (!inCutscene && _currentRoom?.ResourceId != room) break;
            if (inCutscene ? cutsceneFrames++ >= maxCutsceneFrames : frames++ >= maxFrames)
            {
                if (inCutscene)
                    _uncertainties.Add($"A cutscene was still open after {maxCutsceneFrames} continued frames; the preview stopped following it.");
                break;
            }
            AdvanceFrame();
        }
        return _currentRoom?.ResourceId != room;
    }


    /// <summary>
    /// Enters the room a global script loaded and resumes that script there in
    /// the same frame. In ScummVM, <c>startScene</c> ends room, object and local
    /// scripts, but a global script returns from <c>loadRoom</c> and carries on
    /// (a cutscene can pass through several rooms). Returns true when a room was entered.
    /// </summary>
    protected bool DispatchRequestedLoads(string when)
    {
        var entered = false;
        for (var hop = 0; hop < MaxNestedScripts && _requestedLoad is { } request; hop++)
        {
            _requestedLoad = null;
            var item = _pending.FirstOrDefault(pending => pending.Frame == request.Frame);
            if (item is null) continue;
            _pending.Remove(item);
            _dispatchedLoads.Add(request.Event);
            var target = _game.Rooms.First(room => room.ResourceId == request.Event.TargetId);
            EnterReachedLoad(request.Event, target, request.Frame, when);
            entered = true;
            _visitTrace.Add($"{request.Event.SourceLabel} continues at +0x{item.Offset:X4} in Room {target.ResourceId:00} after {request.Event.Operation}");
            _dispatchScope++;
            try
            {
                RunScript(item.Script, _currentObjects, item.Offset, item.Locals, 0, item.ScriptNumber,
                    item.EstimatedLocals, item.PathEstimated, item.Frame);
            }
            finally
            {
                _dispatchScope--;
            }
        }
        return entered;
    }

    /// <summary>
    /// Visits the room a reached load names and records it as the outcome of
    /// the last action when it follows from it: the action's own script, or a
    /// later load by the same global script that carried the action's first load.
    /// </summary>
    protected void EnterReachedLoad(ScummScriptEvent roomLoad, GameResource target, ScriptFrame? frame, string when)
    {
        if (_currentRoom is null || _booting)
        {
            _actionLoadFrame = frame;
            _uncertainties.Add($"{roomLoad.SourceLabel} +0x{roomLoad.Offset:X4} reached {roomLoad.Operation} {when}.");
            if (target.ResourceId != _currentRoom?.ResourceId)
                EnterRoom(target, $"Room {target.ResourceId:00} entered by {roomLoad.SourceLabel} +0x{roomLoad.Offset:X4} ({roomLoad.Operation}) {when}.", roomLoad);
            return;
        }
        var sourceRoomId = _currentRoom.ResourceId ?? 0;
        var beforeUncertainties = _uncertainties.Count;
        ScummObjectActionResult action;
        if (LastAction is { ReachedRoomLoad: null } pending && pending.SourceRoomId == sourceRoomId)
            action = pending with { ReachedRoomLoad = roomLoad, DestinationRoomId = target.ResourceId };
        else if (frame is not null && frame == _actionLoadFrame &&
                 LastAction is { ReachedRoomLoad: { } earlier } last && last.DestinationRoomId == sourceRoomId)
        {
            _uncertainties.Add($"{earlier.SourceLabel} passed through Room {sourceRoomId:00} ({earlier.Operation} at +0x{earlier.Offset:X4}) before this load.");
            action = last with { ReachedRoomLoad = roomLoad, DestinationRoomId = target.ResourceId };
        }
        else
            action = new ScummObjectActionResult(sourceRoomId, 0, 0, roomLoad.Offset, null, null, roomLoad,
                target.ResourceId, []);
        _actionLoadFrame = frame;
        _uncertainties.Add($"{roomLoad.SourceLabel} +0x{roomLoad.Offset:X4} reached {roomLoad.Operation} {when}.");
        VisitReachedRoom(target, action);
        LastAction = action with { Uncertainties = [.. action.Uncertainties, .. _uncertainties.Skip(beforeUncertainties)] };
    }


    /// <summary>Visits a room a reached load names, with the action that led there (the derived session may add its own set-up around the visit).</summary>
    protected abstract ScummSessionSnapshot VisitReachedRoom(GameResource target, ScummObjectActionResult action);

    /// <summary>Continues every pending script by one frame; a derived session adds its own per-frame work.</summary>
    public virtual ScummSessionSnapshot AdvanceFrame() => AdvanceOneFrame();
}

