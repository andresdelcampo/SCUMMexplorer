using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

// Source-only extension: causal draft teaching, literal terminology and dynamic operands.
// Detached sessions read the own-edition draft table; no game/profile writes or native input.
var root = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var failures = new List<string>();
var reports = new List<object>();
int checks = 0;
void Check(bool condition, string label)
{
    checks++;
    if (!condition) failures.Add(label);
}
foreach (var (edition, relative) in new[] {
    ("ega-en", "loom ega mt32 en"),
    ("ega-es", "ALTERNATIVE LOOMS/loom ega mt32 es"),
    ("vga", "loom vga en") })
{
    var game = new GameFolderScanner().Scan(Path.Combine(root, relative));
    var data = ScummDataSources.For(game);
    var blocks = new List<(string Place, ScummScriptDisassembly Script)>();
    var errors = new List<string>();
    void Read(string place, Func<ScummScriptDisassembly?> read)
    {
        try { if (read() is { } script) blocks.Add((place, script)); }
        catch (InvalidDataException ex) { errors.Add($"{place}: {ex.Message}"); }
    }
    foreach (int id in data.GlobalScriptIds()) Read($"global {id}", () => data.GlobalScript(id));
    foreach (int room in game.Rooms.Select(r => r.ResourceId!.Value))
    {
        Read($"room {room} entry", () => data.RoomEntryScript(room));
        Read($"room {room} exit", () => data.RoomExitScript(room));
        foreach (int id in data.RoomLocalScriptIds(room)) Read($"room {room} local {id}", () => data.RoomLocalScript(room, id));
        var rd = data.ReadRoom(room);
        foreach (var (id, verbs) in data.ReadObjectVerbs(room, rd.Objects))
        foreach (var verb in verbs.Where(v => !v.IsFallback))
            Read($"room {room} object {id} verb {verb.VerbId:X2}",
                () => data.ReadObjectVerb(room, rd.Objects.Single(o => o.Id == id), verb.VerbId, out _));
    }
    ScummScriptDisassembly Global(int id) => blocks.Single(b => b.Place == $"global {id}").Script;
    ScummScriptDisassembly Local(int id) => blocks.Single(b => b.Place == $"room 9 local {id}").Script;
    bool Calls(ScummScriptDisassembly script, int target, string argument) => script.Instructions.Any(i =>
        i.Operation == "startScript" && i.Operands == $"0x{target:X2}, [{argument}]");
    string Fingerprint(ScummScriptDisassembly script) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        string.Join("\n", script.Instructions.Select(i => $"{i.Offset:X4}:{i.Bytes}")))));
    var session = new LoomExplorationSession(game, game.V4Archive?.ReadIndexedObjects() ??
        new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL")), completeBoot: game.Id == GameId.Loom);
    var drafts = session.GameStatus.Drafts.Where(d => d.Slot is 12 or 13).ToArray();
    Check(drafts.Length == 2 && drafts.Single(d => d.Slot == 12).ForwardVerb == 0x4E &&
        drafts.Single(d => d.Slot == 12).ReverseVerb == 0x4F, edition + " slot 12 maps to 4E/4F");
    Check(drafts.Single(d => d.Slot == 13).ForwardVerb == 0x50 &&
        drafts.Single(d => d.Slot == 13).ReverseVerb == 0x51, edition + " slot 13 maps to 50/51");
    Check(Calls(Global(101), 212, "0x000C"), edition + " scene 101 plays slot 12 through local 212");
    Check(Calls(Local(212), 23, "local[0x000]"), edition + " local 212 loads supplied slot through 23");
    Check(Calls(Local(223), 212, "0x000D"), edition + " speech restoration scene plays slot 13");
    Check(Calls(Local(223), 34, "0x000C"), edition + " speech restoration marks slot 12 last-note knowledge");
    Check(Calls(Local(224), 34, "0x000D"), edition + " form restoration marks slot 13 last-note knowledge");
    Check(Global(101).Instructions.Any(i => i.Operation == "print" && i.Operands.Contains(
        edition == "ega-es" ? "\"Silencio.\"" : "\"Silence.\"", StringComparison.Ordinal)), edition + " own causative Silence text");
    Check(Global(34).Instructions.Any(i => i.Operation == "or" && i.Operands == "local[0x002] |= 0x4000"),
        edition + " Script 34 marks last-note knowledge, not the reverse flag");
    foreach (var (verb, local) in new[] { (0x4F, 223), (0x51, 224) })
    {
        var responses = blocks.Where(b => b.Place.StartsWith("room 9 object ") && b.Place.EndsWith($"verb {verb:X2}")).ToArray();
        Check(responses.Length == 1 && Calls(responses[0].Script, local, ""), edition + $" {verb:X2} dispatches local {local}");
    }
    var terms = blocks.SelectMany(b => b.Script.Instructions.Where(i => i.Operation is "print" or "printEgo" &&
        Regex.IsMatch(i.Operands, @"silenc|shap|hablar|talk again|duck|pato", RegexOptions.IgnoreCase))
        .Select(i => new { b.Place, i.Offset, i.Operation, i.Operands })).ToArray();
    var dynamic = blocks.SelectMany(b => b.Script.Instructions.Where(i =>
        i.Operation is "startScript" or "chainScript" or "startObject" &&
        !Regex.IsMatch(i.Operands, @"^0x[0-9A-F]+(?:,|$)"))
        .Select(i => new { b.Place, i.Offset, i.Operation, i.Operands })).ToArray();
    var helperCallers = blocks.SelectMany(b => b.Script.Instructions.Where(i => i.Operation == "startScript" &&
        Regex.IsMatch(i.Operands, @"^0x(?:CD|D2), \["))
        .Select(i => new { b.Place, i.Offset, i.Operands })).ToArray();
    var dynamicPlaces = dynamic.Select(d => d.Place).ToHashSet();
    var selected = blocks.Where(b => b.Place is "global 6" or "global 7" or "global 8" or "global 10" or
        "global 23" or "global 26" or "global 34" or "global 101" ||
        dynamicPlaces.Contains(b.Place) || b.Place.StartsWith("room 9 local ") || b.Place.StartsWith("room 9 object ") ||
        b.Place == "room 2 entry" || b.Place.StartsWith("room 2 object "));
    File.WriteAllText(Path.Combine(output, edition + "-source.txt"), string.Join("\n", selected.Select(b =>
        $"{b.Place}; instruction fingerprint {Fingerprint(b.Script)}\n{b.Script.Format()}")));
    var report = new { edition, decodedBlocks = blocks.Count, errors, drafts, terms, dynamic, helperCallers,
        incomplete = blocks.Where(b => !b.Script.HasCompleteControlFlow).Select(b => new { b.Place, b.Script.DecodeNote, b.Script.Issues }),
        fingerprints = selected.Select(b => new { b.Place, b.Script.ContainerName, b.Script.ContainerFileNumber,
            b.Script.ResourceOffset, b.Script.CodeFileOffset, b.Script.CodeLength, instructionSha256 = Fingerprint(b.Script) }),
        room2 = data.ReadRoom(2) };
    reports.Add(report);
    Console.WriteLine($"{edition}: {terms.Length} terminology sites, {dynamic.Length} dynamic first operands, {errors.Count} read errors");
}
File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllLines(Path.Combine(output, "checks.txt"), new[] { $"{checks - failures.Count}/{checks} source assertions passed" }.Concat(failures));
Console.WriteLine($"{checks - failures.Count}/{checks} source assertions passed");
foreach (string failure in failures) Console.Error.WriteLine(failure);
Environment.ExitCode = failures.Count == 0 ? 0 : 1;
