using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>An award the game's scripts give (Indy 3's IQ points: Script 125 with a slot and the points), and the code that gives it.</summary>
public sealed record ScummStatusAward(int Slot, int Points, string Where, string Title);

/// <summary>An outfit the hero can wear: the object the outfit variable holds and the costume the dressing script gives the hero for it.</summary>
public sealed record ScummStatusOutfit(int ObjectId, int Costume, string? Name);

/// <summary>A story bit the game's code raises, and the places that raise it (titles as the scene lists name them).</summary>
public sealed record ScummStatusFlag(string Bit, IReadOnlyList<string> Titles, IReadOnlyList<string> Where);

/// <summary>
/// Game Status values beyond the inventory (user, 9 November 2026: "makes total sense to add those to status": Indy 3's health, outfit, IQ points and
/// story flags, as LOOM's). Every one is found by its role in the edition's own scripts, never by a variable number:
/// <list type="bullet">
/// <item>Health: a variable the scripts reset to one value in several places, that a script lowers (<c>subtract</c>) and tests against 0 (Indy 3's
/// <c>var[0x0C1]</c>, 1000; Script 42 takes a blow, Script 81 is the knockout).</item>
/// <item>Outfit: a variable compared with several objects, each followed by the hero's costume (Indy 3 Script 52 +0x014A: <c>var[0x0B4]</c> 403 is costume
/// 2, 406 costume 3, 405 costume 4); the guards compare it with the outfit they saw (Scripts 58, 60).</item>
/// <item>Awards: a global script that writes its second argument into a string at its first and adds it to a total (Indy 3 Script 125: string 7,
/// <c>var[0x0F4]</c>), with every call that gives literal arguments (73).</item>
/// <item>Story flags: the bits the code sets to 1.</item>
/// </list>
/// </summary>
public sealed partial class ScummStatusSchema
{
    public string? HealthVariable { get; private init; }
    public int HealthMax { get; private init; }
    public string? HealthWhere { get; private init; }
    public string? OutfitVariable { get; private init; }
    public int OutfitActor { get; private init; }
    public IReadOnlyList<ScummStatusOutfit> Outfits { get; private init; } = [];
    public string? OutfitWhere { get; private init; }
    public int? AwardString { get; private init; }
    public string? AwardTotal { get; private init; }
    public int AwardScript { get; private init; }
    public IReadOnlyList<ScummStatusAward> Awards { get; private init; } = [];
    public IReadOnlyList<ScummStatusFlag> Flags { get; private init; } = [];

    public bool IsEmpty => HealthVariable is null && OutfitVariable is null && Awards.Count == 0 && Flags.Count == 0;

    private sealed record Unit(string Label, int Room, int ObjectId, int Verb, string ObjectName, ScummScriptDisassembly Script);

    /// <summary>Reads the schema from the edition's scripts. <paramref name="verbs"/> names verbs as the scene lists do.</summary>
    public static ScummStatusSchema Read(GameLibrary game, ScummCutsceneVerbs? verbs = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        var data = ScummDataSources.For(game);
        var units = Units(game, data).ToList();
        var globals = units.Where(u => u.Room == 0).ToDictionary(u => int.Parse(u.Label[7..], CultureInfo.InvariantCulture));
        // Who starts each script (one hop), for the titles of code no object or entry runs directly.
        var callers = new Dictionary<string, List<Unit>>(StringComparer.Ordinal);
        foreach (var unit in units)
            foreach (var call in unit.Script.Instructions.Where(i => i.Operation is "startScript" or "chainScript"))
                if (Literal(call.Operands.Split(',')[0].Trim()) is int id)
                {
                    var target = id >= data.GlobalScriptCount ? $"Room {unit.Room} local {id}" : $"Script {id}";
                    (callers.TryGetValue(target, out var list) ? list : callers[target] = []).Add(unit);
                }
        string Title(Unit unit, int depth = 0)
        {
            if (unit.ObjectId != 0)
                return ScummCutsceneTitles.Plain(new ScummCutscene(unit.Room, unit.ObjectId, unit.ObjectName, unit.Verb, [], 0, 0, 0, "", Verbs: verbs));
            if (unit.Label.EndsWith(" entry", StringComparison.Ordinal)) return $"Room {unit.Room:00} · Entry";
            if (depth < 2 && callers.TryGetValue(unit.Label, out var by) && by.Count > 0)
                return Title(by.OrderBy(u => u.ObjectId == 0 ? 1 : 0).First(), depth + 1);
            return unit.Room > 0 ? $"Room {unit.Room:00} · A scene" : "A scene";
        }
        // Code no object or entry runs directly (Indy 3's fights, Script 60): named after the line spoken nearest before it (after it, if none).
        string Named(Unit unit, int offset)
        {
            var title = Title(unit);
            if (!title.EndsWith("A scene", StringComparison.Ordinal)) return title;
            var lines = unit.Script.Instructions.Where(i => i.Operation is "print" or "printEgo").Select(i => (i.Offset, Text: SpokenText().Match(i.Operands)))
                .Where(l => l.Text.Success && l.Text.Groups[1].Value.Trim().Length > 2).ToList();
            var near = lines.LastOrDefault(l => l.Offset < offset);
            if (near.Text is null) near = lines.FirstOrDefault(l => l.Offset > offset);
            if (near.Text is null) return title;
            // The disassembly writes the charset's codes as text (\xFF\x03, a new line).
            var said = Regex.Replace(Regex.Replace(near.Text.Groups[1].Value, @"(\\x[0-9A-F]{2})+", " "), @"\s+", " ").Trim();
            return (unit.Room > 0 ? $"Room {unit.Room:00} · " : "") + $"“{(said.Length > 60 ? said[..57].TrimEnd() + "…" : said)}”";
        }
        var schema = new ScummStatusSchema();
        // Health.
        var resets = units.SelectMany(u => u.Script.Instructions.Where(i => i.Operation == "move").Select(i => (u, i)))
            .Select(x => (x.u, x.i, Parts: x.i.Operands.Split(" = "))).Where(x => x.Parts.Length == 2 && Variable(x.Parts[0].Trim()) && Literal(x.Parts[1].Trim()) is > 1)
            .GroupBy(x => (Variable: x.Parts[0].Trim(), Value: Literal(x.Parts[1].Trim())!.Value)).Where(g => g.Count() >= 3).ToList();
        // The hero's is the one an item restores (Indy 3 Room 24's first aid kit: move var[0x0C1] = 1000); an opponent's (var[0x0C3]) is reset by scripts only.
        foreach (var reset in resets.OrderByDescending(g => g.Any(x => x.u.ObjectId != 0) ? 1 : 0).ThenByDescending(g => g.Count()))
        {
            var lowered = units.FirstOrDefault(u => u.Script.Instructions.Any(i => i.Operation == "subtract" && i.Operands.StartsWith(reset.Key.Variable + " -=", StringComparison.Ordinal)) &&
                                                   u.Script.Instructions.Any(i => i.Operation == "isGreater" && i.Operands.StartsWith(reset.Key.Variable + ", 0x0000", StringComparison.Ordinal)));
            if (lowered is null) continue;
            schema = schema.With(health: (reset.Key.Variable, reset.Key.Value, $"set to {reset.Key.Value} in {reset.Count()} places; {lowered.Label} lowers it and tests 0"));
            break;
        }
        // Outfit: isEqual V, object, then (within a few instructions) actorOps A, [costume, C].
        // The dressing script writes the variable too (Indy 3 Script 52 +0x00F0: move var[0x0B4] = local[0x000]); Script 25 also branches on it, with
        // other costumes.
        foreach (var unit in units.Where(u => u.Room == 0))
        {
            var code = unit.Script.Instructions;
            var found = new List<(string Variable, int Object, int Actor, int Costume)>();
            for (var i = 0; i < code.Count; i++)
            {
                if (code[i].Operation != "isEqual") continue;
                var parts = code[i].Operands.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 2 || !Variable(parts[0]) || Literal(parts[1]) is not int value || value < 0x80) continue;
                for (var j = i + 1; j < Math.Min(code.Count, i + 4); j++)
                    if (code[j].Operation == "actorOps" && Literal(code[j].Operands.Split(',', 2)[0].Trim()) is int actor && Costume().Match(code[j].Operands) is { Success: true } m)
                    { found.Add((parts[0], value, actor, int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture))); break; }
            }
            if (found.GroupBy(f => (f.Variable, f.Actor)).FirstOrDefault(g => g.Select(f => f.Object).Distinct().Count() >= 2 &&
                    code.Any(i => i.Operation == "move" && i.Operands.StartsWith(g.Key.Variable + " =", StringComparison.Ordinal))) is not { } outfits) continue;
            var names = ObjectNames(game, data);
            schema = schema.With(outfit: (outfits.Key.Variable, outfits.Key.Actor,
                outfits.GroupBy(f => f.Object).Select(g => new ScummStatusOutfit(g.Key, g.First().Costume, names.GetValueOrDefault(g.Key))).ToList(),
                $"{unit.Label} dresses actor {outfits.Key.Actor} by {outfits.Key.Variable}"));
            break;
        }
        // Awards.
        foreach (var (id, unit) in globals)
        {
            var code = unit.Script.Instructions;
            var write = code.FirstOrDefault(i => i.Operation == "stringOps" && i.Operands.StartsWith("setStringChar,", StringComparison.Ordinal) &&
                                                 i.Operands.EndsWith("local[0x000], local[0x001]", StringComparison.Ordinal));
            var total = code.FirstOrDefault(i => i.Operation == "add" && i.Operands.EndsWith("+= local[0x001]", StringComparison.Ordinal));
            if (write is null || total is null || Literal(write.Operands.Split(',', StringSplitOptions.TrimEntries)[1]) is not int stringId) continue;
            var awards = new List<ScummStatusAward>();
            foreach (var caller in units)
                foreach (var call in caller.Script.Instructions.Where(i => i.Operation == "startScript" && Literal(i.Operands.Split(',')[0].Trim()) == id))
                    if (AwardArguments().Match(call.Operands) is { Success: true } a)
                        awards.Add(new(int.Parse(a.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                            int.Parse(a.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture), $"{caller.Label} +0x{call.Offset:X4}", Named(caller, call.Offset)));
            if (awards.Count < 3) continue;
            schema = schema.With(awards: (stringId, total.Operands.Split(' ')[0], id,
                awards.GroupBy(a => a.Slot).Select(g => g.First()).OrderBy(a => a.Slot).ToList()));
            break;
        }
        // Story flags.
        var flags = new Dictionary<string, List<(Unit Unit, int Offset)>>(StringComparer.Ordinal);
        var flagWhere = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var unit in units)
            foreach (var ins in unit.Script.Instructions.Where(i => i.Operation == "move"))
                if (ins.Operands.Split(" = ") is [var target, var value] && LiteralBit().IsMatch(target.Trim()) && Literal(value.Trim()) == 1)
                {
                    var bit = target.Trim();
                    (flags.TryGetValue(bit, out var list) ? list : flags[bit] = []).Add((unit, ins.Offset));
                    (flagWhere.TryGetValue(bit, out var where) ? where : flagWhere[bit] = []).Add($"{unit.Label} +0x{ins.Offset:X4}");
                }
        return schema.With(flags: flags.OrderBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => new ScummStatusFlag(f.Key, f.Value.Select(u => Named(u.Unit, u.Offset)).Distinct().ToList(), flagWhere[f.Key])).ToList());
    }

    private ScummStatusSchema With((string Variable, int Max, string Where)? health = null,
        (string Variable, int Actor, List<ScummStatusOutfit> Outfits, string Where)? outfit = null,
        (int String, string Total, int Script, List<ScummStatusAward> Awards)? awards = null, List<ScummStatusFlag>? flags = null) => new()
    {
        HealthVariable = health?.Variable ?? HealthVariable, HealthMax = health?.Max ?? HealthMax, HealthWhere = health?.Where ?? HealthWhere,
        OutfitVariable = outfit?.Variable ?? OutfitVariable, OutfitActor = outfit?.Actor ?? OutfitActor, Outfits = outfit?.Outfits ?? Outfits,
        OutfitWhere = outfit?.Where ?? OutfitWhere,
        AwardString = awards?.String ?? AwardString, AwardTotal = awards?.Total ?? AwardTotal, AwardScript = awards?.Script ?? AwardScript,
        Awards = awards?.Awards ?? Awards, Flags = flags ?? Flags,
    };

    private static IEnumerable<Unit> Units(GameLibrary game, IScummDataSource data)
    {
        foreach (var id in data.GlobalScriptIds())
        {
            ScummScriptDisassembly? script = null;
            try { script = data.GlobalScript(id); } catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { }
            if (script is not null) yield return new($"Script {id}", 0, 0, 0, "", script);
        }
        foreach (var room in game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct().Order())
        {
            var found = new List<Unit>();
            try
            {
                if (data.RoomEntryScript(room) is { } entry) found.Add(new($"Room {room} entry", room, 0, 0, "", entry));
                foreach (var local in data.RoomLocalScriptIds(room))
                    if (data.RoomLocalScript(room, local) is { } script) found.Add(new($"Room {room} local {local}", room, 0, 0, "", script));
                var record = data.ReadRoom(room);
                foreach (var (objectId, objectVerbs) in data.ReadObjectVerbs(room, record.Objects))
                {
                    var name = record.ObjectNames.TryGetValue(objectId, out var bytes) ? ScummTextDecoder.DecodeGlyphs(bytes).TrimEnd('\0', '@', ' ') : "";
                    foreach (var verb in objectVerbs)
                        if (data.ReadObjectVerb(room, record.Objects.Last(o => o.Id == objectId), verb.VerbId, out _) is { } script)
                            found.Add(new($"Room {room} object {objectId} verb 0x{verb.VerbId:X2}", room, objectId, verb.VerbId, name, script));
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException) { }
            foreach (var unit in found) yield return unit;
        }
    }

    private static Dictionary<int, string> ObjectNames(GameLibrary game, IScummDataSource data)
    {
        var names = new Dictionary<int, string>();
        foreach (var room in game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct().Order())
        {
            try
            {
                foreach (var (id, bytes) in data.ReadRoom(room).ObjectNames)
                    if (ScummTextDecoder.DecodeGlyphs(bytes).TrimEnd('\0', '@', ' ') is { Length: > 0 } name) names.TryAdd(id, name);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { }
        }
        return names;
    }

    private static bool Variable(string text) => VariableOperand().IsMatch(text);

    private static int? Literal(string text) =>
        text.StartsWith("0x", StringComparison.Ordinal) && int.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : null;

    [GeneratedRegex(@"^var\[0x[0-9A-F]+\]$")]
    private static partial Regex VariableOperand();

    [GeneratedRegex(@"^bit\[0x[0-9A-F]+\]$")]
    private static partial Regex LiteralBit();

    [GeneratedRegex(@"\bcostume, 0x([0-9A-F]+)")]
    private static partial Regex Costume();

    [GeneratedRegex(@"text, ""([^""]*)""")]
    private static partial Regex SpokenText();

    [GeneratedRegex(@"\[0x([0-9A-F]+), 0x([0-9A-F]+)\]$")]
    private static partial Regex AwardArguments();
}
