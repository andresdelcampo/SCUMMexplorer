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
    IReadOnlyCollection<int>? StatesSetInRoom = null)
{
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

    public LoomExplorationSession(GameLibrary game, LoomIndexedObjectSnapshot indexedObjects)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(indexedObjects);
        if (game.Version != ScummVersion.V3)
            throw new InvalidDataException("Only old-v3 LOOM has a session interpreter.");
        _game = game;
        _reader = new LoomScriptReader(ScummScriptDialects.For(game.Id));
        _verbs = new LoomObjectVerbReader(ScummScriptDialects.For(game.Id));
        _runtime = new LoomScriptRuntimeState(indexedObjects.States, indexedObjects.Owners);
        _globalScriptCount = ReadGlobalScriptCount(game);
        // ScummVM runs indexed script 1 at boot for this release family.
        RunIndexedScript(1, 0, null, 0);
    }

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
        _runtime.StatesSetInRoom.Order().ToArray());

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

    public LoomSessionSnapshot AdvanceFrame()
    {
        var runnable = _pending.ToArray();
        _pending.Clear();
        foreach (var item in runnable)
        {
            if (item.RoomId.HasValue && item.RoomId != _currentRoom?.ResourceId) continue;
            RunScript(item.Script, item.RoomObjects, item.Offset, item.Locals, 0);
        }
        return Snapshot;
    }

    private readonly List<LoomScriptEvent> _visitEvents = [];

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
        IReadOnlyDictionary<string, ushort>? locals, int depth)
    {
        var resource = _game.Resources.FirstOrDefault(item => item.Kind == ResourceKind.Script && item.ResourceId == scriptId);
        if (resource is null)
        {
            _uncertainties.Add($"Indexed script {scriptId:00} is absent from the parsed resource list.");
            return null;
        }
        try
        {
            return RunScript(_reader.Decode(resource), _currentObjects, startOffset, locals, depth);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _uncertainties.Add($"Indexed script {scriptId:00} could not be decoded: {ex.Message}");
            return null;
        }
    }

    private LoomBoundedScriptResult? RunStartedScript(int scriptId, int startOffset,
        IReadOnlyDictionary<string, ushort>? locals, int depth)
    {
        if (scriptId < _globalScriptCount)
            return RunIndexedScript(scriptId, startOffset, locals, depth);
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
            return RunScript(script, _currentObjects, startOffset, locals, depth);
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
        IReadOnlyDictionary<string, ushort>? locals, int depth)
    {
        if (depth >= MaxNestedScripts)
        {
            _uncertainties.Add($"Nested script limit reached in {script.Context ?? $"Script {script.ScriptId:00}"}.");
            return new LoomBoundedScriptResult(_runtime.ObjectStates, _runtime.ObjectPositions,
                _runtime.GlobalVariables, [], [], 0, false, "nested script limit reached");
        }
        var result = _executor.Execute(script, _runtime.ObjectStates, objects,
            runtime: _runtime, startOffset: startOffset, initialLocals: locals,
            onEvent: (scriptEvent, _) => HandleEvent(scriptEvent, depth));
        if (result.UnresolvedReason is not null)
            _uncertainties.Add($"{script.Context ?? $"Script {script.ScriptId:00}"} +0x{startOffset:X4}: {result.UnresolvedReason}");
        if (result.ResumeOffset is int next)
        {
            var preservedLocals = result.KnownVariables
                .Where(pair => pair.Key.StartsWith("local[", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            _pending.Add(new PendingScript(script, objects.ToArray(), next, preservedLocals,
                script.Context is null ? null : _currentRoom?.ResourceId));
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
                // cancel instructions remaining in the caller.
                RunStartedScript(scriptId, 0, locals, depth + 1);
                return "";
            case LoomScriptEventKind.ScriptChained:
                return "chainScript changes the current script's control flow";
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
            case LoomScriptEventKind.RoomLoaded:
                return $"script requests Room {scriptEvent.TargetId?.ToString("00", CultureInfo.InvariantCulture) ?? "dynamic"}; room transition awaits bounded session dispatch";
            default:
                return null;
        }
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
        IReadOnlyDictionary<string, ushort> Locals, int? RoomId);
}
