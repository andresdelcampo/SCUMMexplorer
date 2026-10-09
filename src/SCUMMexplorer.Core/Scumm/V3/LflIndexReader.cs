using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

public sealed record IndexedResource(
    ResourceKind Kind,
    int ResourceId,
    int ContainerFileNumber,
    int Offset,
    int DirectoryEntryOffset,
    int? ContainerNumberOffset);

/// <summary>Reads the old SCUMM v3 index directory stored in 00.LFL.</summary>
public sealed class LflIndexReader
{
    public int ReadDeclaredScriptCount(string indexFilePath)
    {
        using var stream = new MemoryStream(LflEncoding.Read(indexFilePath));
        using var reader = new BinaryReader(stream);
        var magic = ReadUInt16(reader);
        if (magic != 0x0100)
            throw new InvalidDataException($"Unexpected 00.LFL signature 0x{magic:X4}; expected the SCUMM v3 signature 0x0100.");
        var globalObjectCount = ReadUInt16(reader);
        Skip(reader, checked(globalObjectCount * 4));
        SkipDirectory(reader);
        SkipDirectory(reader);
        return reader.ReadByte();
    }

    public IReadOnlyList<IndexedResource> Read(string indexFilePath)
    {
        using var stream = new MemoryStream(LflEncoding.Read(indexFilePath));
        using var reader = new BinaryReader(stream);

        var magic = ReadUInt16(reader);
        if (magic != 0x0100)
            throw new InvalidDataException($"Unexpected 00.LFL signature 0x{magic:X4}; expected the SCUMM v3 signature 0x0100.");

        var globalObjectCount = ReadUInt16(reader);
        Skip(reader, checked(globalObjectCount * 4));

        var resources = new List<IndexedResource>();
        ReadDirectory(reader, ResourceKind.Room, resources, roomDirectory: true);
        ReadDirectory(reader, ResourceKind.Costume, resources);
        ReadDirectory(reader, ResourceKind.Script, resources);
        ReadDirectory(reader, ResourceKind.Sound, resources);
        return resources;
    }

    private static void ReadDirectory(BinaryReader reader, ResourceKind kind, List<IndexedResource> output, bool roomDirectory = false)
    {
        var count = reader.ReadByte();
        if (count == byte.MaxValue) throw new InvalidDataException($"Invalid {kind} directory count.");

        var containerNumbers = new int[count];
        var containerNumberOffsets = new int?[count];
        if (roomDirectory)
        {
            // In the v3 room list, the room number is the directory ordinal. The
            // parallel byte table is reserved/unused by the old-file reader.
            Array.Fill(containerNumbers, -1);
            Skip(reader, count);
        }
        else
        {
            for (var i = 0; i < count; i++)
            {
                containerNumberOffsets[i] = checked((int)reader.BaseStream.Position);
                containerNumbers[i] = reader.ReadByte();
            }
        }

        for (var i = 0; i < count; i++)
        {
            var directoryEntryOffset = checked((int)reader.BaseStream.Position);
            var offset = ReadUInt16(reader);
            if (offset == ushort.MaxValue) continue;
            var container = roomDirectory ? i : containerNumbers[i];
            output.Add(new IndexedResource(kind, i, container, offset, directoryEntryOffset,
                containerNumberOffsets[i]));
        }
    }

    private static void SkipDirectory(BinaryReader reader)
    {
        var count = reader.ReadByte();
        if (count == byte.MaxValue) throw new InvalidDataException("Invalid resource directory count.");
        Skip(reader, count);
        Skip(reader, checked(count * 2));
    }

    private static ushort ReadUInt16(BinaryReader reader)
    {
        Span<byte> bytes = stackalloc byte[2];
        reader.ReadExactly(bytes);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    private static void Skip(BinaryReader reader, int byteCount)
    {
        if (byteCount < 0 || reader.BaseStream.Position + byteCount > reader.BaseStream.Length)
            throw new InvalidDataException("The 00.LFL directory ends unexpectedly.");
        reader.BaseStream.Seek(byteCount, SeekOrigin.Current);
    }
}
