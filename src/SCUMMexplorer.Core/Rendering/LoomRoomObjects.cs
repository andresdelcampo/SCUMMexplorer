using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Rendering;

public sealed record RoomObjectImage(int Id, int X, int Y, int Width, int Height, byte[] Pixels);

/// <summary>Reads placed object images from the supplied DOS EGA old-v3 LOOM rooms.</summary>
public sealed class LoomRoomObjects
{
    public IReadOnlyList<RoomObjectImage> Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length < 29) throw new InvalidDataException("The room is too short.");
        var data = encoded.ToArray();
        for (var i = 0; i < data.Length; i++) data[i] ^= 0xFF;
        var roomEnd = Word(data, 0);
        var roomWidth = Word(data, 4);
        var roomHeight = Word(data, 6);
        var records = new LoomRoomObjectDirectory().DecodePlain(data);

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
                    DecodeImage(data, image, image + length, width, height)));
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

    private static ushort Word(byte[] data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
}
