using System.Buffers.Binary;
using System.Text;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>One room of the CD game: its number, the name the index gives it (for example "logo") and where its LF block starts in the decoded DISK01.LEC.</summary>
/// <summary>One script block: Kind is SC (global), EN (entry), EX (exit) or LS (local, with its number); Code is the bytecode only.</summary>
public sealed record ScummV4Script(string Kind, int Room, int Number, byte[] Code, int BlockOffset)
{
    /// <summary>How the script reads in lists: "Global script", "Entry script", "Exit script" or "Local script 200".</summary>
    public string Label => Kind switch { "SC" => $"Global script {Number}", "EN" => "Entry script", "EX" => "Exit script", _ => $"Local script {Number}" };
}

/// <summary>A verb entry of an object: the verb id (0xFF is the default entry) and where its code starts in the object's OC block.</summary>
public sealed record ScummV4Verb(int VerbId, int Offset);

/// <summary>
/// One OC block (the object's code header, verb table and name): the position fields read like the EGA directory's
/// (x, y and width in 8-pixel units, the parent object's number and state, the walk-to point, the facing in the low bits of the height byte).
/// </summary>
public sealed record ScummV4Object(int Id, int X, int Y, int Width, int Height, int Parent, int ParentState, int WalkX, int WalkY, int Direction,
    string Name, IReadOnlyList<ScummV4Verb> Verbs, byte[] Block, int BlockOffset);

/// <summary>A costume block (CO): its number from the index, the room that stores it and its bytes (the body, without the block header).</summary>
public sealed record ScummV4Costume(int Id, int Room, byte[] Body);

/// <summary>A CD sound: <see cref="Speaker"/> is the whole <c>WA</c> block (6-byte header), <see cref="Adlib"/> the <c>AD</c> body; a part is null when the block has none.</summary>
public sealed record ScummV4Sound(int Id, int Room, byte[]? Speaker, byte[]? Adlib)
{
    /// <summary>AdLib music starts with 0x80 at byte 2; the rest are effects.</summary>
    public bool IsMusic => Adlib is { Length: >= 3 } ad && ad[2] == 0x80;

    /// <summary>The v5 (iMUSE) parts: the body of the <c>ROL </c>, <c>ADL </c> and <c>SPK </c> block, each starting at its <c>MDhd</c> block. Null when the sound has no such part (and always null outside v5).</summary>
    public byte[]? ImuseRoland { get; init; }
    public byte[]? ImuseAdlib { get; init; }
    public byte[]? ImuseSpeaker { get; init; }
    /// <summary>The sampled (<c>SBL </c>) version of an effect, see <see cref="ScummV5Digital"/>.</summary>
    public byte[]? ImuseDigital { get; init; }
}

/// <summary>One decoded OI image of a room object: the object's id, its size from the OC block and the picture as palette indices (the room's PA palette).</summary>
public sealed record ScummV4ObjectImage(int Id, int X, int Y, int Width, int Height, byte[] Pixels)
{
    /// <summary>The image's own depth mask (one byte per pixel, 0 or 1), or null when it carries none.</summary>
    public byte[]? Mask { get; init; }
}

public sealed record ScummV4Room(int Number, string Name, int Offset, int Length);

/// <summary>
/// The LOOM CD game's data (SCUMM v4): DISK01.LEC is XOR 0x69 and holds LE &gt; FO + one LF block per room; a block is
/// size u32 (header included), a two-letter tag, the body. 000.LFL is the index (plain blocks). Probe record:
/// .verification/loom-vga/README.md.
/// </summary>
public sealed class ScummV4Archive : IBrowsableArchive
{
    public const string IndexFile = "000.LFL";
    public const string ArchiveFile = "DISK01.LEC";
    private const byte Xor = 0x69;

    private readonly byte[] _lec;
    private readonly byte[] _index;
    private readonly Dictionary<int, ScummV4Room> _rooms;
    private readonly Dictionary<int, byte[]> _charsets = [];

    /// <summary>The per-room-file layout (Indiana Jones 256 colours) stores a costume pose's limb commands as bytes, not words (see <see cref="ScummV4CostumeRenderer.DecodePose"/>).</summary>
    private bool _byteCostumeCommands;

    /// <summary>True for the per-room-file layout (the 256-colour Indy3, a version 3 game): its scripts are not read with the v4 dialect.</summary>
    public bool IsRoomFileGame => _byteCostumeCommands;

    private ScummV4Archive(byte[] lec, byte[] index, IReadOnlyList<ScummV4Room> rooms)
    {
        _lec = lec;
        _index = index;
        Rooms = rooms;
        _rooms = rooms.ToDictionary(r => r.Number);
    }

    public IReadOnlyList<ScummV4Room> Rooms { get; }

    private string? _fingerprint;

    /// <summary>
    /// SHA-256 of the game data the scene lists depend on (the decoded <c>DISK01.LEC</c> and <c>000.LFL</c>), as one hex string; a shipped list is used only when this
    /// matches the one it was made from. The disc image's own bytes (a .bin) are not hashed: the files inside are what the scripts come from.
    /// </summary>
    public string Fingerprint => _fingerprint ??=
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(_lec)) + "-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(_index));

    /// <summary>The disc image the game was read from (its CD audio track carries the voices), or null for a folder of files.</summary>
    public DiscImage? Disc { get; private init; }

    public static bool IsVga(IGameFileSource source) => source.Exists(IndexFile) && source.Exists(ArchiveFile);

    public static ScummV4Archive Load(IGameFileSource source) => Load(source, ArchiveFile);

    /// <summary>The name of disk <paramref name="number"/>'s archive (<c>DISK02.LEC</c>); multi-disk games keep their rooms on several.</summary>
    public static string DiskFileName(int number) => $"DISK{number:00}.LEC";

    /// <summary>Loads one disk of a game: the room blocks of <paramref name="archiveFile"/>, with the index and charsets of the folder.</summary>
    public static ScummV4Archive Load(IGameFileSource source, string archiveFile)
    {
        if (!source.Exists(IndexFile) || !source.Exists(archiveFile)) throw new InvalidDataException($"{IndexFile} and {archiveFile} are both needed.");
        var lec = source.ReadAllBytes(archiveFile);
        for (var i = 0; i < lec.Length; i++) lec[i] ^= Xor;
        var index = source.ReadAllBytes(IndexFile);
        var names = ReadRoomNames(index);
        var top = Blocks(lec, 0, lec.Length).FirstOrDefault(b => b.Tag == "LE")
            ?? throw new InvalidDataException($"{archiveFile} has no LE block.");
        var rooms = new List<ScummV4Room>();
        foreach (var block in Blocks(lec, top.Start + 6, top.Start + top.Size).Where(b => b.Tag == "LF"))
        {
            var number = BinaryPrimitives.ReadUInt16LittleEndian(lec.AsSpan(block.Start + 6));
            rooms.Add(new ScummV4Room(number, names.GetValueOrDefault(number, ""), block.Start, block.Size));
        }
        var archive = new ScummV4Archive(lec, index, rooms) { Disc = (source as DiscFileSource)?.Image };
        for (var number = 1; number <= 4; number++)
            if (source.Exists($"90{number}.LFL")) archive._charsets[number] = source.ReadAllBytes($"90{number}.LFL");
        return archive;
    }

    /// <summary>
    /// The index of the per-room layout (Indiana Jones and the Last Crusade, 256 colours): <c>00.LFL</c> holds the same tagged blocks as <c>000.LFL</c>
    /// ("0R", "0S", "0C", "0N", "0O"), unencrypted, and each <c>NN.LFL</c> is the content of one room's LF block.
    /// </summary>
    public const string RoomFileIndex = "00.LFL";

    /// <summary>Whether the source is a v4 game stored as one plain <c>NN.LFL</c> per room: its <c>00.LFL</c> starts with a "0R" block (the old-v3 index is XOR 0xFF and does not).</summary>
    public static bool IsRoomFileLayout(IGameFileSource source)
    {
        if (!source.Exists(RoomFileIndex)) return false;
        var index = source.ReadAllBytes(RoomFileIndex);
        return index.Length >= 8 && index[4] == (byte)'0' && index[5] == (byte)'R' && BinaryPrimitives.ReadUInt32LittleEndian(index) >= 8;
    }

    /// <summary>
    /// Loads the per-room layout by wrapping every room file (one that starts with an RO block) in an LF block header, which makes it the same bytes as a
    /// disk's archive: the index offsets count from the first block inside the LF block, which is the start of the room file.
    /// </summary>
    public static ScummV4Archive LoadRoomFiles(IGameFileSource source)
    {
        if (!IsRoomFileLayout(source)) throw new InvalidDataException($"{RoomFileIndex} is not a v4 index.");
        var index = source.ReadAllBytes(RoomFileIndex);
        var all = new MemoryStream();
        var rooms = new List<ScummV4Room>();
        var header = new byte[8];
        for (var number = 1; number <= 99; number++)
        {
            var name = $"{number:00}.LFL";
            if (!source.Exists(name)) continue;
            var file = source.ReadAllBytes(name);
            if (file.Length < 6 || file[4] != (byte)'R' || file[5] != (byte)'O') continue;
            var start = (int)all.Length;
            BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)(8 + file.Length));
            header[4] = (byte)'L';
            header[5] = (byte)'F';
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6), (ushort)number);
            all.Write(header);
            all.Write(file);
            rooms.Add(new ScummV4Room(number, "", start, 8 + file.Length));
        }
        if (rooms.Count == 0) throw new InvalidDataException("No room file starts with an RO block.");
        return new ScummV4Archive(all.ToArray(), index, rooms) { _byteCostumeCommands = true };
    }

    /// <summary>Charset 1 to 4 (<c>901.LFL</c> to <c>904.LFL</c>), or null when the disc has no such file.</summary>
    public ScummV4Charset? ReadCharset(int number) =>
        _charsets.TryGetValue(number, out var file) ? ScummV4Charset.Parse(file, number) : null;

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
    public IReadOnlyList<ScummV4Script> ReadScripts(int number)
    {
        if (!_rooms.TryGetValue(number, out var room)) throw new KeyNotFoundException($"Room {number} is not in DISK01.LEC.");
        var scripts = new List<ScummV4Script>();
        foreach (var block in Blocks(_lec, room.Offset + 8, room.Offset + room.Length))
        {
            if (block.Tag == "SC") scripts.Add(new ScummV4Script("SC", number, scripts.Count(s => s.Kind == "SC"), _lec.AsSpan(block.Start + 6, block.Size - 6).ToArray(), block.Start));
            else if (block.Tag == "RO")
                foreach (var child in Blocks(_lec, block.Start + 6, block.Start + block.Size))
                {
                    if (child.Tag is "EN" or "EX") scripts.Add(new ScummV4Script(child.Tag, number, 0, _lec.AsSpan(child.Start + 6, child.Size - 6).ToArray(), child.Start));
                    else if (child.Tag == "LS") scripts.Add(new ScummV4Script("LS", number, _lec[child.Start + 6], _lec.AsSpan(child.Start + 7, child.Size - 7).ToArray(), child.Start));
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
    public IReadOnlyList<ScummV4Object> ReadObjects(int number)
    {
        if (!_rooms.TryGetValue(number, out var room)) throw new KeyNotFoundException($"Room {number} is not in DISK01.LEC.");
        var objects = new List<ScummV4Object>();
        var ro = Blocks(_lec, room.Offset + 8, room.Offset + room.Length).FirstOrDefault(b => b.Tag == "RO");
        if (ro is null) return objects;
        foreach (var oc in Blocks(_lec, ro.Start + 6, ro.Start + ro.Size).Where(b => b.Tag == "OC"))
        {
            if (oc.Size < 21) continue;
            var block = _lec.AsSpan(oc.Start, oc.Size).ToArray();
            var verbs = new List<ScummV4Verb>();
            var p = 19;
            while (p + 3 <= block.Length && block[p] != 0)
            {
                verbs.Add(new ScummV4Verb(block[p], BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(p + 1))));
                p += 3;
            }
            var nameStart = block[18];
            var name = "";
            if (nameStart < block.Length)
            {
                var end = Array.IndexOf(block, (byte)0, nameStart);
                name = Encoding.Latin1.GetString(block, nameStart, (end < 0 ? block.Length : end) - nameStart);
            }
            objects.Add(new ScummV4Object(BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(6)), block[9] * 8, (block[10] & 0x7F) * 8, block[11] * 8,
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
    public ScummV4Script? ReadGlobalScript(int number)
    {
        foreach (var (id, room, block) in ReadDirectory("0S"))
        {
            if (id != number) continue;
            if (block + 6 > _lec.Length || Encoding.Latin1.GetString(_lec, block + 4, 2) != "SC") return null;
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(_lec.AsSpan(block));
            if (size < 6 || block + size > _lec.Length) return null;
            return new ScummV4Script("SC", room, number, _lec.AsSpan(block + 6, size - 6).ToArray(), block);
        }
        return null;
    }

    /// <summary>
    /// The room's OI object images. An OI block is the object id (u16) and a BM-like body: a size dword, one dword offset per 8-pixel strip
    /// (counted from the size field), then the strips (the codecs of the room background). The width and height come from the object's OC
    /// block, which must exist; an image that does not decode is left out. The same object may have several OI blocks (its states, in file order).
    /// </summary>
    public IReadOnlyList<ScummV4ObjectImage> ReadObjectImages(int number)
    {
        if (!_rooms.TryGetValue(number, out var room)) throw new KeyNotFoundException($"Room {number} is not in DISK01.LEC.");
        var ro = Blocks(_lec, room.Offset + 8, room.Offset + room.Length).FirstOrDefault(b => b.Tag == "RO");
        if (ro is null) return [];
        var objects = ReadObjects(number).GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First());
        var images = new List<ScummV4ObjectImage>();
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
            images.Add(new ScummV4ObjectImage(id, owner.X, owner.Y, owner.Width, owner.Height, pixels)
            {
                Mask = ReadMaskPlane(_lec, body, end, owner.Width, owner.Height)
            });
        }
        return images;
    }

    /// <summary>
    /// The index's object table ("0O"): per object four bytes, three of class bits (little endian) and one packing the state (high nibble)
    /// and the owner (low nibble), as the EGA index does. Every room object starts with owner 0xF and state 0.
    /// </summary>
    public ScummIndexedObjectSnapshot ReadIndexedObjects()
    {
        var states = new Dictionary<int, byte>();
        var owners = new Dictionary<int, byte>();
        var classes = new Dictionary<int, uint>();
        var block = Blocks(_index, 0, _index.Length).FirstOrDefault(b => b.Tag == "0O");
        if (block is null) return new ScummIndexedObjectSnapshot(states, owners, classes);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(_index.AsSpan(block.Start + 6));
        for (var id = 0; id < count && block.Start + 8 + (id + 1) * 4 <= block.Start + block.Size; id++)
        {
            var entry = block.Start + 8 + id * 4;
            states[id] = (byte)(_index[entry + 3] >> 4);
            owners[id] = (byte)(_index[entry + 3] & 0x0F);
            classes[id] = (uint)(_index[entry] | _index[entry + 1] << 8 | _index[entry + 2] << 16);
        }
        return new ScummIndexedObjectSnapshot(states, owners, classes);
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

    public IReadOnlyList<BrowsedObjectLabel> ReadObjectLabels(int number)
    {
        try { return ReadObjects(number).Where(o => o.Name.Length > 0 && o.Width > 0 && o.Height > 0).Select(o => new BrowsedObjectLabel(o.Id, o.Name, o.X, o.Y, o.Width, o.Height)).ToList(); }
        catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException) { return []; }
    }

    public ScummWalkBoxSet? ReadWalkBoxes(int number)
    {
        // The 256-colour Indy (per-room files) is a version 3 game in ScummVM: 18-byte boxes without the scale word.
        try { return ReadWalkBoxBlock(number) is { } body ? ScummWalkBoxReader.ReadBlockBody(body, _byteCostumeCommands ? ScummWalkBoxReader.BoxSize : ScummWalkBoxReader.BoxSizeV5) : null; }
        catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException) { return null; }
    }

    /// <summary>The room's SA block body (the scale slots: scale, y, scale, y per slot), or null when the room has none.</summary>
    public byte[]? ReadScaleSlotBlock(int number)
    {
        if (!_rooms.TryGetValue(number, out var room)) throw new KeyNotFoundException($"Room {number} is not in DISK01.LEC.");
        var ro = Blocks(_lec, room.Offset + 8, room.Offset + room.Length).FirstOrDefault(b => b.Tag == "RO");
        var sa = ro is null ? null : Blocks(_lec, ro.Start + 6, ro.Start + ro.Size).FirstOrDefault(b => b.Tag == "SA");
        return sa is null ? null : _lec.AsSpan(sa.Start + 6, sa.Size - 6).ToArray();
    }

    /// <summary>The costumes the index lists (those whose block is a CO block).</summary>
    public IReadOnlyList<ScummV4Costume> ReadCostumes()
    {
        var costumes = new List<ScummV4Costume>();
        foreach (var (id, room, block) in ReadDirectory("0C"))
        {
            if (block + 6 > _lec.Length || Encoding.Latin1.GetString(_lec, block + 4, 2) != "CO") continue;
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(_lec.AsSpan(block));
            if (size < 6 || block + size > _lec.Length) continue;
            costumes.Add(new ScummV4Costume(id, room, _lec.AsSpan(block + 6, size - 6).ToArray()));
        }
        return costumes;
    }

    /// <summary>
    /// The sounds the "0N" directory places in an SO block (30 here; the directory's entries for 24 and 32 point at something
    /// else and are left out). An SO block holds a <c>WA</c> block (PC speaker, whole block kept: the channel pointers count
    /// from its start) and an <c>AD</c> block (AdLib, body only), the same two parts as the EGA bundle.
    /// </summary>
    public IReadOnlyList<ScummV4Sound> ReadSounds()
    {
        var sounds = new List<ScummV4Sound>();
        foreach (var (id, room, block) in ReadDirectory("0N"))
        {
            if (block + 6 > _lec.Length || Encoding.Latin1.GetString(_lec, block + 4, 2) != "SO") continue;
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(_lec.AsSpan(block));
            if (size < 6 || block + size > _lec.Length) continue;
            byte[]? speaker = null, adlib = null;
            foreach (var part in SoundParts(block + 6, block + size))
            {
                if (part.Tag == "WA") speaker ??= _lec.AsSpan(part.Start, part.Size).ToArray();
                else if (part.Tag == "AD") adlib ??= _lec.AsSpan(part.Start + 6, part.Size - 6).ToArray();
            }
            sounds.Add(new ScummV4Sound(id, room, speaker, adlib));
        }
        return sounds.OrderBy(s => s.Id).ToArray();
    }

    /// <summary>The parts of an SO block; the older 256-colour Indiana Jones wraps them in a second SO block, whose parts count the same.</summary>
    private IEnumerable<Block> SoundParts(int start, int end)
    {
        foreach (var part in Blocks(_lec, start, end))
        {
            if (part.Tag == "SO")
                foreach (var inner in SoundParts(part.Start + 6, part.Start + part.Size)) yield return inner;
            else yield return part;
        }
    }

    /// <summary>The costume with this number, or null.</summary>
    public ScummV4Costume? FindCostume(int id) => ReadCostumes().FirstOrDefault(c => c.Id == id);

    /// <summary>Every stored pose of a costume that decodes (pose index and picture, in the palette of the room that stores the costume).</summary>
    public IReadOnlyList<(int Pose, RoomImage Image)> DecodeCostumePoses(ScummV4Costume costume)
    {
        var palette = ReadPalette(costume.Room);
        var poses = new List<(int, RoomImage)>();
        foreach (var pose in ScummV4CostumeRenderer.PosesWithData(costume.Body))
        {
            try
            {
                if (ScummV4CostumeRenderer.DecodePose(costume.Body, pose, palette, _byteCostumeCommands) is { } image) poses.Add((pose, image));
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
        var paBlock = Blocks(_lec, ro.Start + 6, ro.Start + ro.Size).FirstOrDefault(b => b.Tag == "PA");
        if (paBlock is null) return ScummV3EgaRoomDecoder.EgaPalette;
        var pa = paBlock.Start + 6;
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

        // A room without a PA block is a 16-colour (EGA) room: the fixed EGA palette and the EGA strip codes.
        if (children.All(b => b.Tag != "PA")) return DecodeEgaRoom(number, width, height, Child("BM"));
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
        var mask = ReadMaskPlane(_lec, body, end, width, height, out var next);
        // More z-planes follow the first, each the same block (length word, strip offsets, rows): Room 25's house is plane 2.
        var extra = new List<RoomDepthMask>();
        while (mask is not null && next > 0 && ReadMaskPlane(_lec, body, end, width, height, out next, next) is { } more) extra.Add(new RoomDepthMask(width, height, more));
        return new RoomImage(width, height, pixels, palette) { DepthMask = mask is null ? null : new RoomDepthMask(width, height, mask), ExtraDepthMasks = extra };
    }

    /// <summary>
    /// The background of a 16-colour room: the BM body is a length word (where the depth masks start), a word offset per 8-pixel strip (counted from the
    /// body's start) and the strips, which use the old-v3 EGA strip code. The depth masks are not read.
    /// </summary>
    private RoomImage DecodeEgaRoom(int number, int width, int height, Block bm)
    {
        var body = bm.Start + 6;
        var end = bm.Start + bm.Size;
        var strips = width / 8;
        if (bm.Size < 6 + 2 + strips * 2) throw new InvalidDataException($"Room {number} has a bitmap too short for its {strips} strips.");
        var imageEnd = Math.Min(end, body + BinaryPrimitives.ReadUInt16LittleEndian(_lec.AsSpan(body)));
        var pixels = new byte[width * height];
        for (var strip = 0; strip < strips; strip++)
        {
            var relative = BinaryPrimitives.ReadUInt16LittleEndian(_lec.AsSpan(body + 2 + strip * 2));
            if (relative < 2 + strips * 2 || body + relative >= imageEnd) throw new InvalidDataException($"Room {number} strip {strip} starts outside its bitmap.");
            ScummV3EgaRoomDecoder.DecodeStrip(_lec, body + relative, imageEnd, pixels, width, height, strip);
        }
        return new RoomImage(width, height, pixels, ScummV3EgaRoomDecoder.EgaPalette);
    }

    /// <summary>
    /// The depth mask (z-plane 1) a BM or OI body carries after its strips: the body's size field counts the strips, and what follows is a
    /// word (the length of the mask block, itself included), one word per 8-pixel strip (offset from the block's start; 0 = nothing masked),
    /// and per strip run-length rows of 8 mask bits, the EGA mask's coding (a code with bit 7: repeat the next byte; otherwise copy that many
    /// bytes; 0 means 256). Null when the bitmap has no mask block (the size field reaches the end) or the block does not read.
    /// One byte per pixel, 0 or 1.
    /// </summary>
    private static byte[]? ReadMaskPlane(byte[] data, int body, int end, int width, int height) => ReadMaskPlane(data, body, end, width, height, out _);

    /// <summary>The plane at <paramref name="at"/> (the first plane when 0); <paramref name="next"/> is where a further plane would start (0 when the block ends the body).</summary>
    private static byte[]? ReadMaskPlane(byte[] data, int body, int end, int width, int height, out int next, int at = 0)
    {
        next = 0;
        var strips = width / 8;
        var start = at > 0 ? at : body + (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(body));
        if (start + 2 + strips * 2 > end) return null;
        var blockEnd = Math.Min(end, start + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(start)));
        if (blockEnd - start >= 2 + strips * 2 && end - blockEnd >= 2 + strips * 2) next = blockEnd;
        var pixels = new byte[width * height];
        for (var strip = 0; strip < strips; strip++)
        {
            var offset = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(start + 2 + strip * 2));
            if (offset == 0) continue;
            var pos = start + offset;
            if (offset < 2 + strips * 2 || pos >= blockEnd) return null;
            for (var row = 0; row < height;)
            {
                if (pos >= blockEnd) return null;
                var command = data[pos++];
                var run = command & 0x7F;
                if (run == 0) run = 256;
                var rows = Math.Min(run, height - row);
                if ((command & 0x80) != 0)
                {
                    if (pos >= blockEnd) return null;
                    var value = data[pos++];
                    for (var i = 0; i < rows; i++) WriteMaskRow(pixels, width, strip, row++, value);
                }
                else
                {
                    if (pos + rows > blockEnd) return null;
                    for (var i = 0; i < rows; i++) WriteMaskRow(pixels, width, strip, row++, data[pos++]);
                }
            }
        }
        return pixels;
    }

    private static void WriteMaskRow(byte[] pixels, int width, int strip, int row, byte bits)
    {
        for (var x = 0; x < 8; x++) pixels[row * width + strip * 8 + x] = (byte)((bits & (0x80 >> x)) != 0 ? 1 : 0);
    }

    /// <summary>
    /// One 8-pixel-wide strip, the codes this game uses: 0x01 raw (column by column) and the "basic" bit codecs, vertical
    /// 0x0E-0x12 or horizontal 0x18-0x1C (code mod 10 = colour bits). Bits: 0 same colour; 10 a new colour; 110 colour += step;
    /// 111 step = -step, then colour += step (ScummVM's drawStripBasicV/H).
    /// </summary>
    internal static void DecodeStrip(byte[] data, int pos, int end, byte[] pixels, int width, int height, int x0)
    {
        var code = data[pos++];
        // Version 5 adds the transparent twins of the bit codes (0x22-0x26 vertical, 0x2C-0x30 horizontal: drawn like the plain ones, the transparent colour kept) and the
        // major-minor codes (0x40-0x44, 0x54-0x58 transparent, 0x68-0x6C run, 0x7C-0x80 run transparent); a still picture draws them all.
        if (code is >= 0x22 and <= 0x26) code = (byte)(code - 0x22 + 0x0E);
        else if (code is >= 0x2C and <= 0x30) code = (byte)(code - 0x2C + 0x18);
        else if (code is >= 0x40 and <= 0x44 or >= 0x54 and <= 0x58 or >= 0x68 and <= 0x6C or >= 0x7C and <= 0x80)
        {
            DecodeMajorMinorStrip(code % 10, data, pos, end, pixels, width, height, x0);
            return;
        }
        if (code == 0x01)
        {
            for (var x = 0; x < 8; x++)
                for (var y = 0; y < height; y++) pixels[y * width + x0 + x] = data[pos++];
            return;
        }
        if (code is 0x02 or 0x03 or 0x04 or 0x07)
        {
            DecodeOld256Strip(code, data, pos, end, pixels, width, height, x0);
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

    /// <summary>
    /// A major-minor strip (ScummVM <c>MajMinCodec</c>): row by row, eight pixels per row. The first byte is the colour, then bits: 0 keeps the colour; 10 a new colour of
    /// <paramref name="shift"/> bits; 11 and three bits, minus four, a colour step, where a step of zero means "repeat the colour for the next byte's count of pixels".
    /// </summary>
    private static void DecodeMajorMinorStrip(int shift, byte[] data, int pos, int end, byte[] pixels, int width, int height, int x0)
    {
        if (pos + 3 > data.Length) throw new InvalidDataException("A strip ends before its picture does.");
        var color = data[pos];
        var bits = data[pos + 1] | data[pos + 2] << 8;
        var numBits = 16;
        pos += 3;
        var repeat = false;
        var repeatCount = 0;
        int Read(int count)
        {
            if (numBits <= 8)
            {
                if (pos >= data.Length) throw new InvalidDataException("A strip ends before its picture does.");
                bits |= data[pos++] << numBits;
                numBits += 8;
            }
            var value = bits & ((1 << count) - 1);
            numBits -= count;
            bits = (bits >> count) & 0xFFFF;
            return value;
        }
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                pixels[y * width + x0 + x] = color;
                if (!repeat)
                {
                    if (Read(1) == 0) continue;
                    if (Read(1) == 1)
                    {
                        var difference = Read(3) - 4;
                        if (difference != 0) color = (byte)(color + difference);
                        else { repeat = true; repeatCount = Read(8) - 1; }
                    }
                    else color = (byte)Read(shift);
                }
                else if (--repeatCount == 0) repeat = false;
            }
        }
    }

    /// <summary>
    /// One strip of the older 256-colour games (Indiana Jones 256 colours; ScummVM's unkDecode8 to 11), column by column: 0x02 runs (a count byte plus one,
    /// then the colour), 0x03 four-bit codes (repeat a nibble, a list of nibbles, or the high nibble of the following colours), 0x04 a local palette
    /// with runs for the codes past it, 0x07 one bit-coded colour that steps up or down or is set anew.
    /// </summary>
    private static void DecodeOld256Strip(byte code, byte[] data, int pos, int end, byte[] pixels, int width, int height, int x0)
    {
        int column = 0, row = 0;
        // Writes one pixel and moves down the column; false once the strip's eighth column is full.
        bool Put(int color)
        {
            pixels[row * width + x0 + column] = (byte)color;
            if (++row < height) return true;
            row = 0;
            return ++column < 8;
        }
        byte Next() => pos < end ? data[pos++] : throw new InvalidDataException("A strip ends before its picture does.");
        int buffer = 0, mask = 128;
        int Bit()
        {
            if ((mask <<= 1) == 256) { buffer = Next(); mask = 1; }
            return (buffer & mask) != 0 ? 1 : 0;
        }
        int Bits(int count)
        {
            var value = 0;
            for (var i = 0; i < count; i++) value += Bit() << i;
            return value;
        }
        switch (code)
        {
            case 0x02:
                while (true)
                {
                    var run = Next() + 1;
                    var color = Next();
                    for (; run > 0; run--) if (!Put(color)) return;
                }
            case 0x03:
                var high = 0;
                while (true)
                {
                    var c = Bits(4);
                    switch (c >> 2)
                    {
                        case 0:
                            var repeated = Bits(4);
                            for (var i = 0; i < (c & 3) + 2; i++) if (!Put(high * 16 + repeated)) return;
                            break;
                        case 1:
                            for (var i = 0; i < (c & 3) + 1; i++) if (!Put(high * 16 + Bits(4))) return;
                            break;
                        case 2:
                            high = Bits(4);
                            break;
                    }
                }
            case 0x04:
                int colors = Next();
                var local = new byte[colors];
                for (var i = 0; i < colors; i++) local[i] = Next();
                while (true)
                {
                    int entry = Next();
                    if (entry < colors) { if (!Put(local[entry])) return; continue; }
                    var run = entry - colors + 1;
                    var color = Next();
                    for (; run > 0; run--) if (!Put(color)) return;
                }
            default:
                byte shade = Next();
                var increment = 1;
                for (var pixel = 0; pixel < 8 * height; pixel++)
                {
                    Put(shade);
                    var ones = 0;
                    while (ones < 3 && Bit() == 1) ones++;
                    switch (ones)
                    {
                        case 1: increment = -increment; shade = (byte)(shade - increment); break;
                        case 2: shade = (byte)(shade - increment); break;
                        case 3: increment = 1; shade = (byte)Bits(8); break;
                    }
                }
                break;
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
