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
    private readonly List<string> _uncertainties = [];
    private readonly List<PendingScript> _pending = [];
    private GameResource? _currentRoom;
    private IReadOnlyList<LoomRoomObjectRecord> _currentObjects = [];

    public LoomExplorationSession(GameLibrary game, LoomIndexedObjectSnapshot indexedObjects, int randomSeed = 0)
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
        // ScummVM runs indexed script 1 at boot for this release family.
        RunIndexedScript(1, 0, null, 0);
    }

    /// <summary>Seed of the stand-in random source; every <c>getRandomNr</c> value is a labelled pick from it.</summary>
    public int RandomSeed => _runtime.Random.Seed;
    public LoomScriptDisassembly? LastEntryScript { get; private set; }
    public LoomBoundedScriptResult? LastEntryRun { get; private set; }
    public LoomObjectActionResult? LastAction { get; private set; }
    public IReadOnlyList<LoomScriptEvent> LastVisitEvents { get; private set; } = [];
    public IReadOnlyList<LoomScriptEvent> ReachedSoundEvents => _visitEvents.Where(item =>
        item.Certainty == LoomScriptEventCertainty.Reached && item.Kind is
            LoomScriptEventKind.MusicStarted or LoomScriptEventKind.SoundStarted or
            LoomScriptEventKind.MusicStopped or LoomScriptEventKind.SoundStopped).ToArray();
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

        var eventsBeforeVisit = _visitEvents.Count;
        if (_currentRoom is not null)
        {
            RunGlobalHook(30, "before room exit");
            RunEmbeddedRoomScript(_currentRoom, exit: true);
            RunGlobalHook(31, "after room exit");
            // Room-scoped scripts cannot continue into a different room.
            _pending.RemoveAll(item => item.RoomId == _currentRoom.ResourceId);
        }

        _currentRoom = room;
        // startScene clears the draw queue before the entry scripts run.
        _runtime.DrawQueue.Clear();
        _runtime.StatesSetInRoom.Clear();
        _runtime.EstimatedStates.Clear();
        _currentObjects = _objects.Decode(File.ReadAllBytes(room.FilePath));
        foreach (var item in _currentObjects)
            _runtime.ObjectPositions.TryAdd(item.Id, new RoomObjectPosition(item.X, item.Y));
        _runtime.GlobalVariables["var[0x004]"] = (ushort)room.ResourceId.Value;
        if (reachedAction is null)
            _uncertainties.Add($"Room {room.ResourceId:00} selected from the catalog; the route to it was not executed from game input.");
        else
            _uncertainties.Add($"{reachedAction.Evidence} Actor walk and input dispatch were not simulated.");

        RunGlobalHook(28, "before room entry");
        RunEmbeddedRoomScript(room, exit: false);
        RunGlobalHook(29, "after room entry");
        LastVisitEvents = _visitEvents.Skip(eventsBeforeVisit).ToArray();
        return Snapshot;
    }

    public LoomObjectActionResult ActivateObjectVerb(int objectId, int verbId)
    {
        if (_currentRoom?.ResourceId is not int sourceRoomId)
            throw new InvalidOperationException("Visit a room before activating an object verb.");
        var record = _currentObjects.FirstOrDefault(item => item.Id == objectId) ??
                     throw new InvalidDataException($"Object {objectId} has no code record in Room {sourceRoomId:00}.");
        var beforeUncertainties = _uncertainties.Count;
        var beforeEvents = _visitEvents.Count;
        _uncertainties.Add($"Object {objectId} verb 0x{verbId:X2} was activated directly; input dispatch, actor walk, and class-based hit selection were not simulated.");
        if (Snapshot.IsUntouchable(objectId))
            _uncertainties.Add($"Object {objectId} is untouchable (class {LoomObjectClasses.Untouchable}) in this preview, so the game's findObject would not select it; its verb ran only as an exploration action.");
        LoomScriptDisassembly? script = null;
        LoomBoundedScriptResult? run = null;
        try
        {
            script = _verbs.Decode(_currentRoom, record, verbId);
            run = RunScript(script, _currentObjects, 0, null, 0);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _uncertainties.Add($"Room {sourceRoomId:00} Object {objectId} verb 0x{verbId:X2}: {ex.Message}");
        }
        var roomLoad = _visitEvents.Skip(beforeEvents).FirstOrDefault(item =>
            item.Kind == LoomScriptEventKind.RoomLoaded && item.Certainty == LoomScriptEventCertainty.Reached);
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
        var beforeEvents = _visitEvents.Count;
        var runnable = _pending.ToArray();
        _pending.Clear();
        _stopped.Clear();
        foreach (var item in runnable)
        {
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
        }
        var roomLoad = _visitEvents.Skip(beforeEvents).FirstOrDefault(item =>
            item.Kind == LoomScriptEventKind.RoomLoaded && item.Certainty == LoomScriptEventCertainty.Reached);
        if (roomLoad is null || _currentRoom?.ResourceId is not int sourceRoomId) return Snapshot;
        var target = roomLoad.TargetId is int id ? _game.Rooms.FirstOrDefault(item => item.ResourceId == id) : null;
        if (target is null)
        {
            _uncertainties.Add($"Reached room load has no indexed destination ({roomLoad.Operands}).");
            return Snapshot;
        }
        var beforeUncertainties = _uncertainties.Count;
        var action = LastAction is { ReachedRoomLoad: null } pending && pending.SourceRoomId == sourceRoomId
            ? pending with { ReachedRoomLoad = roomLoad, DestinationRoomId = target.ResourceId }
            : new LoomObjectActionResult(sourceRoomId, 0, 0, roomLoad.Offset, null, null, roomLoad,
                target.ResourceId, []);
        _uncertainties.Add($"{roomLoad.SourceLabel} +0x{roomLoad.Offset:X4} reached {roomLoad.Operation} after a frame advance.");
        VisitRoom(target, action);
        LastAction = action with { Uncertainties = [.. action.Uncertainties, .. _uncertainties.Skip(beforeUncertainties)] };
        return Snapshot;
    }

    /// <summary>
    /// Advances frames, at most <paramref name="maxFrames"/>, while scripts are
    /// pending and the session stays in the same room. Returns true when a
    /// continued script reached a room load that the session entered.
    /// </summary>
    public bool ContinueUntilRoomChanges(int maxFrames)
    {
        var room = _currentRoom?.ResourceId;
        for (var frame = 0; frame < maxFrames && _pending.Count > 0 && _currentRoom?.ResourceId == room; frame++)
            AdvanceFrame();
        return _currentRoom?.ResourceId != room;
    }

    private readonly List<LoomScriptEvent> _visitEvents = [];
    private readonly List<string> _visitTrace = [];
    private readonly HashSet<int> _stopped = [];
    private readonly List<LoomRandomPick> _picks = [];
    // Scripts running now, outermost first; the last one is the current script.
    private readonly List<ScriptFrame> _executing = [];
    // beginCutscene's vm.cutSceneData stack, and the script whose cutscene start
    // script is running (vm.cutSceneScriptIndex), which freezeScripts leaves running.
    private readonly Stack<ushort> _cutsceneData = new();
    private ScriptFrame? _cutsceneOwner;

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
        bool freezeResistant = false)
    {
        var frame = new ScriptFrame { FreezeResistant = freezeResistant };
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
                return "chainScript changes the current script's control flow";
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
        _cutsceneData.Push(arguments.GetValueOrDefault("local[0x000]"));
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
        if (_cutsceneData.Count > 0) data = _cutsceneData.Pop();
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
        public int FreezeCount { get; set; }
        public string Label { get; set; } = "";
    }
}
