using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Formats;

/// <summary>Reads the packed owner/state bytes in an old-v3 00.LFL index.</summary>
public sealed class LflObjectStateReader
{
    public IReadOnlyDictionary<int, byte> Read(string indexFilePath) => ReadSnapshot(indexFilePath).States;

    public LoomIndexedObjectSnapshot ReadSnapshot(string indexFilePath)
    {
        var data = LflEncoding.Read(indexFilePath);
        if (data.Length < 4 || BinaryPrimitives.ReadUInt16LittleEndian(data) != 0x0100)
            throw new InvalidDataException("Invalid old-v3 object-state index.");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2));
        if (data.Length < 4 + count * 4)
            throw new InvalidDataException("Truncated old-v3 object-state table.");

        var states = new Dictionary<int, byte>(count);
        var owners = new Dictionary<int, byte>(count);
        for (var id = 0; id < count; id++)
        {
            var packed = data[4 + id * 4 + 3];
            states.Add(id, (byte)(packed >> 4));
            owners.Add(id, (byte)(packed & 0x0F));
        }
        return new LoomIndexedObjectSnapshot(states, owners);
    }
}

public sealed record LoomIndexedObjectSnapshot(
    IReadOnlyDictionary<int, byte> States,
    IReadOnlyDictionary<int, byte> Owners);
