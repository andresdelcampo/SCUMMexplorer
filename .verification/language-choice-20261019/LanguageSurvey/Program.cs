// 19 October 2026: which games under the games root hold several languages, read from their own data (GameFolderScanner.ListLanguages and the containers'
// file lists), never from the folder names of the library. Usage: LanguageSurvey [games root]  (default: SCUMM_GAMES_ROOT, else D:\Juegos\Lucas).
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

var root = args.FirstOrDefault() ?? Environment.GetEnvironmentVariable("SCUMM_GAMES_ROOT") ?? @"D:\Juegos\Lucas";
foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
{
    var name = Path.GetFileName(dir);
    try
    {
        var languages = GameFolderScanner.ListLanguages(dir);
        IGameFileSource? source = DiscImage.FindImageIn(dir) is not null ? new DiscFileSource(dir) : null;
        var containerFiles = source is null ? "" : $" disc files {source.FileNames.Count()}, top folders [{string.Join(", ", source.FileNames.Where(n => n.Contains('/')).Select(n => n[..n.IndexOf('/')]).Distinct(StringComparer.OrdinalIgnoreCase))}]";
        var v5 = ScummV5Archive.FindGames(source ?? new FolderFileSource(dir));
        var game = new GameFolderScanner().Scan(dir);
        Console.WriteLine($"{name}: {game.Title} ({game.Version}{(game.Edition.Length > 0 ? ", " + game.Edition : "")}); v5 candidates [{string.Join(", ", v5.Select(g => $"{g.Stem}:{g.RoomCount}"))}];{containerFiles} -> languages: {(languages.Count == 0 ? "one" : string.Join(", ", languages.Select(l => $"{l.Key}={l.Label}")))}");
    }
    catch (Exception ex) { Console.WriteLine($"{name}: {ex.GetType().Name}: {ex.Message}"); }
}
