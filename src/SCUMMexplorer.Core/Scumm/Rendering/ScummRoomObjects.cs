using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

public sealed record RoomObjectImage(int Id, int X, int Y, int Width, int Height, byte[] Pixels)
{
    /// <summary>
    /// The image's depth mask (z-plane 1), one byte per pixel (0 or 1): ScummVM's <c>getZPlanes</c> finds it at the
    /// image's start plus its first word, and <c>decodeMask</c> writes it over the room's mask under the object's strips
    /// when the object is drawn (EGA strips are never transparent). A strip with offset 0 clears the mask there.
    /// Null when the mask table cannot be read.
    /// </summary>
    public byte[]? Mask { get; init; }
}

/// <summary>Reads placed object images from the supplied DOS EGA old-v3 LOOM rooms.</summary>
public sealed class ScummRoomObjects
{
    public IReadOnlyList<RoomObjectImage> Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length < 29) throw new InvalidDataException("The room is too short.");
        var data = encoded.ToArray();
        for (var i = 0; i < data.Length; i++) data[i] ^= 0xFF;
        var roomEnd = Word(data, 0);
        var roomWidth = Word(data, 4);
        var roomHeight = Word(data, 6);
        var records = new ScummRoomObjectDirectory().DecodePlain(data);

        var objects = new List<RoomObjectImage>();
        foreach (var record in records)
        {
            if (record.ImageOffset is not int image) continue;
            var x = record.X;
            var y = record.Y;
            var width = record.Width;
            var height = record.Height;
            if (width == 0 || height == 0 || width > 1280 || height > 200 || x + width > roomWidth || y + height > roomHeight)
                continue;
            var length = Word(data, image);
            var strips = width / 8;
            if (length < 2 + strips * 2 || image + length > roomEnd ||
                Word(data, image + 2) != 2 + strips * 2) continue;
            try
            {
                objects.Add(new RoomObjectImage(record.Id, x, y, width, height,
                    DecodeImage(data, image, image + length, width, height))
                {
                    Mask = DecodeMask(data, image + length, roomEnd, width, height)
                });
            }
            catch (InvalidDataException)
            {
                // A malformed or unsupported image must not hide the room background.
            }
        }
        return objects;
    }

    private static byte[] DecodeImage(byte[] data, int start, int end, int width, int height)
    {
        var pixels = new byte[width * height];
        var strips = width / 8;
        for (var strip = 0; strip < strips; strip++)
        {
            var offset = Word(data, start + 2 + strip * 2);
            if (offset < 2 + strips * 2 || start + offset >= end)
                throw new InvalidDataException("Invalid object strip offset.");
            var pos = start + offset;
            var x = 0;
            var y = 0;
            while (x < 8)
            {
                if (pos >= end) throw new InvalidDataException("Truncated object strip.");
                var command = data[pos++];
                var copy = (command & 0xC0) == 0x80;
                var alternate = (command & 0xC0) == 0xC0;
                var run = (command & 0x80) != 0 ? command & 0x3F : command >> 4;
                if (alternate && pos >= end) throw new InvalidDataException("Truncated object strip.");
                var pair = alternate ? data[pos++] : (byte)0;
                if (run == 0)
                {
                    if (pos >= end) throw new InvalidDataException("Truncated object strip.");
                    run = data[pos++];
                }
                if (run == 0 || run > width * height) throw new InvalidDataException("Invalid object run.");
                for (var z = 0; z < run; z++)
                {
                    if (x >= 8) throw new InvalidDataException("Object strip overflows.");
                    var px = strip * 8 + x;
                    pixels[y * width + px] = copy
                        ? px > 0 ? pixels[y * width + px - 1] : (byte)0
                        : alternate ? (byte)((z & 1) == 0 ? pair >> 4 : pair & 15)
                        : (byte)(command & 15);
                    if (++y == height) { y = 0; x++; }
                }
            }
        }
        return pixels;
    }

    // ScummVM decompressMaskImg per strip (old bundle: the offsets table starts at the mask's first byte).
    private static byte[]? DecodeMask(byte[] data, int start, int end, int width, int height)
    {
        var strips = width / 8;
        if (start + strips * 2 > end) return null;
        var mask = new byte[width * height];
        for (var strip = 0; strip < strips; strip++)
        {
            var offset = Word(data, start + strip * 2);
            if (offset == 0) continue;
            var pos = start + offset;
            if (offset < strips * 2 || pos >= end) return null;
            var row = 0;
            while (row < height)
            {
                if (pos >= end) return null;
                var command = data[pos++];
                var run = command & 0x7F;
                if (run == 0) run = 256;
                var repeated = (command & 0x80) != 0;
                if (repeated && pos >= end) return null;
                var value = repeated ? data[pos++] : (byte)0;
                for (var index = 0; index < run && row < height; index++, row++)
                {
                    if (!repeated)
                    {
                        if (pos >= end) return null;
                        value = data[pos++];
                    }
                    for (var x = 0; x < 8; x++)
                        mask[row * width + strip * 8 + x] = (byte)((value & (0x80 >> x)) != 0 ? 1 : 0);
                }
            }
        }
        return mask;
    }

    private static ushort Word(byte[] data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
}
