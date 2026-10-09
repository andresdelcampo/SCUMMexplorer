using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Reads the old-v3 00.LFL object table: per object three bytes of class bits
/// (little endian) and one byte packing state (high nibble) and owner (low
/// nibble), as ScummVM's small-header <c>readIndexFile</c> reads them.
/// </summary>
public sealed class LflObjectStateReader
{
    public IReadOnlyDictionary<int, byte> Read(string indexFilePath) => ReadSnapshot(indexFilePath).States;

    public ScummIndexedObjectSnapshot ReadSnapshot(string indexFilePath)
    {
        var data = LflEncoding.Read(indexFilePath);
        if (data.Length < 4 || BinaryPrimitives.ReadUInt16LittleEndian(data) != 0x0100)
            throw new InvalidDataException("Invalid old-v3 object-state index.");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2));
        if (data.Length < 4 + count * 4)
            throw new InvalidDataException("Truncated old-v3 object-state table.");

        var states = new Dictionary<int, byte>(count);
        var owners = new Dictionary<int, byte>(count);
        var classes = new Dictionary<int, uint>(count);
        for (var id = 0; id < count; id++)
        {
            var entry = 4 + id * 4;
            var packed = data[entry + 3];
            states.Add(id, (byte)(packed >> 4));
            owners.Add(id, (byte)(packed & 0x0F));
            classes.Add(id, (uint)(data[entry] | data[entry + 1] << 8 | data[entry + 2] << 16));
        }
        return new ScummIndexedObjectSnapshot(states, owners, classes);
    }
}

public sealed record ScummIndexedObjectSnapshot(
    IReadOnlyDictionary<int, byte> States,
    IReadOnlyDictionary<int, byte> Owners,
    IReadOnlyDictionary<int, uint>? Classes = null);

/// <summary>Old-v3 object classes. Class n is bit n-1 of an object's class data.</summary>
public static class ScummObjectClasses
{
    /// <summary>
    /// Small-header games store kObjectClassUntouchable as class 24 (ScummVM
    /// <c>getClass</c> translates it). <c>findObject</c> skips such objects.
    /// </summary>
    public const int Untouchable = 24;

    public static bool Has(uint classes, int cls) => cls is >= 1 and <= 32 && (classes & (1u << (cls - 1))) != 0;
}
