using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Decodes the room backgrounds of the "enhanced" SCUMM v2 DOS games (Maniac Mansion Enhanced): the room file is XOR 0xFF and the picture is one stream for the
/// whole room, not strips (ScummVM's <c>GdiV2</c>): column by column, top to bottom, each code a run of one colour (high nibble count, low nibble colour) or, with
/// bit 7, a run that repeats what the column to the left shows in those rows (a dither); a zero count takes the next byte. The depth masks follow the picture (read by <c>ReadMask</c>).
/// </summary>
public sealed class ScummV2RoomDecoder : IRoomBackgroundDecoder
{
    public bool CanDecode(string filePath, int scummVersion) =>
        scummVersion == 2 && Path.GetExtension(filePath).Equals(".LFL", StringComparison.OrdinalIgnoreCase);

    public async ValueTask<RoomImage> DecodeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Decode(await File.ReadAllBytesAsync(filePath, cancellationToken));
    }

    public RoomImage Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length < 24) throw new InvalidDataException("The v2 room is too short.");
        var data = encoded.ToArray();
        for (var i = 0; i < data.Length; i++) data[i] ^= 0xFF;

        var roomLength = Word(data, 0);
        var width = Word(data, 4);
        var height = Word(data, 6);
        var imageStart = Word(data, 10);
        if (roomLength < 24 || roomLength > data.Length || width is < 8 or > 1280 || height is < 1 or > 128 || width % 8 != 0 ||
            imageStart < 24 || imageStart >= roomLength)
            throw new InvalidDataException("Invalid v2 room header or dimensions.");

        var pixels = new byte[checked(width * height)];
        var left = new byte[height];    // what the column to the left shows, row by row: a dither run repeats it
        var pos = imageStart;
        var run = 1;
        var dither = false;
        byte color = 0;
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                if (--run == 0)
                {
                    if (pos >= roomLength) throw new InvalidDataException("The v2 room picture is truncated.");
                    var code = data[pos++];
                    dither = (code & 0x80) != 0;
                    run = dither ? code & 0x7F : code >> 4;
                    color = (byte)(code & 0x0F);
                    if (run == 0)
                    {
                        if (pos >= roomLength) throw new InvalidDataException("The v2 room picture is truncated.");
                        run = data[pos++];
                    }
                }
                if (!dither) left[y] = color;
                pixels[y * width + x] = left[y];
            }
        }
        return new RoomImage(width, height, pixels, ScummV3EgaRoomDecoder.EgaPalette) { DepthMask = ReadMask(data, pos, roomLength, width, height) };
    }

    /// <summary>
    /// The mask stream that follows the picture (ScummVM <c>generateStripTable</c>): one byte of 8 mask bits (bit 7 = the leftmost pixel) per row of each 8-pixel strip, strip after strip;
    /// a code is a run (bit 7: the next byte repeated, otherwise that many bytes follow), and a run may cross from one strip to the next. Null when the stream is missing or truncated.
    /// </summary>
    private static RoomDepthMask? ReadMask(byte[] data, int pos, int end, int width, int height)
    {
        var pixels = new byte[width * height];
        var strips = width / 8;
        var strip = 0;
        var row = 0;
        while (strip < strips)
        {
            if (pos >= end) return null;
            var code = data[pos++];
            var repeat = (code & 0x80) != 0;
            var count = code & 0x7F;
            if (count == 0) return null;
            byte value = 0;
            if (repeat)
            {
                if (pos >= end) return null;
                value = data[pos++];
            }
            for (var i = 0; i < count && strip < strips; i++)
            {
                if (!repeat)
                {
                    if (pos >= end) return null;
                    value = data[pos++];
                }
                for (var x = 0; x < 8; x++) pixels[row * width + strip * 8 + x] = (byte)((value & (0x80 >> x)) != 0 ? 1 : 0);
                if (++row == height) { row = 0; strip++; }
            }
        }
        return new RoomDepthMask(width, height, pixels);
    }

    private static ushort Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
