using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>An old-v3 verb table entry and its bounded object-code target.</summary>
public sealed record LoomObjectVerbEntry(int ObjectId, int VerbId, int CodeOffset,
    int TableOffset, int ScriptOffset, int ResourceEnd)
{
    public bool IsFallback => VerbId == 0xFF;
    public string Label => IsFallback ? "Fallback verb" : $"Verb 0x{VerbId:X2}";
}

/// <summary>Reads the three-byte verb entries at OBCD+17 in old-bundle LOOM rooms.</summary>
public sealed class LoomObjectVerbReader(ScummScriptDialect dialect = ScummScriptDialect.Loom)
{
    public IReadOnlyDictionary<int, IReadOnlyList<LoomObjectVerbEntry>> ReadAll(
        ReadOnlySpan<byte> encodedRoom, IReadOnlyList<LoomRoomObjectRecord> records)
    {
        var data = LflEncoding.DecodeXorFF(encodedRoom.ToArray());
        return records.ToDictionary(record => record.Id,
            record => (IReadOnlyList<LoomObjectVerbEntry>)ReadPlain(data, record));
    }

    public LoomScriptDisassembly Decode(GameResource room, LoomRoomObjectRecord record, int verbId)
        => Decode(room, record, verbId, out _);

    /// <summary>Returns the actual selected table entry, including a native fallback.</summary>
    public LoomScriptDisassembly Decode(GameResource room, LoomRoomObjectRecord record, int verbId,
        out LoomObjectVerbEntry selectedEntry)
    {
        if (room.Kind != ResourceKind.Room || room.ResourceId is not int roomId ||
            room.ContainerFileNumber is not int container)
            throw new InvalidDataException("Object verbs require an indexed room.");
        var data = LflEncoding.DecodeXorFF(File.ReadAllBytes(room.FilePath));
        var entries = ReadPlain(data, record);
        var entry = entries.FirstOrDefault(item => item.VerbId == verbId) ??
                    entries.FirstOrDefault(item => item.IsFallback);
        if (entry is null)
            throw new InvalidDataException($"Object {record.Id} has no verb 0x{verbId:X2} or fallback entry.");
        selectedEntry = entry;
        var code = data.AsSpan(entry.ScriptOffset, entry.ResourceEnd - entry.ScriptOffset).ToArray();
        var decoded = LoomScriptDisassembler.Disassemble(code, dialect);
        return new LoomScriptDisassembly(roomId, container, entry.ScriptOffset, code.Length,
            code.Length, decoded.Instructions, decoded.Note, decoded.UndecodedOffset,
            decoded.UndecodedBytes, decoded.TrailingBytes,
            $"Room {roomId:00} Object {record.Id} {entry.Label}", decoded.Issues);
    }

    private static LoomObjectVerbEntry[] ReadPlain(byte[] data, LoomRoomObjectRecord record)
    {
        var start = record.CodeOffset;
        if (start < 0 || start > data.Length - 2)
            throw new InvalidDataException($"Object {record.Id} code offset lies outside the room.");
        var length = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(start, 2));
        var end = start + length;
        if (length < 17 || end > data.Length)
            throw new InvalidDataException($"Object {record.Id} has an invalid OBCD length.");
        var cursor = start + 17;
        var entries = new List<LoomObjectVerbEntry>();
        while (cursor < end && data[cursor] != 0)
        {
            if (cursor > end - 3)
                throw new InvalidDataException($"Object {record.Id} verb table ends in a truncated entry.");
            var relative = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor + 1, 2));
            entries.Add(new LoomObjectVerbEntry(record.Id, data[cursor], start, cursor,
                start + relative, end));
            cursor += 3;
        }
        if (cursor >= end)
            throw new InvalidDataException($"Object {record.Id} verb table has no terminator.");
        var tableEnd = cursor + 1;
        foreach (var entry in entries)
            if (entry.ScriptOffset < tableEnd || entry.ScriptOffset >= end)
                throw new InvalidDataException($"Object {record.Id} verb 0x{entry.VerbId:X2} points outside its bytecode.");
        return entries.ToArray();
    }
}
