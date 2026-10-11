using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

public sealed record ScummCostumeAnimationSlot(
    int Index,
    ushort LimbMask,
    IReadOnlyList<int> Limbs,
    bool HasData);

public sealed record ScummCostumeSummary(
    int ResourceLength,
    int AnimationIndexLimit,
    byte Format,
    bool Mirrored,
    byte PaletteMappingIndex,
    int AnimationCommandOffset,
    IReadOnlyList<ScummCostumeAnimationSlot> AnimationSlots);

/// <summary>Reads the indexed costume headers and animation limb masks in DOS LOOM's old-v3 bundle.</summary>
public sealed class ScummV3CostumeReader
{
    private const int AnimationDataOffsetsStart = 41;

    public ScummCostumeSummary Decode(ReadOnlySpan<byte> decodedContainer, int resourceOffset)
    {
        if (resourceOffset < 0 || resourceOffset > decodedContainer.Length - 2)
            throw new InvalidDataException("The costume offset is outside its LFL container.");

        var resourceLength = ReadWord(decodedContainer, resourceOffset);
        if (resourceLength > decodedContainer.Length - resourceOffset)
            throw new InvalidDataException("The costume resource length is invalid.");
        if (resourceLength >= 6 && (decodedContainer[resourceOffset + 5] & 0x7F) == 0x57)
            return DecodeClassicV1(decodedContainer.Slice(resourceOffset, resourceLength), resourceLength);
        if (resourceLength < AnimationDataOffsetsStart)
            throw new InvalidDataException("The costume resource length is invalid.");

        var resource = decodedContainer.Slice(resourceOffset, resourceLength);
        var animationIndexLimit = resource[4];
        var formatAndFlags = resource[5];
        var format = (byte)(formatAndFlags & 0x7F);
        if (format != 0x58)
        {
            if (format is 0x57 or 0x59 or 0x60 or 0x61)
                throw new NotSupportedException($"Costume format 0x{format:X2} is not supported by the DOS LOOM inspector.");
            throw new InvalidDataException($"The costume has an invalid format byte 0x{format:X2}.");
        }

        var paletteMappingIndex = resource[6];
        var animationCommandOffset = ReadWord(resource, 7);
        if (animationCommandOffset >= resourceLength)
            throw new InvalidDataException("The costume animation-command offset is outside the resource.");

        var tableLength = checked((animationIndexLimit + 1) * 2);
        if (AnimationDataOffsetsStart + tableLength > resource.Length)
            throw new InvalidDataException("The costume animation-offset table is truncated.");

        var slots = new List<ScummCostumeAnimationSlot>(animationIndexLimit + 1);
        for (var index = 0; index <= animationIndexLimit; index++)
        {
            var relativeOffset = ReadWord(resource, AnimationDataOffsetsStart + index * 2);
            if (relativeOffset == 0)
            {
                slots.Add(new ScummCostumeAnimationSlot(index, 0, [], false));
                continue;
            }

            if (relativeOffset > resource.Length - 2)
                throw new InvalidDataException($"Costume animation slot {index} points outside the resource.");

            var limbMask = ReadWord(resource, relativeOffset);
            var limbs = new List<int>();
            for (var limb = 0; limb < 16; limb++)
                if ((limbMask & (0x8000 >> limb)) != 0)
                    limbs.Add(limb);

            slots.Add(new ScummCostumeAnimationSlot(index, limbMask, limbs, true));
        }

        return new ScummCostumeSummary(resourceLength, animationIndexLimit, format,
            (formatAndFlags & 0x80) != 0, paletteMappingIndex, animationCommandOffset, slots);
    }

    /// <summary>
    /// The classic v1 costume (format 0x57, ScummVM's <c>ClassicCostumeLoader</c> with <c>_baseptr</c> four bytes into the resource): the animation count at byte 4,
    /// the animation commands at byte 4 + the word at 6, one word per limb from byte 8 (frame tables), one word per animation from byte 24; an animation is a
    /// limb mask byte (limb 0 is the high bit) then, per limb, a command index and a duration byte (command index 0xFF: no limb).
    /// </summary>
    private static ScummCostumeSummary DecodeClassicV1(ReadOnlySpan<byte> resource, int resourceLength)
    {
        const int animationStart = 24;
        var animationIndexLimit = resource[4];
        var animationCommandOffset = 4 + ReadWord(resource, 6);
        if (animationCommandOffset >= resourceLength || animationStart + (animationIndexLimit + 1) * 2 > resourceLength)
            throw new InvalidDataException("The classic costume's tables are outside the resource.");
        var slots = new List<ScummCostumeAnimationSlot>(animationIndexLimit + 1);
        for (var index = 0; index <= animationIndexLimit; index++)
        {
            var relative = ReadWord(resource, animationStart + index * 2);
            var offset = 4 + relative;
            if (relative == 0 || offset >= resourceLength)
            {
                slots.Add(new ScummCostumeAnimationSlot(index, 0, [], false));
                continue;
            }
            var limbMask = (ushort)(resource[offset] << 8);
            var limbs = new List<int>();
            for (var limb = 0; limb < 8; limb++)
                if ((limbMask & (0x8000 >> limb)) != 0) limbs.Add(limb);
            slots.Add(new ScummCostumeAnimationSlot(index, limbMask, limbs, true));
        }
        return new ScummCostumeSummary(resourceLength, animationIndexLimit, 0x57, (resource[5] & 0x80) != 0, 0, animationCommandOffset, slots);
    }

    private static ushort ReadWord(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
}
