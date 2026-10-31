using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>
/// The scene derivation of a game played through the shared session (each scene's fresh run and setup plan, ScummCutscenePlanner), kept per game
/// folder and language so later launches start from it instead of deriving again (31 October 2026: the setup search made the first derivation take
/// minutes on the large games). The same stamp as <see cref="CdRoomScenesCache"/> (the Core build and the game folder's files) invalidates it.
/// </summary>
internal static class ScenePlanCache
{
    private static readonly string Root = Environment.GetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR") is { Length: > 0 } custom
        ? custom
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SCUMMexplorer", "room-cache");

    private sealed record Stored(string Stamp, List<ScummCutscenePlanner.SavedScene> Scenes);

    private static string FilePath(string gameFolder, string? language)
    {
        var key = Path.GetFullPath(gameFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant() + "|" + language;
        return Path.Combine(Root, "scene-plans-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16] + ".json");
    }

    private static string Stamp(string gameFolder)
    {
        var core = typeof(GameLibrary).Assembly.Location;
        var folder = File.Exists(gameFolder) ? Path.GetDirectoryName(Path.GetFullPath(gameFolder)) : gameFolder;
        var files = folder is not null && Directory.Exists(folder) ? new DirectoryInfo(folder).GetFiles() : [];
        return $"v1|{(File.Exists(core) ? File.GetLastWriteTimeUtc(core).Ticks : 0)}|" +
               string.Join('|', files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));
    }

    public static IReadOnlyList<ScummCutscenePlanner.SavedScene>? Load(string gameFolder, string? language)
    {
        try
        {
            var path = FilePath(gameFolder, language);
            if (!File.Exists(path)) return null;
            var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(path));
            return stored is not null && stored.Stamp == Stamp(gameFolder) ? stored.Scenes : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException) { return null; }
    }

    public static void Save(string gameFolder, string? language, IReadOnlyList<ScummCutscenePlanner.SavedScene> scenes)
    {
        try
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(FilePath(gameFolder, language), JsonSerializer.Serialize(new Stored(Stamp(gameFolder), scenes.ToList())));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { }
    }
}
