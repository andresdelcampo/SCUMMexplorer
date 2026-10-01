using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

var game = new GameFolderScanner().Scan(args[0]);
var decoder = new LoomAdlibAudio();
var rows = game.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId.HasValue)
    .Select(r => new { id = r.ResourceId, lifetime = decoder.ReadLifetime(r) }).ToArray();
File.WriteAllText(args[1], JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
foreach (var row in rows)
    Console.WriteLine($"{row.id}: {row.lifetime.Kind}; {row.lifetime.Callbacks / 472.0:F4}s; {row.lifetime.Reason}");
