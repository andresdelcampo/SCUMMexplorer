using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.App;

/// <summary>
/// The rooms each CD scene plays out in (derived by headless runs, which take many seconds), kept per game folder so the room's
/// "Play Cutscene" button is there at once on later launches. A stamp (the Core assembly's build time and the game folder's
/// file sizes and times) invalidates it after a new build or changed game data.
/// </summary>
internal static class CdRoomScenesCache
{
    // SCUMM_ROOM_CACHE_DIR points a harness at a private folder so it cannot touch the app's cache.
    private static readonly string Root = Environment.GetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR") is { Length: > 0 } custom
        ? custom
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SCUMMexplorer", "room-cache");

    private sealed record Stored(string Stamp, Dictionary<string, int[]> Lists);

    private static string FilePath(string gameFolder)
    {
        var key = Path.GetFullPath(gameFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
        return Path.Combine(Root, "cd-room-scenes-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16] + ".json");
    }

    private static string Stamp(string gameFolder)
    {
        var core = typeof(GameLibrary).Assembly.Location;
        // A disc image is opened through its .cue file: the stamp then covers the files beside it (the .bin).
        var folder = File.Exists(gameFolder) ? Path.GetDirectoryName(Path.GetFullPath(gameFolder)) : gameFolder;
        var files = folder is not null && System.IO.Directory.Exists(folder) ? new DirectoryInfo(folder).GetFiles() : [];
        return $"v1|{(File.Exists(core) ? File.GetLastWriteTimeUtc(core).Ticks : 0)}|" +
               string.Join('|', files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));
    }

    /// <summary>The saved room lists, or null when there are none for this build and game data.</summary>
    public static Dictionary<string, int[]>? Load(string gameFolder)
    {
        try
        {
            var path = FilePath(gameFolder);
            if (!File.Exists(path)) return null;
            var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(path));
            return stored is not null && stored.Stamp == Stamp(gameFolder) ? stored.Lists : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    public static void Save(string gameFolder, Dictionary<string, int[]> lists)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Root);
            File.WriteAllText(FilePath(gameFolder), JsonSerializer.Serialize(new Stored(Stamp(gameFolder), lists)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
