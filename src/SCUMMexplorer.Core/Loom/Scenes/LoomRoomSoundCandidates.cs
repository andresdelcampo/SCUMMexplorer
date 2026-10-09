using System.Globalization;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

public sealed record LoomPossibleSoundCue(int? SoundId, string Evidence);

/// <summary>Finds potential sound starts in room code and literal child-script calls.</summary>
public sealed class LoomRoomSoundCandidates
{
    private const int MaxScriptDepth = 4;
    private ScummScriptReader _scripts = new();
    private ScummObjectVerbReader _verbs = new();

    public IReadOnlyList<LoomPossibleSoundCue> Analyze(GameLibrary game, GameResource room,
        IReadOnlyList<ScummRoomObjectRecord> records)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(room);
        var dialect = ScummScriptDialects.For(game.Id);
        if (_scripts.Dialect != dialect)
        {
            _scripts = new ScummScriptReader(dialect);
            _verbs = new ScummObjectVerbReader(dialect);
        }
        var candidates = new List<LoomPossibleSoundCue>();
        var indexedScripts = game.Resources.Where(item => item.Kind == ResourceKind.Script &&
                item.ResourceId.HasValue).GroupBy(item => item.ResourceId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var cache = new Dictionary<int, ScummScriptDisassembly>();

        void Scan(ScummScriptDisassembly code, IReadOnlyDictionary<int, int> locals,
            string path, HashSet<int> scriptPath, int depth)
        {
            foreach (var instruction in code.Instructions)
            {
                if (instruction.Operation is "startMusic" or "startSound")
                {
                    var target = Resolve(instruction.Operands, locals);
                    candidates.Add(new LoomPossibleSoundCue(target,
                        $"{path} → {instruction.Operation} {instruction.Operands} at +0x{instruction.Offset:X4}"));
                }
                if (depth >= MaxScriptDepth || instruction.Operation is not ("startScript" or "chainScript"))
                    continue;
                var parts = instruction.Operands.Split(',', 2, StringSplitOptions.TrimEntries);
                var scriptId = Resolve(parts[0], locals);
                if (scriptId is not int id || !indexedScripts.TryGetValue(id, out var resource) ||
                    scriptPath.Contains(id)) continue;
                if (!cache.TryGetValue(id, out var child))
                {
                    try { child = _scripts.Decode(resource); cache[id] = child; }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                    { continue; }
                }
                var arguments = parts.Length > 1 ? BindLiteralArguments(parts[1], locals) :
                    new Dictionary<int, int>();
                scriptPath.Add(id);
                Scan(child, arguments, $"{path} → Script {id:00}", scriptPath, depth + 1);
                scriptPath.Remove(id);
            }
        }

        try
        {
            var entry = _scripts.DecodeRoomEntry(room);
            if (entry is not null)
                Scan(entry, new Dictionary<int, int>(), $"Room {room.ResourceId:00} entry", [], 0);
            var encoded = File.ReadAllBytes(room.FilePath);
            var tables = _verbs.ReadAll(encoded, records);
            foreach (var record in records)
            {
                if (!tables.TryGetValue(record.Id, out var verbs)) continue;
                foreach (var verb in verbs)
                {
                    var code = _verbs.Decode(room, record, verb.VerbId);
                    Scan(code, new Dictionary<int, int>(),
                        $"Room {room.ResourceId:00} object {record.Id} {verb.Label}", [], 0);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // Static candidates are optional; reached session events remain authoritative.
        }
        return candidates.Distinct().ToArray();
    }

    private static Dictionary<int, int> BindLiteralArguments(string list,
        IReadOnlyDictionary<int, int> parentLocals)
    {
        var open = list.IndexOf('[');
        var close = list.LastIndexOf(']');
        var values = new Dictionary<int, int>();
        if (open < 0 || close <= open) return values;
        var arguments = list[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries);
        for (var index = 0; index < arguments.Length; index++)
            if (Resolve(arguments[index], parentLocals) is int value) values[index] = value;
        return values;
    }

    private static int? Resolve(string operand, IReadOnlyDictionary<int, int> locals)
    {
        operand = operand.Trim();
        if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(operand.AsSpan(2), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out var literal)) return literal;
        if (operand.StartsWith("local[0x", StringComparison.OrdinalIgnoreCase) &&
            operand.EndsWith(']') &&
            int.TryParse(operand.AsSpan(8, operand.Length - 9), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out var localIndex) &&
            locals.TryGetValue(localIndex, out var value)) return value;
        return null;
    }
}
