using System.Globalization;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

public sealed record LoomSessionSnapshot(
    int? RoomId,
    IReadOnlyDictionary<int, byte> ObjectStates,
    IReadOnlyDictionary<int, RoomObjectPosition> ObjectPositions,
    IReadOnlyDictionary<int, byte> ObjectOwners,
    IReadOnlyDictionary<string, ushort> KnownGlobalVariables,
    IReadOnlyCollection<int> Inventory,
    bool InventoryKnown,
    IReadOnlyCollection<int> ActiveSounds,
    int PendingScripts,
    IReadOnlyList<string> Uncertainties,
    IReadOnlyList<int>? DrawQueue = null,
    IReadOnlyCollection<int>? StatesSetInRoom = null,
    IReadOnlyList<LoomRandomPick>? Picks = null,
    IReadOnlyCollection<int>? EstimatedObjectStates = null,
    IReadOnlyDictionary<int, uint>? Classes = null,
    IReadOnlyList<LoomMessage>? Messages = null)
{
    /// <summary>The messages on the screen: strings drawn since the room was drawn, then the talk message.</summary>
    public IReadOnlyList<LoomMessage> ScreenMessages => Messages ?? [];

    /// <summary>Class bits per object number; empty when the index gave none.</summary>
    public IReadOnlyDictionary<int, uint> ObjectClasses => Classes ?? new Dictionary<int, uint>();

    /// <summary>
    /// ScummVM <c>findObject</c> skips untouchable objects. Unknown classes count as touchable.
    /// </summary>
    public bool IsUntouchable(int objectId) =>
        ObjectClasses.TryGetValue(objectId, out var bits) && LoomObjectClasses.Has(bits, LoomObjectClasses.Untouchable);

    /// <summary>Every seeded <c>getRandomNr</c> stand-in taken on this route, in order.</summary>
    public IReadOnlyList<LoomRandomPick> RandomPicks => Picks ?? [];

    /// <summary>Objects whose state or draw followed a seeded pick. These are estimates, not reached states.</summary>
    public IReadOnlyCollection<int> EstimatedStates => EstimatedObjectStates ?? [];

    /// <summary>Objects drawn since the last redraw, in call order (painted over directory order).</summary>
    public IReadOnlyList<int> DrawOrder => DrawQueue ?? [];

    /// <summary>Objects whose state a reached <c>setState</c> set since this room was entered.</summary>
    public IReadOnlyCollection<int> StatesSetByCode => StatesSetInRoom ?? [];

    public bool IsFaithful => RoomId.HasValue && PendingScripts == 0 && Uncertainties.Count == 0;
}

public sealed record LoomObjectActionResult(
    int SourceRoomId, int ObjectId, int VerbId, int ScriptOffset,
    LoomScriptDisassembly? Script, LoomBoundedScriptResult? Run,
    LoomScriptEvent? ReachedRoomLoad, int? DestinationRoomId,
    IReadOnlyList<string> Uncertainties, string? Input = null)
{
    /// <summary>True when the action was a click sent through the game's input scripts, not a direct verb call.</summary>
    public bool IsGameInput => Input is not null;

    private string Source => Input ?? $"Room {SourceRoomId:00} object {ObjectId}, verb 0x{VerbId:X2}, code 0x{ScriptOffset:X4}";

    public string Evidence => ReachedRoomLoad is null
        ? $"{Source}; no room load was reached."
        : $"{Source}; " +
          $"{ReachedRoomLoad.Operation} at {ReachedRoomLoad.SourceLabel} +0x{ReachedRoomLoad.Offset:X4} → " +
          (DestinationRoomId is int target ? $"Room {target:00}" : "unresolved room") + ".";
}

/// <summary>
/// Keeps a conservative, route-scoped LOOM state. Only instructions reached by
/// bounded execution change the snapshot. Unsupported state or timing is
/// recorded and prevents the snapshot from being labelled faithful.
/// </summary>
public sealed partial class LoomExplorationSession
{
    private const int MaxNestedScripts = 8;
    private readonly GameLibrary _game;
    private readonly LoomSoundClock _soundClock;
    private readonly LoomScriptReader _reader;
    private readonly LoomBoundedScriptExecutor _executor = new();
    private readonly LoomRoomObjectDirectory _objects = new();
    private readonly LoomObjectVerbReader _verbs;
    private readonly LoomScriptRuntimeState _runtime;
    private readonly int _globalScriptCount;
    private readonly UncertaintyList _uncertainties = [];
    private readonly List<PendingScript> _pending = [];
    private GameResource? _currentRoom;
    private IReadOnlyList<LoomRoomObjectRecord> _currentObjects = [];

    /// <param name="completeBoot">
    /// When true (the default), the boot runs past the intro screen and the
    /// opening cutscene until script 001 hands over to the input loop; see
    /// <see cref="CompleteBoot"/>. When false, it stops at script 001's first
    /// room load, as before 27 September.
    /// </param>
    /// <param name="stopAtOpening">
    /// With <paramref name="completeBoot"/>, stops the boot on the frame script 001 starts the opening cutscene
    /// (script 044, +0x0222), after the intro screen's assumed answer. <see cref="BootRunning"/> stays true, and
    /// <see cref="AdvanceFrame"/> plays the rest of the boot frame by frame, exactly as the constructor would.
    /// </param>
    /// <param name="stopAtBootInput">With completeBoot, enters the native intro and awaits actual input,
    /// before the explorer's assumed challenge answer. Frames continue the waiting boot after input.</param>
    public LoomExplorationSession(GameLibrary game, LoomIndexedObjectSnapshot indexedObjects, int randomSeed = 0,
        bool completeBoot = true, bool stopAtOpening = false, bool stopAtBootInput = false)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(indexedObjects);
        if (game.Version != ScummVersion.V3)
            throw new InvalidDataException("Only old-v3 LOOM has a session interpreter.");
        _game = game;
        var soundResources = game.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId.HasValue)
            .GroupBy(r => r.ResourceId!.Value).ToDictionary(group => group.Key, group => group.First());
        var soundLifetimes = new Dictionary<int, LoomSoundLifetime>();
        _soundClock = new LoomSoundClock(id =>
        {
            if (!soundLifetimes.TryGetValue(id, out var lifetime))
                soundLifetimes[id] = lifetime = soundResources.TryGetValue(id, out var resource)
                    ? game.Id == GameId.Loom ? new LoomAdlibAudio().ReadLifetime(resource)
                        : new(LoomSoundLifetimeKind.Unknown, null, false, "native LOOM AdLib clock does not establish this game's sound lifetime")
                    : new(LoomSoundLifetimeKind.Unknown, null, false, "no indexed native sound resource");
            return lifetime;
        });
        _reader = new LoomScriptReader(ScummScriptDialects.For(game.Id));
        _verbs = new LoomObjectVerbReader(ScummScriptDialects.For(game.Id));
        _runtime = new LoomScriptRuntimeState(indexedObjects.States, indexedObjects.Owners, indexedObjects.Classes)
        {
            Random = new LoomSeededRandom(randomSeed)
        };
        _globalScriptCount = ReadGlobalScriptCount(game);
        _runtime.ScriptRunning = IsScriptRunning;
        _runtime.SoundRunning = _soundClock.Status;
        _runtime.OverrideChanged = RecordOverride;
        _runtime.Actors.CostumeSource = id => LoomCostume.Load(game, id);
        _runtime.Verbs.ImageSize = (room, image) => VerbImage(room, image) is { } picture ? (picture.Width, picture.Height) : null;
        _runtime.Messages.TalkAnimation = (actor, start) => _runtime.Actors.TalkAnimation(actor, start, CurrentPlacement());
        // The engine starts with every variable at 0; the frame loop adds to the timers from there.
        foreach (var timer in EngineTimerVariables.Append(TimerTotalVariable))
            _runtime.GlobalVariables[timer] = 0;
        // ScummVM runs indexed script 1 at boot for this release family.
        if (!completeBoot)
        {
            RunIndexedScript(1, 0, null, 0);
            return;
        }
        _booting = true;
        _dispatchScope++;
        try
        {
            RunIndexedScript(1, 0, null, 0);
        }
        finally
        {
            _dispatchScope--;
        }
        DispatchRequestedLoads("during the boot");
        if (stopAtBootInput)
        {
            // Keep script 001 waiting on the intro's source-owned D6B gate.
            // Room 69 input must run local 203/205; CompleteBoot's assumed answer is omitted.
            _booting = false;
            LastAction = null;
            return;
        }
        CompleteBoot(stopAtOpening);
        LastAction = null;
    }

    /// <summary>
    /// The opening cutscene: script 001 +0x0222 starts script 044 and waits while it runs (+0x0225–0x022A). Decoded.
    /// </summary>
    public const int OpeningScript = 44;

    /// <summary>
    /// True while the boot is still running: only after a session built with <c>stopAtOpening</c>, until the frames
    /// played since reach the point where the constructor would have stopped (script 001 has ended or chained on and no
    /// cutscene is open).
    /// </summary>
    public bool BootRunning => _booting;

    /// <summary>Frames the boot ran before it stopped at the opening (0 when it ran to the end).</summary>
    public int BootFramesBeforeOpening { get; private set; }

    private bool BootGoesOn => (_pending.Any(item => item.ScriptNumber == 1) || InCutscene) && _pending.Count > 0;

    private void FinishBoot(int frames)
    {
        _booting = false;
        BootFrames = frames;
        if (_pending.Any(item => item.ScriptNumber == 1))
            _uncertainties.Add($"The boot (script 001) was still running after {frames} frames; the preview stopped following it.");
        _visitTrace.Add($"boot: {frames} frames after script 001's first room load; now in Room {_currentRoom?.ResourceId:00}");
    }

    /// <summary>Frames the boot may run after script 001's first room load.</summary>
    public const int BootFrameLimit = 5000;

    /// <summary>
    /// Continues the boot after script 001 has entered its first room. When
    /// script 001 waits for a bit that only a local script of that room sets,
    /// and the room's own code starts that script after player input (the
    /// intro screen's difficulty and draft check: Room 69 local 205 sets
    /// <c>bit[0xD6B]</c>, and local 203 starts it after a correct draft or a
    /// third wrong one), the session starts that script as if the player had
    /// answered, and labels it. Frames then run until script 001 has ended or
    /// chained on and no cutscene is open, at most <see cref="BootFrameLimit"/>.
    /// </summary>
    private void CompleteBoot(bool stopAtOpening)
    {
        if (_currentRoom is null)
        {
            _booting = false;
            return;
        }
        if (FindAwaitedInput() is { } input)
        {
            _uncertainties.Add(input.Note);
            _visitTrace.Add($"boot: starts Room {_currentRoom.ResourceId:00} local script {input.LocalScript} as the player's answer would");
            _dispatchScope++;
            try
            {
                RunStartedScript(input.LocalScript, 0, null, 0);
            }
            finally
            {
                _dispatchScope--;
            }
            DispatchRequestedLoads("during the boot");
        }
        var frames = 0;
        while (frames < BootFrameLimit && BootGoesOn)
        {
            if (stopAtOpening && _pending.Any(item => item.ScriptNumber == OpeningScript))
            {
                // The rest of the boot is played by AdvanceFrame, which counts on from here.
                _bootFramesPlayed = BootFramesBeforeOpening = frames;
                _playingBoot = true;
                _visitTrace.Add($"boot: stopped after {frames} frames, where script 001 started the opening (script {OpeningScript:000}); " +
                                "the rest plays frame by frame");
                return;
            }
            AdvanceFrame();
            frames++;
        }
        FinishBoot(frames);
    }

    // True while a boot stopped at the opening is played frame by frame; the frames of the boot so far.
    private bool _playingBoot;
    private int _bootFramesPlayed;

    /// <summary>After a frame of a boot stopped at the opening: ends the boot where the constructor would have.</summary>
    private void ContinuePlayedBoot()
    {
        if (!_playingBoot) return;
        _bootFramesPlayed++;
        if (_bootFramesPlayed < BootFrameLimit && BootGoesOn) return;
        _playingBoot = false;
        FinishBoot(_bootFramesPlayed);
    }

    /// <summary>
    /// Jiffies (1/60 s) per frame: ScummVM's main loop waits <c>VAR_TIMER_NEXT</c>
    /// (<c>var[0x013]</c>) jiffies, at least 1. The boot sets 6 (script 001 +0x0017).
    /// </summary>
    public int FrameJiffies => Math.Max(1, _runtime.GlobalVariables.TryGetValue(TimerNextVariable, out var next) ? next : 0);

    private const string TimerNextVariable = "var[0x013]";

    /// <summary>ScummVM v3 <c>VAR_TMR_1</c>..<c>VAR_TMR_3</c> (vars 11–13): jiffies the frame loop adds every frame.</summary>
    private static readonly string[] EngineTimerVariables = ["var[0x00B]", "var[0x00C]", "var[0x00D]"];

    /// <summary>ScummVM v3 <c>VAR_TIMER</c> (var 46): the jiffies of the last frame.</summary>
    private const string TimerVariable = "var[0x02E]";

    /// <summary>ScummVM v3 <c>VAR_TIMER_TOTAL</c> (var 47): jiffies since the start.</summary>
    private const string TimerTotalVariable = "var[0x02F]";

    /// <summary>ScummVM <c>scummLoop</c> clamps the jiffies it counts script delays and talk down by to 15.</summary>
    private const int MaxFrameDelta = 15;

    /// <summary>
    /// The start of ScummVM's <c>scummLoop(delta)</c> for v3: <c>VAR_TIMER</c> = delta, <c>VAR_TIMER_TOTAL</c> and
    /// <c>VAR_TMR_1..3</c> += delta, then <c>decreaseScriptDelay(min(delta, 15))</c>: each paused slot's delay goes down,
    /// and a slot whose delay falls below 0 runs again this frame.
    /// </summary>
    private void UpdateTimersAndDelays(int delta)
    {
        _runtime.GlobalVariables[TimerVariable] = (ushort)delta;
        foreach (var timer in EngineTimerVariables.Append(TimerTotalVariable))
            if (_runtime.GlobalVariables.TryGetValue(timer, out var value))
                _runtime.GlobalVariables[timer] = (ushort)(value + delta);
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

    /// <summary>Frames the completed boot ran after script 001's first room load.</summary>
    public int BootFrames { get; private set; }

    /// <summary>
    /// Script 001 waiting (<c>breakHere</c>, then a branch back while a bit is 0)
    /// for a bit that exactly one local script of the current room sets to 1.
    /// Returns that local script and a note naming the room code that starts it.
    /// </summary>
    private (int LocalScript, string Note)? FindAwaitedInput()
    {
        if (_currentRoom is null || _pending.FirstOrDefault(item => item.ScriptNumber == 1) is not { } boot) return null;
        var wait = boot.Script.Instructions.FirstOrDefault(item => item.Offset == boot.Offset);
        if (wait is not { Operation: "notEqualZero" } || !wait.Operands.StartsWith("bit[", StringComparison.Ordinal)) return null;
        var bit = wait.Operands[..(wait.Operands.IndexOf(']') + 1)];
        var setters = new List<(int Local, int Offset)>();
        var starters = new List<string>();
        for (var local = _globalScriptCount; local < 256; local++)
        {
            LoomScriptDisassembly? script;
            try { script = _reader.DecodeRoomLocalScript(_currentRoom, local, _globalScriptCount); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { continue; }
            if (script is null) continue;
            foreach (var item in script.Instructions)
            {
                if (item.Operation == "move" && item.Operands == $"{bit} = 0x0001") setters.Add((local, item.Offset));
                if (item.Operation == "startScript" && item.Operands.StartsWith("0x", StringComparison.Ordinal))
                    starters.Add($"{local}:{item.Operands[..4]}:{item.Offset:X4}");
            }
        }
        if (setters.Count != 1) return null;
        var (setter, setOffset) = setters[0];
        var startedBy = starters.Where(item => Convert.ToInt32(item.Split(':')[1], 16) == setter)
            .Select(item => $"local {item.Split(':')[0]} +0x{item.Split(':')[2]}").ToArray();
        if (startedBy.Length == 0) return null;
        return (setter, $"Player input assumed: script 001 +0x{wait.Offset:X4} waits for {bit}, which only Room {_currentRoom.ResourceId:00} " +
                        $"local script {setter} sets (+0x{setOffset:X4}). The room's own code starts it after the player's input " +
                        $"({string.Join(", ", startedBy)}), so the preview starts it as if the player had answered. Choices made on that screen were not simulated.");
    }

    /// <summary>
    /// True while a cutscene begun on this route is open: its script is still
    /// running and has not reached <c>endCutscene</c> (ScummVM's cutscene stack).
    /// The player has no control then, so a click's outcome is where it ends.
    /// </summary>
    public bool InCutscene => _cutscenes.Any(item => IsAlive(item.Owner));

    /// <summary>
    /// How many cutscenes this session has begun (<c>beginCutscene</c> reached). The app compares it
    /// before and after an input, with <see cref="EgoPuts"/>, to tell whether the game answered it with a change of scene.
    /// </summary>
    public int CutscenesBegun { get; private set; }

    /// <summary>
    /// How many times a script has put the ego at a point (<see cref="LoomActorTable.EgoPuts"/>). With
    /// <see cref="CutscenesBegun"/> it marks a change of scene: Room 02's script 045 begins a cutscene and
    /// <c>putActor</c>s the ego on the next stretch of the path, which ends the click's walk.
    /// </summary>
    public int EgoPuts => _runtime.Actors.EgoPuts;

    /// <summary>
    /// Test seam: gives a global word a value, as reached code would, so tests
    /// can exercise hooks the boot has not set. Never used by the app.
    /// </summary>
    internal void SetGlobalWord(int variable, ushort value) =>
        _runtime.GlobalVariables[$"var[0x{variable:X3}]"] = value;

    /// <summary>Test seam: runs a synthetic script at the top level, as a frame would. Never used by the app.</summary>
    internal LoomBoundedScriptResult RunTopLevelScript(LoomScriptDisassembly script) =>
        RunScript(script, _currentObjects, 0, null, 0);

    private bool IsAlive(ScriptFrame? frame) =>
        frame is not null && (_executing.Contains(frame) || _pending.Any(item => item.Frame == frame));

    /// <summary>Seed of the stand-in random source; every <c>getRandomNr</c> value is a labelled pick from it.</summary>
    public int RandomSeed => _runtime.Random.Seed;
    public LoomScriptDisassembly? LastEntryScript { get; private set; }

    /// <summary>
    /// The current room's walk boxes, read afresh on each entry as ScummVM's
    /// <c>resetRoomSubBlocks</c> does; <c>setBoxFlags</c> changes this copy. Null
    /// when no room is loaded or its box table could not be read.
    /// </summary>
    public LoomWalkBoxSet? CurrentWalkBoxes => _runtime.WalkBoxes;

    /// <summary>The current class bits per object or actor number (a live view; the snapshot holds a copy).</summary>
    public IReadOnlyDictionary<int, uint> ObjectClasses => _runtime.ObjectClasses;
    public LoomBoundedScriptResult? LastEntryRun { get; private set; }
    public LoomObjectActionResult? LastAction { get; private set; }
    public IReadOnlyList<LoomScriptEvent> LastVisitEvents { get; private set; } = [];
    public IReadOnlyList<LoomScriptEvent> ReachedSoundEvents => _visitEvents.Where(item =>
        item.Certainty == LoomScriptEventCertainty.Reached && item.Kind is
            LoomScriptEventKind.MusicStarted or LoomScriptEventKind.SoundStarted or
            LoomScriptEventKind.MusicStopped or LoomScriptEventKind.SoundStopped).ToArray();

    /// <summary>
    /// Every sound command the session met, in route order, with the room that was
    /// current and the room whose code started the chain of scripts that ran it.
    /// </summary>
    public IReadOnlyList<LoomSessionSoundEvent> SoundEvents => _soundEvents;

    /// <summary>The route position at which the current room was last entered (after the previous room's exit code).</summary>
    public int RoomEntrySequence { get; private set; }
    public LoomSessionSnapshot Snapshot => new(
        _currentRoom?.ResourceId,
        new Dictionary<int, byte>(_runtime.ObjectStates),
        new Dictionary<int, RoomObjectPosition>(_runtime.ObjectPositions),
        new Dictionary<int, byte>(_runtime.ObjectOwners),
        new Dictionary<string, ushort>(_runtime.GlobalVariables),
        KnownInventory(),
        _runtime.GlobalVariables.ContainsKey("var[0x001]"),
        _runtime.ActiveSounds.Order().ToArray(),
        _pending.Count,
        _uncertainties.ToArray(),
        _runtime.DrawQueue.ToArray(),
        _runtime.StatesSetInRoom.Order().ToArray(),
        _picks.ToArray(),
        _runtime.EstimatedStates.Order().ToArray(),
        new Dictionary<int, uint>(_runtime.ObjectClasses),
        _runtime.Messages.OnScreen);

    /// <summary>The message state: the talk message, its delay and <c>VAR_HAVE_MSG</c>, and drawn strings.</summary>
    public LoomMessageTable Messages => _runtime.Messages;

    private int[] KnownInventory() => _runtime.GlobalVariables.TryGetValue("var[0x001]", out var ego)
        ? _runtime.ObjectOwners.Where(pair => pair.Value == ego).Select(pair => pair.Key).Order().ToArray()
        : [];

    public LoomSessionSnapshot VisitRoom(GameResource room, LoomObjectActionResult? reachedAction = null, bool preserveStaffLevel = false)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (room.Kind != ResourceKind.Room || room.ResourceId is null)
            throw new InvalidDataException("The session can only visit an indexed room.");
        if (_currentRoom?.ResourceId == room.ResourceId) return Snapshot;
        _browsingStaffFloor = null;
        if (preserveStaffLevel && reachedAction is null) BeginBrowsingStaffFloor();
        _uncertainties.StartStep();
        if (reachedAction is { IsGameInput: true })
            return EnterRoom(room, $"{reachedAction.Evidence} The explorer's input (assumed) went on through the game's own scripts; walks were taken to end at once.", reachedAction.ReachedRoomLoad);
        if (reachedAction is not null)
            return EnterRoom(room, $"{reachedAction.Evidence} Actor walk and input dispatch were not simulated.", reachedAction.ReachedRoomLoad);
        // The scenes end before the room is entered, so its entry runs outside a cutscene (bit[0xD6E] = 0): LOOM's
        // entries skip the ego's costume size (script 017) and their walk-box watchers while a cutscene is open.
        var closedScenes = CloseScenesForCatalogVisit(room.ResourceId.Value);
        if (closedScenes.Count > 0) EndClosedScenes(room.ResourceId.Value, closedScenes);
        CatalogEnter(room);
        ApplyBrowsingStaffFloor();
        return Snapshot;
    }

    private LoomSessionSnapshot CatalogEnter(GameResource room)
    {
        var arrival = $"Room {room.ResourceId:00} selected from the catalog; the route to it was not executed from game input.";
        _runtime.Messages.RouteSkipped($"Room {room.ResourceId:00} was selected from the catalog, so prints on the route to it did not run");
        if (!_runtime.GlobalVariables.TryGetValue(EgoVariable, out var ego) || !LoomActorTable.IsValid(ego))
            return EnterRoom(room, arrival);
        var (assumed, label, chosen) = PickCatalogArrival(room.ResourceId!.Value, ego);
        // The ego's position before the arrival (where the route would have left it) is not known.
        _runtime.Actors.Forget(ego, assumed is null
            ? $"Room {room.ResourceId:00} was selected from the catalog, and no decoded loadRoomWithEgo, or loadRoom followed by putting the ego in the room, with literal operands brings the ego into it, so where the ego would arrive is unknown"
            : $"Room {room.ResourceId:00} was selected from the catalog, so where the ego stood before its assumed arrival is unknown");
        if (chosen is not null)
        {
            EstimateEgoSize(room.ResourceId.Value, chosen);
            ApplyArrivalClasses(room.ResourceId.Value, chosen);
        }
        return EnterRoom(room, label is null ? arrival : $"{arrival} {label}", assumed, label,
            chosen is { IsLoadRoomWithEgo: false } ? chosen : null);
    }

    /// <summary>
    /// A catalog visit while a cutscene plays leaves the scene, which the game never does: the scene's scripts would
    /// go on loading their own rooms, and input (off since the cutscene start script, script 002's
    /// <c>cursorCommand userInputSoftOff</c>) would stay off in the chosen room, so no click would be read. The session
    /// stops the script that began each open cutscene (innermost first) before the room is entered, and
    /// <see cref="EndClosedScenes"/> then ends them before the room is entered. Returns each closed cutscene's data and its script, or nothing
    /// when no cutscene is open.
    /// </summary>
    private List<(ushort Data, string Script)> CloseScenesForCatalogVisit(int roomId)
    {
        var closed = new List<(ushort, string)>();
        if (!InCutscene) return closed;
        while (_cutscenes.Count > 0)
        {
            _overrides.Remove(_cutscenes.Count);
            var (data, owner) = _cutscenes.Pop();
            var ownerScripts = _pending.Where(item => item.Frame == owner).ToArray();
            var script = ownerScripts.Length == 0
                ? "its script (no longer running)"
                : string.Join(", ", ownerScripts.Select(item => item.ScriptNumber is int number ? $"script {number:000}" : $"a Room {item.RoomId:00} script"));
            foreach (var item in ownerScripts)
                item.Frame.Stopped = true;
            _pending.RemoveAll(item => item.Frame == owner);
            _visitTrace.Add($"catalog visit to Room {roomId:00}: stops {script}, which began a cutscene (data {data})");
            closed.Add((data, script));
        }
        return closed;
    }

    /// <summary>
    /// Ends the cutscenes a catalog visit closed as <c>endCutscene</c> would (ScummVM: <c>VAR_OVERRIDE</c> = 0, then the
    /// script in <c>VAR_CUTSCENE_END_SCRIPT</c> with the cutscene's data), before the chosen room is entered: LOOM's
    /// script 003 turns the cursor and input back on, unfreezes the scripts and makes the camera follow the ego.
    /// An explorer action, labelled as such.
    /// </summary>
    private void EndClosedScenes(int roomId, IReadOnlyList<(ushort Data, string Script)> closed)
    {
        _runtime.GlobalVariables[OverrideVariable] = 0;
        _runtime.EstimatedVariables.Remove(OverrideVariable);
        var known = _runtime.GlobalVariables.TryGetValue(CutsceneEndScriptVariable, out var endScript);
        foreach (var (data, script) in closed)
        {
            _uncertainties.Add($"Room {roomId:00} was selected from the catalog while a cutscene played: the session stopped {script}, " +
                               (known
                                   ? $"and ran the cutscene end script ({CutsceneEndScriptVariable} = {endScript}) with data {data} as endCutscene would, so input is on again. "
                                   : $"but the cutscene end script ({CutsceneEndScriptVariable}) is unknown, so its effects were not applied. ") +
                               "An explorer action: the game never leaves a scene this way, and the scene's remaining room loads and texts did not run.");
            if (!known || endScript == 0) continue;
            _visitTrace.Add($"catalog visit to Room {roomId:00}: endCutscene runs script {endScript} with data {data}");
            RunStartedScript(endScript, 0, new Dictionary<string, ushort> { ["local[0x000]"] = data }, 0, []);
        }
    }

    /// <summary>
    /// A catalog visit does not run the route into the room. When decoded arrivals
    /// with literal operands bring the ego in (<c>loadRoomWithEgo</c>, or
    /// <c>loadRoom</c> followed by putting the ego in the room), one of them is
    /// taken as an assumed arrival. <c>loadRoomWithEgo</c> arrivals (the doorways)
    /// come first; <c>loadRoom</c> arrivals (mostly cutscene scripts) are used only
    /// when a room has none. Within that kind, those in the previous room's own
    /// code come first, then a seeded pick among the distinct placements. Returns a synthetic load
    /// event (for <see cref="PlaceEgoAfterEntry"/> or <see cref="PlaceEgoAfterLoadRoom"/>),
    /// the label that says so and the arrival, or nulls when no arrival is source-backed.
    /// </summary>
    private (LoomScriptEvent? Load, string? Label, LoomEgoArrival? Arrival) PickCatalogArrival(int roomId, int ego)
    {
        // A loadRoom arrival places a literal actor; it is the ego's only when var[0x001] holds it.
        var candidates = LoomEgoArrivalIndex.For(_game, _globalScriptCount).Into(roomId)
            .Where(item => item.EgoActor is null || item.EgoActor == ego).ToArray();
        if (candidates.Length == 0) return (null, null, null);
        var withEgo = candidates.Where(item => item.IsLoadRoomWithEgo).ToArray();
        var all = withEgo.Length > 0 ? withEgo : candidates;
        var previous = _currentRoom?.ResourceId;
        var fromPrevious = all.Where(item => item.SourceRoom is int source && source == previous).ToArray();
        var pool = fromPrevious.Length > 0 ? fromPrevious : all;
        var choices = pool.GroupBy(item => item.PlacementKey).Select(group => group.ToArray()).ToArray();
        var index = 0;
        string how;
        if (choices.Length == 1)
            how = fromPrevious.Length > 0
                ? $"the only one in Room {previous:00}'s own code"
                : $"the only source-backed arrival (Room {previous?.ToString("00", CultureInfo.InvariantCulture) ?? "none"} has none into it)";
        else
        {
            var pick = _runtime.Random.Pick($"catalog arrival into Room {roomId:00}", 0, choices.Length - 1);
            index = pick.Value;
            how = $"seeded pick {index + 1} of {choices.Length} (seed {pick.Seed})" +
                  (fromPrevious.Length > 0 ? $" among those in Room {previous:00}'s own code" : "") + "; alternatives: " +
                  string.Join("; ", choices.Where((_, i) => i != index).Select(Sources));
        }
        if (withEgo.Length == 0) how += "; no decoded loadRoomWithEgo brings the ego in, so this is a loadRoom followed by putting the ego in the room";
        var chosen = choices[index];
        var first = chosen[0];
        var label = $"Assumed arrival: the ego enters as {Sources(chosen)} would bring it in ({how}). " +
                    "Which route led here was not executed; the arrival is a possible outcome, not a reached one.";
        var load = first.IsLoadRoomWithEgo
            ? new LoomScriptEvent(0, first.Source, first.Offset, LoomScriptEventKind.RoomLoaded, "loadRoomWithEgo",
                $"assumed arrival into Room {roomId:00}", ResourceKind.Room, roomId, LoomScriptEventCertainty.Conditional,
                ResolvedArguments: [first.ObjectId, roomId, first.WalkX, first.WalkY])
            : new LoomScriptEvent(0, first.Source, first.Offset, LoomScriptEventKind.RoomLoaded, "loadRoom",
                $"assumed arrival into Room {roomId:00}", ResourceKind.Room, roomId, LoomScriptEventCertainty.Conditional,
                ResolvedArguments: [roomId]);
        return (load, label, first);

        static string Sources(LoomEgoArrival[] group) =>
            string.Join(" or ", group.Select(item => $"{item.Source} +0x{item.Offset:X4}")) + $" ({group[0].DescribePlacement()})";
    }

    /// <summary>
    /// LOOM's script 017 gives the ego one of three costume sizes: <c>startScript 0x11, [n]</c> sets
    /// <c>var[0x07C]</c> = n and the costume from <c>var[0x077..0x079]</c> (decoded). Most room entries pass a
    /// literal size; Rooms 02, 15, 17, 18, 22, 23, 24 and 36 pass <c>var[0x07C]</c>, the size the ego had in the room
    /// it came from. A catalog visit skips that room, so the size left by the room shown before is not the route's.
    /// The route the assumed arrival stands for passed through its source room, whose entry set the size: that
    /// size (followed back through rooms that pass <c>var[0x07C]</c> on) is taken as an estimate, unless the arrival's
    /// own code set it before the room load, as most doors do (<c>move var[0x07C] = n</c>) and some scripts do. With no literal size on the route, the kept
    /// value is labelled as unknown.
    /// </summary>
    private void EstimateEgoSize(int roomId, LoomEgoArrival arrival)
    {
        if (_game.Id != GameId.Loom) return;
        var index = LoomEgoArrivalIndex.For(_game, _globalScriptCount);
        var starts = index.StartsOf(EgoSizeScript).ToArray();
        // Only rooms whose entry keeps the size of the room before read it; the others set their own.
        if (!starts.Any(item => item.IsRoomEntry && item.SourceRoom == roomId && item.FirstArgument == EgoSizeVariable)) return;
        var (size, how) = SizeOnRoute(arrival);
        _runtime.EstimatedVariables.Add(EgoSizeVariable);
        if (size is int value)
        {
            _runtime.GlobalVariables[EgoSizeVariable] = (ushort)value;
            _visitTrace.Add($"catalog visit to Room {roomId:00}: {EgoSizeVariable} = {value} (estimate, {how})");
            _uncertainties.Add($"Room {roomId:00} was selected from the catalog: the ego's size ({EgoSizeVariable}, script {EgoSizeScript:000}) is estimated as {value} from the assumed arrival's route: {how}.");
        }
        else
            _uncertainties.Add($"Room {roomId:00} was selected from the catalog: no literal size for the ego ({EgoSizeVariable}, script {EgoSizeScript:000}) is on the assumed arrival's route, so the size left by the room shown before is kept and is unknown.");

        (int? Size, string How) SizeOnRoute(LoomEgoArrival first)
        {
            // The arrival's own code first (the size it set before the load: most doors move a literal into var[0x07C],
            // Room 16's six give 0, 1 or 2), then the entries of the rooms it came through.
            var own = starts.Where(item => item.Source == first.Source && item.Offset < first.Offset && Literal(item) is not null)
                .Select(item => (item.Offset, Size: Literal(item)!.Value, How: $"{item.Source} +0x{item.Offset:X4} startScript {EgoSizeScript:000}, [{item.FirstArgument}] before the load"))
                .Concat(index.MovesTo(EgoSizeVariable).Where(item => item.Source == first.Source && item.Offset < first.Offset)
                    .Select(item => (item.Offset, Size: item.Value, How: $"{item.Source} +0x{item.Offset:X4} sets {EgoSizeVariable} = {item.Value} before the load")))
                .OrderByDescending(item => item.Offset).ToArray();
            if (own.Length > 0) return (own[0].Size, own[0].How);
            var queue = new Queue<(int Room, string Path)>();
            var seen = new HashSet<int> { roomId };
            if (first.SourceRoom is int source && seen.Add(source)) queue.Enqueue((source, $"the route came through Room {source:00}"));
            while (queue.Count > 0)
            {
                var (room, path) = queue.Dequeue();
                var entry = starts.FirstOrDefault(item => item.IsRoomEntry && item.SourceRoom == room);
                if (entry is null) continue;
                if (Literal(entry) is int literal)
                    return (literal, $"{path}, whose entry sets it (Room {room:00} entry +0x{entry.Offset:X4} startScript {EgoSizeScript:000}, [{entry.FirstArgument}])");
                if (entry.FirstArgument != EgoSizeVariable) continue;
                foreach (var before in index.Into(room))
                    if (before.SourceRoom is int earlier && seen.Add(earlier))
                        queue.Enqueue((earlier, $"{path}, which keeps the size of the room before it; a route came through Room {earlier:00}"));
            }
            return (null, "");
        }

        static int? Literal(LoomScriptStart start) =>
            start.FirstArgument is { } argument && argument.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(argument.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>
    /// A catalog visit skips the arrival's own code, which may change the classes of the room's objects before its
    /// load; the room's entry then tests them. Room 12 is reached only through script 053, which clears object 322's
    /// class 24 (+0x022C) before its <c>loadRoom</c> (+0x0242); Room 12's entry puts the boat (actor 9) and gives
    /// Bobbin costume 17 only while that class is clear (+0x0013). The arrival's literal <c>setClass</c>es on the
    /// target room's objects that come before its load in the same code are applied, labelled as an estimate: which
    /// branch led to the load was not executed.
    /// </summary>
    private void ApplyArrivalClasses(int roomId, LoomEgoArrival arrival)
    {
        var index = LoomEgoArrivalIndex.For(_game, _globalScriptCount);
        foreach (var setClass in index.SetClassesIn(arrival.Source)
                     .Where(item => item.Offset < arrival.Offset && index.RoomHasObject(roomId, item.ObjectId)))
        {
            _runtime.ObjectClasses.TryGetValue(setClass.ObjectId, out var bits);
            foreach (var value in setClass.Values)
            {
                var cls = value & 0x7F;
                if (value == 0) bits = 0;
                else if (cls is >= 1 and <= 32)
                    bits = (value & 0x80) != 0 ? bits | (1u << (cls - 1)) : bits & ~(1u << (cls - 1));
            }
            _runtime.ObjectClasses[setClass.ObjectId] = bits;
            _visitTrace.Add($"catalog visit to Room {roomId:00}: object {setClass.ObjectId} classes set to 0x{bits:X6} ({arrival.Source} +0x{setClass.Offset:X4}, estimate)");
            _uncertainties.Add($"Room {roomId:00} was selected from the catalog: {arrival.Source} +0x{setClass.Offset:X4} sets object {setClass.ObjectId}'s classes " +
                               $"before the assumed arrival's load (+0x{arrival.Offset:X4}), so they were applied (estimate: the branch that led to the load was not executed).");
        }
    }

    /// <summary>LOOM's script 017 (the ego's costume size) and the size it keeps (decoded).</summary>
    private const int EgoSizeScript = 0x11;
    private const string EgoSizeVariable = "var[0x07C]";

    /// <summary>ScummVM <c>VAR_EGO</c>.</summary>
    private const string EgoVariable = "var[0x001]";

    /// <summary>Actor rooms, positions and walk boxes as reached code left them (actors 1–12).</summary>
    public IReadOnlyList<LoomActorSnapshot> Actors => _runtime.Actors.Snapshot();

    private LoomActorTable.Placement CurrentPlacement() =>
        new(_runtime.CurrentRoom, _runtime.WalkBoxes, _runtime.WalkBoxFlagsUnknown, _runtime.ObjectClasses);

    private LoomSessionSnapshot EnterRoom(GameResource room, string arrival, LoomScriptEvent? load = null, string? assumedArrival = null,
        LoomEgoArrival? placeAfterLoadRoom = null)
    {
        var eventsBeforeVisit = _visitEvents.Count;
        var withEgo = load is { Operation: "loadRoomWithEgo", ResolvedArguments: { Count: 4 } } ? load : null;
        int? ego = _runtime.GlobalVariables.TryGetValue(EgoVariable, out var egoValue) && LoomActorTable.IsValid(egoValue) ? egoValue : null;
        if (withEgo is not null)
        {
            // o5_loadRoomWithEgo: ego->putActor(room) before startScene; the ego
            // keeps its coordinates and is hidden, since the room is not current yet.
            if (ego is int egoNumber)
            {
                var before = _runtime.Actors[egoNumber];
                _runtime.Actors.Put(egoNumber, before.X, before.Y, room.ResourceId, CurrentPlacement(), ego);
            }
            else _runtime.Actors.ForgetAll($"{load!.SourceLabel} +0x{load.Offset:X4} moves the ego, whose actor number (var[0x001]) is unknown");
        }
        if (_currentRoom is not null)
        {
            RunGlobalHook(30, "before room exit");
            RunEmbeddedRoomScript(_currentRoom, exit: true);
            RunGlobalHook(31, "after room exit");
            // Room-scoped scripts cannot continue into a different room.
            _pending.RemoveAll(item => item.RoomId == _currentRoom.ResourceId);
        }

        _currentRoom = room;
        _runtime.CurrentRoom = room.ResourceId;
        RoomEntrySequence = _visitEvents.Count;
        // startScene clears the draw queue before the entry scripts run.
        _runtime.DrawQueue.Clear();
        _runtime.StatesSetInRoom.Clear();
        _runtime.EstimatedStates.Clear();
        // startScene hides every actor before the new room is loaded.
        _runtime.Actors.HideAll();
        // startScene stops the talk and the room redraw erases drawn strings.
        _runtime.Messages.RoomEntered(_runtime);
        var roomBytes = File.ReadAllBytes(room.FilePath);
        _currentObjects = _objects.Decode(roomBytes);
        // getObjOrActorName for text code 6: the stored names of this room's objects.
        var objectNames = LoomTextExtractor.ReadObjectNameBytes(LflEncoding.DecodeXorFF(roomBytes), _currentObjects);
        _runtime.ObjectName = id => objectNames.TryGetValue(id, out var name) ? name : null;
        _runtime.WalkBoxFlagsUnknown = null;
        try { _runtime.WalkBoxes = LoomWalkBoxReader.Read(roomBytes); }
        catch (InvalidDataException ex)
        {
            _runtime.WalkBoxes = null;
            _uncertainties.Add($"Room {room.ResourceId:00} walk boxes could not be read: {ex.Message}");
        }
        // showActors: actors whose room is the new one are shown, adjusted into its boxes.
        _runtime.Actors.ShowActorsIn(CurrentPlacement());
        // startScene centres the camera on the first screen (VAR_CAMERA_POS_X = 160).
        var roomWidth = (roomBytes[4] ^ 0xFF) | ((roomBytes[5] ^ 0xFF) << 8);
        _runtime.RoomCameraBounds = _runtime.CameraRange = (160, Math.Max(160, roomWidth - 160));
        _runtime.GlobalVariables[CameraVariable] = 160;
        _runtime.EstimatedVariables.Remove(CameraVariable);
        _runtime.Camera.Reset(160);
        // startScene resets VAR_CAMERA_MIN_X and VAR_CAMERA_MAX_X (v3: vars 17 and 18).
        _runtime.GlobalVariables["var[0x011]"] = 160;
        _runtime.GlobalVariables["var[0x012]"] = (ushort)Math.Max(160, roomWidth - 160);
        _runtime.EstimatedVariables.Remove("var[0x011]");
        _runtime.EstimatedVariables.Remove("var[0x012]");
        foreach (var item in _currentObjects)
            _runtime.ObjectPositions.TryAdd(item.Id, new RoomObjectPosition(item.X, item.Y));
        _runtime.GlobalVariables["var[0x004]"] = (ushort)room.ResourceId!.Value;
        _roomSize = (roomWidth, (roomBytes[6] ^ 0xFF) | ((roomBytes[7] ^ 0xFF) << 8));
        _pointer = null;
        PlaceVirtualMouse();
        _uncertainties.Add(arrival);

        RunGlobalHook(28, "before room entry");
        RunEmbeddedRoomScript(room, exit: false);
        RunGlobalHook(29, "after room entry");
        if (withEgo is not null && ego is int placedEgo) PlaceEgoAfterEntry(withEgo, placedEgo, assumedArrival);
        else if (placeAfterLoadRoom is not null && ego is int putEgo) PlaceEgoAfterLoadRoom(placeAfterLoadRoom, putEgo);
        else if (withEgo is not null) ForgetCamera($"{withEgo.SourceLabel} +0x{withEgo.Offset:X4} puts the camera on the ego, whose actor number is unknown");
        LastVisitEvents = _visitEvents.Skip(eventsBeforeVisit).ToArray();
        return Snapshot;
    }

    /// <summary>
    /// o5_loadRoomWithEgo after <c>startScene</c> (v3/v4): unless the entry code
    /// put the ego somewhere, it stands at the object's walk point; the camera is
    /// put on the ego and follows it; then, when the instruction gives a
    /// destination, the ego walks there. The walk is taken to end at once, an
    /// estimate (see <see cref="LoomActorTable.Walk"/>).
    /// </summary>
    private void PlaceEgoAfterEntry(LoomScriptEvent load, int ego, string? assumedArrival = null)
    {
        var arguments = load.ResolvedArguments!;
        var source = $"{load.SourceLabel} +0x{load.Offset:X4}";
        if (!_runtime.Actors.EgoPositioned)
        {
            var record = arguments[0] is int objectId ? _currentObjects.FirstOrDefault(item => item.Id == objectId) : null;
            if (record is null)
                _runtime.Actors.Forget(ego, arguments[0] is null
                    ? $"{source} brings the ego in at an object the preview cannot read"
                    : $"{source} brings the ego in at object {arguments[0]}, which is not in Room {_currentRoom?.ResourceId:00}");
            else
            {
                _runtime.Actors.Put(ego, record.WalkX, record.WalkY, _currentRoom?.ResourceId, CurrentPlacement(), ego);
                if (assumedArrival is not null)
                    _runtime.Actors.MarkEstimate(ego, $"assumed arrival at {source} after a catalog visit");
                else if (_runtime.EstimatedVariables.Contains(EgoVariable) || load.Certainty != LoomScriptEventCertainty.Reached)
                    _runtime.Actors.MarkEstimate(ego, $"the ego's arrival at {source} follows a seeded pick");
            }
        }
        // camera._cur.x = ego x (o5_loadRoomWithEgo, "based on disassembly"), then
        // setCameraFollows(ego); VAR_CAMERA_POS_X takes the value on the next loop.
        var placed = _runtime.Actors[ego];
        if (placed.X is not int egoX || placed.Room != _currentRoom?.ResourceId)
            ForgetCamera($"{source} puts the camera on the ego, whose position is unknown" +
                         (placed.Unknown is null ? "" : $" ({placed.Unknown})"));
        else SetCamera(egoX, placed.Estimate is null ? null : $"the camera is put on the ego at {source}, whose position is an estimate ({placed.Estimate})",
            $"{source}: camera x = {{0}} (the ego's x)");
        // setCameraFollows(ego): the camera is already on the ego, so it only enters follow mode.
        _runtime.Camera.Mode = LoomCamera.FollowActor;
        _runtime.Camera.Follows = ego;
        if (arguments[2] is int x && x != -1 && arguments[3] is int y)
        {
            // A catalog visit's assumed arrival shows the state after it, so its walk ends at once (estimate).
            _runtime.Actors.Walk(ego, x, y, CurrentPlacement(), $"the ego's walk from {source}", atOnce: assumedArrival is not null);
            // The walk's destination comes from the same assumed or picked arrival.
            if (assumedArrival is not null)
                _runtime.Actors.MarkEstimate(ego, $"assumed arrival at {source} after a catalog visit");
            else if (_runtime.EstimatedVariables.Contains(EgoVariable) || load.Certainty != LoomScriptEventCertainty.Reached)
                _runtime.Actors.MarkEstimate(ego, $"the ego's arrival at {source} follows a seeded pick");
            // A stepped walk moves the camera frame by frame (StepWalksAndCamera); one taken to end at once moves it at once.
            if (_runtime.Actors.Moving(ego) == 0) FollowEgoWalk(_runtime.Actors[ego], source);
        }
        var state = _runtime.Actors[ego];
        _visitTrace.Add($"{source}: the ego (actor {ego}) stands at ({state.X?.ToString() ?? "?"}, {state.Y?.ToString() ?? "?"}), box {state.WalkBox?.ToString() ?? "?"}" +
                        (state.Estimate is null ? "" : $" (estimate: {state.Estimate})"));
    }

    /// <summary>
    /// An assumed <c>loadRoom</c> arrival: the source script carries on after the
    /// load (ScummVM <c>startScene</c> runs the entry first), puts the ego in the
    /// room (<c>putActorInRoom</c>) and applies its decoded puts and walks. Only
    /// those steps and the camera are applied, not the script's other effects.
    /// <c>startScene</c> leaves the camera in normal mode (it does not follow the
    /// ego), so the camera is the script's literal <c>setCameraAt</c> when it has
    /// one, else where the entry left it. Walks end at once; every value is an
    /// estimate labelled with the arrival.
    /// </summary>
    private void PlaceEgoAfterLoadRoom(LoomEgoArrival arrival, int ego)
    {
        var source = $"{arrival.Source} +0x{arrival.Offset:X4}";
        var estimate = $"assumed arrival at {source} after a catalog visit";
        _runtime.Actors.PutInRoom(ego, _currentRoom?.ResourceId, CurrentPlacement(), ego);
        foreach (var step in arrival.Steps!)
        {
            switch (step.Kind)
            {
                case LoomEgoArrivalStepKind.Put:
                    _runtime.Actors.Put(ego, step.A, step.B, CurrentPlacement(), ego);
                    break;
                case LoomEgoArrivalStepKind.PutAtObject:
                {
                    // o5_putActorAtObject: the object's walk point (the index keeps only objects in the room).
                    var record = _currentObjects.FirstOrDefault(item => item.Id == step.A);
                    if (record is null) _runtime.Actors.Put(ego, 240, 120, CurrentPlacement(), ego);
                    else _runtime.Actors.Put(ego, record.WalkX, record.WalkY, CurrentPlacement(), ego);
                    break;
                }
                case LoomEgoArrivalStepKind.Walk:
                    _runtime.Actors.Walk(ego, step.A, step.B, CurrentPlacement(), $"the ego's walk at {arrival.Source} +0x{step.Offset:X4}", atOnce: true);
                    break;
            }
            _runtime.Actors.MarkEstimate(ego, estimate);
        }
        if (arrival.CameraX is int cameraX)
        {
            _runtime.Camera.Mode = LoomCamera.Normal;
            SetCamera(cameraX, $"setCameraAt in {source}'s assumed arrival", $"{source}: setCameraAt, camera x = {{0}}");
        }
        var state = _runtime.Actors[ego];
        _visitTrace.Add($"{source}: the ego (actor {ego}) stands at ({state.X?.ToString() ?? "?"}, {state.Y?.ToString() ?? "?"}), box {state.WalkBox?.ToString() ?? "?"}" +
                        (state.Estimate is null ? "" : $" (estimate: {state.Estimate})"));
    }

    /// <summary>ScummVM <c>VAR_CAMERA_POS_X</c>.</summary>
    private const string CameraVariable = "var[0x002]";

    // The current room's width and height in pixels.
    private (int Width, int Height)? _roomSize;

    /// <summary>
    /// ScummVM's main loop puts the pointer's room position in <c>VAR_VIRT_MOUSE_X/Y</c>
    /// (vars 20 and 21) every frame. The session has no pointer, so it states one:
    /// the pointer rests just below the room picture (over the verb area), under
    /// the camera's centre. No room object reaches that row, so <c>findObject</c>
    /// finds nothing there, and the input loop idles as it would with the pointer
    /// off the room. This is assumed input, and every read of it is labelled.
    /// </summary>
    private void PlaceVirtualMouse()
    {
        if (_roomSize is not { } size) return;
        if (_pointer is { } pointer)
        {
            _runtime.GlobalVariables["var[0x014]"] = (ushort)pointer.X;
            _runtime.GlobalVariables["var[0x015]"] = (ushort)pointer.Y;
            _runtime.EstimatedVariables.Remove("var[0x014]");
            _runtime.EstimatedVariables.Remove("var[0x015]");
            _runtime.VirtualMouse = pointer;
            _runtime.VirtualMouseAssumption = $"assumed input: the pointer rests over the room at ({pointer.X}, {pointer.Y}), {_pointerNote}";
            return;
        }
        var x = CameraX ?? size.Width / 2;
        var y = size.Height;
        _runtime.GlobalVariables["var[0x014]"] = (ushort)x;
        _runtime.GlobalVariables["var[0x015]"] = (ushort)y;
        _runtime.EstimatedVariables.Remove("var[0x014]");
        _runtime.EstimatedVariables.Remove("var[0x015]");
        _runtime.VirtualMouse = (x, y);
        _runtime.VirtualMouseAssumption = $"assumed input: no pointer over the room, so the virtual mouse (var[0x014], var[0x015]) is taken to rest just below the room picture at ({x}, {y})";
    }

    // The pointer over the current room given by SetPointer or Click, or null (no pointer over the room).
    private (int X, int Y)? _pointer;
    private string _pointerNote = HoverNote;
    private const string HoverNote = "placed by the explorer's hover, with no click";

    /// <summary>
    /// Puts the pointer over the current room at a room-pixel point, or takes it
    /// off (null). This is assumed input: from the next frame the virtual mouse
    /// (<c>var[0x014]</c>/<c>var[0x015]</c>) reads it, so the input loop (script 006)
    /// sees what is under it; no click is made. Points outside the room picture
    /// count as no pointer. Entering another room takes the pointer off.
    /// </summary>
    public void SetPointer((int X, int Y)? point)
    {
        _pointer = point is { } p && _roomSize is { } size && p.X >= 0 && p.Y >= 0 && p.X < size.Width && p.Y < size.Height
            ? p : null;
        _pointerNote = HoverNote;
        PlaceVirtualMouse();
    }

    /// <summary>ScummVM <c>VAR_VERB_SCRIPT</c> (v3: var 32): the script <c>runInputScript</c> runs for every click. The boot sets it to 5 (script 001 +0x016B).</summary>
    private const string VerbScriptVariable = "var[0x020]";
    /// <summary>ScummVM <c>VAR_TMR_2</c> (v3: var 12), jiffies the engine adds every frame; script 006 resets it on a click and reads it to find a double click.</summary>
    private const string ClickTimerVariable = "var[0x00C]";
    /// <summary>ScummVM <c>kSceneClickArea</c>: a click in the room picture.</summary>
    public const int SceneClickArea = 2;
    /// <summary>Assumed time since the previous click for a single click: one second (60 jiffies), past script 006's 40-jiffy double-click window.</summary>
    public const int SingleClickGap = 60;
    /// <summary>Assumed time between the two clicks of a double click: 10 jiffies, inside script 006's 40-jiffy window.</summary>
    public const int DoubleClickGap = 10;

    /// <summary>
    /// Sends a left click at a room point to the game, as ScummVM's
    /// <c>checkExecVerbs</c> does for a click in the room: <c>runInputScript(2, 0, 0)</c>
    /// runs the script in <c>VAR_VERB_SCRIPT</c> (script 005) with those arguments,
    /// which chains a room click into the input loop (script 006, <c>local[0]</c> = 1).
    /// Script 006 then does what it does with the object under the pointer: verb
    /// 0x37, the ego's walk, and script 007's sentence. This is assumed input: the
    /// point, and the time since the previous click (<see cref="SingleClickGap"/>, or
    /// <see cref="DoubleClickGap"/> between the two clicks of a double click), which
    /// the engine's timer would give. The click is not sent while user input is off
    /// (<c>cursorCommand</c>), or when no input script is set. The pointer stays at
    /// the point afterwards. Walks end at once, as elsewhere; a <c>waitFor actor</c>
    /// makes the script go on on the next frame, so the sentence and anything it
    /// loads follow in later frames (<see cref="AdvanceFrame"/>).
    /// </summary>
    public LoomObjectActionResult Click(int x, int y, bool doubleClick = false)
    {
        _browsingStaffFloor = null; // Subsequent input belongs to gameplay, beyond catalog arrival.
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before clicking in it.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        var input = $"A {(doubleClick ? "double click" : "click")} at ({x}, {y}) in Room {sourceRoomId:00}, sent to the game's input script as assumed input";
        string? refused = null;
        ushort verbScript = 0;
        if (_roomSize is not { } size || x < 0 || y < 0 || x >= size.Width || y >= size.Height)
            refused = "the point is outside the room picture";
        else if (_runtime.UserPut <= 0)
            refused = "user input is off (cursorCommand), and ScummVM's checkExecVerbs reads no click then";
        else if (!_runtime.GlobalVariables.TryGetValue(VerbScriptVariable, out verbScript) || verbScript == 0)
            refused = $"no input script is set ({VerbScriptVariable}, VAR_VERB_SCRIPT, is 0)";
        if (refused is not null)
        {
            _uncertainties.Add($"{input}: not sent, because {refused}.");
            LastAction = new LoomObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null,
                _uncertainties.Skip(beforeUncertainties).ToArray(), input);
            return LastAction;
        }
        _uncertainties.Add($"{input}. ScummVM runInputScript({SceneClickArea}, 0, {InputMode}) runs script {verbScript} (var[0x020], VAR_VERB_SCRIPT, set by script 001 +0x016B); the click's point and timing are the explorer's, not the game's.");
        if (_runtime.UserPutEstimate is not null)
            _uncertainties.Add($"User input is taken to be on, an estimate: {_runtime.UserPutEstimate}.");
        LastAction = new LoomObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null, [], input);
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            if (doubleClick) SendSceneClick(verbScript, x, y, SingleClickGap, "the first click of a double click");
            SendSceneClick(verbScript, x, y, doubleClick ? DoubleClickGap : SingleClickGap,
                doubleClick ? "the second click of a double click" : "a single click");
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        return FinishInput(beforeUncertainties, beforeEvents);
    }

    /// <summary>
    /// The third argument of <c>runInputScript</c>: ScummVM's <c>checkExecVerbs</c>
    /// passes the mouse button only for versions above 3 (<c>_game.version &gt; 3 ? code : 0</c>),
    /// so LOOM's input script always gets 0. Script 005 +0x0000 sets <c>local[2]</c> = 0 first anyway.
    /// </summary>
    public const int InputMode = 0;
    /// <summary>ScummVM <c>kVerbClickArea</c>: a click in the verb screen.</summary>
    public const int VerbClickArea = 1;
    /// <summary>ScummVM <c>VAR_MOUSE_X</c>/<c>VAR_MOUSE_Y</c> (v3: vars 44 and 45), the pointer's screen point; script 005 reads x for verb 0x31.</summary>
    private const string MouseXVariable = "var[0x02C]";
    private const string MouseYVariable = "var[0x02D]";

    /// <summary>
    /// The verb area as the session's verb slots would draw it, with each image
    /// verb's rectangle from its object's image in the room it was taken from
    /// (<see cref="LoomVerbArea.Compose"/>).
    /// </summary>
    public LoomVerbAreaPicture VerbArea() => LoomVerbArea.Compose(_runtime.Verbs.Snapshot(), VerbImage, _runtime.Verbs.Erases());

    private readonly Dictionary<int, IReadOnlyList<RoomObjectImage>> _verbImageRooms = [];

    private RoomObjectImage? VerbImage(int roomId, int objectId)
    {
        if (!_verbImageRooms.TryGetValue(roomId, out var images))
        {
            var resource = _game.Rooms.FirstOrDefault(item => item.ResourceId == roomId);
            try { images = resource is null ? [] : new LoomRoomObjects().Decode(File.ReadAllBytes(resource.FilePath)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { images = []; }
            _verbImageRooms[roomId] = images;
        }
        return images.FirstOrDefault(item => item.Id == objectId);
    }

    /// <summary>
    /// Sends a left click at a screen point in the verb area (rows
    /// <see cref="LoomVerbArea.Top"/>–199) to the game, as ScummVM's
    /// <c>checkExecVerbs</c> does: <c>findVerbAtPos</c> over the verb slots
    /// (<see cref="LoomVerbArea.FindVerbAt"/>), then <c>runInputScript(1, verb, 0)</c>
    /// on a verb that is on, or <c>runInputScript(1, 0, 0)</c> elsewhere in the verb
    /// screen. Script 005 (area 1) turns a distaff note into script 010, verb 0x35
    /// into script 009 and the close-up (0x36) into script 007. This is assumed
    /// input: the point, which the session puts in <c>VAR_MOUSE_X</c>/<c>Y</c>
    /// (<c>var[0x02C]</c>/<c>var[0x02D]</c>), is the explorer's. The pointer is off the
    /// room picture then, so the room pointer is taken off. Refused while user input
    /// is off or when no input script is set, as <see cref="Click"/> is. Verbs the
    /// session cannot draw (unknown mode, image or position, text verbs) cannot be found.
    /// </summary>
    public LoomObjectActionResult ClickVerbArea(int x, int y)
    {
        _browsingStaffFloor = null;
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before clicking in the verb area.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        var area = VerbArea();
        var verb = LoomVerbArea.FindVerbAt(area.Items, x, y);
        var input = verb is null
            ? $"A click at screen ({x}, {y}) in the verb area, on no verb that is on, sent to the game's input script as assumed input"
            : $"A click at screen ({x}, {y}) on verb 0x{verb.VerbId:X2} (slot {verb.Slot}) in the verb area, sent to the game's input script as assumed input";
        string? refused = null;
        ushort verbScript = 0;
        if (x < 0 || x >= LoomVerbArea.Width || y < LoomVerbArea.Top || y >= LoomVerbArea.Top + LoomVerbArea.Height)
            refused = "the point is outside the verb area";
        else if (_runtime.UserPut <= 0)
            refused = "user input is off (cursorCommand), and ScummVM's checkExecVerbs reads no click then";
        else if (!_runtime.GlobalVariables.TryGetValue(VerbScriptVariable, out verbScript) || verbScript == 0)
            refused = $"no input script is set ({VerbScriptVariable}, VAR_VERB_SCRIPT, is 0)";
        if (refused is not null)
        {
            _uncertainties.Add($"{input}: not sent, because {refused}.");
            LastAction = new LoomObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null,
                _uncertainties.Skip(beforeUncertainties).ToArray(), input);
            return LastAction;
        }
        var verbId = verb?.VerbId ?? 0;
        _uncertainties.Add($"{input}. ScummVM checkExecVerbs: findVerbAtPos, then runInputScript({VerbClickArea}, 0x{verbId:X2}, {InputMode}) runs script {verbScript} " +
                           "(var[0x020], VAR_VERB_SCRIPT); the click's point is the explorer's, not the game's, and is put in var[0x02C]/var[0x02D] (VAR_MOUSE_X/Y).");
        if (verb?.Estimate is { } estimate)
            _uncertainties.Add($"Verb 0x{verbId:X2}'s slot follows an estimate: {estimate}.");
        if (area.Notes.Count > 0)
            _uncertainties.Add($"Verbs the session cannot draw cannot be found by the click: {string.Join("; ", area.Notes)}.");
        if (_runtime.UserPutEstimate is not null)
            _uncertainties.Add($"User input is taken to be on, an estimate: {_runtime.UserPutEstimate}.");
        LastAction = new LoomObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null, [], input);
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            // The pointer is over the verb screen, below the room picture (VAR_VIRT_MOUSE_Y past the room).
            _pointer = null;
            PlaceVirtualMouse();
            _runtime.GlobalVariables[MouseXVariable] = (ushort)x;
            _runtime.GlobalVariables[MouseYVariable] = (ushort)y;
            _runtime.EstimatedVariables.Remove(MouseXVariable);
            _runtime.EstimatedVariables.Remove(MouseYVariable);
            _visitTrace.Add($"click: verb area at screen ({x}, {y}), " + (verb is null ? "on no verb" : $"verb 0x{verbId:X2} (slot {verb.Slot})") +
                            $", runs script {verbScript} with [{VerbClickArea}, {verbId}, {InputMode}] (runInputScript)");
            RunStartedScript(verbScript, 0, new Dictionary<string, ushort>(StringComparer.Ordinal)
            {
                ["local[0x000]"] = VerbClickArea,
                ["local[0x001]"] = (ushort)verbId,
                ["local[0x002]"] = InputMode
            }, 0);
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        return FinishInput(beforeUncertainties, beforeEvents);
    }

    /// <summary>ScummVM <c>kKeyClickArea</c>: a key press.</summary>
    public const int KeyClickArea = 4;
    /// <summary>ScummVM <c>VAR_CUTSCENEEXIT_KEY</c> (v3: var 24); the boot sets it to 0x1B (script 001 +0x012C).</summary>
    private const string CutsceneExitKeyVariable = "var[0x018]";
    /// <summary>ScummVM <c>VAR_RESTART_KEY</c> (v3: var 42); the boot sets it to 0x142, F8 (script 001 +0x0131).</summary>
    private const string RestartKeyVariable = "var[0x02A]";
    /// <summary>ScummVM <c>VAR_PAUSE_KEY</c> (v3: var 43); the boot sets it to 0x20, space (script 001 +0x0136).</summary>
    private const string PauseKeyVariable = "var[0x02B]";

    /// <summary>
    /// A key as ScummVM's <c>processKeyboard</c> hands it to scripts: printable
    /// characters as their ASCII code, Escape 0x1B, F1–F9 as 315–323 (0x13B–0x143),
    /// Ctrl+A–Z as 1–26.
    /// </summary>
    public static string DescribeKey(int key) => key switch
    {
        0x1B => "Escape",
        >= 0x13B and <= 0x143 => $"F{key - 0x13B + 1}",
        >= 1 and <= 26 => $"Ctrl+{(char)('A' + key - 1)}",
        0x20 => "space",
        > 0x20 and < 0x7F => $"'{(char)key}'",
        _ => $"key 0x{key:X2}"
    };

    /// <summary>
    /// Sends a key press to the game, as ScummVM's <c>processKeyboard</c> and
    /// <c>checkExecVerbs</c> do for v3: the restart key (<c>VAR_RESTART_KEY</c>) and
    /// the pause key (<c>VAR_PAUSE_KEY</c>) and '.' (talk stop) are the engine's and are not sent;
    /// Escape, while <c>VAR_CUTSCENEEXIT_KEY</c> is nonzero, first runs <c>abortCutscene</c>
    /// (the script that set the open cutscene's override continues at its skip jump
    /// on the next frame, with <c>VAR_OVERRIDE</c> = 1) and then goes on as that
    /// variable's key; otherwise, with user input on,
    /// <c>runInputScript(4, key, 1)</c> runs <c>VAR_VERB_SCRIPT</c> (script 005, area 4:
    /// note letters, Escape, text speed, F5–F7, Ctrl+V). A verb whose key matches would
    /// be clicked instead (<c>runInputScript(1, verb, 1)</c>), but no decoded
    /// <c>verbOps</c> in the release sets a key, so the session does not keep keys.
    /// The key is assumed input.
    /// </summary>
    public LoomObjectActionResult PressKey(int key)
    {
        _browsingStaffFloor = null;
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before pressing a key.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        var name = DescribeKey(key);
        var input = $"The key {name} (0x{key:X2}) in Room {sourceRoomId:00}, sent to the game's input script as assumed input";
        bool Is(string variable) => _runtime.GlobalVariables.TryGetValue(variable, out var value) && value != 0 && value == key;
        string? refused = null;
        ushort verbScript = 0;
        // processKeyboard: Escape, while VAR_CUTSCENEEXIT_KEY is nonzero, runs
        // abortCutscene, then passes VAR_CUTSCENEEXIT_KEY's value on as the key.
        string? aborted = null;
        if (key == 0x1B && _runtime.GlobalVariables.TryGetValue(CutsceneExitKeyVariable, out var exitKey) && exitKey != 0)
        {
            aborted = AbortCutscene();
            _uncertainties.Add($"{input}: ScummVM processKeyboard runs abortCutscene first ({CutsceneExitKeyVariable}, VAR_CUTSCENEEXIT_KEY, is 0x{exitKey:X2}): {aborted}.");
            key = exitKey;
        }
        if (key <= 0 || key > 0x1FF)
            refused = "it is not a key code the engine sends";
        else if (Is(RestartKeyVariable))
            refused = $"it is the restart key ({RestartKeyVariable}, VAR_RESTART_KEY): the engine restarts the game, which the session does not model";
        else if (Is(PauseKeyVariable))
            refused = $"it is the pause key ({PauseKeyVariable}, VAR_PAUSE_KEY): the engine pauses the game and the scripts never see it";
        else if (key == '.')
            refused = "'.' is the engine's talk-stop key (v3 has no VAR_TALKSTOP_KEY, so it is always on): it ends the current message and the scripts never see it";
        else if (_runtime.UserPut <= 0)
            refused = "user input is off (cursorCommand), and ScummVM's checkExecVerbs reads no key then";
        else if (!_runtime.GlobalVariables.TryGetValue(VerbScriptVariable, out verbScript) || verbScript == 0)
            refused = $"no input script is set ({VerbScriptVariable}, VAR_VERB_SCRIPT, is 0)";
        if (refused is not null)
        {
            // After abortCutscene the key was acted on, even when no script reads it.
            _uncertainties.Add(aborted is null
                ? $"{input}: not sent, because {refused}."
                : $"{input}: the key did not reach the input script after abortCutscene, because {refused}.");
            LastAction = new LoomObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null,
                _uncertainties.Skip(beforeUncertainties).ToArray(), input);
            return LastAction;
        }
        _uncertainties.Add($"{input}. ScummVM checkExecVerbs: runInputScript({KeyClickArea}, 0x{key:X2}, 1) runs script {verbScript} (var[0x020], VAR_VERB_SCRIPT); the key is the explorer's, not the game's.");
        if (_runtime.UserPutEstimate is not null)
            _uncertainties.Add($"User input is taken to be on, an estimate: {_runtime.UserPutEstimate}.");
        LastAction = new LoomObjectActionResult(sourceRoomId, 0, 0, 0, null, null, null, null, [], input);
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            _visitTrace.Add($"key: {name} runs script {verbScript} with [{KeyClickArea}, 0x{key:X2}, 1] (runInputScript)");
            RunStartedScript(verbScript, 0, new Dictionary<string, ushort>(StringComparer.Ordinal)
            {
                ["local[0x000]"] = KeyClickArea,
                ["local[0x001]"] = (ushort)key,
                ["local[0x002]"] = 1
            }, 0);
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        return FinishInput(beforeUncertainties, beforeEvents);
    }

    // After an input script ran: dispatch the loads it requested, or visit the room a reached load names.
    private LoomObjectActionResult FinishInput(int beforeUncertainties, int beforeEvents)
    {
        var action = LastAction! with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
        LastAction = action;
        if (DispatchRequestedLoads("during the click"))
        {
            LastAction = LastAction with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
            return LastAction;
        }
        var roomLoad = _visitEvents.Skip(beforeEvents).FirstOrDefault(item =>
            item.Kind == LoomScriptEventKind.RoomLoaded && item.Certainty == LoomScriptEventCertainty.Reached &&
            !_dispatchedLoads.Contains(item));
        var target = roomLoad?.TargetId is int id ? _game.Rooms.FirstOrDefault(item => item.ResourceId == id) : null;
        if (roomLoad is not null && target is not null)
        {
            action = action with { ReachedRoomLoad = roomLoad, DestinationRoomId = target.ResourceId };
            VisitRoom(target, action);
            action = action with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
        }
        LastAction = action;
        return action;
    }

    private void SendSceneClick(int verbScript, int x, int y, int gap, string which)
    {
        _pointer = (x, y);
        _pointerNote = "where the explorer clicked; it stays there after the click";
        PlaceVirtualMouse();
        _runtime.VirtualMouseAssumption = $"assumed input: {which} at ({x}, {y}), sent by the explorer";
        _runtime.GlobalVariables[ClickTimerVariable] = (ushort)gap;
        _runtime.EstimatedVariables.Remove(ClickTimerVariable);
        _uncertainties.Add($"Assumed input timing for {which}: var[0x00C] (VAR_TMR_2, jiffies since script 006 last reset it) = {gap}; the frame loop counts it on from there, but the player's time before the click is not known.");
        _visitTrace.Add($"click: {which} at ({x}, {y}) runs script {verbScript} with [{SceneClickArea}, 0, {InputMode}] (runInputScript)");
        RunStartedScript(verbScript, 0, new Dictionary<string, ushort>(StringComparer.Ordinal)
        {
            ["local[0x000]"] = SceneClickArea,
            ["local[0x001]"] = 0,
            ["local[0x002]"] = InputMode
        }, 0);
    }

    /// <summary>The pointer the session reads as the virtual mouse, or null when it assumes none over the room.</summary>
    public (int X, int Y)? Pointer => _pointer;

    /// <summary>Verb slots as reached <c>verbOps</c> left them: the verb bar and the verb-area close-up (verb 0x36).</summary>
    public IReadOnlyList<LoomVerbSnapshot> Verbs => _runtime.Verbs.Snapshot();

    /// <summary>The camera's centre x in room pixels (<c>var[0x002]</c>), or null when it is unknown.</summary>
    public int? CameraX => _runtime.GlobalVariables.TryGetValue(CameraVariable, out var x) ? x : null;

    /// <summary>True when <see cref="CameraX"/> comes from an estimate (a walk end, a seeded pick, an instant pan).</summary>
    public bool CameraEstimated => _runtime.EstimatedVariables.Contains(CameraVariable);

    /// <summary>
    /// The camera's limits in the current room (<c>VAR_CAMERA_MIN_X</c>/<c>MAX_X</c>): half a screen from each edge,
    /// narrowed by <c>roomOps roomScroll</c> (Room 41's entry: 176..184); null when no room is loaded.
    /// </summary>
    public (int Min, int Max)? CameraRange => _runtime.CameraRange;

    /// <summary>Puts the camera at <paramref name="x"/>, kept half a screen from the room's edges (<c>cameraMoved</c>).</summary>
    private void SetCamera(int x, string? estimate, string trace)
    {
        var (min, max) = _runtime.CameraRange ?? (160, 160);
        var clamped = (ushort)Math.Clamp(x, min, max);
        _runtime.GlobalVariables[CameraVariable] = clamped;
        _runtime.Camera.DestX = x;
        _runtime.Camera.MovingToActor = false;
        if (estimate is null) _runtime.EstimatedVariables.Remove(CameraVariable);
        else
        {
            _runtime.EstimatedVariables.Add(CameraVariable);
            _uncertainties.Add($"Camera x (var[0x002]) = {clamped} is an estimate: {estimate}.");
        }
        _visitTrace.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, trace, clamped) +
                        (estimate is null ? "" : " (estimate)"));
    }

    private void ForgetCamera(string reason)
    {
        _runtime.GlobalVariables.Remove(CameraVariable);
        _runtime.EstimatedVariables.Remove(CameraVariable);
        _uncertainties.Add($"Camera x (var[0x002]) is unknown: {reason}.");
    }

    /// <summary>
    /// ScummVM <c>moveCamera</c> in follow mode: once the followed actor is more
    /// than 10 strips (80 pixels) from the camera's centre strip, the camera moves
    /// to the actor. The walk ends at once in this preview, so the camera takes the
    /// end point at once too, and both are estimates.
    /// </summary>
    private void FollowEgoWalk(LoomActorSnapshot walked, string source)
    {
        if (!_runtime.GlobalVariables.TryGetValue(CameraVariable, out var camera)) return;
        if (walked.X is not int endX || walked.Room != _currentRoom?.ResourceId)
        {
            ForgetCamera($"it follows the ego's walk from {source}, whose end is unknown");
            return;
        }
        // _screenStartStrip = camera / 8 - 20; the triggers are strips 10 and 30.
        var strip = endX / 8 - (camera / 8 - 20);
        var reason = $"it follows the ego's walk from {source}, which is taken to end at once ({walked.Estimate ?? "walk timing and path not simulated"})";
        if (strip is < 10 or > 30)
            SetCamera(endX, reason, $"{source}: the camera follows the ego's walk to x = {{0}}");
        else if (_runtime.EstimatedVariables.Add(CameraVariable))
            _uncertainties.Add($"Camera x (var[0x002]) = {camera} is an estimate: {reason}; the walk's end stays within the camera's triggers.");
    }

    public LoomObjectActionResult ActivateObjectVerb(int objectId, int verbId)
    {
        _browsingStaffFloor = null;
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before activating an object verb.");
        var record = _currentObjects.FirstOrDefault(item => item.Id == objectId) ??
                     throw new InvalidDataException($"Object {objectId} has no code record in Room {sourceRoomId:00}.");
        _uncertainties.StartStep();
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        _uncertainties.Add($"Object {objectId} verb 0x{verbId:X2} was activated directly; input dispatch, actor walk, and class-based hit selection were not simulated.");
        if (Snapshot.IsUntouchable(objectId))
            _uncertainties.Add($"Object {objectId} is untouchable (class {LoomObjectClasses.Untouchable}) in this preview, so the game's findObject would not select it; its verb ran only as an exploration action.");
        LoomScriptDisassembly? script = null;
        LoomBoundedScriptResult? run = null;
        _dispatchScope++;
        _entersRoomLoads++;
        try
        {
            script = _verbs.Decode(_currentRoom, record, verbId);
            run = RunScript(script, _currentObjects, 0, null, 0);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _uncertainties.Add($"Room {sourceRoomId:00} Object {objectId} verb 0x{verbId:X2}: {ex.Message}");
        }
        finally
        {
            _dispatchScope--;
            _entersRoomLoads--;
        }
        LastAction = new LoomObjectActionResult(sourceRoomId, objectId, verbId,
            script?.ResourceOffset ?? record.CodeOffset, script, run, null, null,
            _uncertainties.Skip(beforeUncertainties).ToArray());
        // A global script the verb started may have loaded a room and carried on.
        if (DispatchRequestedLoads("during the action"))
        {
            LastAction = LastAction with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
            return LastAction;
        }
        var roomLoad = _visitEvents.Skip(beforeEvents).FirstOrDefault(item =>
            item.Kind == LoomScriptEventKind.RoomLoaded && item.Certainty == LoomScriptEventCertainty.Reached &&
            !_dispatchedLoads.Contains(item));
        var targetId = roomLoad?.TargetId;
        var target = targetId is int id
            ? _game.Rooms.FirstOrDefault(item => item.ResourceId == id) : null;
        if (roomLoad is not null && target is null)
            _uncertainties.Add($"Reached room load has no indexed destination ({roomLoad.Operands}).");
        var action = new LoomObjectActionResult(sourceRoomId, objectId, verbId,
            script?.ResourceOffset ?? record.CodeOffset, script, run, roomLoad,
            target?.ResourceId, _uncertainties.Skip(beforeUncertainties).ToArray());
        if (target is not null)
        {
            VisitRoom(target, action);
            action = action with { Uncertainties = _uncertainties.Skip(beforeUncertainties).ToArray() };
        }
        LastAction = action;
        return action;
    }

    /// <summary>
    /// Continues every pending script by one frame. When a continued script
    /// reaches a literal room load, the session visits that room, as
    /// <see cref="ActivateObjectVerb"/> does for a load reached directly, and
    /// the last action records the continued load as its outcome.
    /// </summary>
    public LoomSessionSnapshot AdvanceFrame()
    {
        var snapshot = AdvanceOneFrame();
        if (_browsingStaffFloor.HasValue) { ApplyBrowsingStaffFloor(); snapshot = Snapshot; }
        if (!_playingBoot) return snapshot;
        ContinuePlayedBoot();
        return Snapshot;
    }

    private LoomSessionSnapshot AdvanceOneFrame()
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
            item.Kind == LoomScriptEventKind.RoomLoaded && item.Certainty == LoomScriptEventCertainty.Reached &&
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
    /// in the room (<see cref="LoomActorTable.StepWalks"/>), then <c>moveCamera</c> moves the camera up to 8 pixels
    /// (<see cref="LoomCamera.Move"/>); the next frame's <c>processActors</c> draws and animates them.
    /// </summary>
    private void StepWalksAndCamera()
    {
        if (_currentRoom is null) return;
        var place = CurrentPlacement();
        _runtime.Actors.StepWalks(place);
        if (!_runtime.GlobalVariables.TryGetValue(CameraVariable, out var camera) || _runtime.RoomCameraBounds is not { } bounds) return;
        var (min, max) = _runtime.CameraRange ?? bounds;
        var cameraState = _runtime.Camera;
        LoomActorSnapshot? followed = null;
        if (cameraState.Mode == LoomCamera.FollowActor || cameraState.MovingToActor)
            followed = LoomActorTable.IsValid(cameraState.Follows) ? _runtime.Actors[cameraState.Follows] : null;
        var moved = cameraState.Move(camera, min, max, bounds, followed?.X);
        if (moved == camera) return;
        _runtime.GlobalVariables[CameraVariable] = (ushort)moved;
        if (followed?.Estimate is not null && _runtime.EstimatedVariables.Add(CameraVariable))
            _uncertainties.Add($"Camera x (var[0x002]) = {moved} is an estimate: it follows actor {followed.Number}, whose position is an estimate ({followed.Estimate}).");
    }

    /// <summary>True while an actor in the current room walks or turns (ScummVM <c>_moving</c>).</summary>
    public bool ActorsMoving => _runtime.Actors.Snapshot().Any(actor => actor.Moving != 0 && actor.Room == _currentRoom?.ResourceId);

    /// <summary>
    /// True while the ego (<c>VAR_EGO</c>) walks or turns. Playback waits for this, not for every actor: ambient
    /// actors (script 042's swan speck in Room 02) walk from point to point without end.
    /// </summary>
    public bool EgoMoving => _runtime.GlobalVariables.TryGetValue(EgoVariable, out var ego) && LoomActorTable.IsValid(ego) &&
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
            if (camera.MovingToActor && LoomActorTable.IsValid(camera.Follows) && _runtime.Actors[camera.Follows].X is int actorX)
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
    /// Since messages last their talk delay (<see cref="LoomMessageTable"/>),
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
    private bool DispatchRequestedLoads(string when)
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
    private void EnterReachedLoad(LoomScriptEvent roomLoad, GameResource target, ScriptFrame? frame, string when)
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
        LoomObjectActionResult action;
        if (LastAction is { ReachedRoomLoad: null } pending && pending.SourceRoomId == sourceRoomId)
            action = pending with { ReachedRoomLoad = roomLoad, DestinationRoomId = target.ResourceId };
        else if (frame is not null && frame == _actionLoadFrame &&
                 LastAction is { ReachedRoomLoad: { } earlier } last && last.DestinationRoomId == sourceRoomId)
        {
            _uncertainties.Add($"{earlier.SourceLabel} passed through Room {sourceRoomId:00} ({earlier.Operation} at +0x{earlier.Offset:X4}) before this load.");
            action = last with { ReachedRoomLoad = roomLoad, DestinationRoomId = target.ResourceId };
        }
        else
            action = new LoomObjectActionResult(sourceRoomId, 0, 0, roomLoad.Offset, null, null, roomLoad,
                target.ResourceId, []);
        _actionLoadFrame = frame;
        _uncertainties.Add($"{roomLoad.SourceLabel} +0x{roomLoad.Offset:X4} reached {roomLoad.Operation} {when}.");
        VisitRoom(target, action);
        LastAction = action with { Uncertainties = [.. action.Uncertainties, .. _uncertainties.Skip(beforeUncertainties)] };
    }

    /// <summary>
    /// ScummEngine::isScriptRunning: a global or local script slot with this
    /// number is running (executing, waiting for a later frame, or frozen).
    /// A script that ended at a step the preview does not model might still
    /// run in the game, so that answer is an estimate.
    /// </summary>
    private (bool Running, string? Estimate) IsScriptRunning(int number)
    {
        var running = _executing.Any(frame => frame.Number == number && !frame.Stopped) ||
                      _pending.Any(item => item.ScriptNumber == number && !item.Frame.Stopped) ||
                      _frameScripts.Any(item => item.ScriptNumber == number && !item.Frame.Stopped);
        return running || !_endedUnmodelled.Contains(number)
            ? (running, null)
            : (false, $"script {number} ended at a step the preview does not model, so it may still run in the game");
    }

    private readonly List<LoomScriptEvent> _visitEvents = [];
    private readonly List<LoomSessionSoundEvent> _soundEvents = [];
    private readonly List<string> _visitTrace = [];
    private readonly List<LoomRandomPick> _picks = [];
    // Scripts running now, outermost first; the last one is the current script.
    private readonly List<ScriptFrame> _executing = [];
    // Slots still awaiting their turn this frame remain visible to nested
    // freeze/unfreeze and isScriptRunning, as vm.slot does in runAllScripts.
    private readonly List<PendingScript> _frameScripts = [];
    // beginCutscene's vm.cutSceneData stack, and the script whose cutscene start
    // script is running (vm.cutSceneScriptIndex), which freezeScripts leaves running.
    private readonly Stack<(ushort Data, ScriptFrame? Owner)> _cutscenes = new();
    // ScummVM vm.cutScenePtr/cutSceneScript per cutscene depth (0: no cutscene open).
    private readonly Dictionary<int, (ScriptFrame Frame, int JumpOffset)> _overrides = new();
    private ScriptFrame? _cutsceneOwner;
    // Room loads by global scripts: the script pauses after the load, the session
    // enters the room, and the script resumes there (only inside an action or frame).
    private int _dispatchScope;
    // Inside an action, input or frame advance, the session enters the first
    // reached room load once the scripts have run (ActivateObjectVerb, FinishInput, AdvanceFrame).
    private int _entersRoomLoads;
    // True while the constructor runs the boot; its loads are not player actions.
    private bool _booting;
    private (LoomScriptEvent Event, ScriptFrame Frame)? _requestedLoad;
    private readonly HashSet<LoomScriptEvent> _dispatchedLoads = new(ReferenceEqualityComparer.Instance);
    private ScriptFrame? _actionLoadFrame;
    // chainScript: the script that chained, and what it starts once it has ended.
    private (ScriptFrame Frame, int Script, IReadOnlyDictionary<string, ushort> Arguments,
        IReadOnlyCollection<string> EstimatedArguments, bool PathEstimated)? _chained;
    // Script numbers whose last run ended at a step the preview does not model.
    private readonly HashSet<int> _endedUnmodelled = [];

    /// <summary>ScummVM <c>VAR_CUTSCENE_START_SCRIPT</c>, <c>VAR_CUTSCENE_END_SCRIPT</c> and <c>VAR_OVERRIDE</c>.</summary>
    private const string CutsceneStartScriptVariable = "var[0x023]";
    private const string CutsceneEndScriptVariable = "var[0x024]";
    private const string OverrideVariable = "var[0x005]";

    /// <summary>Session-level steps (such as stopped scripts) in the order they were applied.</summary>
    public IReadOnlyList<string> SessionTrace => _visitTrace;

    private void RunGlobalHook(int variableId, string phase)
    {
        var key = $"var[0x{variableId:X3}]";
        if (!_runtime.GlobalVariables.TryGetValue(key, out var scriptId))
        {
            _uncertainties.Add($"Global {phase} hook {key} is unknown because startup has not established it.");
            return;
        }
        if (scriptId != 0) RunIndexedScript(scriptId, 0, null, 0);
    }

    private void RunEmbeddedRoomScript(GameResource room, bool exit)
    {
        LoomScriptDisassembly? script;
        try
        {
            script = exit ? _reader.DecodeRoomExit(room) : _reader.DecodeRoomEntry(room);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _uncertainties.Add($"Room {room.ResourceId:00} {(exit ? "exit" : "entry")} script unavailable: {ex.Message}");
            return;
        }
        if (!exit) { LastEntryScript = script; LastEntryRun = null; }
        if (script is null) return;
        var result = RunScript(script, _currentObjects, 0, null, 0);
        if (!exit) LastEntryRun = result;
    }

    private LoomBoundedScriptResult? RunIndexedScript(int scriptId, int startOffset,
        IReadOnlyDictionary<string, ushort>? locals, int depth,
        IReadOnlyCollection<string>? estimatedLocals = null, bool pathEstimated = false,
        ScriptFrame? frame = null)
    {
        var resource = _game.Resources.FirstOrDefault(item => item.Kind == ResourceKind.Script && item.ResourceId == scriptId);
        if (resource is null)
        {
            _uncertainties.Add($"Indexed script {scriptId:00} is absent from the parsed resource list.");
            return null;
        }
        try
        {
            return RunScript(_reader.Decode(resource), _currentObjects, startOffset, locals, depth, scriptId,
                estimatedLocals, pathEstimated, frame);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _uncertainties.Add($"Indexed script {scriptId:00} could not be decoded: {ex.Message}");
            return null;
        }
    }

    private LoomBoundedScriptResult? RunStartedScript(int scriptId, int startOffset,
        IReadOnlyDictionary<string, ushort>? locals, int depth,
        IReadOnlyCollection<string>? estimatedLocals = null, bool pathEstimated = false,
        bool freezeResistant = false, ScriptFrame? parent = null, bool recursive = false)
    {
        // A started script carries the room origin of the script that started it.
        parent ??= _executing.Count > 0 ? _executing[^1] : null;
        // runScript: a start that is not recursive first ends the running copies (stopScript).
        if (!recursive)
        {
            var ended = StopRunningScript(scriptId);
            if (ended > 0)
            {
                _visitTrace.Add($"script {scriptId} started again: runScript ended its running copy first");
            }
        }
        var frame = new ScriptFrame { FreezeResistant = freezeResistant, Recursive = recursive, OriginRoom = parent?.OriginRoom };
        if (scriptId < _globalScriptCount)
            return RunIndexedScript(scriptId, startOffset, locals, depth, estimatedLocals, pathEstimated, frame);
        if (_currentRoom is null)
        {
            _uncertainties.Add($"Local script {scriptId:00} cannot run before a room is loaded.");
            return null;
        }
        try
        {
            var script = _reader.DecodeRoomLocalScript(_currentRoom, scriptId, _globalScriptCount);
            if (script is null)
            {
                _uncertainties.Add($"Room {_currentRoom.ResourceId:00} has no local script {scriptId:00}.");
                return null;
            }
            return RunScript(script, _currentObjects, startOffset, locals, depth, scriptId,
                estimatedLocals, pathEstimated, frame);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _uncertainties.Add($"Room {_currentRoom.ResourceId:00} local script {scriptId:00} could not be decoded: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// o5_startObject → runObjectScript (v3): nothing happens for object 0 or an
    /// object that is neither in the room nor held (<c>WIO_NOT_FOUND</c>), or
    /// when the object has no entry for the verb and no fallback
    /// (<c>getVerbEntrypoint</c> 0). Otherwise the verb's code runs at once with
    /// the arguments as locals (<c>runScriptNested</c>). Held objects' code is
    /// not modelled; stopping the object's earlier script (<c>stopObjectScript</c>) is not modelled.
    /// </summary>
    private void StartObjectScript(LoomScriptEvent scriptEvent, int depth)
    {
        var source = $"{scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}";
        if (scriptEvent.ResolvedArguments is not [int objectId, int verbId])
        {
            _uncertainties.Add($"{source}: startObject reads an object or verb the preview does not know ({scriptEvent.Operands}); the parent continues without its effects.");
            return;
        }
        if (objectId == 0 || _currentRoom is null) return;
        var record = _currentObjects.FirstOrDefault(item => item.Id == objectId);
        if (record is null)
        {
            _uncertainties.Add($"{source}: startObject {objectId}, verb 0x{verbId:X2}: the object is not in Room {_currentRoom.ResourceId:00}; its code runs only if it is held, and inventory code is not modelled.");
            return;
        }
        if (!TryReadLocalArguments(scriptEvent.Operands, _runtime.GlobalVariables, out var locals))
        {
            _uncertainties.Add($"{source}: startObject {objectId} has arguments depending on unknown variables; the parent continues without its effects.");
            return;
        }
        LoomScriptDisassembly script;
        try { script = _verbs.Decode(_currentRoom, record, verbId); }
        catch (InvalidDataException ex) when (ex.Message.Contains("has no verb", StringComparison.Ordinal))
        {
            _visitTrace.Add($"{source}: object {objectId} has no verb 0x{verbId:X2} and no fallback, so startObject does nothing");
            return;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _uncertainties.Add($"{source}: object {objectId} verb 0x{verbId:X2} could not be decoded: {ex.Message}");
            return;
        }
        var parent = _executing.Count > 0 ? _executing[^1] : null;
        _visitTrace.Add($"{source}: startObject runs object {objectId} verb 0x{verbId:X2}");
        RunScript(script, _currentObjects, 0, locals, depth + 1, null,
            EstimatedArguments(scriptEvent),
            scriptEvent.Certainty != LoomScriptEventCertainty.Reached,
            new ScriptFrame { OriginRoom = parent?.OriginRoom });
    }

    private static int ReadGlobalScriptCount(GameLibrary game)
    {
        var indexPath = Path.Combine(game.FolderPath, "00.LFL");
        if (File.Exists(indexPath))
            return new LflIndexReader().ReadDeclaredScriptCount(indexPath);
        return game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue)
                   .Select(item => item.ResourceId!.Value).DefaultIfEmpty(-1).Max() + 1;
    }

    private LoomBoundedScriptResult RunScript(LoomScriptDisassembly script,
        IReadOnlyList<LoomRoomObjectRecord> objects, int startOffset,
        IReadOnlyDictionary<string, ushort>? locals, int depth, int? scriptNumber = null,
        IReadOnlyCollection<string>? estimatedLocals = null, bool pathEstimated = false,
        ScriptFrame? frame = null)
    {
        if (depth >= MaxNestedScripts)
        {
            _uncertainties.Add($"Nested script limit reached in {script.Context ?? $"Script {script.ScriptId:00}"}.");
            return new LoomBoundedScriptResult(_runtime.ObjectStates, _runtime.ObjectPositions,
                _runtime.GlobalVariables, [], [], 0, false, "nested script limit reached");
        }
        frame ??= new ScriptFrame();
        frame.Label = script.Context ?? $"Script {script.ScriptId:00}";
        frame.Number = scriptNumber;
        // Indexed scripts run in global slots; room entry, exit, local and verb code is room-owned.
        frame.Global = script.Context is null;
        if (!frame.Global) frame.OriginRoom = _currentRoom?.ResourceId;
        _executing.Add(frame);
        LoomBoundedScriptResult result;
        try
        {
            result = _executor.Execute(script, _runtime.ObjectStates, objects,
                runtime: _runtime, startOffset: startOffset, initialLocals: locals,
                onEvent: (scriptEvent, _) =>
                {
                    var handling = HandleEvent(scriptEvent, depth);
                    // stopScript also kills executing/nested slots. A stopped caller
                    // cannot resume after its child returns, even if a new copy of
                    // the same script number was started in the meantime.
                    if (frame.Stopped) return LoomBoundedScriptExecutor.StopAfterEvent;
                    // runScriptNested restores a caller only when its freezeCount is zero.
                    // A child may freeze its still-executing parent; save the parent's next
                    // instruction instead of letting it open another scope while frozen.
                    if (string.IsNullOrEmpty(handling) && frame.FreezeCount > 0)
                    {
                        _visitTrace.Add($"{frame.Label} pauses after {scriptEvent.Operation} +0x{scriptEvent.Offset:X4}: nested execution froze its caller");
                        return LoomBoundedScriptExecutor.PauseAfterEvent;
                    }
                    return handling;
                },
                estimatedLocals: estimatedLocals, pathEstimated: pathEstimated);
        }
        finally
        {
            _executing.RemoveAt(_executing.Count - 1);
        }
        var label = script.Context ?? $"Script {script.ScriptId:00}";
        // o5_delay leaves the slot ssPaused with its delay; any other pause or end clears it.
        frame.Delay = result.ResumeOffset is null ? null : result.Delay;
        if (frame.Delay is int delay) _visitTrace.Add($"{label} waits {delay} jiffies (delay) before +0x{result.ResumeOffset:X4}");
        if (result.DelayEstimate is not null) _uncertainties.Add($"{label}: {result.DelayEstimate}; when it runs again is an estimate.");
        foreach (var pick in result.RandomPicks)
        {
            _picks.Add(pick);
            _uncertainties.Add($"{label} +0x{startOffset:X4}: {pick.Describe()}.");
        }
        if (result.UnresolvedReason is not null)
            _uncertainties.Add($"{label} +0x{startOffset:X4}: {result.UnresolvedReason}");
        if (scriptNumber is int number)
        {
            if (!result.ReachedStop && result.ResumeOffset is null) _endedUnmodelled.Add(number);
            else _endedUnmodelled.Remove(number);
        }
        if (_chained is { } chain && chain.Frame == frame)
        {
            _chained = null;
            _visitTrace.Add($"{label} chains to script {chain.Script}");
            RunStartedScript(chain.Script, 0, chain.Arguments, depth, chain.EstimatedArguments, chain.PathEstimated,
                frame.FreezeResistant, frame, frame.Recursive);
        }
        if (result.ResumeOffset is int next && !frame.Stopped)
        {
            var preservedLocals = result.KnownVariables
                .Where(pair => pair.Key.StartsWith("local[", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            _pending.Add(new PendingScript(script, next, preservedLocals,
                script.Context is null ? null : _currentRoom?.ResourceId, scriptNumber,
                result.EstimatedLocals, result.PathEstimated, frame));
        }
        return result;
    }

    private string? HandleEvent(LoomScriptEvent scriptEvent, int depth)
    {
        _visitEvents.Add(scriptEvent);
        if (scriptEvent.Kind is LoomScriptEventKind.MusicStarted or LoomScriptEventKind.SoundStarted or
            LoomScriptEventKind.MusicStopped or LoomScriptEventKind.SoundStopped)
            _soundEvents.Add(new LoomSessionSoundEvent(scriptEvent, _visitEvents.Count - 1, _runtime.CurrentRoom,
                _executing.Count > 0 ? _executing[^1].OriginRoom : null));
        switch (scriptEvent.Kind)
        {
            case LoomScriptEventKind.ScriptStarted:
                if (scriptEvent.TargetId is not int scriptId)
                {
                    _uncertainties.Add("A started script has a dynamic target; the parent continues without its effects.");
                    return "";
                }
                if (!TryReadLocalArguments(scriptEvent.Operands, _runtime.GlobalVariables, out var locals))
                {
                    _uncertainties.Add($"{scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: started script {scriptId:00} has arguments depending on unknown variables; the parent continues without its effects.");
                    return "";
                }
                // startScript launches a child. An unresolved child does not
                // cancel instructions remaining in the caller. A child started
                // behind a seeded pick, or given a picked value, runs as an estimate.
                RunStartedScript(scriptId, 0, locals, depth + 1,
                    EstimatedArguments(scriptEvent),
                    scriptEvent.Certainty != LoomScriptEventCertainty.Reached, scriptEvent.FreezeResistant,
                    recursive: scriptEvent.Recursive);
                return "";
            case LoomScriptEventKind.CutsceneStarted:
                return BeginCutscene(scriptEvent, depth);
            case LoomScriptEventKind.CutsceneEnded:
                return EndCutscene(scriptEvent, depth);
            case LoomScriptEventKind.ScriptsFrozen:
                return FreezeScripts(scriptEvent);
            case LoomScriptEventKind.ScriptChained:
                // o5_chainScript ends the current script and starts the target in
                // its place, with the arguments and its freeze resistance.
                if (scriptEvent.TargetId is not int chained)
                    return "chained script depends on an unknown variable";
                if (_executing.Count == 0 || !TryReadLocalArguments(scriptEvent.Operands, _runtime.GlobalVariables, out var chainArguments))
                    return "chained script arguments depend on unknown variables";
                _chained = (_executing[^1], chained, chainArguments,
                    EstimatedArguments(scriptEvent),
                    scriptEvent.Certainty != LoomScriptEventCertainty.Reached);
                return LoomBoundedScriptExecutor.StopAfterEvent;
            case LoomScriptEventKind.ScriptStopped:
                if (scriptEvent.TargetId is not int stoppedScript)
                    return "stopped script depends on an unknown variable";
                // ScummEngine::stopScript ends every running slot with this
                // number; a script that is not running is left as it is.
                var pendingStopped = _pending.Any(item => item.ScriptNumber == stoppedScript && !item.Frame.Stopped);
                var removed = StopRunningScript(stoppedScript);
                _visitTrace.Add(removed == 0
                    ? $"stopScript {stoppedScript} at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: script {stoppedScript} was not running"
                    : $"stopScript {stoppedScript} at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: ended its {(pendingStopped ? "pending" : "running")} run");
                return "";
            case LoomScriptEventKind.ObjectScriptStarted:
                StartObjectScript(scriptEvent, depth);
                return "";
            case LoomScriptEventKind.MusicStarted:
            case LoomScriptEventKind.SoundStarted:
                if (scriptEvent.TargetId is not int soundId) return "sound target depends on an unknown variable";
                _runtime.ActiveSounds.Add(soundId);
                _soundClock.Start(soundId, scriptEvent.Certainty != LoomScriptEventCertainty.Reached);
                var soundStatus = _soundClock.Status(soundId);
                if (soundStatus.Estimate is not null)
                    _uncertainties.Add($"Sound {soundId:00} status is an estimate: {soundStatus.Estimate}.");
                return "";
            case LoomScriptEventKind.MusicStopped:
                _runtime.ActiveSounds.Clear();
                _soundClock.StopAll();
                return "";
            case LoomScriptEventKind.SoundStopped:
                if (scriptEvent.TargetId is not int stoppedId) return "stopped sound target depends on an unknown variable";
                _runtime.ActiveSounds.Remove(stoppedId);
                _soundClock.Stop(stoppedId, scriptEvent.Certainty != LoomScriptEventCertainty.Reached);
                return "";
            case LoomScriptEventKind.RoomLoaded when scriptEvent.Certainty != LoomScriptEventCertainty.Reached:
                return $"possible Room {scriptEvent.TargetId?.ToString("00", CultureInfo.InvariantCulture) ?? "dynamic"} load follows a seeded pick, so the session does not enter it";
            case LoomScriptEventKind.RoomLoaded when _dispatchScope > 0 && _requestedLoad is null &&
                                                      _executing.Count > 0 && _executing[^1].Global &&
                                                      scriptEvent.TargetId is int loadedRoom &&
                                                      _game.Rooms.Any(room => room.ResourceId == loadedRoom):
                // A global script survives startScene: pause it here, enter the
                // room, then resume it there (DispatchRequestedLoads).
                _requestedLoad = (scriptEvent, _executing[^1]);
                return LoomBoundedScriptExecutor.PauseAfterEvent;
            case LoomScriptEventKind.RoomLoaded when _entersRoomLoads > 0 && _executing.Count > 0 && !_executing[^1].Global &&
                                                      scriptEvent.TargetId is int enteredRoom &&
                                                      _game.Rooms.Any(room => room.ResourceId == enteredRoom):
                // ScummVM startScene ends the room, object or local script that
                // loads the room (_currentScript = 0xFF); the session enters the room next.
                _visitTrace.Add($"{scriptEvent.Operation} at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4} ends this room-owned script (startScene); the session enters Room {enteredRoom:00}");
                return LoomBoundedScriptExecutor.StopAfterEvent;
            case LoomScriptEventKind.RoomLoaded:
                return $"script requests Room {scriptEvent.TargetId?.ToString("00", CultureInfo.InvariantCulture) ?? "dynamic"}; room transition awaits bounded session dispatch";
            default:
                return null;
        }
    }

    /// <summary>
    /// ScummEngine::beginCutscene: pushes the first argument as the cutscene's
    /// data and runs the script in <c>VAR_CUTSCENE_START_SCRIPT</c> with the
    /// arguments. The calling script stays exempt from a freeze while it runs.
    /// </summary>
    private string BeginCutscene(LoomScriptEvent scriptEvent, int depth)
    {
        if (!TryReadLocalArguments(scriptEvent.Operands, _runtime.GlobalVariables, out var arguments))
            return "cutscene arguments depend on unknown variables";
        _cutscenes.Push((arguments.GetValueOrDefault("local[0x000]"), _executing.Count > 0 ? _executing[^1] : null));
        CutscenesBegun++;
        _overrides.Remove(_cutscenes.Count);
        if (!_runtime.GlobalVariables.TryGetValue(CutsceneStartScriptVariable, out var startScript))
        {
            _uncertainties.Add($"{scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: the cutscene start script ({CutsceneStartScriptVariable}) is unknown; its effects were not applied.");
            return "";
        }
        _visitTrace.Add($"cutscene at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4} runs script {startScript}");
        if (startScript == 0) return "";
        var owner = _cutsceneOwner;
        _cutsceneOwner = _executing.Count > 0 ? _executing[^1] : null;
        try
        {
            RunStartedScript(startScript, 0, arguments, depth + 1,
                EstimatedArguments(scriptEvent),
                scriptEvent.Certainty != LoomScriptEventCertainty.Reached);
        }
        finally
        {
            _cutsceneOwner = owner;
        }
        return "";
    }

    /// <summary>
    /// ScummEngine::endCutscene: clears <c>VAR_OVERRIDE</c>, pops the cutscene's
    /// data and runs the script in <c>VAR_CUTSCENE_END_SCRIPT</c> with it.
    /// </summary>
    private string EndCutscene(LoomScriptEvent scriptEvent, int depth)
    {
        var estimate = scriptEvent.Certainty != LoomScriptEventCertainty.Reached;
        _runtime.GlobalVariables[OverrideVariable] = 0;
        if (estimate) _runtime.EstimatedVariables.Add(OverrideVariable);
        else _runtime.EstimatedVariables.Remove(OverrideVariable);
        ushort data = 0;
        _overrides.Remove(_cutscenes.Count);
        if (_cutscenes.Count > 0) data = _cutscenes.Pop().Data;
        else _uncertainties.Add($"{scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: endCutscene without a cutscene begun on this route; its data is taken as 0.");
        if (!_runtime.GlobalVariables.TryGetValue(CutsceneEndScriptVariable, out var endScript))
        {
            _uncertainties.Add($"{scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: the cutscene end script ({CutsceneEndScriptVariable}) is unknown; its effects were not applied.");
            return "";
        }
        _visitTrace.Add($"endCutscene at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4} runs script {endScript}");
        if (endScript != 0)
            RunStartedScript(endScript, 0, new Dictionary<string, ushort> { ["local[0x000]"] = data }, depth + 1,
                [], estimate);
        return "";
    }

    /// <summary>
    /// ScummEngine::beginOverride and endOverride: the running script's skip
    /// jump is recorded for the open cutscene depth, or cleared.
    /// </summary>
    private void RecordOverride(int? jumpOffset)
    {
        if (_executing.Count == 0) return;
        if (jumpOffset is int offset) _overrides[_cutscenes.Count] = (_executing[^1], offset);
        else _overrides.Remove(_cutscenes.Count);
    }

    /// <summary>
    /// ScummEngine::abortCutscene: the script that set the override at the open
    /// cutscene depth continues at its skip jump, unfrozen, with
    /// <c>VAR_OVERRIDE</c> = 1, and the override is cleared. With no override it
    /// does nothing. Returns what happened, for the key's label.
    /// </summary>
    /// <summary>
    /// True when Escape would skip now (ScummVM <c>processKeyboard</c> → <c>abortCutscene</c>): <c>VAR_CUTSCENEEXIT_KEY</c>
    /// is set, and the open cutscene has an override (<c>beginOverride</c>) whose script waits for a later frame.
    /// Before a cutscene reaches its <c>beginOverride</c> (script 052 sets it at +0x00C6, in Room 11), Escape does nothing.
    /// </summary>
    public bool CutsceneSkippable =>
        _runtime.GlobalVariables.TryGetValue(CutsceneExitKeyVariable, out var exitKey) && exitKey != 0 &&
        _overrides.TryGetValue(_cutscenes.Count, out var point) && _pending.Any(item => item.Frame == point.Frame);

    private string AbortCutscene()
    {
        var depth = _cutscenes.Count;
        if (!_overrides.TryGetValue(depth, out var point))
            return depth == 0
                ? "no cutscene is open and no override is set, so it does nothing"
                : "the open cutscene has no override set (beginOverride), so it does nothing";
        var index = _pending.FindIndex(item => item.Frame == point.Frame);
        if (index < 0)
            return $"the override's script ({point.Frame.Label}) is not waiting for a later frame, so the session does not move it to its skip jump";
        _overrides.Remove(depth);
        _pending[index] = _pending[index] with { Offset = point.JumpOffset };
        point.Frame.FreezeCount = 0;
        _runtime.GlobalVariables[OverrideVariable] = 1;
        _runtime.EstimatedVariables.Remove(OverrideVariable);
        _visitTrace.Add($"abortCutscene: {point.Frame.Label} continues at its skip jump +0x{point.JumpOffset:X4}, {OverrideVariable} = 1");
        return $"{point.Frame.Label} continues at its skip jump (+0x{point.JumpOffset:X4}) on the next frame, unfrozen, with {OverrideVariable} (VAR_OVERRIDE) = 1";
    }

    /// <summary>
    /// ScummEngine::freezeScripts and unfreezeScripts. A nonzero flag freezes
    /// every other running script, except freeze-resistant ones when the flag
    /// is below 0x80, and leaves the cutscene's owner running; 0 undoes one
    /// freeze. A frozen script keeps its place and does not run on later frames.
    /// </summary>
    private string FreezeScripts(LoomScriptEvent scriptEvent)
    {
        var operand = scriptEvent.Operands.Trim();
        ushort flag;
        if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            ushort.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var literal))
            flag = literal;
        else if (!_runtime.GlobalVariables.TryGetValue(operand, out flag))
            return "freezeScripts flag depends on an unknown variable";
        var current = _executing.Count > 0 ? _executing[^1] : null;
        var slots = _executing.Where(frame => frame != current && !frame.Stopped)
            .Concat(_pending.Select(item => item.Frame))
            .Concat(_frameScripts.Where(item => !item.Frame.Stopped).Select(item => item.Frame))
            .Distinct().ToArray();
        if (flag != 0)
        {
            foreach (var slot in slots.Where(slot => !slot.FreezeResistant || flag >= 0x80))
                slot.FreezeCount++;
            if (_cutsceneOwner is not null) _cutsceneOwner.FreezeCount = 0;
        }
        else
        {
            foreach (var slot in slots.Where(slot => slot.FreezeCount > 0))
                slot.FreezeCount--;
        }
        var frozen = slots.Where(slot => slot.FreezeCount > 0).Select(slot => slot.Label).ToArray();
        _visitTrace.Add($"freezeScripts {operand} at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: " +
                        (frozen.Length == 0 ? "no script is frozen" : $"frozen: {string.Join(", ", frozen)}"));
        return "";
    }

    /// <summary>
    /// The child's locals that hold an estimate: an argument read from an
    /// estimated global, or an estimated local the executor passed by value.
    /// </summary>
    private string[] EstimatedArguments(LoomScriptEvent scriptEvent)
    {
        var operands = scriptEvent.Operands;
        var open = LoomBoundedScriptExecutor.ArgumentListStart(operands);
        var close = operands.LastIndexOf(']');
        if (open < 0 || close <= open) return [];
        var passed = scriptEvent.EstimatedArgumentIndexes ?? [];
        return operands[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select((argument, index) => (argument, index))
            .Where(item => _runtime.EstimatedVariables.Contains(item.argument) || passed.Contains(item.index))
            .Select(item => $"local[0x{item.index:X3}]").ToArray();
    }

    private static bool TryReadLocalArguments(string operands,
        IReadOnlyDictionary<string, ushort> globals, out IReadOnlyDictionary<string, ushort> locals)
    {
        var values = new Dictionary<string, ushort>(StringComparer.Ordinal);
        var open = LoomBoundedScriptExecutor.ArgumentListStart(operands);
        var close = operands.LastIndexOf(']');
        if (open < 0 || close <= open) { locals = values; return true; }
        var arguments = operands[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < arguments.Length; index++)
        {
            var arg = arguments[index];
            if (arg.StartsWith("var[", StringComparison.Ordinal))
            {
                if (!globals.TryGetValue(arg, out var dynamicValue)) { locals = values; return false; }
                values[$"local[0x{index:X3}]"] = dynamicValue;
            }
            else if (arg.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                     ushort.TryParse(arg.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var literal))
                values[$"local[0x{index:X3}]"] = literal;
            else { locals = values; return false; }
        }
        locals = values;
        return true;
    }

    /// <summary>
    /// The route's uncertainties in order. A script that loops for many frames
    /// reports the same limitation each frame; within one step (a visit, an
    /// action or a run of frames, see <see cref="StartStep"/>) it is kept once.
    /// </summary>
    private sealed class UncertaintyList : List<string>
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        public new void Add(string item)
        {
            if (_seen.Add(item)) base.Add(item);
        }
        public void StartStep() => _seen.Clear();
    }

    private sealed record PendingScript(LoomScriptDisassembly Script, int Offset,
        IReadOnlyDictionary<string, ushort> Locals, int? RoomId, int? ScriptNumber,
        IReadOnlyCollection<string> EstimatedLocals, bool PathEstimated, ScriptFrame Frame);

    /// <summary>stopScript kills existing global/local slots, including nested
    /// callers and slots awaiting their turn in this frame. It does not kill a
    /// replacement created later with the same number (runScriptNested).</summary>
    private int StopRunningScript(int number)
    {
        var slots = _executing.Concat(_pending.Select(item => item.Frame))
            .Concat(_frameScripts.Select(item => item.Frame))
            .Where(frame => frame.Number == number && !frame.Stopped).Distinct().ToArray();
        foreach (var slot in slots) slot.Stopped = true;
        _pending.RemoveAll(item => item.Frame.Stopped);
        return slots.Length;
    }

    /// <summary>
    /// The freeze state of one running script slot (ScummVM <c>ScriptSlot</c>'s
    /// <c>freezeResistant</c> and <c>freezeCount</c>). It stays with the script
    /// while it runs and while it waits for a later frame.
    /// </summary>
    private sealed class ScriptFrame
    {
        public bool FreezeResistant { get; init; }
        /// <summary>ScummVM <c>ScriptSlot.recursive</c>: a <c>chainScript</c> from this slot passes it on.</summary>
        public bool Recursive { get; init; }
        /// <summary>Global or local script number; null for room entry, exit and verb code.</summary>
        public int? Number { get; set; }
        /// <summary>Cancellation belongs to this slot instance, never to a later copy of its number.</summary>
        public bool Stopped { get; set; }
        public bool Global { get; set; }
        public int FreezeCount { get; set; }
        /// <summary>ScummVM <c>ScriptSlot.delay</c> while the slot is <c>ssPaused</c> by <c>delay</c>/<c>delayVariable</c>; null otherwise.</summary>
        public int? Delay { get; set; }
        public string Label { get; set; } = "";
        /// <summary>
        /// The room whose entry, exit, local or verb code started this chain of
        /// scripts; null when it began in a global script the engine started
        /// (the boot or a room hook).
        /// </summary>
        public int? OriginRoom { get; set; }
    }
}
