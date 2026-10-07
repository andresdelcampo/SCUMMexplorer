using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

Directory.CreateDirectory(args[1]);
foreach (var edition in new[] { "loom ega mt32 en", "loom vga en" })
{
    var game = new GameFolderScanner().Scan(Path.Combine(args[0], edition));
    var data = ScummDataSources.For(game);
    var glossary = LoomGlossary.Load(game);
    var scripts = data.GlobalScriptIds().Select(id => data.GlobalScript(id)!).ToArray();
    var prefix = edition.Contains("vga") ? "vga" : "ega";
    File.WriteAllText(Path.Combine(args[1], prefix + ".json"), JsonSerializer.Serialize(scripts));
    File.WriteAllLines(Path.Combine(args[1], prefix + "-scripts.txt"), scripts.SelectMany(s =>
        new[] { $"== Script {s.ScriptId:000} · {glossary.Script(s.ScriptId)?.Name ?? "(unnamed)"}" }
            .Concat(s.Instructions.Select(i => $"{i.Offset:X4} {i.Operation} {i.Operands}"))));
    File.WriteAllLines(Path.Combine(args[1], prefix + "-readable.txt"), scripts.Select(s => new LoomScriptPresenter(glossary).Present(s)));
    File.WriteAllLines(Path.Combine(args[1], prefix + "-titles.txt"), scripts.Select(s => $"{s.ScriptId:000}\t{glossary.Script(s.ScriptId)?.Name ?? "(unnamed)"}"));
}
