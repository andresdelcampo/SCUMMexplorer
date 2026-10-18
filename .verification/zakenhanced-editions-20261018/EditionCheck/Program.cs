using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

var root = args[0];
var fixture = Path.Combine(root, "zakE ega en");
var inventory = new List<object>();
foreach (var directory in Directory.GetDirectories(root).Order())
{
    var detected = new GameFolderScanner().Scan(directory);
    if (GameSupport.FamilyOf(detected) != GameFamily.ZakEnhanced) continue;
    inventory.Add(new { Folder = directory, detected.Title, detected.Edition, Version = detected.Version.ToString() });
}
var game = new GameFolderScanner().Scan(fixture);
var data = ScummDataSources.For(game);
var roomCount = game.Rooms.Count(room => data.ReadRoom(room.ResourceId!.Value).Objects is not null);
var script = data.GlobalScript(140)!;
var pixels = new byte[64];
new ScummV2Charset(2).DrawPlainLine(pixels, 8, 0, [39], 0, 0, 7);
Console.WriteLine(JsonSerializer.Serialize(new {
    Family = GameSupport.FamilyOf(game).ToString(), inventory,
    IndexSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(fixture, "00.LFL")))),
    game.Edition, Version = game.Version.ToString(), Dialect = ScummProfile.For(game).Dialect.ToString(),
    DataSource = data.GetType().Name, RoomsRead = roomCount,
    Resources = game.Resources.GroupBy(r => r.Kind).ToDictionary(g => g.Key.ToString(), g => g.Count()),
    FirstSceneText = script.Instructions.First(i => i.Operation == "print").Operands,
    ApostrophePixelSha256 = Convert.ToHexString(SHA256.HashData(pixels)),
    AlternateEditionCoverage = "Blocked: no alternate local family copy; Classic is a separate family"
}, new JsonSerializerOptions { WriteIndented = true }));
