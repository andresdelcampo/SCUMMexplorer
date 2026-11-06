using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>A possible room destination reached statically from an object verb.</summary>
public sealed record ScummDoorwayTarget(int RoomId, int InstructionOffset, string Path);

/// <summary>
/// Finds room loads that an object verb can reach, directly or through the
/// scripts it starts. Room-local scripts often hold the actual exit (for
/// example LOOM Room 03 objects 161–164 start local scripts 200–202, which load
/// Rooms 04, 05 and 10). Global scripts are followed only for
/// <c>loadRoomWithEgo</c>, the ego's own room change, so cutscenes that visit
/// other rooms are not mistaken for doorways. Literal script arguments are
/// substituted for the callee's locals. All results are possible destinations;
/// their conditions are not evaluated.
/// <para>
/// R-6.13b (user, 6 November 2026: "we need a clear definition"): <b>a doorway is an object you walk through.</b> The code the game runs for walking to it
/// (its Walk to verb, or, without one, its default code outside any cutscene) can load a room. Any other object is a hotspot, whatever its
/// other verbs lead to: pushing Indy 3's statues in Room 48 (the trap), drinking from a Grail in Rooms 86 and 93, using a book.
/// </para>
/// </summary>
public sealed class ScummDoorwayFinder
{
    /// <summary>The verb number of an object's default code (<c>getVerbEntrypoint</c>'s catch-all).</summary>
    private const int DefaultVerb = 0xFF;
    private const int MaxDepth = 3;
    private static readonly Regex Literal = new(@"^0x(?<value>[0-9A-Fa-f]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex LocalVariable = new(@"^local\[0x(?<index>[0-9A-Fa-f]+)\]$", RegexOptions.CultureInvariant);
    private readonly GameLibrary _game;
    private readonly IScummDataSource _data;
    private readonly Dictionary<string, ScummScriptDisassembly?> _cache = [];

    public ScummDoorwayFinder(GameLibrary game)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _data = ScummDataSources.For(game);
    }

    /// <summary>The finder over a given data source: one disk of a browsed v4 game, which <see cref="ScummDataSources.For"/> does not cover.</summary>
    public ScummDoorwayFinder(GameLibrary game, IScummDataSource data)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public IReadOnlyList<ScummDoorwayTarget> Find(GameResource room, ScummScriptDisassembly verbScript)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(verbScript);
        var results = new List<ScummDoorwayTarget>();
        Walk(room, verbScript, null, 0, "", allowPlainLoad: true, [], results, stopAtCutscene: false);
        return results.DistinctBy(item => item.RoomId).ToArray();
    }

    /// <summary>The verb entries of an object that decide whether it is a doorway (R-6.13b): its Walk to verb, else its default code.</summary>
    public IReadOnlyList<ScummObjectVerbEntry> DoorVerbs(IReadOnlyList<ScummObjectVerbEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        // A game whose verb cannot be told (too few doors in the rooms that can be read) keeps every verb.
        if (WalkVerb() is not int walk) return entries;
        var own = entries.Where(entry => entry.VerbId == walk).ToArray();
        return own.Length > 0 ? own : entries.Where(entry => entry.VerbId == DefaultVerb).ToArray();
    }

    /// <summary>The rooms a door verb (<see cref="DoorVerbs"/>) leads to: default code that hands over to a script with a cutscene is a scene, not a door (Indy 3 Room 86's Grails, Script 122).</summary>
    public IReadOnlyList<ScummDoorwayTarget> FindDoorway(GameResource room, ScummScriptDisassembly verbScript, ScummObjectVerbEntry verb)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(verbScript);
        ArgumentNullException.ThrowIfNull(verb);
        var results = new List<ScummDoorwayTarget>();
        Walk(room, verbScript, null, 0, "", allowPlainLoad: true, [], results, stopAtCutscene: verb.VerbId == DefaultVerb);
        return results.DistinctBy(item => item.RoomId).ToArray();
    }

    private static readonly Dictionary<string, int?> WalkVerbs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The game's Walk to verb, read from its own data: the verb whose code holds <c>loadRoomWithEgo</c> in most objects, by far (0x0A in Indy 3 and Monkey Island's floppy,
    /// 0x0B in Monkey Island 2 and its CD, 0x0D in Maniac Mansion and Zak McKracken, 0x38 in LOOM; copies of one game differ, so no table: the Spanish Monkey Island CD has 0x0A).
    /// Reads rooms until the answer is clear. Null when it never is.
    /// </summary>
    private int? WalkVerb()
    {
        const int enough = 10;
        lock (WalkVerbs)
        {
            if (WalkVerbs.TryGetValue(_game.FolderPath, out var known)) return known;
            var counts = new Dictionary<int, int>();
            int? found = null;
            foreach (var room in _game.Rooms.Where(item => item.ResourceId is not null).DistinctBy(item => item.ResourceId))
            {
                var roomId = room.ResourceId!.Value;
                try
                {
                    var records = _data.ReadRoom(roomId).Objects;
                    var verbs = _data.ReadObjectVerbs(roomId, records);
                    foreach (var record in records)
                        foreach (var entry in verbs.GetValueOrDefault(record.Id) ?? [])
                            if (entry.VerbId != DefaultVerb && _data.ReadObjectVerb(roomId, record, entry.VerbId, out _).Instructions.Any(item => item.Operation == "loadRoomWithEgo"))
                                counts[entry.VerbId] = counts.GetValueOrDefault(entry.VerbId) + 1;
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or ArgumentException or NotSupportedException or EndOfStreamException) { continue; }
                var ranked = counts.OrderByDescending(pair => pair.Value).Take(2).ToArray();
                if (ranked.Length > 0 && ranked[0].Value >= enough && ranked[0].Value >= (ranked.Length > 1 ? ranked[1].Value : 0) * 3) { found = ranked[0].Key; break; }
            }
            WalkVerbs[_game.FolderPath] = found;
            return found;
        }
    }

    private void Walk(GameResource room, ScummScriptDisassembly script, IReadOnlyDictionary<int, int>? locals,
        int depth, string path, bool allowPlainLoad, HashSet<string> visiting, List<ScummDoorwayTarget> results, bool stopAtCutscene)
    {
        var inCutscene = false;
        foreach (var instruction in script.Instructions)
        {
            if (stopAtCutscene)
            {
                // A script the code hands over to that plays a cutscene is a scene; the code's own short cutscene (a door opening: Monkey Island 2 Room 53) only comes first.
                if (instruction.Operation == "cutscene") { if (depth > 0) return; inCutscene = true; continue; }
                if (instruction.Operation == "endCutscene") { inCutscene = false; continue; }
                if (inCutscene) continue;
            }
            if (instruction.Operation is "loadRoom" or "loadRoomWithEgo")
            {
                if (instruction.Operation == "loadRoom" && !allowPlainLoad) continue;
                var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                var operand = instruction.Operation == "loadRoom" ? parts[0] : parts.ElementAtOrDefault(1) ?? "";
                if (TryResolve(operand, locals, out var target) &&
                    _game.Rooms.Any(item => item.ResourceId == target) && target != room.ResourceId)
                    results.Add(new ScummDoorwayTarget(target, instruction.Offset,
                        path.Length == 0 ? "direct" : path.TrimStart(' ', '→')));
                continue;
            }
            if (depth >= MaxDepth || instruction.Operation is not ("startScript" or "chainScript")) continue;
            var split = instruction.Operands.Split(',', 2, StringSplitOptions.TrimEntries);
            if (!TryResolve(split[0], locals, out var scriptId)) continue;
            var arguments = ReadArguments(split.ElementAtOrDefault(1), locals);
            var isLocal = scriptId >= _data.GlobalScriptCount;
            var key = isLocal ? $"{room.ResourceId}:{scriptId}" : $"g:{scriptId}";
            if (!visiting.Add(key)) continue;
            var child = Load(room, scriptId, isLocal, key);
            if (child is not null)
                Walk(room, child, arguments, depth + 1,
                    $"{path} → {(isLocal ? "local" : "global")} script {scriptId}", allowPlainLoad && isLocal,
                    visiting, results, stopAtCutscene);
            visiting.Remove(key);
        }
    }

    private ScummScriptDisassembly? Load(GameResource room, int scriptId, bool isLocal, string key)
    {
        if (_cache.TryGetValue(key, out var cached)) return cached;
        ScummScriptDisassembly? script = null;
        try
        {
            script = isLocal ? _data.RoomLocalScript(room.ResourceId!.Value, scriptId) : _data.GlobalScript(scriptId);
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
}
