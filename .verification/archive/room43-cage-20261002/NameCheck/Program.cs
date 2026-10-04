using System.Text.Json;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Discovery;

var original = JsonSerializer.Deserialize<LoomReplayCatalog>(File.ReadAllText("src/SCUMMexplorer.App/loom-replays.json"),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
var named = LoomReplayCatalog.Load(new GameFolderScanner().Scan(args[0]));
if (named.Replays.Length != original.Replays.Length || named.Replays.Length == 0) throw new Exception("Catalog mismatch.");
var changes = new List<object>();
for (int i = 0; i < original.Replays.Length; i++)
{
    var before = original.Replays[i]; var after = named.Replays[i];
    if (JsonSerializer.Serialize(after with { Title = before.Title }) != JsonSerializer.Serialize(before))
        throw new Exception("Non-title recipe change: " + before.Id);
    if (after.Title.Contains("Draft response ")) throw new Exception("Numbered response remains: " + after.Title);
    if (before.Title != after.Title) changes.Add(new { before.Id, before.Verb, before = before.Title, after = after.Title });
}
if (JsonSerializer.Serialize(named with { Replays = original.Replays }) != JsonSerializer.Serialize(original))
    throw new Exception("Catalog evidence changed.");
File.WriteAllText(args[1], JsonSerializer.Serialize(new { entries = named.Replays.Length, renamed = changes.Count, changes },
    new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{changes.Count} draft responses named across {named.Replays.Length} generated entries; all recipes, memberships and evidence unchanged.");
