using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Formats;

public sealed record LoomCostumeAnimationSlot(
    int Index,
    ushort LimbMask,
    IReadOnlyList<int> Limbs,
    bool HasData);

public sealed record LoomCostumeSummary(
    int ResourceLength,
    int AnimationIndexLimit,
    byte Format,
    bool Mirrored,
    byte PaletteMappingIndex,
    int AnimationCommandOffset,
    IReadOnlyList<LoomCostumeAnimationSlot> AnimationSlots);

/// <summary>Reads the indexed costume headers and animation limb masks in DOS LOOM's old-v3 bundle.</summary>
public sealed class LoomCostumeReader
{
    private const int AnimationDataOffsetsStart = 41;

    public LoomCostumeSummary Decode(ReadOnlySpan<byte> decodedContainer, int resourceOffset)
    {
        if (resourceOffset < 0 || resourceOffset > decodedContainer.Length - 2)
            throw new InvalidDataException("The costume offset is outside its LFL container.");

        var resourceLength = ReadWord(decodedContainer, resourceOffset);
        if (resourceLength < AnimationDataOffsetsStart || resourceLength > decodedContainer.Length - resourceOffset)
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

        var slots = new List<LoomCostumeAnimationSlot>(animationIndexLimit + 1);
        for (var index = 0; index <= animationIndexLimit; index++)
        {
            var relativeOffset = ReadWord(resource, AnimationDataOffsetsStart + index * 2);
            if (relativeOffset == 0)
            {
                slots.Add(new LoomCostumeAnimationSlot(index, 0, [], false));
                continue;
            }

            if (relativeOffset > resource.Length - 2)
                throw new InvalidDataException($"Costume animation slot {index} points outside the resource.");

            var limbMask = ReadWord(resource, relativeOffset);
            var limbs = new List<int>();
            for (var limb = 0; limb < 16; limb++)
                if ((limbMask & (0x8000 >> limb)) != 0)
                    limbs.Add(limb);

            slots.Add(new LoomCostumeAnimationSlot(index, limbMask, limbs, true));
        }

        return new LoomCostumeSummary(resourceLength, animationIndexLimit, format,
            (formatAndFlags & 0x80) != 0, paletteMappingIndex, animationCommandOffset, slots);
    }

    private static ushort ReadWord(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
}
