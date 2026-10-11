using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Decodes the room backgrounds of the classic SCUMM v1 games (Maniac Mansion, Zak McKracken; ScummVM's <c>GdiV1</c>), XOR 0xFF. A room is a grid of 8x8 tiles
/// (header bytes 4 and 5: tiles across and down). Four run-length streams (word offsets at 10, 12, 14, 16) give 256 character patterns of eight rows of four 2-bit
/// pixels each (every pixel drawn twice as wide), the character of each tile, a colour for each tile and a mask character per tile (not read). A pixel is one of three
/// room colours (header bytes 6 to 8), the fourth being the tile's own (its colour byte, low three bits); the number is a C64 colour that the game's table
/// (<c>v1ColorMaps</c>) turns into an EGA colour.
/// </summary>
public sealed class ScummV1RoomDecoder(ScummV1Game game)
{
    private static readonly byte[] ManiacMansionColors = [0x00, 0x0F, 0x04, 0x03, 0x05, 0x02, 0x01, 0x0E, 0x0C, 0x06, 0x0C, 0x08, 0x07, 0x0A, 0x09, 0x08];
    private static readonly byte[] ZakColors = [0x00, 0x0F, 0x04, 0x03, 0x05, 0x02, 0x01, 0x0E, 0x0C, 0x06, 0x0D, 0x08, 0x07, 0x0A, 0x09, 0x07];

    public RoomImage Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length < 22) throw new InvalidDataException("The v1 room is too short.");
        var data = encoded.ToArray();
        for (var i = 0; i < data.Length; i++) data[i] ^= 0xFF;

        var tilesAcross = (int)data[4];
        var tilesDown = (int)data[5];
        if (tilesAcross is < 1 or > 160 || tilesDown is < 1 or > 24) throw new InvalidDataException("Invalid v1 room dimensions.");
        var tileCount = tilesAcross * tilesDown;
        var characters = DecodeStream(data, Word(data, 10), 2048);
        var picture = DecodeStream(data, Word(data, 12), tileCount);
        var tileColors = DecodeStream(data, Word(data, 14), tileCount);

        var map = game == ScummV1Game.ZakMcKracken ? ZakColors : ManiacMansionColors;
        var width = tilesAcross * 8;
        var height = tilesDown * 8;
        var pixels = new byte[width * height];
        var colors = new int[4] { data[6], data[7], data[8], 0 };
        for (var tx = 0; tx < tilesAcross; tx++)
        {
            for (var ty = 0; ty < tilesDown; ty++)
            {
                colors[3] = tileColors[ty + tx * tilesDown] & 7;
                var character = picture[ty + tx * tilesDown] * 8;
                for (var row = 0; row < 8; row++)
                {
                    var bits = characters[character + row];
                    for (var pair = 0; pair < 4; pair++)
                    {
                        var shade = map[colors[(bits >> (6 - 2 * pair)) & 3] & 15];
                        var at = (ty * 8 + row) * width + tx * 8 + pair * 2;
                        pixels[at] = pixels[at + 1] = shade;
                    }
                }
            }
        }
        return new RoomImage(width, height, pixels, ScummV3EgaRoomDecoder.EgaPalette);
    }

    /// <summary>
    /// ScummVM's <c>decodeV1Gfx</c>: four common bytes, then codes: bit 7 a run (1-32) of one of the common bytes (bits 5 and 6), bit 6 a run (1-64) of the next byte,
    /// otherwise 1-64 literal bytes.
    /// </summary>
    private static byte[] DecodeStream(byte[] data, int start, int size)
    {
        if (start < 0 || start + 4 > data.Length) throw new InvalidDataException("A v1 room stream starts outside the room.");
        var output = new byte[size];
        var pos = start;
        var common = data.AsSpan(pos, 4).ToArray();
        pos += 4;
        byte Next() => pos < data.Length ? data[pos++] : throw new InvalidDataException("A v1 room stream is truncated.");
        var x = 0;
        while (x < size)
        {
            var run = Next();
            if ((run & 0x80) != 0)
            {
                var color = common[(run >> 5) & 3];
                for (var z = 0; z <= (run & 0x1F) && x < size; z++) output[x++] = color;
            }
            else if ((run & 0x40) != 0)
            {
                var count = run & 0x3F;
                var color = Next();
                for (var z = 0; z <= count && x < size; z++) output[x++] = color;
            }
            else
            {
                for (var z = 0; z <= run && x < size; z++) output[x++] = Next();
            }
        }
        return output;
    }

    private static int Word(byte[] data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
}
