using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Rendering;

/// <summary>Tells scenes from the dummy rooms that only carry other resources in their LFL file.</summary>
public static class LoomRoomKinds
{
    /// <summary>
    /// True for a room whose header declares a single 8-pixel strip and no objects (LOOM rooms 82-85:
    /// their LFL files hold 24, 6, 1 and 1 sounds). Nothing loads them as a scene; the sounds stay in the
    /// resource list. Unreadable rooms are not banks.
    /// </summary>
    public static bool IsResourceBank(GameResource room)
    {
        if (room.Kind != ResourceKind.Room) return false;
        try
        {
            using var stream = File.OpenRead(room.FilePath);
            var header = new byte[21];
            if (stream.Read(header, 0, header.Length) < header.Length) return false;
            var width = (header[4] ^ 0xFF) | ((header[5] ^ 0xFF) << 8);
            var objects = header[20] ^ 0xFF;
            return width == 8 && objects == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
