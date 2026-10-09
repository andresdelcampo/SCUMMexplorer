using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>A local old-bundle object record, including objects without artwork.</summary>
public sealed record ScummRoomObjectRecord(
    int Id, int LocalIndex, int X, int Y, int Width, int Height,
    int ParentLocalIndex, byte ParentRequiredState, int WalkX, int WalkY,
    byte Direction, int CodeOffset, int? ImageOffset);

/// <summary>Reads the local object directory and code headers of a DOS EGA old-v3 room.</summary>
public sealed class ScummRoomObjectDirectory
{
    public IReadOnlyList<ScummRoomObjectRecord> Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length < 29) throw new InvalidDataException("The room is too short.");
        var data = encoded.ToArray();
        for (var i = 0; i < data.Length; i++) data[i] ^= 0xFF;
        return DecodePlain(data);
    }

    internal IReadOnlyList<ScummRoomObjectRecord> DecodePlain(ReadOnlySpan<byte> data)
    {
        var roomEnd = Word(data, 0);
        var roomWidth = Word(data, 4);
        var roomHeight = Word(data, 6);
        var count = data[20];
        var directoryEnd = 29 + count * 4;
        if (roomEnd > data.Length || roomEnd < directoryEnd ||
            roomWidth is < 8 or > 1280 || roomHeight is < 1 or > 200)
            throw new InvalidDataException("Invalid room object directory.");

        var records = new List<ScummRoomObjectRecord>(count);
        for (var i = 0; i < count; i++)
        {
            var image = Word(data, 29 + i * 2);
            var code = Word(data, 29 + count * 2 + i * 2);
            if (code < directoryEnd || code + 16 > roomEnd) continue;
            var id = Word(data, code + 4);
            if (id == 0) continue;
            var x = data[code + 7] * 8;
            var y = (data[code + 8] & 0x7F) * 8;
            var width = data[code + 9] * 8;
            var height = data[code + 15] & 0xF8;
            var nextSharesImage = i + 1 < count && Word(data, 29 + (i + 1) * 2) == image;
            int? imageOffset = image >= directoryEnd && image + 4 <= roomEnd &&
                               image != code && !nextSharesImage ? image : null;
            records.Add(new ScummRoomObjectRecord(id, i + 1, x, y, width, height,
                data[code + 10], (byte)(data[code + 8] >> 7),
                Word(data, code + 11), Word(data, code + 13),
                (byte)(data[code + 15] & 7), code, imageOffset));
        }
        return records;
    }

    private static ushort Word(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));

    /// <summary>
    /// The stored name bytes of each object of one room (ScummVM <c>getObjOrActorName</c>
    /// for a room object: the string the object's code header points to), as the
    /// charset draws them; objects without a readable name are left out.
    /// </summary>
    public static IReadOnlyDictionary<int, byte[]> ReadObjectNameBytes(byte[] plainRoom,
        IEnumerable<ScummRoomObjectRecord> records)
    {
        var names = new Dictionary<int, byte[]>();
        foreach (var record in records)
            if (TryReadObjectNameBytes(plainRoom, record.CodeOffset, out var start, out var length))
                names[record.Id] = plainRoom.AsSpan(start, length).ToArray();
        return names;
    }

    internal static bool TryReadObjectNameBytes(byte[] room, int codeOffset, out int start, out int length)
    {
        start = length = 0;
        if (codeOffset < 0 || codeOffset > room.Length - 17) return false;
        var size = room[codeOffset] | room[codeOffset + 1] << 8;
        var end = codeOffset + size;
        var relative = room[codeOffset + 16];
        start = codeOffset + relative;
        if (size < 18 || end > room.Length || relative < 17 || start >= end) return false;
        var terminator = FindStringTerminator(room.AsSpan(start, end - start));
        if (terminator <= 0) return false;
        length = terminator;
        return true;
    }

    private static int FindStringTerminator(ReadOnlySpan<byte> bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == 0) return i;
            if (bytes[i] != 0xFF || i + 1 >= bytes.Length) continue;
            var control = bytes[++i];
            var parameterCount = control is 1 or 2 or 3 or 8 ? 0 : 2;
            i = Math.Min(bytes.Length - 1, i + parameterCount);
        }
        return -1;
    }
}
