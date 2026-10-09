using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

/// <summary>Propagates possible room contexts along decoded calls and room changes.
/// Resource container numbers are never used as execution-room evidence.</summary>
internal sealed class ConversationRoomResolver
{
    private readonly Dictionary<(int Script, int Offset), Dictionary<int, string>> _contexts = [];
    public List<string> Diagnostics { get; } = [];

    public IReadOnlyList<RoomContext> At(int scriptId, int offset) =>
        _contexts.TryGetValue((scriptId, offset), out var rooms)
            ? rooms.OrderBy(pair => pair.Value.StartsWith("Cutscene skip:", StringComparison.Ordinal))
                .ThenBy(pair => pair.Key).Select(pair => new RoomContext(pair.Key, pair.Value)).ToArray()
            : [];

    public ConversationRoomResolver(GameLibrary game)
    {
        var reader = new ScummScriptReader(ScummScriptDialect.V3Indy3);
        var verbReader = new ScummObjectVerbReader(ScummScriptDialect.V3Indy3);
        var directory = new ScummRoomObjectDirectory();
        var chains = new LflResourceChain();
        var globals = new Dictionary<int, Source>();
        var locals = new Dictionary<(int Room, int Script), Source>();
        var roots = new List<(Source Source, int Room)>();
        var globalCount = game.Resources.Count(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue);
        void TryRead(Action action, string name)
        {
            try { action(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException)
            { Diagnostics.Add($"{name}: {ex.Message}"); }
        }
        foreach (var resource in game.Resources.Where(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue))
        {
            if (chains.FindLeftover(game, resource) is not null) continue;
            TryRead(() => globals[resource.ResourceId!.Value] = new Source(
                $"Script {resource.ResourceId}", resource.ResourceId, reader.Decode(resource)), resource.Name);
        }
        foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue))
        {
            var id = room.ResourceId!.Value;
            void Add(ScummScriptDisassembly? data, string name)
            {
                if (data is not null) roots.Add((new Source(name, null, data), id));
            }
            TryRead(() => Add(reader.DecodeRoomEntry(room), $"Room {id:00} entry"), $"Room {id} entry");
            TryRead(() => Add(reader.DecodeRoomExit(room), $"Room {id:00} exit"), $"Room {id} exit");
            TryRead(() =>
            {
                var bytes = File.ReadAllBytes(room.FilePath);
                foreach (var localId in ScummScriptReader.ReadRoomLocalScriptIds(bytes, globalCount))
                {
                    var data = reader.DecodeRoomLocalScript(room, localId, globalCount);
                    if (data is null) continue;
                    var source = new Source($"Room {id:00} local {localId}", null, data);
                    locals[(id, localId)] = source;
                    roots.Add((source, id));
                }
                var objects = directory.Decode(bytes);
                var tables = verbReader.ReadAll(bytes, objects);
                foreach (var obj in objects)
                    if (tables.TryGetValue(obj.Id, out var verbs))
                        foreach (var verb in verbs)
                            TryRead(() => Add(verbReader.Decode(room, obj, verb.VerbId),
                                $"Room {id:00} object {obj.Id} verb {verb.VerbId:X2}"), $"Room {id} object {obj.Id}");
            }, $"Room {id} scripts");
        }

        var pending = new Queue<State>();
        var visited = new HashSet<(Source Source, int Offset, int Room, bool Skip)>();
        foreach (var root in roots)
            pending.Enqueue(new State(root.Source, 0, root.Room, root.Source.Name));
        // Some rooms bind a global interaction dispatcher on entry and clear
        // that same variable on exit. Match both ends of that binding plus an
        // actual indirect call, rather than guessing from script storage.
        var dynamicDispatches = globals.Values.SelectMany(source => source.Code.Values
            .Where(i => i.Operation is "startScript" or "chainScript")
            .Select(i => (Source: source, Instruction: i, Operand: i.Operands.Split(',')[0].Trim())))
            .Where(call => call.Operand.StartsWith("var[", StringComparison.Ordinal)).ToArray();
        foreach (var root in roots.Where(r => r.Source.Name.EndsWith(" entry", StringComparison.Ordinal)))
        {
            var exits = roots.Where(r => r.Room == root.Room && r.Source.Name.EndsWith(" exit", StringComparison.Ordinal))
                .SelectMany(r => r.Source.Code.Values).ToArray();
            foreach (var binding in root.Source.Code.Values.Where(i => i.Operation == "move"))
            {
                var parts = binding.Operands.Split('=').Select(p => p.Trim()).ToArray();
                if (parts.Length != 2 || Literal(parts[1]) is not int target || target == 0 ||
                    !globals.TryGetValue(target, out var callee)) continue;
                if (!exits.Any(i => i.Operation == "move" &&
                    i.Operands.Split('=').Select(p => p.Trim()).SequenceEqual(new[] { parts[0], "0x0000" }))) continue;
                foreach (var dispatch in dynamicDispatches.Where(call => call.Operand == parts[0]))
                    pending.Enqueue(new State(callee, 0, root.Room,
                        $"{root.Source.Name} +0x{binding.Offset:X4} binds {parts[0]}; " +
                        $"{dispatch.Source.Name} +0x{dispatch.Instruction.Offset:X4} dispatches it; room exit clears binding"));
            }
        }
        while (pending.TryDequeue(out var state))
        {
            var isSkip = state.Evidence.StartsWith("Cutscene skip:", StringComparison.Ordinal);
            if (!visited.Add((state.Source, state.Offset, state.Room, isSkip)) ||
                !state.Source.Code.TryGetValue(state.Offset, out var instruction)) continue;
            if (state.Source.ScriptId is int scriptId && state.Room > 0)
            {
                if (!_contexts.TryGetValue((scriptId, state.Offset), out var rooms))
                    _contexts[(scriptId, state.Offset)] = rooms = [];
                if (!rooms.TryGetValue(state.Room, out var oldEvidence) ||
                    oldEvidence.StartsWith("Cutscene skip:", StringComparison.Ordinal) && !isSkip)
                    rooms[state.Room] = state.Evidence;
            }
            var length = instruction.Bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (instruction.Operation == "beginOverride" &&
                state.Source.Code.TryGetValue(state.Offset + length, out var overrideJump) &&
                overrideJump.Operation == "jumpRelative")
            {
                var jumpLength = overrideJump.Bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                pending.Enqueue(state with { Offset = state.Offset + length + jumpLength });
                pending.Enqueue(state with { Offset = state.Offset + length,
                    Evidence = isSkip ? state.Evidence : "Cutscene skip: " + state.Evidence });
                continue;
            }
            var roomId = state.Room;
            if (instruction.Operation is "loadRoom" or "loadRoomWithEgo")
            {
                var parts = instruction.Operands.Split(',');
                var operand = parts[instruction.Operation == "loadRoom" ? 0 : 1].Trim();
                roomId = Literal(operand) ?? 0;
            }
            var nextEvidence = roomId != state.Room
                ? $"{(isSkip ? "Cutscene skip: " : "")}{state.Source.Name} +0x{state.Offset:X4} loads Room {roomId:00}" : state.Evidence;
            if (instruction.Operation is "startScript" or "chainScript")
            {
                if (Literal(instruction.Operands.Split(',')[0].Trim()) is int target)
                {
                    var callee = globals.GetValueOrDefault(target) ?? locals.GetValueOrDefault((roomId, target));
                    if (callee is not null)
                        pending.Enqueue(new State(callee, 0, roomId,
                            $"{nextEvidence} → {state.Source.Name} +0x{state.Offset:X4} → {callee.Name}"));
                }
                if (instruction.Operation == "chainScript") continue;
            }
            if (instruction.Operation == "stopObjectCode") continue;
            var branch = Regex.Match(instruction.Operands, @"->\s*0x([0-9A-Fa-f]+)");
            if (branch.Success)
            {
                var target = int.Parse(branch.Groups[1].Value, NumberStyles.HexNumber);
                pending.Enqueue(new State(state.Source, target, roomId, nextEvidence));
                if (instruction.Operation == "jumpRelative") continue;
            }
            pending.Enqueue(new State(state.Source, state.Offset + length, roomId, nextEvidence));
        }
    }

    private static int? Literal(string value) =>
        value.StartsWith("0x", StringComparison.Ordinal) &&
        int.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result) ? result : null;

    internal sealed record RoomContext(int RoomId, string Evidence);
    private sealed class Source(string name, int? scriptId, ScummScriptDisassembly data)
    {
        public string Name { get; } = name;
        public int? ScriptId { get; } = scriptId;
        public Dictionary<int, ScummScriptInstruction> Code { get; } = data.Instructions.ToDictionary(i => i.Offset);
    }
    private sealed record State(Source Source, int Offset, int Room, string Evidence);
}
