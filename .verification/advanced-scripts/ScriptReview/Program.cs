using System.Text.Json;
using SCUMMexplorer.Core.Discovery;

Directory.CreateDirectory(args[1]);
foreach (var edition in new[] { "loom ega mt32 en", "loom ega mt32 es", "loom vga en" })
{
    var folder = Path.Combine(args[0], edition);
    if (!Directory.Exists(folder)) folder = Path.Combine(args[0], "ALTERNATIVE LOOMS", edition);
    var game = new GameFolderScanner().Scan(folder);
    var data = ScummDataSources.For(game);
    var glossary = LoomGlossary.Load(game);
    var skipped = new List<string>();
    var scripts = data.GlobalScriptIds().Select(id =>
    {
        try { return data.GlobalScript(id); }
        catch (InvalidDataException ex) { skipped.Add($"Script {id:000}: {ex.Message}"); return null; }
    }).OfType<ScummScriptDisassembly>().ToArray();
    var prefix = edition.Contains("vga") ? "vga" : edition.EndsWith("es") ? "ega-es" : "ega";
    File.WriteAllText(Path.Combine(args[1], prefix + ".json"), JsonSerializer.Serialize(scripts));
    File.WriteAllLines(Path.Combine(args[1], prefix + "-skipped.txt"), skipped);
    File.WriteAllLines(Path.Combine(args[1], prefix + "-scripts.txt"), scripts.SelectMany(s =>
        new[] { $"== Script {s.ScriptId:000} · {glossary.Script(s.ScriptId)?.Name ?? "(unnamed)"}" }
            .Concat(s.Instructions.Select(i => $"{i.Offset:X4} {i.Operation} {i.Operands}"))));
    File.WriteAllLines(Path.Combine(args[1], prefix + "-readable.txt"), scripts.Select(s => new LoomScriptPresenter(glossary).Present(s)));
    File.WriteAllLines(Path.Combine(args[1], prefix + "-titles.txt"), scripts.Select(s => $"{s.ScriptId:000}\t{glossary.Script(s.ScriptId)?.Name ?? "(unnamed)"}"));
}
