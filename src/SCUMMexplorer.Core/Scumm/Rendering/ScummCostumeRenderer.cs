using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>Renders a stored, static DOS LOOM costume pose without running game scripts.</summary>
public sealed class ScummCostumeRenderer
{
    private const int FrameOffsetsStart = 9;
    private const int AnimationDataOffsetsStart = 41;
    private const byte OpaqueBlack = 16;
    private const int MaximumCelDimension = 2048;
    private const int MaximumImagePixels = 4_194_304;

    private static readonly uint[] EgaPalette =
    [
        0x00000000, 0xFF0000AA, 0xFF00AA00, 0xFF00AAAA,
        0xFFAA0000, 0xFFAA00AA, 0xFFAA5500, 0xFFAAAAAA,
        0xFF555555, 0xFF5555FF, 0xFF55FF55, 0xFF55FFFF,
        0xFFFF5555, 0xFFFF55FF, 0xFFFFFF55, 0xFFFFFFFF,
        0xFF000000
    ];

    public RoomImage? DecodePose(ReadOnlySpan<byte> decodedContainer, int resourceOffset, int animationIndex)
    {
        if (resourceOffset < 0 || resourceOffset > decodedContainer.Length - 2)
            throw new InvalidDataException("The costume offset is outside its LFL container.");

        var resourceLength = ReadWord(decodedContainer, resourceOffset);
        if (resourceLength < AnimationDataOffsetsStart || resourceLength > decodedContainer.Length - resourceOffset)
            throw new InvalidDataException("The costume resource length is invalid.");

        var resource = decodedContainer.Slice(resourceOffset, resourceLength);
        var format = (byte)(resource[5] & 0x7F);
        if (format != 0x58)
        {
            if (format is 0x57 or 0x59 or 0x60 or 0x61)
                throw new NotSupportedException($"Costume format 0x{format:X2} is not supported by the DOS LOOM renderer.");
            throw new InvalidDataException($"The costume has an invalid format byte 0x{format:X2}.");
        }

        var animationLimit = resource[4];
        if (animationIndex < 0 || animationIndex > animationLimit)
            throw new InvalidDataException("The selected pose is outside this costume.");

        var animationTableEnd = AnimationDataOffsetsStart + (animationLimit + 1) * 2;
        if (animationTableEnd > resource.Length)
            throw new InvalidDataException("The costume pose table is incomplete.");

        var animationCommands = ReadWord(resource, 7);
        if (animationCommands >= resource.Length)
            throw new InvalidDataException("The costume image data is incomplete.");

        var poseOffset = ReadWord(resource, AnimationDataOffsetsStart + animationIndex * 2);
        if (poseOffset == 0) return null;
        EnsureRange(resource, poseOffset, 2, "The costume pose data is incomplete.");

        var mask = ReadWord(resource, poseOffset);
        var cursor = poseOffset + 2;
        var xMove = -72;
        var yMove = -100;
        var drawToRight = animationIndex % 4 != 0 || (resource[5] & 0x80) != 0;
        var cels = new List<DecodedCel>();

        for (var limb = 0; limb < 16; limb++)
        {
            if ((mask & (0x8000 >> limb)) == 0) continue;

            EnsureRange(resource, cursor, 1, "The costume pose data is incomplete.");
            var commandIndex = resource[cursor++];
            if (commandIndex == 0xFF) continue;

            EnsureRange(resource, cursor, 1, "The costume pose data is incomplete.");
            cursor++; // The pose command's duration and mirror flags belong to game playback.

            EnsureRange(resource, animationCommands + commandIndex, 1, "The costume animation commands are incomplete.");
            var command = resource[animationCommands + commandIndex];
            if (command is 0x79 or 0x7A) continue;

            var celCode = command & 0x7F;
            if (celCode == 0x7B) continue;

            var frameTable = ReadWord(resource, FrameOffsetsStart + limb * 2);
            EnsureRange(resource, frameTable + celCode * 2, 2, "The costume frame table is incomplete.");
            var celOffset = ReadWord(resource, frameTable + celCode * 2);
            EnsureRange(resource, celOffset, 12, "The costume image data is incomplete.");

            var width = ReadWord(resource, celOffset);
            var height = ReadWord(resource, celOffset + 2);
            if (width == 0 || height == 0)
            {
                xMove += ReadSignedWord(resource, celOffset + 8);
                yMove -= ReadSignedWord(resource, celOffset + 10);
                continue;
            }
            if (width > MaximumCelDimension || height > MaximumCelDimension || (long)width * height > MaximumImagePixels)
                throw new InvalidDataException("The costume contains an image that is too large to display.");

            var relativeX = checked(xMove + ReadSignedWord(resource, celOffset + 4));
            var left = drawToRight ? relativeX : checked(-relativeX - width);
            var top = checked(yMove + ReadSignedWord(resource, celOffset + 6));
            var pixels = DecodeCel(resource, celOffset + 12, width, height);
            cels.Add(new DecodedCel(left, top, width, height, pixels));

            xMove = checked(xMove + ReadSignedWord(resource, celOffset + 8));
            yMove = checked(yMove - ReadSignedWord(resource, celOffset + 10));
        }

        if (cels.Count == 0) return null;

        var minX = cels.Min(cel => cel.Left);
        var minY = cels.Min(cel => cel.Top);
        var maxX = cels.Max(cel => checked(cel.Left + cel.Width));
        var maxY = cels.Max(cel => checked(cel.Top + cel.Height));
        var canvasWidth = maxX - minX;
        var canvasHeight = maxY - minY;
        if (canvasWidth <= 0 || canvasHeight <= 0 || canvasWidth > MaximumCelDimension || canvasHeight > MaximumCelDimension ||
            (long)canvasWidth * canvasHeight > MaximumImagePixels)
            throw new InvalidDataException("The costume image bounds are invalid.");

        var canvas = new byte[canvasWidth * canvasHeight];
        var hasVisiblePixels = false;
        foreach (var cel in cels)
        {
            for (var x = 0; x < cel.Width; x++)
            for (var y = 0; y < cel.Height; y++)
            {
                var sourceX = drawToRight ? x : cel.Width - 1 - x;
                var color = cel.Pixels[y * cel.Width + sourceX];
                if (color == 0) continue;
                // Old-bundle costumes use their header byte as the pixel color
                // that maps to palette entry zero. Entry zero is black in-game;
                // it is not the RLE transparency value (which is always color 0).
                var mappedColor = color == resource[6] ? OpaqueBlack : color;
                canvas[(cel.Top - minY + y) * canvasWidth + cel.Left - minX + x] = mappedColor;
                hasVisiblePixels = true;
            }
        }

        return hasVisiblePixels ? new RoomImage(canvasWidth, canvasHeight, canvas, EgaPalette) : null;
    }

    private static byte[] DecodeCel(ReadOnlySpan<byte> resource, int offset, int width, int height)
    {
        var pixelCount = checked(width * height);
        var pixels = new byte[pixelCount];
        var source = offset;
        var written = 0;
        while (written < pixelCount)
        {
            EnsureRange(resource, source, 1, "The costume's compressed image is incomplete.");
            var run = resource[source++];
            var color = (byte)(run >> 4);
            var length = run & 0x0F;
            if (length == 0)
            {
                EnsureRange(resource, source, 1, "The costume's compressed image is incomplete.");
                length = resource[source++];
            }
            if (length == 0 || length > pixelCount - written)
                throw new InvalidDataException("The costume's compressed image has an invalid run.");

            for (var i = 0; i < length; i++, written++)
            {
                if (color == 0) continue;
                var x = written / height;
                var y = written % height;
                pixels[y * width + x] = color;
            }
        }
        return pixels;
    }

    private static ushort ReadWord(ReadOnlySpan<byte> data, int offset)
    {
        EnsureRange(data, offset, 2, "The costume data is incomplete.");
        return BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
    }

    private static short ReadSignedWord(ReadOnlySpan<byte> data, int offset) => unchecked((short)ReadWord(data, offset));

    private static void EnsureRange(ReadOnlySpan<byte> data, int offset, int count, string message)
    {
        if (offset < 0 || count < 0 || offset > data.Length - count)
            throw new InvalidDataException(message);
    }

    private sealed record DecodedCel(int Left, int Top, int Width, int Height, byte[] Pixels);
}
