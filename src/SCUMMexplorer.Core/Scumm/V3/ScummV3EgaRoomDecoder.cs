using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>Decodes old-v3 DOS EGA room backgrounds verified with LOOM and Indiana Jones EGA.</summary>
public class ScummV3EgaRoomDecoder : IRoomBackgroundDecoder
{
    // Standard 16-colour DOS EGA palette, stored as 0xAARRGGBB.
    private static readonly uint[] EgaPalette =
    [
        0xFF000000, 0xFF0000AA, 0xFF00AA00, 0xFF00AAAA,
        0xFFAA0000, 0xFFAA00AA, 0xFFAA5500, 0xFFAAAAAA,
        0xFF555555, 0xFF5555FF, 0xFF55FF55, 0xFF55FFFF,
        0xFFFF5555, 0xFFFF55FF, 0xFFFFFF55, 0xFFFFFFFF
    ];

    public bool CanDecode(string filePath, int scummVersion) =>
        scummVersion == 3 && Path.GetExtension(filePath).Equals(".LFL", StringComparison.OrdinalIgnoreCase);

    public async ValueTask<RoomImage> DecodeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        return Decode(bytes);
    }

    public RoomImage Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length < 24)
            throw new InvalidDataException("The old-v3 EGA room is too short.");

        var data = encoded.ToArray();
        for (var i = 0; i < data.Length; i++) data[i] ^= 0xFF;

        var roomLength = Word(data, 0);
        var width = Word(data, 4);
        var height = Word(data, 6);
        var imageStart = Word(data, 10);
        if (roomLength < 24 || roomLength > data.Length || width is < 8 or > 1280 ||
            height is < 1 or > 200 || width % 8 != 0 || imageStart < 24 || imageStart + 2 > roomLength)
            throw new InvalidDataException("Invalid old-v3 EGA room header or dimensions.");

        var imageLength = Word(data, imageStart);
        var imageEnd = imageStart + imageLength;
        var stripCount = width / 8;
        if (imageLength < 2 + stripCount * 2 || imageEnd > roomLength)
            throw new InvalidDataException("The room background extends beyond its resource.");

        var pixels = new byte[checked(width * height)];
        for (var strip = 0; strip < stripCount; strip++)
        {
            var relativeOffset = Word(data, imageStart + 2 + strip * 2);
            var pos = imageStart + relativeOffset;
            if (relativeOffset < 2 + stripCount * 2 || pos >= imageEnd)
                throw new InvalidDataException($"Invalid background strip {strip} offset.");

            var x = 0;
            var y = 0;
            while (x < 8)
            {
                if (pos >= imageEnd) throw new InvalidDataException($"Background strip {strip} is truncated.");
                var command = data[pos++];
                var copyPrevious = (command & 0xC0) == 0x80;
                var alternating = (command & 0xC0) == 0xC0;
                var run = (command & 0x80) != 0 ? command & 0x3F : command >> 4;
                if (copyPrevious || alternating) run = command & 0x3F;
                if (alternating && pos >= imageEnd)
                    throw new InvalidDataException($"Background strip {strip} is truncated.");
                var pair = alternating ? data[pos++] : (byte)0;
                if (run == 0)
                {
                    if (pos >= imageEnd) throw new InvalidDataException($"Background strip {strip} is truncated.");
                    run = data[pos++];
                }
                if (run == 0 || run > 8 * height - (x * height + y))
                    throw new InvalidDataException($"Invalid run in background strip {strip}.");

                for (var z = 0; z < run; z++)
                {
                    var absoluteX = strip * 8 + x;
                    pixels[y * width + absoluteX] = copyPrevious
                        ? absoluteX > 0 ? pixels[y * width + absoluteX - 1] : (byte)0
                        : alternating ? (byte)((z & 1) == 0 ? pair >> 4 : pair & 0x0F)
                        : (byte)(command & 0x0F);
                    if (++y == height) { y = 0; x++; }
                }
            }
        }

        var depthMask = DecodeDepthMask(data, imageEnd, roomLength, width, height);
        return new RoomImage(width, height, pixels, EgaPalette) { DepthMask = depthMask };
    }

    private static RoomDepthMask? DecodeDepthMask(byte[] data, int maskStart, int roomLength, int width, int height)
    {
        var stripCount = width / 8;
        var tableLength = stripCount * 2;
        var maskEnd = FindNextImageOffset(data, roomLength, maskStart);

        // Older/synthetic resources can end at the color strips without carrying a mask table.
        if (maskStart + tableLength > maskEnd) return null;

        var pixels = new byte[checked(width * height)];
        for (var strip = 0; strip < stripCount; strip++)
        {
            var relativeOffset = Word(data, maskStart + strip * 2);
            if (relativeOffset == 0) continue;
            if (relativeOffset < tableLength || maskStart + relativeOffset >= maskEnd)
                throw new InvalidDataException($"Invalid room depth-mask strip {strip} offset.");

            var pos = maskStart + relativeOffset;
            var row = 0;
            while (row < height)
            {
                if (pos >= maskEnd) throw new InvalidDataException($"Room depth-mask strip {strip} is truncated.");
                var command = data[pos++];
                var repeated = (command & 0x80) != 0;
                var run = command & 0x7F;
                // A zero count wraps to 256 in the original byte-sized SCUMM loop.
                if (run == 0) run = 256;

                if (repeated)
                {
                    if (pos >= maskEnd) throw new InvalidDataException($"Room depth-mask strip {strip} is truncated.");
                    var value = data[pos++];
                    var rows = Math.Min(run, height - row);
                    for (var index = 0; index < rows; index++)
                        WriteMaskRow(pixels, width, strip, row++, value);
                }
                else
                {
                    var rows = Math.Min(run, height - row);
                    if (pos + rows > maskEnd)
                        throw new InvalidDataException($"Room depth-mask strip {strip} is truncated.");
                    for (var index = 0; index < rows; index++)
                        WriteMaskRow(pixels, width, strip, row++, data[pos++]);
                }
            }
        }

        return new RoomDepthMask(width, height, pixels);
    }

    private static int FindNextImageOffset(byte[] data, int roomLength, int maskStart)
    {
        var objectCount = data[20];
        var imageOffsetsEnd = 29 + objectCount * 2;
        if (imageOffsetsEnd > roomLength) return roomLength;

        var nextImage = roomLength;
        for (var index = 0; index < objectCount; index++)
        {
            var offset = Word(data, 29 + index * 2);
            if (offset >= maskStart && offset < nextImage) nextImage = offset;
        }
        return nextImage;
    }

    private static void WriteMaskRow(byte[] pixels, int width, int strip, int row, byte packedBits)
    {
        var start = row * width + strip * 8;
        for (var x = 0; x < 8; x++)
            pixels[start + x] = (byte)((packedBits & (0x80 >> x)) != 0 ? 1 : 0);
    }

    private static ushort Word(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
}
