using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>One touch of a global variable or story bit by a script instruction.</summary>
public sealed record LoomStateTouch(string Variable, bool Write, string Where, string Instruction = "");

/// <summary>What a scene's code reads and writes, statically (every decoded instruction, no execution).</summary>
public sealed record LoomSceneScan(IReadOnlyList<string> Scripts, IReadOnlyList<LoomStateTouch> Touches,
    IReadOnlyList<string> CostumeChanges, IReadOnlyList<string> Unreadable,
    IReadOnlyList<int> PrintedActors, IReadOnlyList<int> PlacedActors)
{
    public IEnumerable<string> Reads => Touches.Where(t => !t.Write).Select(t => t.Variable).Distinct();
    public IEnumerable<string> Writes => Touches.Where(t => t.Write).Select(t => t.Variable).Distinct();

    /// <summary>The touches made by the scene's own code: everything except the shared system scripts 000-043 (cutscene hooks, drafts, the distaff display).</summary>
    public IEnumerable<LoomStateTouch> OwnTouches => Touches.Where(t => !LoomSceneStateScanner.IsSystemCode(t.Where));
}

/// <summary>A place whose code puts an actor into a room and then at a point (an object for putActorAtObject, else X/Y).</summary>
public sealed record LoomActorPlacer(int Actor, int Room, string Where, int? X, int? Y, int? AtObject);

/// <summary>A place whose own code assigns a constant to a variable (see <see cref="LoomSceneStateScanner.FindConstantWriters"/>).</summary>
public sealed record LoomStateSource(int Room, int? ObjectId, int? VerbId, string Where, string Instruction);

/// <summary>
/// A static scan of the code a scene runs: the named scripts, the global and room-local scripts they start with a
/// constant number, and the room entry scripts of the rooms the scene loads with a constant number. It reports which
/// global variables and story bits are read and written and which instructions change an actor's costume. It is
/// evidence for choosing a labelled start setup (see <see cref="LoomSceneSetupPlanner"/>), not a model of the scene.
/// </summary>
public sealed partial class LoomSceneStateScanner
{
    private readonly GameLibrary _game;
    private readonly LoomScriptReader _reader;
    private readonly int _globalScriptCount;
    private readonly Dictionary<int, LoomScriptDisassembly?> _globals = [];
    private readonly Dictionary<(int Room, int Script), LoomScriptDisassembly?> _locals = [];
    private readonly Dictionary<int, LoomScriptDisassembly?> _entries = [];

    public LoomSceneStateScanner(GameLibrary game)
    {
        _game = game;
        _reader = new LoomScriptReader(ScummScriptDialects.For(game.Id));
        var index = Path.Combine(game.FolderPath, "00.LFL");
        _globalScriptCount = File.Exists(index) ? new LflIndexReader().ReadDeclaredScriptCount(index) : 0;
    }

    public static bool IsSystemCode(string where) =>
        where.StartsWith("Script ", StringComparison.Ordinal) && int.TryParse(where.AsSpan(7, 3), out var id) && id < SystemScriptLimit;

    /// <summary>Global scripts below this number are the engine-side hooks, not story scenes (script 044 is the opening).</summary>
    public const int SystemScriptLimit = 44;

    /// <summary>
    /// Calls <paramref name="visit"/> for every object verb (grouped by code), room entry, local script and global script of
    /// the game, object verbs first, in room order.
    /// </summary>
    private void ForEachCodeUnit(Action<LoomScriptDisassembly?, int, int?, int?, string> visit)
    {
        var verbs = new LoomObjectVerbReader(ScummScriptDialects.For(_game.Id));
        var directory = new LoomRoomObjectDirectory();
        var rooms = _game.Rooms.Where(r => r.ResourceId.HasValue).OrderBy(r => r.ResourceId).ToArray();
        foreach (var room in rooms)
        {
            var id = room.ResourceId!.Value;
            try
            {
                var bytes = File.ReadAllBytes(room.FilePath);
                var records = directory.Decode(bytes);
                var tables = verbs.ReadAll(bytes, records);
                foreach (var record in records)
                    if (tables.TryGetValue(record.Id, out var entries))
                        foreach (var group in entries.GroupBy(item => item.ScriptOffset))
                            visit(verbs.Decode(room, record, group.First().VerbId), id, record.Id, group.First().VerbId,
                                $"Room {id:00} object {record.Id} verb {(group.First().IsFallback ? "fallback" : $"0x{group.First().VerbId:X2}")}");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
        }
        foreach (var room in rooms)
        {
            var id = room.ResourceId!.Value;
            visit(Entry(id), id, null, null, $"Room {id:00} entry");
            try
            {
                foreach (var local in LoomScriptReader.ReadRoomLocalScriptIds(File.ReadAllBytes(room.FilePath), _globalScriptCount))
                    visit(Script(id, local), id, null, null, $"Room {id:00} local {local}");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { }
        }
        for (var g = 0; g < _globalScriptCount; g++) visit(Script(0, g), 0, null, null, $"Script {g:000}");
    }

    /// <summary>
    /// Every room entry and exit, local script and object verb (and global script) whose code itself assigns
    /// <paramref name="value"/> to <paramref name="variable"/> with a plain <c>move</c>; the real places that can
    /// establish that state. Object verbs come first (they are what a player or the explorer can start).
    /// </summary>
    public IReadOnlyList<LoomStateSource> FindConstantWriters(string variable, int value)
    {
        var wanted = $"{variable} = 0x{value:X4}";
        var found = new List<LoomStateSource>();
        ForEachCodeUnit((script, room, obj, verb, label) =>
        {
            if (script is null) return;
            foreach (var ins in script.Instructions)
                if (ins.Operation == "move" && ins.Operands == wanted)
                    found.Add(new(room, obj, verb, $"{label} +0x{ins.Offset:X4}", $"move {ins.Operands}"));
        });
        return found;
    }

    /// <summary>Every plain <c>move</c> of <paramref name="value"/> into a story bit (D72 and up) in the game's code, in code order.</summary>
    public IReadOnlyList<LoomStateSource> FindStoryBitMoves(int value)
    {
        var found = new List<LoomStateSource>();
        var tail = $" = 0x{value:X4}";
        ForEachCodeUnit((script, room, obj, verb, label) =>
        {
            if (script is null) return;
            foreach (var ins in script.Instructions)
                if (ins.Operation == "move" && ins.Operands.StartsWith("bit[0x", StringComparison.Ordinal) && ins.Operands.EndsWith(tail, StringComparison.Ordinal) &&
                    int.TryParse(ins.Operands.AsSpan(6, ins.Operands.IndexOf(']') - 6), System.Globalization.NumberStyles.HexNumber, null, out var bit) && bit >= 0xD72)
                    found.Add(new(room, obj, verb, $"{label} +0x{ins.Offset:X4}", $"move {ins.Operands}"));
        });
        return found;
    }

    /// <summary>Every instruction in the game's code that assigns <paramref name="variable"/> (any value), in code order; the audit's view of who can change a state.</summary>
    public IReadOnlyList<LoomStateTouch> FindWriters(string variable)
    {
        var found = new List<LoomStateTouch>();
        ForEachCodeUnit((script, _, _, _, label) =>
        {
            if (script is null) return;
            var touches = new List<LoomStateTouch>();
            foreach (var ins in script.Instructions)
            {
                touches.Clear();
                Classify(ins, $"{label} +0x{ins.Offset:X4}", touches, []);
                found.AddRange(touches.Where(t => t.Write && t.Variable == variable));
            }
        });
        return found;
    }

    /// <summary>Every <c>actorOps</c> costume assignment in the game's code (object verbs, room entries, local and global scripts), in code order, with its operands.</summary>
    public IReadOnlyList<(int Room, string Where, string Operands)> FindCostumeChanges()
    {
        var found = new List<(int, string, string)>();
        ForEachCodeUnit((script, room, _, _, label) =>
        {
            if (script is null) return;
            foreach (var ins in script.Instructions)
                if (ins.Operation == "actorOps" && ins.Operands.Contains("costume,", StringComparison.Ordinal))
                    found.Add((room, $"{label} +0x{ins.Offset:X4}", ins.Operands));
        });
        return found;
    }

    /// <summary>
    /// What the entry script of <paramref name="room"/> (run when the room is entered) and the scripts it starts with a
    /// constant number read and write; the start-room dependencies the scene's own scripts do not show.
    /// </summary>
    public LoomSceneScan ScanRoomEntry(int room) => Scan(room, [], entryOfStartRoom: true);

    /// <summary>
    /// The places in the game's code that put <paramref name="actor"/> into <paramref name="room"/> and then at a point
    /// (<c>putActor</c> with literals, or <c>putActorAtObject</c>) within the next three instructions, in code order.
    /// </summary>
    public IReadOnlyList<LoomActorPlacer> FindActorPlacers(int actor, int room)
    {
        var found = new List<LoomActorPlacer>();
        ForEachCodeUnit((script, _, _, _, label) =>
        {
            if (script is null) return;
            var list = script.Instructions;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Operation != "putActorInRoom" || ActorRoomOf(list[i]) is not (int a, int r) || a != actor || r != room) continue;
                foreach (var next in list.Skip(i + 1).Take(3))
                {
                    var parts = next.Operands.Split(", ");
                    if (next.Operation == "putActor" && parts.Length == 3 && Literal(parts[0]) == actor && Literal(parts[1]) is int x && Literal(parts[2]) is int y)
                    { found.Add(new(actor, room, $"{label} +0x{list[i].Offset:X4}", (short)x, (short)y, null)); break; }
                    if (next.Operation == "putActorAtObject" && parts.Length == 2 && Literal(parts[0]) == actor && Literal(parts[1]) is int obj)
                    { found.Add(new(actor, room, $"{label} +0x{list[i].Offset:X4}", null, null, obj)); break; }
                }
            }
        });
        return found;
    }

    /// <summary>
    /// Scans scripts <paramref name="scriptIds"/> as seen from <paramref name="startRoom"/>, following constant calls,
    /// and the code of object <paramref name="objectId"/>'s verb <paramref name="verbId"/> in that room when given.
    /// </summary>
    public LoomSceneScan Scan(int startRoom, IEnumerable<int> scriptIds, int objectId = 0, int verbId = 0, bool entryOfStartRoom = false)
    {
        var touches = new List<LoomStateTouch>();
        var costume = new List<string>();
        var names = new List<string>();
        var unreadable = new List<string>();
        var printed = new SortedSet<int>();
        var placed = new SortedSet<int>();
        var seen = new HashSet<string>();
        var pending = new Queue<(int Room, int Script, bool Entry)>();

        void Process(LoomScriptDisassembly script, string label, int room)
        {
            names.Add(label);
            foreach (var ins in script.Instructions)
            {
                var where = $"{label} +0x{ins.Offset:X4}";
                Classify(ins, where, touches, costume);
                if (ins.Operation == "print" && ins.Operands.Contains("text,", StringComparison.Ordinal) && FirstConstant(ins.Operands) is int speaker and > 1 and < 13)
                    printed.Add(speaker);
                if (ins.Operation == "putActorInRoom" && ActorRoomOf(ins) is (int actor, int toRoom) && toRoom != 0) placed.Add(actor);
                if (ins.Operation is "startScript" or "chainScript" && FirstConstant(ins.Operands) is int callee) pending.Enqueue((room, callee, false));
                else if (ins.Operation is "loadRoom" or "loadRoomWithEgo" && RoomArgument(ins) is int target && target != room) pending.Enqueue((target, 0, true));
            }
        }

        if (objectId != 0 && VerbCode(startRoom, objectId, verbId) is { } verbScript)
        {
            var label = $"Room {startRoom:00} object {objectId} verb 0x{verbId:X2}";
            seen.Add(label);
            Process(verbScript, label, startRoom);
        }
        if (entryOfStartRoom) pending.Enqueue((startRoom, 0, true));
        foreach (var id in scriptIds) pending.Enqueue((startRoom, id, false));
        while (pending.Count > 0)
        {
            var (room, id, entry) = pending.Dequeue();
            var script = entry ? Entry(room) : Script(room, id);
            var label = entry ? $"Room {room:00} entry" : id < _globalScriptCount ? $"Script {id:000}" : $"Room {room:00} local {id}";
            if (!seen.Add(label)) continue;
            if (script is null) { unreadable.Add(label); continue; }
            Process(script, label, room);
        }
        return new LoomSceneScan(names, touches, costume, unreadable, [.. printed], [.. placed]);
    }

    /// <summary>Object <paramref name="objectId"/>'s verb code (its fallback when it has no entry for the verb) in a room, or null.</summary>
    private LoomScriptDisassembly? VerbCode(int room, int objectId, int verbId)
    {
        try
        {
            var resource = _game.Rooms.FirstOrDefault(r => r.ResourceId == room);
            if (resource is null) return null;
            var record = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(resource.FilePath)).FirstOrDefault(r => r.Id == objectId);
            return record is null ? null : new LoomObjectVerbReader(ScummScriptDialects.For(_game.Id)).Decode(resource, record, verbId);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { return null; }
    }

    /// <summary>(actor, room) of a <c>putActorInRoom</c> with two literals.</summary>
    private static (int Actor, int Room)? ActorRoomOf(LoomScriptInstruction ins)
    {
        var parts = ins.Operands.Split(", ");
        return parts.Length == 2 && Literal(parts[0]) is int a && Literal(parts[1]) is int r ? (a, r) : null;
    }

    private static int? Literal(string text) =>
        text.StartsWith("0x", StringComparison.Ordinal) && int.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var value) ? value : null;

    private LoomScriptDisassembly? Script(int room, int id)
    {
        try
        {
            if (id < _globalScriptCount)
            {
                if (_globals.TryGetValue(id, out var cached)) return cached;
                var resource = _game.Resources.FirstOrDefault(r => r.Kind == ResourceKind.Script && r.ResourceId == id);
                return _globals[id] = resource is null ? null : _reader.Decode(resource);
            }
            if (_locals.TryGetValue((room, id), out var local)) return local;
            var resourceRoom = _game.Rooms.FirstOrDefault(r => r.ResourceId == room);
            return _locals[(room, id)] = resourceRoom is null ? null : _reader.DecodeRoomLocalScript(resourceRoom, id, _globalScriptCount);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException) { return null; }
    }

    private LoomScriptDisassembly? Entry(int room)
    {
        if (_entries.TryGetValue(room, out var cached)) return cached;
        try
        {
            var resourceRoom = _game.Rooms.FirstOrDefault(r => r.ResourceId == room);
            return _entries[room] = resourceRoom is null ? null : _reader.DecodeRoomEntry(resourceRoom);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException) { return _entries[room] = null; }
    }

    [GeneratedRegex(@"(?:bit|var)\[0x[0-9A-F]+\]")]
    private static partial Regex VariableRegex();

    private static void Classify(LoomScriptInstruction ins, string where, List<LoomStateTouch> touches, List<string> costume)
    {
        if (ins.Operation == "actorOps" && ins.Operands.Contains("costume,", StringComparison.Ordinal))
            costume.Add($"{where}: {ins.Operands}");
        var variables = VariableRegex().Matches(ins.Operands).Select(m => m.Value).ToList();
        if (variables.Count == 0) return;
        // The first operand is the result of an expression, a get* query, a random pick or a range set; it is
        // written. Everything else named in an instruction (conditions, arguments, expression terms) is read.
        var writesFirst = ins.Operation is "expression" or "setVarRange" or "getRandomNr" or "getDist" or "findObject" or
            "getObjectOwner" or "getInventoryCount" or "findInventory" or "getClosestObjActor" or "getStringWidth" or
            "isScriptRunning" or "isSoundRunning" or "getVerbEntrypoint" or "actorFromPos" or
            "move" or "add" or "subtract" or "increment" or "decrement" ||
            ins.Operation.StartsWith("getActor", StringComparison.Ordinal);
        var first = ins.Operands.Length > 0 && VariableRegex().Match(ins.Operands) is { Success: true, Index: 0 } m0 ? m0.Value : null;
        for (var i = 0; i < variables.Count; i++)
            touches.Add(new(variables[i], writesFirst && i == 0 && first == variables[0], where, $"{ins.Operation} {ins.Operands}"));
    }

    private static int? FirstConstant(string operands)
    {
        var match = Regex.Match(operands, @"^0x([0-9A-F]+)");
        return match.Success ? Convert.ToInt32(match.Groups[1].Value, 16) : null;
    }

    private static int? RoomArgument(LoomScriptInstruction ins)
    {
        // loadRoomWithEgo names the object first; loadRoom only the room.
        var text = ins.Operation == "loadRoomWithEgo" ? string.Join(", ", ins.Operands.Split(", ").Skip(1)) : ins.Operands;
        return FirstConstant(text);
    }
}
