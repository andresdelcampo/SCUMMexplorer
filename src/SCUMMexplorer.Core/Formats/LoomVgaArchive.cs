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
    private readonly Dictionary<int, LoomVgaRoom> _rooms;

    private LoomVgaArchive(byte[] lec, IReadOnlyList<LoomVgaRoom> rooms)
    {
        _lec = lec;
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
        var names = ReadRoomNames(source.ReadAllBytes(IndexFile));
        var top = Blocks(lec, 0, lec.Length).FirstOrDefault(b => b.Tag == "LE")
            ?? throw new InvalidDataException("DISK01.LEC has no LE block.");
        var rooms = new List<LoomVgaRoom>();
        foreach (var block in Blocks(lec, top.Start + 6, top.Start + top.Size).Where(b => b.Tag == "LF"))
        {
            var number = BinaryPrimitives.ReadUInt16LittleEndian(lec.AsSpan(block.Start + 6));
            rooms.Add(new LoomVgaRoom(number, names.GetValueOrDefault(number, ""), block.Start, block.Size));
        }
        return new LoomVgaArchive(lec, rooms);
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
