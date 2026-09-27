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
    IReadOnlyDictionary<int, uint>? Classes = null)
{
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
    IReadOnlyList<string> Uncertainties)
{
    public string Evidence => ReachedRoomLoad is null
        ? $"Room {SourceRoomId:00} object {ObjectId}, verb 0x{VerbId:X2}, code 0x{ScriptOffset:X4}; no room load was reached."
        : $"Room {SourceRoomId:00} object {ObjectId}, verb 0x{VerbId:X2}, code 0x{ScriptOffset:X4}; " +
          $"{ReachedRoomLoad.Operation} at {ReachedRoomLoad.SourceLabel} +0x{ReachedRoomLoad.Offset:X4} → " +
          (DestinationRoomId is int target ? $"Room {target:00}" : "unresolved room") + ".";
}

/// <summary>
/// Keeps a conservative, route-scoped LOOM state. Only instructions reached by
/// bounded execution change the snapshot. Unsupported state or timing is
/// recorded and prevents the snapshot from being labelled faithful.
/// </summary>
public sealed class LoomExplorationSession
{
    private const int MaxNestedScripts = 8;
    private readonly GameLibrary _game;
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
    public LoomExplorationSession(GameLibrary game, LoomIndexedObjectSnapshot indexedObjects, int randomSeed = 0,
        bool completeBoot = true)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(indexedObjects);
        if (game.Version != ScummVersion.V3)
            throw new InvalidDataException("Only old-v3 LOOM has a session interpreter.");
        _game = game;
        _reader = new LoomScriptReader(ScummScriptDialects.For(game.Id));
        _verbs = new LoomObjectVerbReader(ScummScriptDialects.For(game.Id));
        _runtime = new LoomScriptRuntimeState(indexedObjects.States, indexedObjects.Owners, indexedObjects.Classes)
        {
            Random = new LoomSeededRandom(randomSeed)
        };
        _globalScriptCount = ReadGlobalScriptCount(game);
        _runtime.ScriptRunning = IsScriptRunning;
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
        CompleteBoot();
        _booting = false;
        LastAction = null;
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
    private void CompleteBoot()
    {
        if (_currentRoom is null) return;
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
        while (frames < BootFrameLimit && (_pending.Any(item => item.ScriptNumber == 1) || InCutscene) && _pending.Count > 0)
        {
            AdvanceFrame();
            frames++;
        }
        BootFrames = frames;
        if (_pending.Any(item => item.ScriptNumber == 1))
            _uncertainties.Add($"The boot (script 001) was still running after {frames} frames; the preview stopped following it.");
        _visitTrace.Add($"boot: {frames} frames after script 001's first room load; now in Room {_currentRoom?.ResourceId:00}");
    }

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
        new Dictionary<int, uint>(_runtime.ObjectClasses));

    private int[] KnownInventory() => _runtime.GlobalVariables.TryGetValue("var[0x001]", out var ego)
        ? _runtime.ObjectOwners.Where(pair => pair.Value == ego).Select(pair => pair.Key).Order().ToArray()
        : [];

    public LoomSessionSnapshot VisitRoom(GameResource room, LoomObjectActionResult? reachedAction = null)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (room.Kind != ResourceKind.Room || room.ResourceId is null)
            throw new InvalidDataException("The session can only visit an indexed room.");
        if (_currentRoom?.ResourceId == room.ResourceId) return Snapshot;
        _uncertainties.StartStep();
        if (reachedAction is null && _runtime.GlobalVariables.TryGetValue(EgoVariable, out var ego) && LoomActorTable.IsValid(ego))
            _runtime.Actors.Forget(ego, $"Room {room.ResourceId:00} was selected from the catalog, so where the ego would arrive was not executed");
        return EnterRoom(room, reachedAction is null
            ? $"Room {room.ResourceId:00} selected from the catalog; the route to it was not executed from game input."
            : $"{reachedAction.Evidence} Actor walk and input dispatch were not simulated.", reachedAction?.ReachedRoomLoad);
    }

    /// <summary>ScummVM <c>VAR_EGO</c>.</summary>
    private const string EgoVariable = "var[0x001]";

    /// <summary>Actor rooms, positions and walk boxes as reached code left them (actors 1–12).</summary>
    public IReadOnlyList<LoomActorSnapshot> Actors => _runtime.Actors.Snapshot();

    private LoomActorTable.Placement CurrentPlacement() =>
        new(_runtime.CurrentRoom, _runtime.WalkBoxes, _runtime.WalkBoxFlagsUnknown, _runtime.ObjectClasses);

    private LoomSessionSnapshot EnterRoom(GameResource room, string arrival, LoomScriptEvent? load = null)
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
        var roomBytes = File.ReadAllBytes(room.FilePath);
        _currentObjects = _objects.Decode(roomBytes);
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
        // startScene resets VAR_CAMERA_MIN_X and VAR_CAMERA_MAX_X (v3: vars 17 and 18).
        _runtime.GlobalVariables["var[0x011]"] = 160;
        _runtime.GlobalVariables["var[0x012]"] = (ushort)Math.Max(160, roomWidth - 160);
        _runtime.EstimatedVariables.Remove("var[0x011]");
        _runtime.EstimatedVariables.Remove("var[0x012]");
        foreach (var item in _currentObjects)
            _runtime.ObjectPositions.TryAdd(item.Id, new RoomObjectPosition(item.X, item.Y));
        _runtime.GlobalVariables["var[0x004]"] = (ushort)room.ResourceId!.Value;
        _roomSize = (roomWidth, (roomBytes[6] ^ 0xFF) | ((roomBytes[7] ^ 0xFF) << 8));
        PlaceVirtualMouse();
        _uncertainties.Add(arrival);

        RunGlobalHook(28, "before room entry");
        RunEmbeddedRoomScript(room, exit: false);
        RunGlobalHook(29, "after room entry");
        if (withEgo is not null && ego is int placedEgo) PlaceEgoAfterEntry(withEgo, placedEgo);
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
    private void PlaceEgoAfterEntry(LoomScriptEvent load, int ego)
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
                if (_runtime.EstimatedVariables.Contains(EgoVariable) || load.Certainty != LoomScriptEventCertainty.Reached)
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
        if (arguments[2] is int x && x != -1 && arguments[3] is int y)
        {
            _runtime.Actors.Walk(ego, x, y, CurrentPlacement(), $"the ego's walk from {source}");
            FollowEgoWalk(_runtime.Actors[ego], source);
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
        var x = CameraX ?? size.Width / 2;
        var y = size.Height;
        _runtime.GlobalVariables["var[0x014]"] = (ushort)x;
        _runtime.GlobalVariables["var[0x015]"] = (ushort)y;
        _runtime.EstimatedVariables.Remove("var[0x014]");
        _runtime.EstimatedVariables.Remove("var[0x015]");
        _runtime.VirtualMouse = (x, y);
        _runtime.VirtualMouseAssumption = $"assumed input: no pointer over the room, so the virtual mouse (var[0x014], var[0x015]) is taken to rest just below the room picture at ({x}, {y})";
    }

    /// <summary>The camera's centre x in room pixels (<c>var[0x002]</c>), or null when it is unknown.</summary>
    public int? CameraX => _runtime.GlobalVariables.TryGetValue(CameraVariable, out var x) ? x : null;

    /// <summary>True when <see cref="CameraX"/> comes from an estimate (a walk end, a seeded pick, an instant pan).</summary>
    public bool CameraEstimated => _runtime.EstimatedVariables.Contains(CameraVariable);

    /// <summary>Puts the camera at <paramref name="x"/>, kept half a screen from the room's edges (<c>cameraMoved</c>).</summary>
    private void SetCamera(int x, string? estimate, string trace)
    {
        var (min, max) = _runtime.CameraRange ?? (160, 160);
        var clamped = (ushort)Math.Clamp(x, min, max);
        _runtime.GlobalVariables[CameraVariable] = clamped;
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
        PlaceVirtualMouse();
        var beforeEvents = _visitEvents.Count;
        var runnable = _pending.ToArray();
        _pending.Clear();
        _stopped.Clear();
        _dispatchScope++;
        try
        {
            foreach (var item in runnable)
            {
                // A room load earlier in this frame ended the room's scripts.
                if (item.RoomId.HasValue && item.RoomId != _currentRoom?.ResourceId) continue;
                // An earlier script in this frame may have stopped this one.
                if (item.ScriptNumber is int number && _stopped.Contains(number)) continue;
                // runAllScripts skips frozen slots; they keep their place.
                if (item.Frame.FreezeCount > 0)
                {
                    _pending.Add(item);
                    continue;
                }
                RunScript(item.Script, item.RoomObjects, item.Offset, item.Locals, 0, item.ScriptNumber,
                    item.EstimatedLocals, item.PathEstimated, item.Frame);
                DispatchRequestedLoads("after a frame advance");
            }
        }
        finally
        {
            _dispatchScope--;
        }
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
    /// Advances frames while scripts are pending, until the session has left
    /// the room and no cutscene is open. Frames outside a cutscene count
    /// against <paramref name="maxFrames"/>; frames inside one count against
    /// <paramref name="maxCutsceneFrames"/>, because the player has no control
    /// until the cutscene ends. Returns true when the session is in another room.
    /// </summary>
    public bool ContinueUntilRoomChanges(int maxFrames, int maxCutsceneFrames = 1000)
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
        var running = _executing.Any(frame => frame.Number == number) ||
                      _pending.Any(item => item.ScriptNumber == number);
        return running || !_endedUnmodelled.Contains(number)
            ? (running, null)
            : (false, $"script {number} ended at a step the preview does not model, so it may still run in the game");
    }

    private readonly List<LoomScriptEvent> _visitEvents = [];
    private readonly List<LoomSessionSoundEvent> _soundEvents = [];
    private readonly List<string> _visitTrace = [];
    private readonly HashSet<int> _stopped = [];
    private readonly List<LoomRandomPick> _picks = [];
    // Scripts running now, outermost first; the last one is the current script.
    private readonly List<ScriptFrame> _executing = [];
    // beginCutscene's vm.cutSceneData stack, and the script whose cutscene start
    // script is running (vm.cutSceneScriptIndex), which freezeScripts leaves running.
    private readonly Stack<(ushort Data, ScriptFrame? Owner)> _cutscenes = new();
    private ScriptFrame? _cutsceneOwner;
    // Room loads by global scripts: the script pauses after the load, the session
    // enters the room, and the script resumes there (only inside an action or frame).
    private int _dispatchScope;
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
        bool freezeResistant = false, ScriptFrame? parent = null)
    {
        // A started script carries the room origin of the script that started it.
        parent ??= _executing.Count > 0 ? _executing[^1] : null;
        var frame = new ScriptFrame { FreezeResistant = freezeResistant, OriginRoom = parent?.OriginRoom };
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
                onEvent: (scriptEvent, _) => HandleEvent(scriptEvent, depth),
                estimatedLocals: estimatedLocals, pathEstimated: pathEstimated);
        }
        finally
        {
            _executing.RemoveAt(_executing.Count - 1);
        }
        var label = script.Context ?? $"Script {script.ScriptId:00}";
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
                frame.FreezeResistant, frame);
        }
        if (result.ResumeOffset is int next)
        {
            var preservedLocals = result.KnownVariables
                .Where(pair => pair.Key.StartsWith("local[", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            _pending.Add(new PendingScript(script, objects.ToArray(), next, preservedLocals,
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
                    _uncertainties.Add($"Started script {scriptId:00} has arguments depending on unknown variables; the parent continues without its effects.");
                    return "";
                }
                // startScript launches a child. An unresolved child does not
                // cancel instructions remaining in the caller. A child started
                // behind a seeded pick, or given a picked value, runs as an estimate.
                RunStartedScript(scriptId, 0, locals, depth + 1,
                    EstimatedArguments(scriptEvent.Operands, _runtime.EstimatedVariables),
                    scriptEvent.Certainty != LoomScriptEventCertainty.Reached, scriptEvent.FreezeResistant);
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
                    EstimatedArguments(scriptEvent.Operands, _runtime.EstimatedVariables),
                    scriptEvent.Certainty != LoomScriptEventCertainty.Reached);
                return LoomBoundedScriptExecutor.StopAfterEvent;
            case LoomScriptEventKind.ScriptStopped:
                if (scriptEvent.TargetId is not int stoppedScript)
                    return "stopped script depends on an unknown variable";
                // ScummEngine::stopScript ends every running slot with this
                // number; a script that is not running is left as it is.
                var removed = _pending.RemoveAll(item => item.ScriptNumber == stoppedScript);
                _stopped.Add(stoppedScript);
                _visitTrace.Add(removed == 0
                    ? $"stopScript {stoppedScript} at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: script {stoppedScript} was not running"
                    : $"stopScript {stoppedScript} at {scriptEvent.SourceLabel} +0x{scriptEvent.Offset:X4}: ended its pending run");
                return "";
            case LoomScriptEventKind.ObjectScriptStarted:
                _uncertainties.Add("A started object script has an unresolved entrypoint; the parent continues without its effects.");
                return "";
            case LoomScriptEventKind.MusicStarted:
            case LoomScriptEventKind.SoundStarted:
                if (scriptEvent.TargetId is not int soundId) return "sound target depends on an unknown variable";
                _runtime.ActiveSounds.Add(soundId);
                _uncertainties.Add($"Sound {soundId:00} was started; its natural completion time is not simulated.");
                return "";
            case LoomScriptEventKind.MusicStopped:
                _runtime.ActiveSounds.Clear();
                return "";
            case LoomScriptEventKind.SoundStopped:
                if (scriptEvent.TargetId is not int stoppedId) return "stopped sound target depends on an unknown variable";
                _runtime.ActiveSounds.Remove(stoppedId);
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
                EstimatedArguments(scriptEvent.Operands, _runtime.EstimatedVariables),
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
        var slots = _executing.Where(frame => frame != current)
            .Concat(_pending.Select(item => item.Frame)).Distinct().ToArray();
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

    private static string[] EstimatedArguments(string operands, IReadOnlySet<string> estimatedGlobals)
    {
        var open = operands.IndexOf('[');
        var close = operands.LastIndexOf(']');
        if (open < 0 || close <= open) return [];
        return operands[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select((argument, index) => (argument, index))
            .Where(item => estimatedGlobals.Contains(item.argument))
            .Select(item => $"local[0x{item.index:X3}]").ToArray();
    }

    private static bool TryReadLocalArguments(string operands,
        IReadOnlyDictionary<string, ushort> globals, out IReadOnlyDictionary<string, ushort> locals)
    {
        var values = new Dictionary<string, ushort>(StringComparer.Ordinal);
        var open = operands.IndexOf('[');
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

    private sealed record PendingScript(LoomScriptDisassembly Script,
        IReadOnlyList<LoomRoomObjectRecord> RoomObjects, int Offset,
        IReadOnlyDictionary<string, ushort> Locals, int? RoomId, int? ScriptNumber,
        IReadOnlyCollection<string> EstimatedLocals, bool PathEstimated, ScriptFrame Frame);

    /// <summary>
    /// The freeze state of one running script slot (ScummVM <c>ScriptSlot</c>'s
    /// <c>freezeResistant</c> and <c>freezeCount</c>). It stays with the script
    /// while it runs and while it waits for a later frame.
    /// </summary>
    private sealed class ScriptFrame
    {
        public bool FreezeResistant { get; init; }
        /// <summary>Global or local script number; null for room entry, exit and verb code.</summary>
        public int? Number { get; set; }
        public bool Global { get; set; }
        public int FreezeCount { get; set; }
        public string Label { get; set; } = "";
        /// <summary>
        /// The room whose entry, exit, local or verb code started this chain of
        /// scripts; null when it began in a global script the engine started
        /// (the boot or a room hook).
        /// </summary>
        public int? OriginRoom { get; set; }
    }
}
