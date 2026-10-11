using System.Buffers.Binary;
using System.Text;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>Which files hold a v5 game: the stem shared by <c>NAME.000</c> (the index) and <c>NAME.001</c> (the resources), with the folder in front on a disc, and how many rooms the index lists.</summary>
public sealed record ScummV5Game(string Stem, int RoomCount);

/// <summary>
/// A SCUMM v5 game (Indiana Jones and the Fate of Atlantis, Monkey Island 1 CD; ScummVM <c>readIndexFile</c> for version 5): <c>NAME.000</c> is the index and
/// <c>NAME.001</c> the resources, every byte XOR 0x69; blocks are a four-letter tag and a big-endian size that counts its 8-byte header. The index has
/// <c>RNAM</c> (room names), <c>MAXS</c> and the directories <c>DROO DSCR DSOU DCOS DCHR DOBJ</c>: a word count, a room byte per resource, then a little-endian dword offset
/// per resource. <c>NAME.001</c> is <c>LECF</c> holding <c>LOFF</c> (each room's offset: the first block inside its <c>LFLF</c>) and the <c>LFLF</c> blocks, which hold
/// <c>ROOM SCRP SOUN COST CHAR</c>; a resource of the directories sits at its room's offset plus its own. Read for browsing: rooms (picture and palette) and costumes.
/// </summary>
public sealed class ScummV5Archive : IBrowsableArchive
{
    private const byte Xor = 0x69;

    private readonly byte[] _resources;
    private readonly byte[] _index;
    private readonly Dictionary<int, ScummV4Room> _rooms;

    private ScummV5Archive(byte[] index, byte[] resources, IReadOnlyList<ScummV4Room> rooms)
    {
        _index = index;
        _resources = resources;
        Rooms = rooms;
        _rooms = rooms.ToDictionary(room => room.Number);
    }

    public IReadOnlyList<ScummV4Room> Rooms { get; }

    /// <summary>
    /// The game on a folder or disc: every <c>X.000</c> with an <c>X.001</c> beside it whose index parses is a candidate, and the one with the most rooms wins (a full game over its
    /// demos); on a tie an <c>ENGLISH</c> folder, then the first. Null when there is none.
    /// </summary>
    public static ScummV5Game? FindGame(IGameFileSource source)
    {
        ScummV5Game? best = null;
        foreach (var name in source.FileNames.Where(n => n.EndsWith(".000", StringComparison.OrdinalIgnoreCase)))
        {
            var stem = name[..^4];
            if (!source.Exists(stem + ".001")) continue;
            int rooms;
            try { rooms = CountRooms(Decode(source.ReadAllBytes(name))); }
            catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException) { continue; }
            if (rooms <= 0) continue;
            var candidate = new ScummV5Game(stem, rooms);
            if (best is null || rooms > best.RoomCount ||
                rooms == best.RoomCount && stem.Contains("ENGLISH", StringComparison.OrdinalIgnoreCase) && !best.Stem.Contains("ENGLISH", StringComparison.OrdinalIgnoreCase))
                best = candidate;
        }
        return best;
    }

    public static ScummV5Archive Load(IGameFileSource source, ScummV5Game game)
    {
        var index = Decode(source.ReadAllBytes(game.Stem + ".000"));
        var resources = Decode(source.ReadAllBytes(game.Stem + ".001"));
        var names = ReadRoomNames(index);
        var top = Blocks(resources, 0, resources.Length).FirstOrDefault(b => b.Tag == "LECF") ?? throw new InvalidDataException("The resource file has no LECF block.");
        var children = Blocks(resources, top.Start + 8, top.Start + top.Size);
        var loff = children.FirstOrDefault(b => b.Tag == "LOFF") ?? throw new InvalidDataException("The resource file has no LOFF block.");
        var offsets = new Dictionary<int, int>();
        var count = resources[loff.Start + 8];
        for (var i = 0; i < count && loff.Start + 9 + i * 5 + 5 <= loff.Start + loff.Size; i++)
            offsets[resources[loff.Start + 9 + i * 5]] = (int)BinaryPrimitives.ReadUInt32LittleEndian(resources.AsSpan(loff.Start + 10 + i * 5));
        var rooms = new List<ScummV4Room>();
        foreach (var block in children.Where(b => b.Tag == "LFLF"))
            foreach (var (number, offset) in offsets.Where(pair => pair.Value == block.Start + 8))
                rooms.Add(new ScummV4Room(number, names.GetValueOrDefault(number, ""), offset, block.Size - 8));
        if (rooms.Count == 0) throw new InvalidDataException("No room of the index is in the resource file.");
        return new ScummV5Archive(index, resources, rooms.OrderBy(r => r.Number).ToArray());
    }

    private static byte[] Decode(byte[] bytes)
    {
        var decoded = new byte[bytes.Length];
        for (var i = 0; i < bytes.Length; i++) decoded[i] = (byte)(bytes[i] ^ Xor);
        return decoded;
    }

    private static int CountRooms(byte[] index)
    {
        var droo = Blocks(index, 0, index.Length).FirstOrDefault(b => b.Tag == "DROO");
        return droo is null ? 0 : BinaryPrimitives.ReadUInt16LittleEndian(index.AsSpan(droo.Start + 8));
    }

    /// <summary>The RNAM block: a room byte then nine name bytes (XOR 0xFF), ten bytes each, ended by room 0.</summary>
    private static Dictionary<int, string> ReadRoomNames(byte[] index)
    {
        var names = new Dictionary<int, string>();
        var rnam = Blocks(index, 0, index.Length).FirstOrDefault(b => b.Tag == "RNAM");
        if (rnam is null) return names;
        for (var p = rnam.Start + 8; p + 10 <= rnam.Start + rnam.Size && index[p] != 0; p += 10)
        {
            var name = new byte[9];
            for (var i = 0; i < 9; i++) name[i] = (byte)(index[p + 1 + i] ^ 0xFF);
            names[index[p]] = Encoding.Latin1.GetString(name).TrimEnd('\0');
        }
        return names;
    }

    /// <summary>A directory of the index: per resource number its room (0: none) and the offset inside that room's LFLF content.</summary>
    private IEnumerable<(int Id, int Room, int Position)> ReadDirectory(string tag)
    {
        var block = Blocks(_index, 0, _index.Length).FirstOrDefault(b => b.Tag == tag);
        if (block is null) yield break;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(_index.AsSpan(block.Start + 8));
        var rooms = block.Start + 10;
        var offsets = rooms + count;
        for (var id = 0; id < count && offsets + (id + 1) * 4 <= block.Start + block.Size; id++)
        {
            var room = _index[rooms + id];
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(_index.AsSpan(offsets + id * 4));
            if (room == 0 || offset == 0xFFFFFFFF || !_rooms.TryGetValue(room, out var owner)) continue;
            yield return (id, room, owner.Offset + (int)offset);
        }
    }

    public IReadOnlyList<ScummV4Costume> ReadCostumes()
    {
        var costumes = new List<ScummV4Costume>();
        foreach (var (id, room, position) in ReadDirectory("DCOS"))
        {
            if (position + 8 > _resources.Length || Encoding.Latin1.GetString(_resources, position, 4) != "COST") continue;
            var size = (int)BinaryPrimitives.ReadUInt32BigEndian(_resources.AsSpan(position + 4));
            if (size < 8 || position + size > _resources.Length) continue;
            costumes.Add(new ScummV4Costume(id, room, _resources.AsSpan(position + 8, size - 8).ToArray()));
        }
        return costumes;
    }

    public ScummV4Costume? FindCostume(int id) => ReadCostumes().FirstOrDefault(c => c.Id == id);

    /// <summary>Sounds (SOUN blocks with ADL, ROL and SPK parts) are not read for v5 yet.</summary>
    public IReadOnlyList<ScummV4Sound> ReadSounds() => [];

    public IReadOnlyList<(int Pose, RoomImage Image)> DecodeCostumePoses(ScummV4Costume costume)
    {
        // The costume has the layout of LOOM CD's (offsets count from 6 bytes before its body: ScummVM's baseptr is two bytes into the 8-byte block header).
        var palette = ReadPalette(costume.Room);
        var poses = new List<(int, RoomImage)>();
        foreach (var pose in ScummV4CostumeRenderer.PosesWithData(costume.Body))
        {
            try
            {
                if (ScummV4CostumeRenderer.DecodePose(costume.Body, pose, palette) is { } image) poses.Add((pose, image));
            }
            catch (InvalidDataException) { }
        }
        return poses;
    }

    private Block Room(int number)
    {
        if (!_rooms.TryGetValue(number, out var room)) throw new KeyNotFoundException($"Room {number} is not in the resource file.");
        return Blocks(_resources, room.Offset, room.Offset + room.Length).FirstOrDefault(b => b.Tag == "ROOM") ?? throw new InvalidDataException($"Room {number} has no ROOM block.");
    }

    /// <summary>A room's 256-colour palette: its <c>CLUT</c> block (version 5), or the <c>APAL</c> inside <c>PALS</c> (later versions).</summary>
    public IReadOnlyList<uint> ReadPalette(int number)
    {
        var room = Room(number);
        var children = Blocks(_resources, room.Start + 8, room.Start + room.Size);
        var start = children.FirstOrDefault(b => b.Tag == "CLUT")?.Start + 8;
        if (start is null && children.FirstOrDefault(b => b.Tag == "PALS") is { } pals)
            start = FindNested(pals, "APAL")?.Start + 8;
        if (start is null) throw new InvalidDataException($"Room {number} has no palette.");
        var palette = new uint[256];
        for (var i = 0; i < 256; i++)
            palette[i] = 0xFF000000u | (uint)(_resources[start.Value + i * 3] << 16) | (uint)(_resources[start.Value + i * 3 + 1] << 8) | _resources[start.Value + i * 3 + 2];
        return palette;
    }

    private Block? FindNested(Block parent, string tag)
    {
        foreach (var child in Blocks(_resources, parent.Start + 8, parent.Start + parent.Size))
        {
            if (child.Tag == tag) return child;
            if (child.Tag == "WRAP" && FindNested(child, tag) is { } found) return found;
        }
        return null;
    }

    public RoomImage DecodeRoom(int number)
    {
        var room = Room(number);
        var children = Blocks(_resources, room.Start + 8, room.Start + room.Size);
        var header = children.FirstOrDefault(b => b.Tag == "RMHD") ?? throw new InvalidDataException($"Room {number} has no RMHD block.");
        var width = BinaryPrimitives.ReadUInt16LittleEndian(_resources.AsSpan(header.Start + 8));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(_resources.AsSpan(header.Start + 10));
        if (width is < 8 or > 2048 || height is < 8 or > 400 || width % 8 != 0)
            throw new InvalidDataException($"Room {number} has an unsupported size {width} x {height}.");
        var rmim = children.FirstOrDefault(b => b.Tag == "RMIM") ?? throw new InvalidDataException($"Room {number} has no RMIM block.");
        var im00 = Blocks(_resources, rmim.Start + 8, rmim.Start + rmim.Size).FirstOrDefault(b => b.Tag == "IM00") ?? throw new InvalidDataException($"Room {number} has no IM00 block.");
        var smap = Blocks(_resources, im00.Start + 8, im00.Start + im00.Size).FirstOrDefault(b => b.Tag == "SMAP") ?? throw new InvalidDataException($"Room {number} has no SMAP block.");
        var palette = ReadPalette(number);
        var pixels = new byte[width * height];
        var end = smap.Start + smap.Size;
        for (var strip = 0; strip < width / 8; strip++)
        {
            var start = smap.Start + (int)BinaryPrimitives.ReadUInt32LittleEndian(_resources.AsSpan(smap.Start + 8 + strip * 4));
            if (start >= end) throw new InvalidDataException($"Room {number} strip {strip} starts outside its bitmap.");
            ScummV4Archive.DecodeStrip(_resources, start, end, pixels, width, height, strip * 8);
        }
        return new RoomImage(width, height, pixels, palette);
    }

    /// <summary>
    /// The room's <c>BOXD</c> block: a count word, then 20-byte boxes (four signed corner pairs, mask, flags, scale word) as in the v4 games; the box matrix is the
    /// separate <c>BOXM</c> block and is not read.
    /// </summary>
    public ScummWalkBoxSet? ReadWalkBoxes(int number)
    {
        try
        {
            var room = Room(number);
            if (Blocks(_resources, room.Start + 8, room.Start + room.Size).FirstOrDefault(b => b.Tag == "BOXD") is not { } boxd || boxd.Size < 10) return null;
            var count = BinaryPrimitives.ReadUInt16LittleEndian(_resources.AsSpan(boxd.Start + 8));
            if (count > 255 || 10 + count * ScummWalkBoxReader.BoxSizeV5 > boxd.Size) return null;
            var body = new byte[1 + count * ScummWalkBoxReader.BoxSizeV5];
            body[0] = (byte)count;
            _resources.AsSpan(boxd.Start + 10, count * ScummWalkBoxReader.BoxSizeV5).CopyTo(body.AsSpan(1));
            return ScummWalkBoxReader.ReadBlockBody(body);
        }
        catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException) { return null; }
    }

    private sealed record Block(string Tag, int Start, int Size);

    private static List<Block> Blocks(byte[] data, int start, int end)
    {
        var list = new List<Block>();
        for (var p = start; p + 8 <= end && p + 8 <= data.Length;)
        {
            var size = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(p + 4));
            if (size < 8 || p + size > end) break;
            list.Add(new Block(Encoding.Latin1.GetString(data, p, 4), p, size));
            p += size;
        }
        return list;
    }
}
