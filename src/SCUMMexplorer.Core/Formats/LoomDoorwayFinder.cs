using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

/// <summary>A possible room destination reached statically from an object verb.</summary>
public sealed record LoomDoorwayTarget(int RoomId, int InstructionOffset, string Path);

/// <summary>
/// Finds room loads that an object verb can reach, directly or through the
/// scripts it starts. Room-local scripts often hold the actual exit (for
/// example LOOM Room 03 objects 161–164 start local scripts 200–202, which load
/// Rooms 04, 05 and 10). Global scripts are followed only for
/// <c>loadRoomWithEgo</c>, the ego's own room change, so cutscenes that visit
/// other rooms are not mistaken for doorways. Literal script arguments are
/// substituted for the callee's locals. All results are possible destinations;
/// their conditions are not evaluated.
/// </summary>
public sealed class LoomDoorwayFinder
{
    private const int MaxDepth = 3;
    private static readonly Regex Literal = new(@"^0x(?<value>[0-9A-Fa-f]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex LocalVariable = new(@"^local\[0x(?<index>[0-9A-Fa-f]+)\]$", RegexOptions.CultureInvariant);
    private readonly GameLibrary _game;
    private readonly LoomScriptReader _reader;
    private readonly int _globalScriptCount;
    private readonly Dictionary<string, LoomScriptDisassembly?> _cache = [];

    public LoomDoorwayFinder(GameLibrary game)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _reader = new LoomScriptReader(ScummScriptDialects.For(game.Id));
        var indexPath = Path.Combine(game.FolderPath, "00.LFL");
        try
        {
            _globalScriptCount = File.Exists(indexPath)
                ? new LflIndexReader().ReadDeclaredScriptCount(indexPath)
                : DefaultScriptCount(game);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException)
        {
            _globalScriptCount = DefaultScriptCount(game);
        }
    }

    public IReadOnlyList<LoomDoorwayTarget> Find(GameResource room, LoomScriptDisassembly verbScript)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(verbScript);
        var results = new List<LoomDoorwayTarget>();
        Walk(room, verbScript, null, 0, "", allowPlainLoad: true, [], results);
        return results.DistinctBy(item => item.RoomId).ToArray();
    }

    private void Walk(GameResource room, LoomScriptDisassembly script, IReadOnlyDictionary<int, int>? locals,
        int depth, string path, bool allowPlainLoad, HashSet<string> visiting, List<LoomDoorwayTarget> results)
    {
        foreach (var instruction in script.Instructions)
        {
            if (instruction.Operation is "loadRoom" or "loadRoomWithEgo")
            {
                if (instruction.Operation == "loadRoom" && !allowPlainLoad) continue;
                var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                var operand = instruction.Operation == "loadRoom" ? parts[0] : parts.ElementAtOrDefault(1) ?? "";
                if (TryResolve(operand, locals, out var target) &&
                    _game.Rooms.Any(item => item.ResourceId == target) && target != room.ResourceId)
                    results.Add(new LoomDoorwayTarget(target, instruction.Offset,
                        path.Length == 0 ? "direct" : path.TrimStart(' ', '→')));
                continue;
            }
            if (depth >= MaxDepth || instruction.Operation is not ("startScript" or "chainScript")) continue;
            var split = instruction.Operands.Split(',', 2, StringSplitOptions.TrimEntries);
            if (!TryResolve(split[0], locals, out var scriptId)) continue;
            var arguments = ReadArguments(split.ElementAtOrDefault(1), locals);
            var isLocal = scriptId >= _globalScriptCount;
            var key = isLocal ? $"{room.ResourceId}:{scriptId}" : $"g:{scriptId}";
            if (!visiting.Add(key)) continue;
            var child = Load(room, scriptId, isLocal, key);
            if (child is not null)
                Walk(room, child, arguments, depth + 1,
                    $"{path} → {(isLocal ? "local" : "global")} script {scriptId}", allowPlainLoad && isLocal,
                    visiting, results);
            visiting.Remove(key);
        }
    }

    private LoomScriptDisassembly? Load(GameResource room, int scriptId, bool isLocal, string key)
    {
        if (_cache.TryGetValue(key, out var cached)) return cached;
        LoomScriptDisassembly? script = null;
        try
        {
            if (isLocal) script = _reader.DecodeRoomLocalScript(room, scriptId, _globalScriptCount);
            else if (_game.Resources.FirstOrDefault(item => item.Kind == ResourceKind.Script &&
                         item.ResourceId == scriptId) is { } resource)
                script = _reader.Decode(resource);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException)
        {
            script = null;
        }
        _cache[key] = script;
        return script;
    }

    private static Dictionary<int, int>? ReadArguments(string? list, IReadOnlyDictionary<int, int>? locals)
    {
        if (list is null) return null;
        var open = list.IndexOf('[');
        var close = list.LastIndexOf(']');
        if (open < 0 || close <= open) return null;
        var values = new Dictionary<int, int>();
        var items = list[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < items.Length; index++)
            if (TryResolve(items[index], locals, out var value)) values[index] = value;
        return values;
    }

    private static bool TryResolve(string operand, IReadOnlyDictionary<int, int>? locals, out int value)
    {
        value = 0;
        var text = operand.Trim();
        var literal = Literal.Match(text);
        if (literal.Success)
            return int.TryParse(literal.Groups["value"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        var local = LocalVariable.Match(text);
        return local.Success && locals is not null &&
               int.TryParse(local.Groups["index"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var index) &&
               locals.TryGetValue(index, out value);
    }

    private static int DefaultScriptCount(GameLibrary game) =>
        game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue)
            .Select(item => item.ResourceId!.Value).DefaultIfEmpty(-1).Max() + 1;
}
