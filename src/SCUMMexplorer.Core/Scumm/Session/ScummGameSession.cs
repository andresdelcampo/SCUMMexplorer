using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The data-driven concrete session: any game whose boot is the engine's (indexed script 1 runs at start, ScummVM
/// <c>runBootscript</c>) and whose behaviour comes from its own scripts and the <see cref="ScummProfile"/>. It adds nothing a
/// game-specific session would: no staff, drafts or catalog. LOOM keeps <c>LoomExplorationSession</c> because of those; the other
/// games use this class (S4b, 14 October 2026; first game: Indiana Jones and the Last Crusade EGA).
/// </summary>
public sealed class ScummGameSession : ScummSession
{
    public ScummGameSession(GameLibrary game, ScummIndexedObjectSnapshot indexedObjects, int randomSeed = 0,
        ScummScriptObserver? observer = null, IScummDataSource? data = null, bool boot = true)
        : base(game, indexedObjects, randomSeed, observer, data,
            AdlibLifetime(game))
    {
        // ScummEngine::resetScummVars (vars.cpp): the sound card the session plays through (AdLib is 3), the video mode, the text speed.
        var profile = ScummProfile.For(game);
        SoundCard = AdlibCard;
        if (profile.VideoMode is int videoMode) _runtime.SetGlobalVariable(ScummVars.VideoMode, (ushort)videoMode);
        _runtime.SetGlobalVariable(ScummVars.CharInc, ScummProfile.CharIncAtStart);
        // resetScummVars (scumm.cpp): the lights start as base palette, actors' own colours and room lights on (1 | 8 | 2).
        _runtime.SetGlobalVariable(ScummVars.CurrentLights, 11);
        foreach (var (variable, value) in profile.EngineVariablesAtStart) _runtime.SetGlobalVariable(variable, value);
        // The engine allocates every global variable zeroed (scumm.cpp: _scummVars = calloc), so a variable no script has written yet reads 0, not "unknown".
        for (var variable = 0; variable < profile.GlobalVariableCount; variable++)
        {
            var key = $"var[0x{variable:X3}]";
            if (!_runtime.GlobalVariables.ContainsKey(key)) _runtime.SetGlobalVariable(key, 0);
        }
        _runtime.CameraFollowStartsScene = profile.CameraFollowStartsScene;
        _bootInput = profile.BootInput;
        _waitsForControl = profile.BootEndsWhenPlayerHasControl;
        if (boot) Boot();
    }

    /// <summary><c>VAR_SOUNDCARD</c> for AdLib (vars.cpp: 0 PC speaker, 1 Tandy, 2 CMS, 3 AdLib, 4 Roland).</summary>
    private const int AdlibCard = 3;

    // The session's device is AdLib (ScummVM's Player_AD for the old-bundle games): the native clock of a sound is the AdLib resource's own.
    // A resource that is not an old-bundle AdLib part has no established lifetime.
    private static Func<GameResource, ScummSoundLifetime> AdlibLifetime(GameLibrary game)
    {
        var timing = ScummProfile.For(game).AdlibTiming;
        var adlib = new ScummAdlibAudio();
        // A game on several v4 disks stores its sounds in the disks' SO blocks (the AD part is the AdLib song); the browsed disks play at half the LOOM tempo
        // (by ear, 11 October, the explorer's own S3 decision), so the session's music timer follows the same estimate.
        if (game.V4Disks is { } disks && disks.Disks.OfType<ScummV4Archive>().Any())
        {
            var adlibParts = new Dictionary<int, byte[]?>();
            foreach (var archive in disks.Disks.OfType<ScummV4Archive>())
                foreach (var sound in archive.ReadSounds())
                    adlibParts.TryAdd(sound.Id, sound.Adlib);
            return resource => resource.ResourceId is int id && adlibParts.GetValueOrDefault(id) is { } part
                ? adlib.ReadLifetime(part, timing, halfTempo: true)
                : new(ScummSoundLifetimeKind.Unknown, null, false, "no AdLib part in the disks' sound blocks");
        }
        return resource => adlib.ReadLifetime(resource, timing);
    }

    /// <summary>
    /// A room visit that does not play the route to the room (S5, 14 October 2026): the room is entered from the state the boot left, as ScummVM's
    /// <c>room</c> debugger command would, and its global entry hook, entry script and child scripts run. The ego arrives as one decoded
    /// <c>loadRoomWithEgo</c> into the room would bring it (a seeded, labelled pick among the distinct placements, those in the room the session
    /// is in first); no story state is written. The visit is a state preview, never a reached outcome.
    /// </summary>
    public ScummSessionSnapshot VisitRoom(GameResource room)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (room.Kind != ResourceKind.Room || room.ResourceId is not int roomId) throw new InvalidDataException("The session can only visit an indexed room.");
        if (_currentRoom?.ResourceId == roomId) return Snapshot;
        _uncertainties.StartStep();
        var arrival = $"Room {roomId:00} selected without playing the route to it (state preview): its entry scripts ran from the state the boot left.";
        _runtime.Messages.RouteSkipped($"Room {roomId:00} was entered without its route, so prints on the route to it did not run");
        if (!_runtime.GlobalVariables.TryGetValue(EgoVariable, out var ego) || !ScummActorTable.IsValid(ego)) return EnterRoom(room, arrival);
        var candidates = ScummEgoArrivalIndex.For(_game).Into(roomId);
        if (candidates.Count == 0)
        {
            _uncertainties.Add($"Room {roomId:00} has no decoded loadRoomWithEgo with literal operands that brings the ego into it, so the ego is not placed in it.");
            return EnterRoom(room, arrival);
        }
        var previous = _currentRoom?.ResourceId;
        var fromPrevious = candidates.Where(item => item.SourceRoom is int source && source == previous).ToArray();
        var pool = fromPrevious.Length > 0 ? fromPrevious : candidates.ToArray();
        var choices = pool.GroupBy(item => item.PlacementKey).Select(group => group.ToArray()).ToArray();
        var index = 0;
        string how;
        if (choices.Length == 1) how = "the only source-backed arrival" + (fromPrevious.Length > 0 ? $" in Room {previous:00}'s own code" : "");
        else
        {
            var pick = _runtime.Random.Pick($"arrival into Room {roomId:00}", 0, choices.Length - 1);
            index = pick.Value;
            how = $"seeded pick {index + 1} of {choices.Length} (seed {pick.Seed})" + (fromPrevious.Length > 0 ? $" among those in Room {previous:00}'s own code" : "");
        }
        var chosen = choices[index];
        var first = chosen[0];
        var label = $"Assumed arrival: the ego enters as {string.Join(" or ", chosen.Select(item => item.Describe()))} would bring it in ({how}). " +
                    "Which route led here was not played; the arrival is a possible outcome, not a reached one.";
        var load = new ScummScriptEvent(0, first.Source, first.Offset, ScummScriptEventKind.RoomLoaded, "loadRoomWithEgo",
            $"assumed arrival into Room {roomId:00}", ResourceKind.Room, roomId, ScummScriptEventCertainty.Conditional,
            ResolvedArguments: [first.ObjectId, roomId, first.WalkX, first.WalkY]);
        // o5_loadRoomWithEgo puts the ego in the room with the position it had (putActor(room)): the entry code sees it where the previous room left it.
        _uncertainties.Add($"Before its assumed arrival the ego keeps the position it had in the room the session was in (estimate: the route that would have moved it was not played).");
        return EnterRoom(room, $"{arrival} {label}", load, label);
    }

    /// <summary>
    /// Plays frames until the room has settled and says how it ended (S5): no cutscene, message or movement and no change in object states,
    /// positions, draw order or actors for <paramref name="quietFrames"/> frames; or the room was left (a script loaded another room); or the
    /// frame limit was reached (ambient scripts that never rest). The state of the room is the last one captured while the room was current.
    /// </summary>
    public ScummSettleResult Settle(int frameLimit = 2400, int quietFrames = 90)
    {
        var room = _currentRoom?.ResourceId ?? throw new InvalidOperationException("Visit a room before settling it.");
        var capture = Capture();
        // An object whose state or position changes four times within 240 frames is an ambient animator (a crowd, a flame): it never rests in a
        // living room, so it does not keep the room from settling; it is shown in the phase of its loop the room was in when the rest began.
        var changes = new Dictionary<int, Queue<int>>();
        var previous = ObjectValues();
        var signature = Signature(changes, 0);
        var quiet = 0;
        for (var frame = 1; frame <= frameLimit; frame++)
        {
            AdvanceFrame();
            if (_currentRoom?.ResourceId != room) return new(capture, ScummSettleOutcome.RoomLeft, frame, _currentRoom?.ResourceId);
            var now = ObjectValues();
            foreach (var (id, value) in now)
                if (!previous.TryGetValue(id, out var before) || before != value)
                {
                    if (!changes.TryGetValue(id, out var queue)) changes[id] = queue = new();
                    queue.Enqueue(frame);
                }
            foreach (var queue in changes.Values)
                while (queue.Count > 0 && queue.Peek() < frame - 240) queue.Dequeue();
            previous = now;
            var current = Signature(changes, frame);
            var busy = InCutscene || Messages.HaveMsg != 0 || EgoMoving || CameraMoving;
            capture = Capture();
            if (current != signature) { signature = current; quiet = 0; }
            else quiet = busy ? 0 : quiet + 1;
            if (quiet >= quietFrames) return new(capture, ScummSettleOutcome.Settled, frame, null);
        }
        return new(Capture(), ScummSettleOutcome.FrameLimit, frameLimit, null);
    }

    private Dictionary<int, int> ObjectValues()
    {
        var values = new Dictionary<int, int>(_runtime.ObjectStates.Count);
        foreach (var pair in _runtime.ObjectStates) values[pair.Key] = pair.Value;
        foreach (var pair in _runtime.ObjectPositions) values[pair.Key + 100000] = pair.Value.X * 1000 + pair.Value.Y;
        return values;
    }

    private ScummRoomCapture Capture() => new(Snapshot, Actors, CameraX, _runtime.ShadowPalette.ToArray(), _runtime.RoomColors.ToArray(),
        _runtime.GlobalVariables.GetValueOrDefault(ScummVars.CurrentLights), _runtime.Flashlight, CurrentWalkBoxes);

    private long Signature(Dictionary<int, Queue<int>> changes, int frame)
    {
        bool Ambient(int id) => changes.TryGetValue(id, out var queue) && queue.Count >= 4;
        var hash = new HashCode();
        foreach (var pair in _runtime.ObjectStates.OrderBy(item => item.Key))
            if (!Ambient(pair.Key)) { hash.Add(pair.Key); hash.Add(pair.Value); }
        foreach (var pair in _runtime.ObjectPositions.OrderBy(item => item.Key))
            if (!Ambient(pair.Key + 100000)) { hash.Add(pair.Key); hash.Add(pair.Value.X); hash.Add(pair.Value.Y); }
        foreach (var actor in Actors) { hash.Add(actor.Number); hash.Add(actor.Room); hash.Add(actor.X); hash.Add(actor.Y); hash.Add(actor.Visible); hash.Add(actor.Look.Costume); }
        foreach (var colour in _runtime.ShadowPalette.Take(16)) hash.Add(colour);
        foreach (var colour in _runtime.RoomColors.Take(16)) hash.Add(colour);
        hash.Add(CameraX);
        return hash.ToHashCode();
    }
    private readonly ScummBootInputPlan? _bootInput;
    private readonly bool _waitsForControl;

    /// <summary>The player can act: a room is current, no cutscene is open and the game has user input on (<c>cursorCommand userInputOn</c>).</summary>
    public bool PlayerHasControl => _currentRoom is not null && !InCutscene && _runtime.UserPut > 0;

    /// <summary>Frames in which nothing changes before the boot is taken to wait for the player.</summary>
    public const int BootStallFrames = 400;

    private const int MaxBootKeys = 40;

    // The labelled assumed input of a waiting boot (R-15.5, R-1.11): the digits of the answer the game itself computed (read from its variable,
    // never written), else the continue key. The keys reach the game through VAR_VERB_SCRIPT, like a player's.
    private int PressBootKeys(ScummBootInputPlan plan)
    {
        var answer = plan.AnswerVariable is null ? 0 : _runtime.GlobalVariables.GetValueOrDefault(plan.AnswerVariable);
        var keys = answer != 0 ? answer.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(digit => (int)digit).ToArray() : [plan.ContinueKey];
        _visitTrace.Add($"boot: Player input assumed: {(answer != 0 ? $"the digits of {plan.AnswerVariable} = {answer}, the answer the game's own script derived" : $"key {DescribeKey(plan.ContinueKey)}")} " +
                        "after the boot stood still " + BootStallFrames + " frames");
        foreach (var key in keys) _bootKeys.Enqueue(key);
        return keys.Length;
    }

    // One key per frame: a game's input script keeps the last key in a variable that its waiting script reads on its next turn.
    private readonly Queue<int> _bootKeys = new();

    /// <summary>What the boot did: the frames it ran and why it stopped.</summary>
    public ScummBootReport? BootReport { get; private set; }

    protected override ScummSessionSnapshot VisitReachedRoom(GameResource target, ScummObjectActionResult action) =>
        EnterRoom(target, $"Room {target.ResourceId:00} reached by {action.Evidence}", action.ReachedRoomLoad);

    /// <summary>Runs the boot script, answers the input it waits for (if any, labelled), and plays frames until the script has ended or chained on.</summary>
    private const int MaxBootAnswers = 24;

    private void Boot()
    {
        RunBootScript();
        var answers = AssumeBootAnswer() is null ? 0 : 1;
        var frames = 0;
        var stalled = 0;
        string? lastState = null;
        var keysPressed = 0;
        // A game whose script 1 hands over to an opening (Monkey Island: title music in a room of its own, then the first room) is booted until the
        // player has control: user input on, no cutscene open, a room on screen.
        var untilControl = _waitsForControl;
        while (frames < BootFrameLimit && (BootGoesOn || untilControl && !PlayerHasControl && _pending.Count > 0))
        {
            if (_bootKeys.Count > 0) PressKey(_bootKeys.Dequeue());
            AdvanceFrame();
            frames++;
            // A boot that waits for the player (a key, a typed answer) changes nothing for a long time: answer it as a player would, through the game's input script.
            var state = $"{_currentRoom?.ResourceId}|{string.Join(",", _pending.Select(item => $"{item.ScriptNumber}:{item.Offset}"))}";
            stalled = state == lastState && _bootKeys.Count == 0 ? stalled + 1 : 0;
            lastState = state;
            if (_bootInput is not null && stalled >= BootStallFrames && keysPressed < MaxBootKeys)
            {
                keysPressed += PressBootKeys(_bootInput);
                stalled = 0;
            }
            // A screen that asks again after a wrong answer (the answering script ended without releasing the boot) is answered again, a few times.
            if (answers < MaxBootAnswers && _pending.Any(item => item.ScriptNumber == BootScript) && AssumeBootAnswer() is not null) answers++;
        }
        FinishBoot(frames);
        BootReport = new ScummBootReport(frames, frames >= BootFrameLimit, _currentRoom?.ResourceId, _roomsEntered.ToArray());
        LastAction = null;
    }
}

/// <summary>How a settle ended: the room rested, a script left it, or the frame limit came first.</summary>
public enum ScummSettleOutcome { Settled, RoomLeft, FrameLimit }

/// <summary>Everything the picture of a room needs, taken at one frame while the room was current.</summary>
public sealed record ScummRoomCapture(ScummSessionSnapshot Snapshot, IReadOnlyList<ScummActorSnapshot> Actors, int? CameraX,
    byte[] ShadowPalette, byte[] RoomColors, int Lights, (int X, int Y) Flashlight, ScummWalkBoxSet? WalkBoxes);

/// <summary>The settled state of a visited room, the frames it took and, when a script left the room, the room it went to.</summary>
public sealed record ScummSettleResult(ScummRoomCapture State, ScummSettleOutcome Outcome, int Frames, int? LeftTo);

/// <summary>The outcome of a generic boot: frames played, whether the frame limit stopped it, the room it ended in and the rooms it entered.</summary>
public sealed record ScummBootReport(int Frames, bool HitFrameLimit, int? FinalRoom, IReadOnlyList<int> RoomsEntered);
