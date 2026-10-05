using System.Buffers.Binary;
using System.Text;
using SCUMMexplorer.Core.Rendering;
using SCUMMexplorer.Core.Storage;

namespace SCUMMexplorer.Core.Formats;

/// <summary>One room of the CD game: its number, the name the index gives it (for example "logo") and where its LF block starts in the decoded DISK01.LEC.</summary>
/// <summary>One script block: Kind is SC (global), EN (entry), EX (exit) or LS (local, with its number); Code is the bytecode only.</summary>
public sealed record LoomVgaScript(string Kind, int Room, int Number, byte[] Code, int BlockOffset)
{
    /// <summary>How the script reads in lists: "Global script", "Entry script", "Exit script" or "Local script 200".</summary>
    public string Label => Kind switch { "SC" => $"Global script {Number}", "EN" => "Entry script", "EX" => "Exit script", _ => $"Local script {Number}" };
}

/// <summary>A verb entry of an object: the verb id (0xFF is the default entry) and where its code starts in the object's OC block.</summary>
public sealed record LoomVgaVerb(int VerbId, int Offset);

/// <summary>
/// One OC block (the object's code header, verb table and name): the position fields read like the EGA directory's
/// (x, y and width in 8-pixel units, the parent object's number and state, the walk-to point, the facing in the low bits of the height byte).
/// </summary>
public sealed record LoomVgaObject(int Id, int X, int Y, int Width, int Height, int Parent, int ParentState, int WalkX, int WalkY, int Direction,
    string Name, IReadOnlyList<LoomVgaVerb> Verbs, byte[] Block, int BlockOffset);

/// <summary>A costume block (CO): its number from the index, the room that stores it and its bytes (the body, without the block header).</summary>
public sealed record LoomVgaCostume(int Id, int Room, byte[] Body);

/// <summary>One decoded OI image of a room object: the object's id, its size from the OC block and the picture as palette indices (the room's PA palette).</summary>
public sealed record LoomVgaObjectImage(int Id, int X, int Y, int Width, int Height, byte[] Pixels);

public sealed record LoomVgaRoom(int Number, string Name, int Offset, int Length);

/// <summary>
/// The LOOM CD game's data (SCUMM v4): DISK01.LEC is XOR 0x69 and holds LE &gt; FO + one LF block per room; a block is
/// size u32 (header included), a two-letter tag, the body. 000.LFL is the index (plain blocks). Probe record:
/// .verification/loom-vga/README.md.
/// </summary>
public sealed class LoomVgaArchive
{
    public const string IndexFile = "000.LFL";
    public const string ArchiveFile = "DISK01.LEC";
    private const byte Xor = 0x69;

    private readonly byte[] _lec;
    private readonly byte[] _index;
    private readonly Dictionary<int, LoomVgaRoom> _rooms;

    private LoomVgaArchive(byte[] lec, byte[] index, IReadOnlyList<LoomVgaRoom> rooms)
    {
        _lec = lec;
        _index = index;
        Rooms = rooms;
        _rooms = rooms.ToDictionary(r => r.Number);
    }

    public IReadOnlyList<LoomVgaRoom> Rooms { get; }

    public static bool IsVga(IGameFileSource source) => source.Exists(IndexFile) && source.Exists(ArchiveFile);

    public static LoomVgaArchive Load(IGameFileSource source)
    {
        if (!IsVga(source)) throw new InvalidDataException("000.LFL and DISK01.LEC are both needed.");
        var lec = source.ReadAllBytes(ArchiveFile);
        for (var i = 0; i < lec.Length; i++) lec[i] ^= Xor;
        var index = source.ReadAllBytes(IndexFile);
        var names = ReadRoomNames(index);
        var top = Blocks(lec, 0, lec.Length).FirstOrDefault(b => b.Tag == "LE")
            ?? throw new InvalidDataException("DISK01.LEC has no LE block.");
        var rooms = new List<LoomVgaRoom>();
        foreach (var block in Blocks(lec, top.Start + 6, top.Start + top.Size).Where(b => b.Tag == "LF"))
        {
            var number = BinaryPrimitives.ReadUInt16LittleEndian(lec.AsSpan(block.Start + 6));
            rooms.Add(new LoomVgaRoom(number, names.GetValueOrDefault(number, ""), block.Start, block.Size));
        }
        return new LoomVgaArchive(lec, index, rooms);
    }

    /// <summary>The RN block: room number (one plain byte), a 9-byte name (XOR 0xFF); a zero room number ends the list.</summary>
    private static Dictionary<int, string> ReadRoomNames(byte[] index)
    {
        var names = new Dictionary<int, string>();
        var rn = Blocks(index, 0, index.Length).FirstOrDefault(b => b.Tag == "RN");
        if (rn is null) return names;
        for (var p = rn.Start + 6; p + 10 <= rn.Start + rn.Size && index[p] != 0; p += 10)
        {
            var name = new byte[9];
            for (var i = 0; i < 9; i++) name[i] = (byte)(index[p + 1 + i] ^ 0xFF);
            names[index[p]] = Encoding.Latin1.GetString(name).TrimEnd('\0');
        }
        return names;
    }

    /// <summary>The room's scripts in file order: SC blocks sit beside RO in the LF block; EN, EX and LS are inside RO. A local script's first body byte is its number.</summary>
    public IReadOnlyList<LoomVgaScript> ReadScripts(int number)
    {
        if (!_rooms.TryGetValue(number, out var room)) throw new KeyNotFoundException($"Room {number} is not in DISK01.LEC.");
        var scripts = new List<LoomVgaScript>();
        foreach (var block in Blocks(_lec, room.Offset + 8, room.Offset + room.Length))
        {
            if (block.Tag == "SC") scripts.Add(new LoomVgaScript("SC", number, scripts.Count(s => s.Kind == "SC"), _lec.AsSpan(block.Start + 6, block.Size - 6).ToArray(), block.Start));
            else if (block.Tag == "RO")
                foreach (var child in Blocks(_lec, block.Start + 6, block.Start + block.Size))
                {
                    if (child.Tag is "EN" or "EX") scripts.Add(new LoomVgaScript(child.Tag, number, 0, _lec.AsSpan(child.Start + 6, child.Size - 6).ToArray(), child.Start));
                    else if (child.Tag == "LS") scripts.Add(new LoomVgaScript("LS", number, _lec[child.Start + 6], _lec.AsSpan(child.Start + 7, child.Size - 7).ToArray(), child.Start));
                }
        }
        return scripts;
    }

    /// <summary>
    /// The room's objects from its OC blocks. Layout, from the data (the verb offsets land exactly on the code that follows the name):
    /// block offset 6 the id (u16), 9 x/8, 10 y/8 (bit 7: the parent state), 11 width/8, 12 the parent object, 13 and 15 the walk-to
    /// point (words), 17 height and facing, 18 the name's offset, 19 the verb table (a verb byte and a word offset each, ended by a
    /// zero verb), then the zero-ended name and the code.
    /// </summary>
    public IReadOnlyList<LoomVgaObject> ReadObjects(int number)
    {
        if (!_rooms.TryGetValue(number, out var room)) throw new KeyNotFoundException($"Room {number} is not in DISK01.LEC.");
        var objects = new List<LoomVgaObject>();
        var ro = Blocks(_lec, room.Offset + 8, room.Offset + room.Length).FirstOrDefault(b => b.Tag == "RO");
        if (ro is null) return objects;
        foreach (var oc in Blocks(_lec, ro.Start + 6, ro.Start + ro.Size).Where(b => b.Tag == "OC"))
        {
            if (oc.Size < 21) continue;
            var block = _lec.AsSpan(oc.Start, oc.Size).ToArray();
            var verbs = new List<LoomVgaVerb>();
            var p = 19;
            while (p + 3 <= block.Length && block[p] != 0)
            {
                verbs.Add(new LoomVgaVerb(block[p], BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(p + 1))));
                p += 3;
            }
            var nameStart = block[18];
            var name = "";
            if (nameStart < block.Length)
            {
                var end = Array.IndexOf(block, (byte)0, nameStart);
                name = Encoding.Latin1.GetString(block, nameStart, (end < 0 ? block.Length : end) - nameStart);
            }
            objects.Add(new LoomVgaObject(BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(6)), block[9] * 8, (block[10] & 0x7F) * 8, block[11] * 8,
                block[17] & 0xF8, block[12], block[10] >> 7, BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(13)), BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(15)),
                block[17] & 7, name, verbs, block, oc.Start));
        }
        return objects;
    }

    /// <summary>
    /// A resource directory of 000.LFL ("0S" scripts, "0N" sounds, "0C" costumes): a count word, then per resource number a room byte and a
    /// dword offset (counted from the first block inside that room's LF block, which is 8 bytes after the LF block's start). Room 0 means
    /// the resource does not exist.
    /// </summary>
    private IEnumerable<(int Id, int Room, int Block)> ReadDirectory(string tag)
    {
        var block = Blocks(_index, 0, _index.Length).FirstOrDefault(b => b.Tag == tag);
        if (block is null) yield break;
        var body = block.Start + 6;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(_index.AsSpan(body));
        for (var id = 0; id < count && body + 2 + (id + 1) * 5 <= block.Start + block.Size; id++)
        {
            var room = _index[body + 2 + id * 5];
            if (room == 0 || !_rooms.TryGetValue(room, out var owner)) continue;
            yield return (id, room, owner.Offset + 8 + (int)BinaryPrimitives.ReadUInt32LittleEndian(_index.AsSpan(body + 3 + id * 5)));
        }
    }

    /// <summary>
    /// The number a room's local scripts start from. The index's "0S" table lists 199 slots, but every local script of the data is
    /// numbered 200 or higher, so 200 is the boundary the session uses between global and local numbers.
    /// </summary>
    public const int LocalScriptBase = 200;

    /// <summary>The numbers of the global scripts the "0S" directory places in an SC block, ascending.</summary>
    public IReadOnlyList<int> GlobalScriptNumbers() =>
        ReadDirectory("0S").Where(entry => entry.Block + 6 <= _lec.Length && Encoding.Latin1.GetString(_lec, entry.Block + 4, 2) == "SC")
            .Select(entry => entry.Id).Where(id => id < LocalScriptBase).Order().ToArray();

    /// <summary>The numbered global script's bytecode, found through the "0S" directory (room and offset), or null when the index has none.</summary>
    public LoomVgaScript? ReadGlobalScript(int number)
    {
        foreach (var (id, room, block) in ReadDirectory("0S"))
        {
            if (id != number) continue;
            if (block + 6 > _lec.Length || Encoding.Latin1.GetString(_lec, block + 4, 2) != "SC") return null;
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(_lec.AsSpan(block));
            if (size < 6 || block + size > _lec.Length) return null;
            return new LoomVgaScript("SC", room, number, _lec.AsSpan(block + 6, size - 6).ToArray(), block);
        }
        return null;
    }

    /// <summary>
    /// The room's OI object images. An OI block is the object id (u16) and a BM-like body: a size dword, one dword offset per 8-pixel strip
    /// (counted from the size field), then the strips (the codecs of the room background). The width and height come from the object's OC
    /// block, which must exist; an image that does not decode is left out. The same object may have several OI blocks (its states, in file order).
    /// </summary>
    public IReadOnlyList<LoomVgaObjectImage> ReadObjectImages(int number)
    {
        if (!_rooms.TryGetValue(number, out var room)) throw new KeyNotFoundException($"Room {number} is not in DISK01.LEC.");
        var ro = Blocks(_lec, room.Offset + 8, room.Offset + room.Length).FirstOrDefault(b => b.Tag == "RO");
        if (ro is null) return [];
        var objects = ReadObjects(number).GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First());
        var images = new List<LoomVgaObjectImage>();
        foreach (var oi in Blocks(_lec, ro.Start + 6, ro.Start + ro.Size).Where(b => b.Tag == "OI"))
        {
            if (oi.Size < 14) continue;
            var id = BinaryPrimitives.ReadUInt16LittleEndian(_lec.AsSpan(oi.Start + 6));
            if (!objects.TryGetValue(id, out var owner) || owner.Width < 8 || owner.Height < 1) continue;
            var body = oi.Start + 8;
            var end = oi.Start + oi.Size;
            var pixels = new byte[owner.Width * owner.Height];
            try
            {
                for (var strip = 0; strip < owner.Width / 8; strip++)
                {
                    var offset = BinaryPrimitives.ReadUInt32LittleEndian(_lec.AsSpan(body + 4 + strip * 4));
                    if (body + offset >= end) throw new InvalidDataException("strip outside the image");
                    DecodeStrip(_lec, (int)(body + offset), end, pixels, owner.Width, owner.Height, strip * 8);
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException) { continue; }
            images.Add(new LoomVgaObjectImage(id, owner.X, owner.Y, owner.Width, owner.Height, pixels));
        }
        return images;
    }

    /// <summary>
    /// The index's object table ("0O"): per object four bytes, three of class bits (little endian) and one packing the state (high nibble)
    /// and the owner (low nibble), as the EGA index does. Every room object starts with owner 0xF and state 0.
    /// </summary>
    public LoomIndexedObjectSnapshot ReadIndexedObjects()
    {
        var states = new Dictionary<int, byte>();
        var owners = new Dictionary<int, byte>();
        var classes = new Dictionary<int, uint>();
        var block = Blocks(_index, 0, _index.Length).FirstOrDefault(b => b.Tag == "0O");
        if (block is null) return new LoomIndexedObjectSnapshot(states, owners, classes);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(_index.AsSpan(block.Start + 6));
        for (var id = 0; id < count && block.Start + 8 + (id + 1) * 4 <= block.Start + block.Size; id++)
        {
            var entry = block.Start + 8 + id * 4;
            states[id] = (byte)(_index[entry + 3] >> 4);
            owners[id] = (byte)(_index[entry + 3] & 0x0F);
            classes[id] = (uint)(_index[entry] | _index[entry + 1] << 8 | _index[entry + 2] << 16);
        }
        return new LoomIndexedObjectSnapshot(states, owners, classes);
    }

    /// <summary>The room's size from its HD block.</summary>
    public (int Width, int Height) ReadRoomSize(int number)
    {
        if (!_rooms.TryGetValue(number, out var room)) throw new KeyNotFoundException($"Room {number} is not in DISK01.LEC.");
        var ro = Blocks(_lec, room.Offset + 8, room.Offset + room.Length).FirstOrDefault(b => b.Tag == "RO")
            ?? throw new InvalidDataException($"Room {number} has no RO block.");
        var hd = Blocks(_lec, ro.Start + 6, ro.Start + ro.Size).FirstOrDefault(b => b.Tag == "HD")
            ?? throw new InvalidDataException($"Room {number} has no HD block.");
        return (BinaryPrimitives.ReadUInt16LittleEndian(_lec.AsSpan(hd.Start + 6)), BinaryPrimitives.ReadUInt16LittleEndian(_lec.AsSpan(hd.Start + 8)));
    }

    /// <summary>The room's BX block body (walk boxes and box matrix), or null when the room has none.</summary>
    public byte[]? ReadWalkBoxBlock(int number)
    {
        if (!_rooms.TryGetValue(number, out var room)) throw new KeyNotFoundException($"Room {number} is not in DISK01.LEC.");
        var ro = Blocks(_lec, room.Offset + 8, room.Offset + room.Length).FirstOrDefault(b => b.Tag == "RO");
        var bx = ro is null ? null : Blocks(_lec, ro.Start + 6, ro.Start + ro.Size).FirstOrDefault(b => b.Tag == "BX");
        return bx is null ? null : _lec.AsSpan(bx.Start + 6, bx.Size - 6).ToArray();
    }

    /// <summary>The costumes the index lists (those whose block is a CO block).</summary>
    public IReadOnlyList<LoomVgaCostume> ReadCostumes()
    {
        var costumes = new List<LoomVgaCostume>();
        foreach (var (id, room, block) in ReadDirectory("0C"))
        {
            if (block + 6 > _lec.Length || Encoding.Latin1.GetString(_lec, block + 4, 2) != "CO") continue;
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(_lec.AsSpan(block));
            if (size < 6 || block + size > _lec.Length) continue;
            costumes.Add(new LoomVgaCostume(id, room, _lec.AsSpan(block + 6, size - 6).ToArray()));
        }
        return costumes;
    }

    /// <summary>The costume with this number, or null.</summary>
    public LoomVgaCostume? FindCostume(int id) => ReadCostumes().FirstOrDefault(c => c.Id == id);

    /// <summary>Every stored pose of a costume that decodes (pose index and picture, in the palette of the room that stores the costume).</summary>
    public IReadOnlyList<(int Pose, RoomImage Image)> DecodeCostumePoses(LoomVgaCostume costume)
    {
        var palette = ReadPalette(costume.Room);
        var poses = new List<(int, RoomImage)>();
        foreach (var pose in LoomVgaCostumeRenderer.PosesWithData(costume.Body))
        {
            try
            {
                if (LoomVgaCostumeRenderer.DecodePose(costume.Body, pose, palette) is { } image) poses.Add((pose, image));
            }
            catch (InvalidDataException) { }
        }
        return poses;
    }

    /// <summary>A room's 256-colour palette (the PA block), for things drawn in that room.</summary>
    public IReadOnlyList<uint> ReadPalette(int number)
    {
        if (!_rooms.TryGetValue(number, out var room)) throw new KeyNotFoundException($"Room {number} is not in DISK01.LEC.");
        var ro = Blocks(_lec, room.Offset + 8, room.Offset + room.Length).First(b => b.Tag == "RO");
        var pa = Blocks(_lec, ro.Start + 6, ro.Start + ro.Size).First(b => b.Tag == "PA").Start + 6;
        var palette = new uint[256];
        for (var i = 0; i < 256; i++)
            palette[i] = 0xFF000000u | (uint)(_lec[pa + 2 + i * 3] << 16) | (uint)(_lec[pa + 3 + i * 3] << 8) | _lec[pa + 4 + i * 3];
        return palette;
    }

    public RoomImage DecodeRoom(int number)
    {
        if (!_rooms.TryGetValue(number, out var room)) throw new KeyNotFoundException($"Room {number} is not in DISK01.LEC.");
        var ro = Blocks(_lec, room.Offset + 8, room.Offset + room.Length).FirstOrDefault(b => b.Tag == "RO")
            ?? throw new InvalidDataException($"Room {number} has no RO block.");
        var children = Blocks(_lec, ro.Start + 6, ro.Start + ro.Size);
        Block Child(string tag) => children.FirstOrDefault(b => b.Tag == tag) ?? throw new InvalidDataException($"Room {number} has no {tag} block.");
        var hd = Child("HD");
        var width = BinaryPrimitives.ReadUInt16LittleEndian(_lec.AsSpan(hd.Start + 6));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(_lec.AsSpan(hd.Start + 8));
        if (width is < 8 or > 2048 || height is < 8 or > 400 || width % 8 != 0)
            throw new InvalidDataException($"Room {number} has an unsupported size {width} x {height}.");

        var pa = Child("PA").Start + 6;
        var palette = new uint[256];
        for (var i = 0; i < 256; i++)
            palette[i] = 0xFF000000u | (uint)(_lec[pa + 2 + i * 3] << 16) | (uint)(_lec[pa + 3 + i * 3] << 8) | _lec[pa + 4 + i * 3];

        var bm = Child("BM");
        var body = bm.Start + 6;
        var end = bm.Start + bm.Size;
        var pixels = new byte[width * height];
        for (var strip = 0; strip < width / 8; strip++)
        {
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(_lec.AsSpan(body + 4 + strip * 4));
            if (body + offset >= end) throw new InvalidDataException($"Room {number} strip {strip} starts outside its bitmap.");
            DecodeStrip(_lec, (int)(body + offset), end, pixels, width, height, strip * 8);
        }
        return new RoomImage(width, height, pixels, palette);
    }

    /// <summary>
    /// One 8-pixel-wide strip, the codes this game uses: 0x01 raw (column by column) and the "basic" bit codecs, vertical
    /// 0x0E-0x12 or horizontal 0x18-0x1C (code mod 10 = colour bits). Bits: 0 same colour; 10 a new colour; 110 colour += step;
    /// 111 step = -step, then colour += step (ScummVM's drawStripBasicV/H).
    /// </summary>
    private static void DecodeStrip(byte[] data, int pos, int end, byte[] pixels, int width, int height, int x0)
    {
        var code = data[pos++];
        if (code == 0x01)
        {
            for (var x = 0; x < 8; x++)
                for (var y = 0; y < height; y++) pixels[y * width + x0 + x] = data[pos++];
            return;
        }
        var vertical = code is >= 0x0E and <= 0x12;
        if (!vertical && code is not (>= 0x18 and <= 0x1C))
            throw new InvalidDataException($"Unsupported strip code 0x{code:X2}.");
        var shift = code % 10;
        var mask = 0xFF >> (8 - shift);
        int color = data[pos++], bits = data[pos++], count = 8, step = -1;
        void Fill()
        {
            if (count > 8) return;
            bits |= (pos < end ? data[pos] : 0) << count; pos++; count += 8;
        }
        int Bit() { count--; var b = bits & 1; bits >>= 1; return b; }
        var outer = vertical ? 8 : height;
        var inner = vertical ? height : 8;
        for (var a = 0; a < outer; a++)
        {
            for (var b = 0; b < inner; b++)
            {
                Fill();
                var (x, y) = vertical ? (a, b) : (b, a);
                pixels[y * width + x0 + x] = (byte)color;
                if (Bit() == 0) continue;
                if (Bit() == 0) { Fill(); color = bits & mask; bits >>= shift; count -= shift; step = -1; }
                else if (Bit() == 0) color = (color + step) & 0xFF;
                else { step = -step; color = (color + step) & 0xFF; }
            }
        }
    }

    private sealed record Block(string Tag, int Start, int Size);

    private static List<Block> Blocks(byte[] data, int start, int end)
    {
        var list = new List<Block>();
        for (var p = start; p + 6 <= end;)
        {
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(p));
            if (size < 6 || p + size > end) break;
            list.Add(new Block(Encoding.Latin1.GetString(data, p + 4, 2), p, size));
            p += size;
        }
        return list;
    }
}
