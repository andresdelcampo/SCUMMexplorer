using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.App;

/// <summary>
/// The room picker's thumbnails, kept as PNG files per game folder so reopening the last game does not
/// render them again. Opening a game with the button always rebuilds the cache. A stamp (the Core
/// assembly's build time and the game's <c>00.LFL</c> size and time) invalidates it after a new build
/// or changed game data; a missing room file makes it unusable.
/// </summary>
internal static class RoomThumbnailCache
{
    // SCUMM_ROOM_CACHE_DIR points a harness at a private folder so it cannot touch the app's cache.
    private static readonly string Root = Environment.GetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR") is { Length: > 0 } custom
        ? custom
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SCUMMexplorer", "room-cache");

    private static string Directory(string gameFolder)
    {
        var key = Path.GetFullPath(gameFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
        return Path.Combine(Root, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16]);
    }

    private static string Stamp(string gameFolder)
    {
        var core = typeof(GameLibrary).Assembly.Location;
        var index = new FileInfo(Path.Combine(gameFolder, "00.LFL"));
        var app = typeof(RoomThumbnailCache).Assembly.Location;
        return $"v2|{(File.Exists(core) ? File.GetLastWriteTimeUtc(core).Ticks : 0)}|{File.GetLastWriteTimeUtc(app).Ticks}|" +
               $"{(index.Exists ? index.Length : 0)}|{(index.Exists ? index.LastWriteTimeUtc.Ticks : 0)}";
    }

    private static string RoomFile(string directory, int roomId) => Path.Combine(directory, $"room-{roomId:00}.png");

    /// <summary>Empties the game's cache and stamps it, ready for <see cref="Save"/>.</summary>
    public static void Begin(string gameFolder)
    {
        try
        {
            var directory = Directory(gameFolder);
            if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, recursive: true);
            System.IO.Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "stamp.txt"), Stamp(gameFolder));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public static void Save(string gameFolder, int roomId, BitmapSource bitmap)
    {
        try
        {
            var directory = Directory(gameFolder);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(RoomFile(directory, roomId));
            encoder.Save(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { }
    }

    /// <summary>Every requested room's thumbnail, or null when the cache is missing, stale or incomplete.</summary>
    public static IReadOnlyDictionary<int, BitmapSource>? TryLoad(string gameFolder, IEnumerable<int> roomIds)
    {
        try
        {
            var directory = Directory(gameFolder);
            var stampFile = Path.Combine(directory, "stamp.txt");
            if (!File.Exists(stampFile) || File.ReadAllText(stampFile) != Stamp(gameFolder)) return null;
            var result = new Dictionary<int, BitmapSource>();
            foreach (var id in roomIds)
            {
                var file = RoomFile(directory, id);
                if (!File.Exists(file)) return null;
                var decoder = new PngBitmapDecoder(new Uri(file), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                frame.Freeze();
                result[id] = frame;
            }
            return result.Count == 0 ? null : result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or
                                       FileFormatException or ArgumentException or UriFormatException) { return null; }
    }
}
