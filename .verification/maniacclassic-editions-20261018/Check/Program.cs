using System.Security.Cryptography;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

var folder = args.FirstOrDefault() ?? @"D:\Juegos\Lucas\mmansion ega en";
Console.WriteLine("Local folder inventory: " + string.Join("; ", Directory.GetDirectories(Path.GetDirectoryName(folder)!).Select(Path.GetFileName).Order()));
var game = new GameFolderScanner().Scan(folder);
var data = ScummDataSources.For(game);
Console.WriteLine($"Fixture: {folder}; family={GameSupport.FamilyOf(game)}; edition={game.Edition}; demo={game.IsDemo}");
Console.WriteLine($"Version={game.Version}; engine={ScummProfile.For(game).EngineVersion}; dialect={ScummProfile.For(game).Dialect}; reader={data.GetType().Name}");
Console.WriteLine("Detection: " + game.DetectionNote);
foreach (var kind in game.Resources.GroupBy(resource => resource.Kind)) Console.WriteLine($"Resources {kind.Key}: {kind.Count()}");
foreach (var name in new[] { "00.LFL", "45.LFL", "MANIAC.EXE" })
{
    var path = Directory.GetFiles(folder).SingleOrDefault(path => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase));
    Console.WriteLine(path is null ? $"{name}: absent" : $"{name}: {new FileInfo(path).Length} bytes, SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}");
}
var paths = Directory.GetFiles(folder, "*.LFL").ToDictionary(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
Console.WriteLine("Content-only identity agrees: " + (KnownGameFiles.IdentifyEdition(paths)?.Label == game.Edition));
Console.WriteLine("Spanish label transform present: " + (ManiacPreviewText.For(game) is not null));
var session = ManiacCutsceneSetup.NewSession(game);
Console.WriteLine($"Reviewed native startup: room={session.CurrentRoomId}; control={session.PlayerHasControl}; cutscene={session.InCutscene}; deliveredInputs={session.DeliveredInput.Count}");
var planner = ManiacCutsceneSetup.Planner(game);
Console.WriteLine($"Own-data scan: {planner.Triggers.Count} triggers; {planner.Groups.Count} groups; {planner.Groups.Count(g => g.IsScene)} scenes");
Console.WriteLine($"Native verbs: 09={planner.Verbs.WordFor(9)}, 05={planner.Verbs.WordFor(5)}, 04={planner.Verbs.WordFor(4)}");
if (GameSupport.FamilyOf(game) != GameFamily.ManiacClassic || session.CurrentRoomId != 44 || !session.PlayerHasControl || planner.Triggers.Count != 59)
    throw new InvalidDataException("Available reference-copy boundary check failed.");
Console.WriteLine("PASS available-copy checks. Alternate classic releases: BLOCKED, no additional local copy identified. Not an original-game comparison.");
