using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Renders a stored pose of a LOOM CD costume (SCUMM v4/v5 "classic" costume, 16-colour format 0x58) without running scripts.
/// Layout, read from the data: byte 0 the last animation index, 1 the format (bit 7: no mirroring), 2 to 17 the 16-colour palette
/// (cel colour to room palette index; colour 0 is transparent), 18 the animation-command offset, 20 sixteen limb frame-table offsets,
/// 52 the animation (pose) offsets. A pose is a limb mask word, then per limb a command index and a flags byte; a cel has width,
/// height, relative x and y, and move x and y (words), then nibble run-length data in columns. Every stored offset counts from the start of the CO block (its 6-byte header included; ScummVM's _baseptr for small-header v4), so the body index is the offset minus 6. A pose is a limb mask word (little endian, bit 15 first), then per limb a word (0xFFFF: none) and, unless it is 0xFFFF, a length byte.
/// </summary>
public static class ScummV4CostumeRenderer
{
    private const int PaletteStart = 2;

    /// <summary>The colours a costume has: 16 (format 0x58) or 32 (0x59, version 5). The tables after the palette move up by the difference.</summary>
    private static int ColorCount(ReadOnlySpan<byte> body) => (body[1] & 0x7F) == 0x59 ? 32 : 16;
    private static int CommandsOffsetOf(ReadOnlySpan<byte> body) => PaletteStart + ColorCount(body);
    private static int LimbTableOf(ReadOnlySpan<byte> body) => CommandsOffsetOf(body) + 2;
    private static int AnimationTableOf(ReadOnlySpan<byte> body) => LimbTableOf(body) + 32;
    private const int BaseShift = 6;
    private const int MaximumCelDimension = 1024;
    private const int MaximumImagePixels = 1_048_576;

    /// <summary>The highest pose index the costume declares.</summary>
    public static int LastAnimation(ReadOnlySpan<byte> body) => body.Length > 1 ? body[0] : -1;

    public static IEnumerable<int> PosesWithData(ReadOnlySpan<byte> body)
    {
        var poses = new List<int>();
        for (var i = 0; i <= LastAnimation(body); i++)
            if (body.Length > 1 && AnimationTableOf(body) + i * 2 + 2 <= body.Length && ReadWord(body, AnimationTableOf(body) + i * 2) != 0) poses.Add(i);
        return poses;
    }

    /// <summary>
    /// A stored pose. <paramref name="byteCommands"/> is for the older 256-colour games (Indiana Jones, which ScummVM treats as version 3): a limb's command index is one
    /// byte (0xFF: none) instead of a word, and a command code from 0x79 up draws nothing. LOOM CD (version 4) stores words.
    /// </summary>
    public static RoomImage? DecodePose(ReadOnlySpan<byte> body, int animationIndex, IReadOnlyList<uint> roomPalette, bool byteCommands = false)
    {
        if (body.Length < 2 || body.Length < AnimationTableOf(body)) throw new InvalidDataException("The costume is too short.");
        var format = body[1] & 0x7F;
        if (format is not (0x58 or 0x59)) throw new NotSupportedException($"Costume format 0x{format:X2} is not supported.");
        var colors = ColorCount(body);
        if (animationIndex < 0 || animationIndex > body[0]) throw new InvalidDataException("The pose is outside this costume.");
        var poseOffset = ReadWord(body, AnimationTableOf(body) + animationIndex * 2);
        if (poseOffset == 0) return null;
        poseOffset -= BaseShift;
        var commands = ReadWord(body, CommandsOffsetOf(body)) - BaseShift;
        var mask = ReadWord(body, poseOffset);
        var cursor = poseOffset + 2;
        var drawToRight = animationIndex % 4 != 0 || (body[1] & 0x80) != 0;
        int xMove = 0, yMove = 0;
        var cels = new List<(int Left, int Top, int Width, int Height, byte[] Pixels)>();

        for (var limb = 0; limb < 16; limb++)
        {
            if ((mask & (0x8000 >> limb)) == 0) continue;
            int commandIndex;
            if (byteCommands)
            {
                commandIndex = Byte(body, cursor++);
                if (commandIndex == 0xFF) continue;
            }
            else
            {
                commandIndex = ReadWord(body, cursor);
                cursor += 2;
                if (commandIndex == 0xFFFF) continue;
            }
            cursor++; // the length and loop flag belong to playback
            var command = Byte(body, commands + commandIndex);
            if (command is 0x79 or 0x7A) continue;
            var celCode = command & 0x7F;
            if (celCode == 0x7B || byteCommands && celCode >= 0x79) continue;
            var frameTable = ReadWord(body, LimbTableOf(body) + limb * 2) - BaseShift;
            var celOffset = ReadWord(body, frameTable + celCode * 2) - BaseShift;
            var width = ReadWord(body, celOffset);
            var height = ReadWord(body, celOffset + 2);
            if (width == 0 || height == 0)
            {
                xMove += ReadSigned(body, celOffset + 8);
                yMove -= ReadSigned(body, celOffset + 10);
                continue;
            }
            if (width > MaximumCelDimension || height > MaximumCelDimension || width * height > MaximumImagePixels)
                throw new InvalidDataException("The costume contains an image that is too large to display.");
            var relativeX = xMove + ReadSigned(body, celOffset + 4);
            var left = drawToRight ? relativeX : -relativeX - width;
            var top = yMove + ReadSigned(body, celOffset + 6);
            cels.Add((left, top, width, height, DecodeCel(body, celOffset + 12, width, height, colors)));
            xMove += ReadSigned(body, celOffset + 8);
            yMove -= ReadSigned(body, celOffset + 10);
        }
        if (cels.Count == 0) return null;

        var minX = cels.Min(c => c.Left); var minY = cels.Min(c => c.Top);
        var canvasWidth = cels.Max(c => c.Left + c.Width) - minX;
        var canvasHeight = cels.Max(c => c.Top + c.Height) - minY;
        if (canvasWidth <= 0 || canvasHeight <= 0 || canvasWidth > 4096 || canvasHeight > 4096) throw new InvalidDataException("The costume image bounds are invalid.");
        // Index 0 stays transparent; the others become the room palette entry the costume's palette names.
        var palette = new uint[256];
        for (var i = 0; i < 256; i++) palette[i] = i < roomPalette.Count ? roomPalette[i] : 0xFF000000;
        palette[0] = 0;
        var canvas = new byte[canvasWidth * canvasHeight];
        var any = false;
        foreach (var cel in cels)
            for (var x = 0; x < cel.Width; x++)
                for (var y = 0; y < cel.Height; y++)
                {
                    var color = cel.Pixels[y * cel.Width + (drawToRight ? x : cel.Width - 1 - x)];
                    if (color == 0) continue;
                    var index = body[PaletteStart + color];
                    // A palette entry that names room colour 0 (black) would read as transparent: show it as its own opaque black.
                    canvas[(cel.Top - minY + y) * canvasWidth + cel.Left - minX + x] = index == 0 ? (byte)255 : index;
                    any = true;
                }
        if (!any) return null;
        palette[255] = 0xFF000000;
        return new RoomImage(canvasWidth, canvasHeight, canvas, palette);
    }

    private static byte[] DecodeCel(ReadOnlySpan<byte> body, int offset, int width, int height, int colors)
    {
        var count = width * height;
        var pixels = new byte[count];
        var source = offset;
        var written = 0;
        while (written < count)
        {
            var run = Byte(body, source++);
            // 16 colours: a nibble each for colour and count; 32 colours: five bits of colour, three of count (ScummVM's shr 3 and mask 7).
            var color = (byte)(colors == 16 ? run >> 4 : run >> 3);
            var length = colors == 16 ? run & 0x0F : run & 0x07;
            if (length == 0) length = Byte(body, source++);
            if (length == 0 || length > count - written) throw new InvalidDataException("The costume's compressed image has an invalid run.");
            for (var i = 0; i < length; i++, written++)
                if (color != 0) pixels[written % height * width + written / height] = color;
        }
        return pixels;
    }

    private static byte Byte(ReadOnlySpan<byte> data, int offset) =>
        offset >= 0 && offset < data.Length ? data[offset] : throw new InvalidDataException("The costume data is incomplete.");

    private static ushort ReadWord(ReadOnlySpan<byte> data, int offset) =>
        offset >= 0 && offset + 2 <= data.Length ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]) : throw new InvalidDataException("The costume data is incomplete.");

    private static int ReadSigned(ReadOnlySpan<byte> data, int offset) => (short)ReadWord(data, offset);
}
