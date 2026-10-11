using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The objects of a room of the classic v1 and the enhanced v2 games (Maniac Mansion, Zak McKracken; XOR 0xFF files). The room header's byte 20 counts the objects;
/// from byte 28 come a word offset per object to its image (0 or past the first object code: none), then a word offset to its code (ScummVM's
/// <c>loadRoomObjects</c>). The code holds the global object number (word at +4), the position (byte at +7 and the low seven bits of +8, in units of 8 pixels), the
/// width (+9, units of 8) and the height (+13, multiple of 8). The image has the room picture's own format at the object's size: v2 the column-by-column run stream,
/// v1 a grid of tile characters (row by row), then the colour of each tile, both drawn with the room's character patterns and colours.
/// ScummVM draws the objects from the last to the second (<c>drawRoomObjects</c>: the first is never drawn), and in these versions only those whose state, the high
/// nibble of the object's byte in the index, has bit 3 set (<c>kObjectState_08</c>).
/// </summary>
public static class ScummOldRoomObjects
{
    /// <summary>
    /// An object of a room: the global number, the name stored in its code (plain characters; a Spanish accent is stored as a code of its own), its rectangle, where its image
    /// is (0: none) and whether it is drawn at the start of the game.
    /// </summary>
    public sealed record Entry(int Id, string Name, int X, int Y, int Width, int Height, int ImageOffset, bool Visible);

    private const int StateVisible = 8;

    /// <summary>
    /// The state of every global object at the start of the game: the high nibble of each object's byte in <c>00.LFL</c>. Null when the index cannot be read.
    /// </summary>
    public static byte[]? ReadInitialStates(string folderPath, ScummVersion version, ScummV1Game? v1Game)
    {
        try
        {
            var indexPath = Directory.EnumerateFiles(folderPath, "00.LFL").FirstOrDefault();
            if (indexPath is null) return null;
            var data = LflEncoding.DecodeXorFF(File.ReadAllBytes(indexPath));
            int count;
            int first;
            if (version == ScummVersion.V1)
            {
                count = v1Game == ScummV1Game.ZakMcKracken ? 775 : 800;
                first = 2;
            }
            else
            {
                if (data.Length < 4) return null;
                count = data[2] | data[3] << 8;
                first = 4;
            }
            if (first + count > data.Length) return null;
            var states = new byte[count];
            for (var i = 0; i < count; i++) states[i] = (byte)(data[first + i] >> 4);
            return states;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>The room's objects in directory order (the first is never drawn); empty when the room has none. <paramref name="decoded"/> is the room file after XOR 0xFF.</summary>
    public static IReadOnlyList<Entry> Read(byte[] decoded, byte[]? states)
    {
        if (decoded.Length < 30) return [];
        var count = decoded[20];
        if (count < 2 || 28 + 4 * count > decoded.Length) return [];
        var codes = new int[count];
        for (var i = 0; i < count; i++) codes[i] = Word(decoded, 28 + 2 * count + 2 * i);
        var firstCode = codes.Where(c => c > 0).DefaultIfEmpty(decoded.Length).Min();
        var entries = new List<Entry>();
        for (var i = 0; i < count; i++)
        {
            var code = codes[i];
            if (code == 0 || code + 15 > decoded.Length) continue;
            var image = Word(decoded, 28 + 2 * i);
            var id = Word(decoded, code + 4);
            var nameStart = code + decoded[code + 14];
            var name = "";
            if (nameStart < decoded.Length)
            {
                var nameEnd = Array.IndexOf(decoded, (byte)0, nameStart);
                if (nameEnd > nameStart) name = System.Text.Encoding.Latin1.GetString(decoded, nameStart, Math.Min(nameEnd - nameStart, 40));
            }
            var hasImage = image != 0 && image < firstCode;
            entries.Add(new Entry(id, name, decoded[code + 7] * 8, (decoded[code + 8] & 0x7F) * 8, decoded[code + 9] * 8, decoded[code + 13] & 0xF8,
                hasImage ? image : 0, i >= 1 && hasImage && states is not null && id < states.Length && (states[id] & StateVisible) != 0));
        }
        return entries;
    }

    /// <summary>
    /// The room picture with the objects that are visible at the start of the game painted over it. <paramref name="states"/> are the initial object states
    /// (<see cref="ReadInitialStates"/>); without them the room is returned as it is.
    /// </summary>
    public static RoomImage ComposeVisible(RoomImage room, ReadOnlySpan<byte> encoded, ScummV1Game? v1Game, byte[]? states)
    {
        if (states is null || encoded.Length < 30) return room;
        var data = encoded.ToArray();
        for (var i = 0; i < data.Length; i++) data[i] ^= 0xFF;
        var pixels = room.Pixels.ToArray();
        byte[]? characters = null;
        var entries = Read(data, states);
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (!entry.Visible || entry.Width == 0 || entry.Height == 0 || entry.X + entry.Width > room.Width || entry.Y + entry.Height > room.Height) continue;
            try
            {
                if (v1Game is { } v1) PaintV1(data, entry.ImageOffset, entry.X, entry.Y, entry.Width, entry.Height, v1, pixels, room.Width, ref characters);
                else PaintV2(data, entry.ImageOffset, entry.X, entry.Y, entry.Width, entry.Height, pixels, room.Width);
            }
            catch (InvalidDataException)
            {
                // A malformed image must not hide the room.
            }
        }
        return room with { Pixels = pixels };
    }

    private static void PaintV2(byte[] data, int pos, int x, int y, int width, int height, byte[] canvas, int canvasWidth)
    {
        var run = 1;
        var dither = false;
        byte color = 0;
        for (var column = 0; column < width; column++)
        {
            for (var row = 0; row < height; row++)
            {
                if (--run == 0)
                {
                    if (pos >= data.Length) throw new InvalidDataException("The object image is truncated.");
                    var code = data[pos++];
                    dither = (code & 0x80) != 0;
                    run = dither ? code & 0x7F : code >> 4;
                    color = (byte)(code & 0x0F);
                    if (run == 0)
                    {
                        if (pos >= data.Length) throw new InvalidDataException("The object image is truncated.");
                        run = data[pos++];
                    }
                }
                var at = (y + row) * canvasWidth + x + column;
                // A dither run repeats what the column to the left shows (for the first column, the room behind the object).
                if (dither) { if (x + column > 0) canvas[at] = canvas[at - 1]; }
                else canvas[at] = color;
            }
        }
    }

    private static void PaintV1(byte[] data, int image, int x, int y, int width, int height, ScummV1Game game, byte[] canvas, int canvasWidth, ref byte[]? characters)
    {
        characters ??= ScummV1RoomDecoder.Characters(data);
        var across = width / 8;
        var count = across * (height / 8);
        var pos = image + 4;
        if (image + 4 > data.Length) throw new InvalidDataException("The object image starts outside the room.");
        var common = data.AsSpan(image, 4).ToArray();
        var picture = ScummV1RoomDecoder.DecodeCodes(data, ref pos, common, count);
        var tileColors = ScummV1RoomDecoder.DecodeCodes(data, ref pos, common, count);
        var map = ScummV1RoomDecoder.EgaColors(game);
        var colors = new int[4] { data[6], data[7], data[8], 0 };
        for (var tile = 0; tile < count; tile++)
        {
            colors[3] = tileColors[tile] & 7;
            var left = x + tile % across * 8;
            var top = y + tile / across * 8;
            for (var row = 0; row < 8; row++)
            {
                var bits = characters[picture[tile] * 8 + row];
                for (var pair = 0; pair < 4; pair++)
                {
                    var shade = map[colors[(bits >> (6 - 2 * pair)) & 3] & 15];
                    var at = (top + row) * canvasWidth + left + pair * 2;
                    canvas[at] = canvas[at + 1] = shade;
                }
            }
        }
    }

    private static int Word(byte[] data, int offset) => data[offset] | data[offset + 1] << 8;
}
