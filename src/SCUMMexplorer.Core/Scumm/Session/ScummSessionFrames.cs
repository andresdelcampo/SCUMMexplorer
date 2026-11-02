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

    protected string TimerNextVariable => _runtime.IsV2 ? ScummVars.V2.TimerNext : ScummVars.TimerNext;

    /// <summary>ScummVM v3 <c>VAR_TMR_1</c>..<c>VAR_TMR_3</c> (vars 11–13): jiffies the frame loop adds every frame.</summary>
    protected IEnumerable<string> EngineTimerVariables => _runtime.IsV2 ? [] : [ScummVars.Timer1, ScummVars.Timer2, ScummVars.Timer3, .. _extraTimerVariables];

    // The timers some games have besides these (ScummProfile.ExtraTimerVariables); scummLoop adds the frame's jiffies to them too.
    private readonly IReadOnlyList<string> _extraTimerVariables = [];

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
        if (!_runtime.IsV2) _runtime.SetGlobalVariable(TimerVariable, (ushort)delta);
        foreach (var timer in EngineTimerVariables.Concat(_runtime.IsV2 ? [] : new[] { TimerTotalVariable }))
            if (_runtime.GlobalVariables.TryGetValue(timer, out var value))
                _runtime.SetGlobalVariable(timer, (ushort)(value + delta));
        var counted = Math.Min(delta, MaxFrameDelta);
        foreach (var item in _pending)
        {
            if (item.Frame.Delay is not int delay) continue;
            // A picked scene's own scripts do not wait out their long timers (HurryUntilCutscene): time passes for them at once. A pause under
            // ten seconds (600 jiffies) is part of the scene and runs as the game times it.
            if (_hurried.Count > 0 && item.ScriptNumber is int hurried && _hurried.Contains(hurried) && delay >= HurryFromJiffies)
            {
                _uncertainties.Add($"{item.Frame.Label} waited {delay} jiffies (delay); for the picked scene that time passed at once (an exploration action).");
                delay = counted;
            }
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

    protected string MusicTimerVariable => _runtime.MusicTimerVariable;

    protected ScummSessionSnapshot AdvanceOneFrame()
    {
        StepOneFrame();
        return Snapshot;
    }

    /// <summary>One frame of <see cref="AdvanceOneFrame"/> without building the snapshot (a headless run that never reads it: the cutscene derivation).</summary>
    protected void StepOneFrame()
    {
        _inventoryStatusFrame++;
        StepDeferredBoxPuts();
        FeedWaitingInput();
        // scummLoop (v3): the talk delay goes down by the frame's jiffies and displayDialog
        // runs before the scripts; VAR_HAVE_MSG takes _haveMsg.
        // processActors ends the previous frame: animateCostume steps the limbs of the actors in the room.
        _runtime.Actors.AnimateCostumes(CurrentPlacement());
        UpdateTimersAndDelays(FrameJiffies);
        foreach (var sound in _soundClock.Advance(FrameJiffies))
        {
            _runtime.ActiveSounds.Remove(sound);
            _visitTrace.Add($"Sound {sound:00} completed on its {(_runtime.IsV2 ? "Player_V2 control" : "native AdLib resource")} clock.");
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
            // checkExecVerbs follows runAllScripts in scummLoop: the player's queued click or key runs the input script.
            DeliverQueuedInput();
            // checkAndRunSentenceScript follows runAllScripts and the input check in scummLoop.
            if (RunSentenceScript()) DispatchRequestedLoads("after a frame advance");
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
        if (roomLoad is null || _currentRoom is null) return;
        var target = roomLoad.TargetId is int id ? IndexedRoom(id) : null;
        if (target is null)
        {
            _uncertainties.Add($"Reached room load has no indexed destination ({roomLoad.Operands}).");
            return;
        }
        // S6 Indy4 (17 October 2026): setCameraFollows starts a scene only for an actor outside the current room. Room 04 local 200's endCutscene runs Script 21,
        // whose own actorFollowCamera (dispatched in this frame) has already entered Room 05; the caller's follow at +0x0093 then finds Indy there.
        if (roomLoad.Operation == "actorFollowCamera" && target.ResourceId == _runtime.CurrentRoom &&
            (!_runtime.IsV2 || roomLoad.TargetId == _runtime.GlobalVariables.GetValueOrDefault(ScummVars.Room)))
        {
            _visitTrace.Add($"{roomLoad.SourceLabel} +0x{roomLoad.Offset:X4}: actorFollowCamera finds the actor in Room {target.ResourceId:00} already; no second startScene");
            return;
        }
        EnterReachedLoad(roomLoad, target, null, "after a frame advance");
    }

    /// <summary>
    /// ScummVM <c>checkAndRunSentenceScript</c>: unless a copy of the sentence script (<c>VAR_SENTENCE_SCRIPT</c>) is running and not frozen,
    /// the last sentence <c>doSentence</c> queued is taken off the stack and the sentence script runs with its verb and two objects as
    /// locals 0 to 2 (as a script of no room: <c>_currentScript = 0xFF</c>). Returns true when it ran one.
    /// </summary>
    protected bool RunSentenceScript()
    {
        if (_runtime.Sentences.Count == 0) return false;
        var number = _runtime.IsV2 ? 2 : _runtime.GlobalVariables.GetValueOrDefault(ScummVars.SentenceScript);
        if (number != 0 && (_executing.Any(frame => frame.Number == number && !frame.Stopped && frame.FreezeCount == 0) ||
                            _pending.Any(item => item.ScriptNumber == number && !item.Frame.Stopped && item.Frame.FreezeCount == 0)))
            return false;
        var (verb, objectA, objectB) = _runtime.Sentences[^1];
        _runtime.Sentences.RemoveAt(_runtime.Sentences.Count - 1);
        if (number == 0) return false;
        _visitTrace.Add($"sentence script {number} runs verb 0x{verb:X2} with objects {objectA} and {objectB}");
        if (_runtime.IsV2)
        {
            _runtime.SetGlobalVariable(ScummVars.V2.ActiveVerb, (ushort)verb);
            _runtime.SetGlobalVariable(ScummVars.V2.ActiveObject1, (ushort)objectA);
            _runtime.SetGlobalVariable(ScummVars.V2.ActiveObject2, (ushort)objectB);
            _runtime.SetGlobalVariable(ScummVars.V2.VerbAllowed, (ushort)(ObjectVerbEntrypoint(objectA, verb) is > 0 ? 1 : 0));
        }
        RunStartedScript(number, 0, _runtime.IsV2 ? null : new Dictionary<string, ushort>
        {
            ["local[0x000]"] = (ushort)verb, ["local[0x001]"] = (ushort)objectA, ["local[0x002]"] = (ushort)objectB
        }, 0);
        return true;
    }

    private (int Mode, int Follows)? _panRestore;

    /// <summary>The camera limits the player may pan within (the game's own <c>VAR_CAMERA_MIN_X</c>/<c>MAX_X</c> of the room), or null outside a room.</summary>
    public (int Min, int Max)? CameraPanRange => _runtime.CameraRange ?? _runtime.RoomCameraBounds;

    /// <summary>
    /// R-6.49 (user, 25 Oct 2026): exploring a wide room while it is played. Moves the game's own camera by <paramref name="pixels"/> the way
    /// <c>panCameraTo</c> does (it travels 8 pixels a frame to the new place, inside the game's camera limits), so the picture and the camera stay one thing
    /// and every click is a valid point of the game's screen. A camera that followed an actor is put in normal mode until that actor walks again.
    /// An exploration action, not something the game did.
    /// </summary>
    public void PanCameraBy(int pixels)
    {
        if (CameraPanRange is not { } range || CameraX is not int x) return;
        var camera = _runtime.Camera;
        if (camera.Mode == ScummCamera.FollowActor && _panRestore is null) _panRestore = (camera.Mode, camera.Follows);
        var basis = _panRestore is not null && camera.Mode == ScummCamera.Normal ? camera.DestX : x;
        camera.Mode = ScummCamera.Normal;
        camera.MovingToActor = false;
        // The destination stays within a few frames of the camera, so letting go of the button stops the view soon after.
        camera.DestX = Math.Clamp(Math.Clamp(basis + pixels, x - 96, x + 96), range.Min, Math.Max(range.Min, range.Max));
    }

    /// <summary>R-6.49: puts the camera at <paramref name="roomX"/> at once (as <c>setCameraAt</c>), so a click on a place the game was not showing can be delivered.</summary>
    public void JumpCameraTo(int roomX)
    {
        if (CameraPanRange is not { } range) return;
        var camera = _runtime.Camera;
        if (camera.Mode == ScummCamera.FollowActor && _panRestore is null) _panRestore = (camera.Mode, camera.Follows);
        camera.Mode = ScummCamera.Normal;
        camera.MovingToActor = false;
        var target = Math.Clamp(roomX, range.Min, Math.Max(range.Min, range.Max));
        camera.DestX = target;
        _runtime.WriteCameraPixels(target);
    }

    /// <summary>
    /// The end of ScummVM's <c>scummLoop</c> after the scripts: <c>walkActors</c> steps every walking or turning actor
    /// in the room (<see cref="ScummActorTable.StepWalks"/>), then <c>moveCamera</c> moves the camera up to 8 pixels
    /// (<see cref="ScummCamera.Move"/>); the next frame's <c>processActors</c> draws and animates them.
    /// </summary>
    protected void StepWalksAndCamera()
    {
        if (_currentRoom is null) return;
        // R-6.49: a camera the user panned goes back to following its actor once that actor walks again (a script that sets another mode ends it too).
        if (_panRestore is { } restore)
        {
            if (_runtime.Camera.Mode != ScummCamera.Normal) _panRestore = null;
            else if (EgoMoving) { _runtime.Camera.Mode = restore.Mode; _runtime.Camera.Follows = restore.Follows; _panRestore = null; }
        }
        var place = CurrentPlacement();
        _runtime.Actors.StepWalks(place);
        if (_runtime.ReadCameraPixels() is not int camera || _runtime.RoomCameraBounds is not { } bounds) return;
        var (min, max) = _runtime.CameraRange ?? bounds;
        var cameraState = _runtime.Camera;
        ScummActorSnapshot? followed = null;
        if (cameraState.Mode == ScummCamera.FollowActor || cameraState.MovingToActor)
            followed = _runtime.Actors.Contains(cameraState.Follows) ? _runtime.Actors[cameraState.Follows] : null;
        var moved = cameraState.Move(camera, min, max, bounds, followed?.X);
        if (moved == camera) return;
        _runtime.WriteCameraPixels(moved);
        if (followed?.Estimate is not null && _runtime.EstimatedVariables.Add(CameraVariable))
            _uncertainties.Add($"Camera x (var[0x002]) = {moved} is an estimate: it follows actor {followed.Number}, whose position is an estimate ({followed.Estimate}).");
    }

    /// <summary>True while an actor in the current room walks or turns (ScummVM <c>_moving</c>).</summary>
    public bool ActorsMoving => _runtime.Actors.Snapshot().Any(actor => actor.Moving != 0 && actor.Room == _currentRoom?.ResourceId);

    /// <summary>
    /// True while the ego (<c>VAR_EGO</c>) walks or turns. Playback waits for this, not for every actor: ambient
    /// actors (script 042's swan speck in Room 02) walk from point to point without end.
    /// </summary>
    public bool EgoMoving => _runtime.GlobalVariables.TryGetValue(EgoVariable, out var ego) && _runtime.Actors.Contains(ego) &&
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
            if (camera.MovingToActor && _runtime.Actors.Contains(camera.Follows) && _runtime.Actors[camera.Follows].X is int actorX)
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
            var target = request.Event.TargetId == NullRoom ? null : IndexedRoom(request.Event.TargetId!.Value);
            // The script that loaded the room is still a live slot while the room is entered (ScummVM startScene runs inside the script's
            // own opcode), so the room's entry code sees it with isScriptRunning.
            // S6 Indy4 (17 October 2026): setCameraFollows starts a scene only for an actor outside the current room. When the deferred follow is dispatched
            // after the script that called it nested (Room 04 local 200's endCutscene ran Script 21) has itself entered that room, the room is not entered twice.
            if (request.Event.Operation == "actorFollowCamera" && target is not null && target.ResourceId == _runtime.CurrentRoom &&
                (!_runtime.IsV2 || request.Event.TargetId == _runtime.GlobalVariables.GetValueOrDefault(ScummVars.Room)))
                _visitTrace.Add($"{request.Event.SourceLabel} +0x{request.Event.Offset:X4}: actorFollowCamera finds the actor in Room {target.ResourceId:00} already; no second startScene");
            else
            {
                _loadingFrames.Add(request.Frame);
                try
                {
                    if (target is null) EnterNullRoom(request.Event, when);
                    else EnterReachedLoad(request.Event, target, request.Frame, when);
                }
                finally
                {
                    _loadingFrames.Remove(request.Frame);
                }
                entered = true;
            }
            _visitTrace.Add($"{request.Event.SourceLabel} continues at +0x{item.Offset:X4} in Room {target?.ResourceId ?? NullRoom:00} after {request.Event.Operation}");
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

    /// <summary>
    /// Opens a room from the catalog: entered from the state the session has, its entry scripts run. A game's session says how the hero arrives;
    /// by itself the room is only entered (a state preview).
    /// </summary>
    public virtual ScummSessionSnapshot VisitRoom(GameResource room)
    {
        ArgumentNullException.ThrowIfNull(room);
        _uncertainties.StartStep();
        EndOpeningRoute();
        return EnterRoom(room, $"Room {room.ResourceId:00} opened from the catalog (an exploration action; state preview).");
    }

    /// <summary>Continues every pending script by one frame; a derived session adds its own per-frame work.</summary>
    public virtual ScummSessionSnapshot AdvanceFrame() => AdvanceOneFrame();

    /// <summary>
    /// <see cref="AdvanceFrame"/> for a caller that does not read the frame's snapshot (the cutscene runs, the planner's verb search):
    /// the same frame, without copying the session's states, variables and lists into a snapshot nobody reads. A session that adds per-frame work of its own
    /// in <see cref="AdvanceFrame"/> (LOOM's staff verbs) leaves it out here.
    /// </summary>
    public void StepFrame() => StepOneFrame();

    /// <summary>
    /// Plays a room's entry again (S7, 17 October 2026, to play a room's entry cutscene): a room the session is not in is visited as
    /// <see cref="VisitRoom"/> does; the room it is in is loaded again as a <c>loadRoom</c> of the current room would (ScummVM <c>startScene</c>: the
    /// exit code, then the entry hook and entry script from the state the session has). An exploration action and a state preview, never a reached outcome.
    /// </summary>
    public ScummSessionSnapshot EnterRoomAgain(GameResource room)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (_currentRoom?.ResourceId != room.ResourceId) return VisitRoom(room);
        _uncertainties.StartStep();
        EndOpeningRoute();
        var label = $"Room {room.ResourceId:00} entered again to play its entry scripts (an exploration action, as a loadRoom of the current room would; state preview).";
        _uncertainties.Add(label);
        // A picked scene's setup may want the room loaded under a pseudo room number (its entry code reads VAR_ROOM), or the hero to come in by an
        // object (VAR_WALKTO_OBJ, as loadRoomWithEgo sets it).
        var number = room.ResourceId!.Value;
        if (WantedPseudoRoom is int pseudo && _runtime.PseudoRooms.GetValueOrDefault(pseudo) == room.ResourceId)
        {
            WantedPseudoRoom = null;
            number = pseudo;
        }
        if (WantedArrivalObject is int door)
        {
            WantedArrivalObject = null;
            return EnterRoom(room, label, new ScummScriptEvent(0, "explorer visit", 0, ScummScriptEventKind.RoomLoaded, "loadRoomWithEgo",
                $"the hero brought in by object {door} for a picked scene", ResourceKind.Room, number, ScummScriptEventCertainty.Conditional,
                ResolvedArguments: [door, number, -1, -1]));
        }
        if (number != room.ResourceId)
            return EnterRoom(room, label, new ScummScriptEvent(0, "explorer visit", 0, ScummScriptEventKind.RoomLoaded, "loadRoom",
                $"load of pseudo room {number} for a picked scene", ResourceKind.Room, number, ScummScriptEventCertainty.Conditional, ResolvedArguments: [number]));
        return EnterRoom(room, label);
    }
}

