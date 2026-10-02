using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

// Fresh loaded resources; no LoomExplorationSession, runtime, effect dispatch or known-state injection.
if (args.Length != 2 || File.Exists(args[1])) throw new ArgumentException("Expected game folder and fresh report path.");
var game = new GameFolderScanner().Scan(args[0]);
var reader = new LoomScriptReader();
var analyzer = new LoomCorrelatedNumericAnalyzer();
var code = new[] { 22, 23, 30, 31 }.ToDictionary(id => id, id => reader.Decode(game.Resources.Single(r => r.Kind == ResourceKind.Script && r.ResourceId == id)));
var boot = reader.DecodeRoomLocalScript(game.Rooms.Single(r => r.ResourceId == 1), 211, 200)!;
var prior = JsonDocument.Parse(File.ReadAllText(".verification/correlated-drafts-20261002/predicate-summary.json")).RootElement;
var domain = JsonDocument.Parse(File.ReadAllText(".verification/room41-certainty-20261002/source-domains.json")).RootElement;
var tables = domain.GetProperty("tables").EnumerateArray().ToArray();
var packed = tables.Select(t => t.GetProperty("packed").EnumerateArray().Select(v => v.GetUInt16()).ToArray()).ToArray();
int Offset(JsonElement table, string field) => int.Parse(table.GetProperty(field).GetString()!.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
void Require(bool ok, string message) { if (!ok) throw new InvalidDataException(message); }
for (var slot = 0; slot < tables.Length; slot++)
{
    var source = boot.Instructions.Single(i => i.Offset == Offset(tables[slot], "table"));
    ushort[] literals = source.Operation == "move"
        ? [ushort.Parse(source.Operands.Split(" = ")[1].AsSpan(2), NumberStyles.HexNumber)]
        : source.Operands[(source.Operands.LastIndexOf('[') + 1)..^1].Split(',', StringSplitOptions.TrimEntries)
            .Select(s => ushort.Parse(s.AsSpan(2), NumberStyles.HexNumber)).ToArray();
    Require((slot == 0 ? literals.Take(1) : literals).SequenceEqual(packed[slot]), "Loaded boot alternatives differ from the pinned source domain.");
    if (slot != 0)
    {
        var pick = boot.Instructions.Single(i => i.Offset == Offset(tables[slot], "random"));
        var write = boot.Instructions.Single(i => i.Offset == Offset(tables[slot], "write"));
        Require(pick.Operation == "getRandomNr" && pick.Operands == "local[0x001], 0x02", "Boot random bound changed.");
        var selected = boot.Instructions.Single(i => i.Offset == pick.Offset + 4);
        Require(selected.Operation == "move" && selected.Operands == "local[0x000] = local[0x002+local[0x001]]", "Boot correlated table selection changed.");
        Require(write.Operation == "move" && write.Operands == $"var[0x032+0x{slot * 2:X3}] = local[0x000]", "Boot selected write changed.");
    }
}
Dictionary<string, LoomNumericValue> Globals(ushort[] words) => Enumerable.Range(0, words.Length)
    .SelectMany(slot => new[] {
        new KeyValuePair<string, LoomNumericValue>($"var[0x{0x32 + 2 * slot:X3}]", LoomNumericValue.Hypothesis(words[slot], $"Room01 local211 +{tables[slot].GetProperty("write").GetString()}; whole boot word alternative")),
        new KeyValuePair<string, LoomNumericValue>($"var[0x{0x33 + 2 * slot:X3}]", LoomNumericValue.Opaque($"response for slot {slot}", "Response intentionally outside numeric proof")) })
    .ToDictionary(p => p.Key, p => p.Value);
var flags = new List<LoomNumericWorldResult>();
for (var slot = 0; slot < packed.Length; slot++)
    for (var alternative = 0; alternative < packed[slot].Length; alternative++)
        foreach (ushort mask in new ushort[] { 0, 0x2000, 0x4000, 0x6000 })
        {
            var words = new ushort[5]; words[slot] = (ushort)(packed[slot][alternative] | mask);
            var result = analyzer.Analyze(code[23], [new($"mask-{slot}-{alternative}-{mask}", "Hypothetical boot word and optional OR flags; constrained slot index.", Globals(words),
                [LoomNumericValue.Hypothesis((ushort)slot, "Constrained caller index")])]).Single();
            Require(result.ReachedStop && result.UnresolvedReason is null, "023 must finish its complete numeric code.");
            var expected = prior.GetProperty("maskCases").EnumerateArray().Single(c => c.GetProperty("slot").GetInt32() == slot &&
                c.GetProperty("alternative").GetInt32() == alternative && c.GetProperty("flags").GetUInt16() == mask);
            Require(Enumerable.Range(0, 4).Select(n => result.Globals[$"var[0x{0x52 + n:X3}]"].Number!.Value)
                .SequenceEqual(expected.GetProperty("notes").EnumerateArray().Select(n => n.GetUInt16())), "023 tuples differ.");
            foreach (var (key, property) in new[] { ("bit[0xD69]", "learned"), ("bit[0xD6A]", "canReverse"), ("bit[0xD6B]", "used") })
                Require(result.Globals[key].Number == expected.GetProperty(property).GetUInt16(), "023 flag projections differ.");
            Require(result.Globals["var[0x065]"].Number is null, "Opaque response must remain opaque.");
            flags.Add(result);
        }
var assignments = new List<ushort[]>();
void Product(int slot, ushort[] words)
{
    if (slot == 5) { assignments.Add(words.ToArray()); return; }
    foreach (var word in packed[slot]) { words[slot] = word; Product(slot + 1, words); }
}
Product(0, new ushort[5]);
LoomNumericWorld Input(string id, ushort[] words, IReadOnlyList<LoomNumericValue> notes, bool reverse = false)
{
    var globals = Globals(words);
    for (var n = 0; n < 4; n++) globals[$"var[0x{0x56 + n:X3}]"] = notes[reverse ? 3 - n : n];
    return new(id, "Hypothetical whole-word source assignment and matching whole slot4 input; begins after D70 gate. No runtime selection/path/effect proof.", globals);
}
var worlds = new List<LoomNumericWorld>();
foreach (var (words, index) in assignments.Select((w, i) => (w, i)))
{
    var alternatives = Array.IndexOf(packed[4], words[4]);
    var decoded = flags.Single(f => f.Id == $"mask-4-{alternatives}-0");
    var notes = Enumerable.Range(0, 4).Select(n => decoded.Globals[$"var[0x{0x52 + n:X3}]"]).ToArray();
    worlds.Add(Input($"world-{index}-forward", words, notes));
    worlds.Add(Input($"world-{index}-reverse", words, notes, true));
}
var scan = analyzer.Analyze(code[22], worlds, id => code.GetValueOrDefault(id), startOffset: 0x64);
Require(scan.Count == 162 && flags.Count == 52, "World coverage changed.");
var comparisons = new HashSet<(int Slot, ushort Input, ushort Candidate, int Offset)>();
foreach (var result in scan)
{
    var reverse = result.Id.EndsWith("reverse");
    Require(result.UnresolvedReason is null && result.Boundary is { Operation: "chainScript", Target: 26 }, "Scan must end before026.");
    Require(result.Boundary!.Site.Offset == (reverse ? 0x125 : 0xC3) && result.Boundary.Arguments[0].Number == 4 &&
        result.Boundary.Arguments[1].Number == (reverse ? 1 : 0), "Constrained source worlds must select slot4.");
    if (reverse) continue;
    foreach (var branch in result.Branches.Where(b => b.Site.Script == 22 && !b.Condition &&
        b.Site.Offset is 0x7D or 0x92 or 0xA7 or 0xBC or 0xDF or 0xF4 or 0x109 or 0x11E))
    {
        var slot = branch.Locals["local[0x000]"].Number!.Value;
        if (slot < 4) comparisons.Add((slot, result.Globals["var[0x03A]"].Number!.Value,
            result.Globals[$"var[0x{0x32 + 2 * slot:X3}]"].Number!.Value, branch.Site.Offset));
    }
}
var expectedComparisons = domain.GetProperty("comparisons").EnumerateArray().Select(c =>
    (c.GetProperty("earlierSlot").GetInt32(), packed[4][c.GetProperty("inputAlternative").GetInt32()],
        packed[c.GetProperty("earlierSlot").GetInt32()][c.GetProperty("candidateAlternative").GetInt32()],
        int.Parse(c.GetProperty("comparisonOffset").GetString()!.AsSpan(3), NumberStyles.HexNumber))).ToHashSet();
Require(comparisons.SetEquals(expectedComparisons), "51 source comparison cases differ.");
var fabricated = new List<object>();
foreach (var match in prior.GetProperty("fabricatedInputs").EnumerateArray())
{
    var words = assignments[0].ToArray();
    var slot = match.GetProperty("earlierSlot").GetInt32();
    var input = match.GetProperty("input").EnumerateArray().Select(n => n.GetUInt16()).ToArray();
    var reverse = match.GetProperty("reverse").GetInt32();
    var alternative = tables[slot].GetProperty("notes").EnumerateArray().Select((n, i) => (n, i))
        .Single(p => (reverse == 1 ? p.n.EnumerateArray().Reverse() : p.n.EnumerateArray()).Select(n => n.GetUInt16()).SequenceEqual(input)).i;
    words[slot] = packed[slot][alternative];
    var result = analyzer.Analyze(code[22], [Input("fabricated", words, input.Select(n => LoomNumericValue.Hypothesis(n, "Fabricated Cartesian input, absent from slot4 source tuples")).ToArray())],
        id => code.GetValueOrDefault(id), startOffset: 0x64).Single();
    Require(result.UnresolvedReason is null && result.Boundary!.Arguments[0].Number == slot && result.Boundary.Arguments[1].Number == reverse, "Fabricated witness differs.");
    fabricated.Add(new { input, slot, reverse });
}
var missing = analyzer.Analyze(code[22], [new("unknown-input", "No selected input values.", Globals(assignments[0]))], id => code.GetValueOrDefault(id), startOffset: 0x64).Single();
Require(missing.UnresolvedReason?.Contains("unknown external value var[0x056]") == true, "Missing input must fail closed.");
var gate = analyzer.Analyze(code[22], [worlds[0]], id => code.GetValueOrDefault(id)).Single();
Require(gate.UnresolvedReason?.Contains("unknown external value bit[0xD70]") == true, "The omitted D70 gate remains unresolved.");
var practice = reader.DecodeRoomLocalScript(game.Rooms.Single(r => r.ResourceId == 6), 201, 200)!;
var practiceResults = new List<LoomNumericWorldResult>();
for (var alternative = 0; alternative < 3; alternative++)
    for (ushort index = 0; index < 4; index++)
    {
        var tuple = flags.Single(f => f.Id == $"mask-2-{alternative}-0");
        var globals = tuple.Globals.ToDictionary(p => p.Key, p => p.Value);
        globals["var[0x064]"] = LoomNumericValue.Hypothesis(0, "Hypothetical practice mode0 excludes030's mode2 callee027; not runtime state.");
        var result = analyzer.Analyze(practice, [new($"practice-{alternative}-{index}", "Source slot2 tuple; constrained note index and mode0; starts at local201+0021, before030/031 argument boundary.",
            globals, [LoomNumericValue.Hypothesis(index, "Hypothetical caller note index")])], id => code.GetValueOrDefault(id), startOffset: 0x21).Single();
        Require(result.Boundary is { Operation: "chainScript", Target: 31 } && result.Boundary.Arguments.Single().Number == tuple.Globals[$"var[0x{0x52 + index:X3}]"].Number,
            "Practice numeric arguments differ.");
        Require(!result.ReachedStop && result.UnresolvedReason?.Contains("callee did not return") == true, "A chain cannot be treated as a returned child.");
        Require(result.Boundary!.Arguments.Single().Sources.Any(s => s.Contains("Room 06 local script 201") && s.EndsWith("+0x0028")), "Practice caller source missing.");
        practiceResults.Add(result);
    }
var earlyStop = analyzer.Analyze(code[31], [new("early031-slice", "Instruction-only0019 slice with hypothetical D671; no entry/path/scheduler proof.",
    new Dictionary<string, LoomNumericValue> { ["bit[0xD67]"] = LoomNumericValue.Hypothesis(1, "Slice entry hypothesis") })], startOffset: 0x19).Single();
var terminal = analyzer.Analyze(code[31], [new("terminal031-slice", "Instruction-only031D slice; no reached-entry/scheduler/callee/cancellation proof.",
    new Dictionary<string, LoomNumericValue> { ["bit[0xD67]"] = LoomNumericValue.Hypothesis(1, "Slice entry hypothesis") })], startOffset: 0x31D).Single();
Require(earlyStop.ReachedStop && earlyStop.Globals["bit[0xD67]"].Number == 1 && terminal.ReachedStop && terminal.Globals["bit[0xD67]"].Number == 0,
    "Instruction-only early-stop/completion distinction changed.");
var report = new {
    scope = "Generic Core source-only correlated numeric analysis. Hypotheses never enter runtime; unsupported effects/yields/callees stop. No selected runtime input/index, path, effect, cancellation or090 completion proof.",
    bootAssignments = assignments.Count, numericWorlds = scan.Count, flagCases = flags.Count,
    forwardComparisons = comparisons.Count, firstComponentOverlaps = comparisons.Count(c => c.Offset is not (0x7D or 0xDF)), fabricated,
    numericWrites = flags.SelectMany(f => f.Writes).Where(w => !w.Key.StartsWith("local[")).Select(w => w.Key).Distinct().Order().ToArray(),
    gate = gate.UnresolvedReason, missingInput = missing.UnresolvedReason, estimatesChanged = false, native090Completed = false,
    fingerprints = Directory.EnumerateFiles(args[0], "*.LFL").Order().ToDictionary(p => Path.GetFileName(p), p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant()),
    worlds = scan, masks = flags, practice = practiceResults, earlyStop, terminal
};
Directory.CreateDirectory(Path.GetDirectoryName(args[1])!);
File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("162 correlated worlds,52 flag cases,51 comparisons/14 overlaps,four fabricated witnesses; source hypotheses only.");
