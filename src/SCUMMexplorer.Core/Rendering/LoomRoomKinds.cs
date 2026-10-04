using System.Collections.Concurrent;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Rendering;

/// <summary>Tells scenes from the dummy rooms that only carry other resources in their LFL file.</summary>
public static class LoomRoomKinds
{
    private static readonly ConcurrentDictionary<(string Path, long Length, DateTime Written), bool> BlankRooms = new();

    /// <summary>
    /// True for a room that is no scene, only a carrier of other resources: its header declares a single 8-pixel
    /// strip and no objects (LOOM rooms 82-85: their LFL files hold 24, 6, 1 and 1 sounds), or it has no objects and
    /// its background is one flat colour and <paramref name="flatBackgroundCounts"/> is set (used for the demos, whose
    /// rooms 72-84 are black screens that hold sounds, costumes and scripts; the full game's flat Room 61 stays a
    /// scene, as decided earlier). Nothing loads them as a scene; their other resources stay in the resource list.
    /// Unreadable rooms are not banks.
    /// </summary>
    public static bool IsResourceBank(GameResource room) => IsResourceBank(room, flatBackgroundCounts: false);

    public static bool IsResourceBank(GameResource room, bool flatBackgroundCounts)
    {
        if (room.Kind != ResourceKind.Room) return false;
        try
        {
            var info = new FileInfo(room.FilePath);
            using var stream = info.OpenRead();
            var header = new byte[21];
            if (stream.Read(header, 0, header.Length) < header.Length) return false;
            var width = (header[4] ^ 0xFF) | ((header[5] ^ 0xFF) << 8);
            var objects = header[20] ^ 0xFF;
            if (objects != 0) return false;
            if (width == 8) return true;
            return flatBackgroundCounts && BlankRooms.GetOrAdd((info.FullName, info.Length, info.LastWriteTimeUtc), _ => HasFlatBackground(info.FullName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static bool HasFlatBackground(string path)
    {
        try
        {
            var pixels = new OldV3EgaRoomDecoder().Decode(File.ReadAllBytes(path)).Pixels.Span;
            if (pixels.Length == 0) return false;
            var first = pixels[0];
            foreach (var pixel in pixels)
                if (pixel != first) return false;
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException) { return false; }
    }
}
