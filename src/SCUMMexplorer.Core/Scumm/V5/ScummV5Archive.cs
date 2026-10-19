using System.Buffers.Binary;
using System.Text;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>Which files hold a v5 game: the stem shared by <c>NAME.000</c> (the index) and <c>NAME.001</c> (the resources), with the folder in front on a disc, and how many rooms the index lists.</summary>
public sealed record ScummV5Game(string Stem, int RoomCount);

/// <summary>A sound of the CD game that plays a track of the disc: the sound's number, the room that stores it, the track, its loop count (0xFF: for ever) and the stretch (in 1/75 s) played.</summary>
public sealed record ScummCdTrackSound(int SoundId, int Room, int Track, int Loops, int StartFrame, int EndFrame);

/// <summary>A v5 script block: kind (<c>SCRP</c>, <c>LSCR</c>, <c>ENCD</c>, <c>EXCD</c>), number (the room number for entry and exit), room, position in the resource file and bytecode.</summary>
public sealed record ScummV5Script(string Kind, int Number, int Room, int Position, byte[] Code);

/// <summary>One verb of a v5 object: its <c>VERB</c> block (offsets are from its start), the verb number and where its code starts in the block.</summary>
public sealed record ScummV5ObjectVerb(int ObjectId, string? ObjectName, int VerbId, int StartOffset, int Position, byte[] Block);

/// <summary>
/// A SCUMM v5 game (Indiana Jones and the Fate of Atlantis, Monkey Island 1 CD; ScummVM <c>readIndexFile</c> for version 5): <c>NAME.000</c> is the index and
/// <c>NAME.001</c> the resources, every byte XOR 0x69; blocks are a four-letter tag and a big-endian size that counts its 8-byte header. The index has
/// <c>RNAM</c> (room names), <c>MAXS</c> and the directories <c>DROO DSCR DSOU DCOS DCHR DOBJ</c>: a word count, a room byte per resource, then a little-endian dword offset
/// per resource. <c>NAME.001</c> is <c>LECF</c> holding <c>LOFF</c> (each room's offset: the first block inside its <c>LFLF</c>) and the <c>LFLF</c> blocks, which hold
/// <c>ROOM SCRP SOUN COST CHAR</c>; a resource of the directories sits at its room's offset plus its own. Read for browsing: rooms (picture, palette, depth masks) and costumes.
/// </summary>
public sealed partial class ScummV5Archive : IBrowsableArchive
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
    /// <param name="source">The folder or disc.</param>
    /// <param name="folder">S9 (18 October 2026): a disc folder to read the game from (Monkey Island 1 CD carries ENGLISH, FRENCH, GERMAN, ITALIAN and SPANISH,
    /// each with its own NAME.000/.001); null keeps the default choice (the most rooms, ENGLISH on a tie).</param>
    public static ScummV5Game? FindGame(IGameFileSource source, string? folder = null)
    {
        ScummV5Game? best = null;
        foreach (var name in source.FileNames.Where(n => n.EndsWith(".000", StringComparison.OrdinalIgnoreCase) &&
                                                         (folder is null || n.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))))
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

    /// <summary>
    /// Charset <paramref name="number"/> from the <c>DCHR</c> directory, or null when the index has none: a <c>CHAR</c> block whose body (after its 8-byte header) is the
    /// font of <see cref="ScummV4Charset"/> (ScummVM <c>CharsetRendererCommon::setCurID</c>: the font starts 29 bytes into a version 5 resource, 8 header bytes and 21).
    /// </summary>
    public ScummV4Charset? ReadCharset(int number)
    {
        foreach (var (id, _, position) in ReadDirectory("DCHR"))
        {
            if (id != number || position + 8 > _resources.Length || Encoding.Latin1.GetString(_resources, position, 4) != "CHAR") continue;
            var size = (int)BinaryPrimitives.ReadUInt32BigEndian(_resources.AsSpan(position + 4));
            if (size < 8 || position + size > _resources.Length) return null;
            return ScummV4Charset.Parse(_resources.AsSpan(position + 8, size - 8).ToArray(), number);
        }
        return null;
    }

    /// <summary>
    /// The sounds that start a track of the game's CD (Monkey Island 1 CD): a <c>SOUN</c> block without a <c>SOU </c> part whose bytes 24 to 31 are the track number, the loop
    /// count (0xFF: for ever) and the start and end as minutes, seconds and frames (ScummVM <c>soundcd.cpp</c>, <c>getCDTrackIdFromSoundId</c>).
    /// </summary>
    public IReadOnlyList<ScummCdTrackSound> ReadCdTrackSounds()
    {
        var sounds = new List<ScummCdTrackSound>();
        foreach (var (id, room, position) in ReadDirectory("DSOU"))
        {
            if (position + 32 > _resources.Length || Encoding.Latin1.GetString(_resources, position, 4) != "SOUN") continue;
            var size = (int)BinaryPrimitives.ReadUInt32BigEndian(_resources.AsSpan(position + 4));
            if (size < 32 || size > 40 || Encoding.Latin1.GetString(_resources, position + 8, 4) == "SOU ") continue;
            int Frames(int at) => (_resources[position + at] * 60 + _resources[position + at + 1]) * 75 + _resources[position + at + 2];
            sounds.Add(new ScummCdTrackSound(id, room, _resources[position + 24], _resources[position + 25], Frames(26), Frames(29)));
        }
        return sounds;
    }

    /// <summary>
    /// The sounds: <c>SOUN</c> &gt; <c>SOU </c> &gt; the parts of the sound cards (<c>ROL </c> MT-32, <c>ADL </c> AdLib, <c>SPK </c> PC speaker; <c>SBL </c> digital is left out), each an
    /// <c>MDhd</c> block and a standard MIDI file with iMUSE system exclusives (see <see cref="ScummImuseSong"/>). Each sound holds the music for every card.
    /// </summary>
    public IReadOnlyList<ScummV4Sound> ReadSounds()
    {
        var sounds = new List<ScummV4Sound>();
        foreach (var (id, room, position) in ReadDirectory("DSOU"))
        {
            if (position + 8 > _resources.Length || Encoding.Latin1.GetString(_resources, position, 4) != "SOUN") continue;
            var size = (int)BinaryPrimitives.ReadUInt32BigEndian(_resources.AsSpan(position + 4));
            if (size < 8 || position + size > _resources.Length) continue;
            byte[]? rol = null, adl = null, spk = null, sbl = null;
            // SOUN > SOU > the parts of every card (ROL, ADL, SPK, or SBL). A part that starts with an MDhd block states its size 8 bytes short (the
            // MDhd size counts its body only), so the next part starts 8 bytes after where the size says; SOU and SOUN carry the same slip.
            var end = position + size;
            var p = position + 8;
            if (p + 8 <= end && Encoding.Latin1.GetString(_resources, p, 4) == "SOU ") p += 8;
            while (p + 12 <= end)
            {
                var tag = Encoding.Latin1.GetString(_resources, p, 4);
                var partSize = (int)BinaryPrimitives.ReadUInt32BigEndian(_resources.AsSpan(p + 4));
                if (partSize < 8) break;
                var inner = Encoding.Latin1.GetString(_resources, p + 8, 4);
                var mdhd = inner is "MDhd" or "AUhd" or "WVhd";
                var length = partSize + (mdhd ? 8 : 0);
                if (p + length > _resources.Length) break;
                var body = _resources.AsSpan(p + 8, length - 8).ToArray();
                switch (tag)
                {
                    case "ROL ": rol ??= body; break;
                    case "ADL ": adl ??= body; break;
                    case "SPK ": spk ??= body; break;
                    case "SBL ": sbl ??= body; break;
                }
                p += length;
            }
            if (rol is null && adl is null && spk is null && sbl is null) continue;
            sounds.Add(new ScummV4Sound(id, room, null, null) { ImuseRoland = rol, ImuseAdlib = adl, ImuseSpeaker = spk, ImuseDigital = sbl });
        }
        return sounds.OrderBy(s => s.Id).ToArray();
    }

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
        // The depth masks are the ZP01.. blocks after SMAP in the same IM00: a word per strip at offset 8 (counted from the block), the rows coded as in v4.
        var planes = Blocks(_resources, im00.Start + 8, im00.Start + im00.Size).Where(b => b.Tag.StartsWith("ZP", StringComparison.Ordinal) && b.Tag != "ZP00")
            .OrderBy(b => b.Tag, StringComparer.Ordinal)
            .Select(b => ScummV4Archive.DecodeMaskStrips(_resources, b.Start, 8, b.Start + b.Size, width, height))
            .TakeWhile(plane => plane is not null).ToArray();
        return new RoomImage(width, height, pixels, palette)
        {
            DepthMask = planes.Length > 0 ? new RoomDepthMask(width, height, planes[0]!) : null,
            ExtraDepthMasks = planes.Skip(1).Select(plane => new RoomDepthMask(width, height, plane!)).ToArray()
        };
    }

    /// <summary>
    /// The room's objects (ScummVM <c>resetRoomObject</c>, version 5): per object an <c>OBCD</c> block (<c>CDHD</c>: id word, x, y, width and height in 8-pixel units, flags, parent,
    /// walk-to point as words, facing; then <c>VERB</c> and <c>OBNA</c>, the zero-ended name) and an <c>OBIM</c> block (<c>IMHD</c> with the id and image count, then <c>IM01</c>,
    /// <c>IM02</c>… one per state, each an <c>SMAP</c> like the room's, sized by the CDHD). An image that does not decode is left out.
    /// </summary>
    public BrowsedObjects ReadBrowsedObjects(int number)
    {
        var room = Room(number);
        var children = Blocks(_resources, room.Start + 8, room.Start + room.Size);
        var records = new List<ScummRoomObjectRecord>();
        var sizes = new Dictionary<int, (int X, int Y, int W, int H)>();
        var names = new Dictionary<int, string>();
        foreach (var obcd in children.Where(b => b.Tag == "OBCD"))
        {
            var parts = Blocks(_resources, obcd.Start + 8, obcd.Start + obcd.Size);
            if (parts.FirstOrDefault(b => b.Tag == "CDHD") is not { } cdhd || cdhd.Size < 8 + 13) continue;
            var h = cdhd.Start + 8;
            var id = BinaryPrimitives.ReadUInt16LittleEndian(_resources.AsSpan(h));
            var (x, y, w, ht) = (_resources[h + 2] * 8, _resources[h + 3] * 8, _resources[h + 4] * 8, _resources[h + 5] * 8);
            var flags = _resources[h + 6];
            sizes[id] = (x, y, w, ht);
            records.Add(new ScummRoomObjectRecord(id, records.Count + 1, x, y, w, ht, _resources[h + 7], (byte)(flags == 0x80 ? 1 : flags & 0xF),
                BinaryPrimitives.ReadInt16LittleEndian(_resources.AsSpan(h + 8)), BinaryPrimitives.ReadInt16LittleEndian(_resources.AsSpan(h + 10)), (byte)(_resources[h + 12] & 7),
                obcd.Start - room.Start, null));
            if (parts.FirstOrDefault(b => b.Tag == "OBNA") is { } obna)
            {
                var end = Array.IndexOf(_resources, (byte)0, obna.Start + 8, obna.Size - 8);
                var name = Encoding.Latin1.GetString(_resources, obna.Start + 8, (end < 0 ? obna.Start + obna.Size : end) - obna.Start - 8);
                if (name.Length > 0) names[id] = name;
            }
        }
        var images = new List<RoomObjectImage>();
        foreach (var obim in children.Where(b => b.Tag == "OBIM"))
        {
            var parts = Blocks(_resources, obim.Start + 8, obim.Start + obim.Size);
            if (parts.FirstOrDefault(b => b.Tag == "IMHD") is not { } imhd || imhd.Size < 10) continue;
            var id = BinaryPrimitives.ReadUInt16LittleEndian(_resources.AsSpan(imhd.Start + 8));
            if (!sizes.TryGetValue(id, out var box) || box.W < 8 || box.H < 1) continue;
            foreach (var state in parts.Where(b => b.Tag.Length == 4 && b.Tag.StartsWith("IM", StringComparison.Ordinal) && b.Tag != "IMHD"))
            {
                if (Blocks(_resources, state.Start + 8, state.Start + state.Size).FirstOrDefault(b => b.Tag == "SMAP") is not { } smap) continue;
                var pixels = new byte[box.W * box.H];
                try
                {
                    for (var strip = 0; strip < box.W / 8; strip++)
                    {
                        var start = smap.Start + (int)BinaryPrimitives.ReadUInt32LittleEndian(_resources.AsSpan(smap.Start + 8 + strip * 4));
                        if (start >= smap.Start + smap.Size) throw new InvalidDataException("strip outside the image");
                        ScummV4Archive.DecodeStrip(_resources, start, smap.Start + smap.Size, pixels, box.W, box.H, strip * 8);
                    }
                }
                catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException) { continue; }
                images.Add(new RoomObjectImage(id, box.X, box.Y, box.W, box.H, pixels));
            }
        }
        var imaged = images.Select(i => i.Id).ToHashSet();
        return new BrowsedObjects(records.Select(r => imaged.Contains(r.Id) ? r with { ImageOffset = r.CodeOffset } : r).ToArray(), BrowsedObjects.NumberStates(images), names);
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

    /// <summary>
    /// The room's <c>OBCD</c> blocks: <c>CDHD</c> (object number word, x, y, width and height in 8-pixel units, flags, parent, walk point, facing) and <c>OBNA</c> (the name).
    /// </summary>
    public IReadOnlyList<BrowsedObjectLabel> ReadObjectLabels(int number)
    {
        var labels = new List<BrowsedObjectLabel>();
        try
        {
            // Every object with a box, named or not, whose parent chain is in the required state at the start. The parent byte of CDHD counts the room's
            // objects from 1; the start state of the room's objects is taken as 0 (the DOBJ state nibbles are not read here).
            var all = new List<(BrowsedObjectLabel Label, int Parent, int ParentState)>();
            var room = Room(number);
            foreach (var obcd in Blocks(_resources, room.Start + 8, room.Start + room.Size).Where(b => b.Tag == "OBCD"))
            {
                var parts = Blocks(_resources, obcd.Start + 8, obcd.Start + obcd.Size);
                if (parts.FirstOrDefault(b => b.Tag == "CDHD") is not { Size: >= 18 } header) continue;
                var at = header.Start + 8;
                var name = "";
                if (parts.FirstOrDefault(b => b.Tag == "OBNA") is { } nameBlock)
                {
                    var nameEnd = Array.IndexOf(_resources, (byte)0, nameBlock.Start + 8, nameBlock.Size - 8);
                    if (nameEnd > nameBlock.Start + 8)
                        name = new string(Encoding.Latin1.GetString(_resources, nameBlock.Start + 8, nameEnd - nameBlock.Start - 8).Where(c => c >= ' ').ToArray());
                }
                all.Add((new BrowsedObjectLabel(BinaryPrimitives.ReadUInt16LittleEndian(_resources.AsSpan(at)), name, _resources[at + 2] * 8,
                    (_resources[at + 3] & 0x7F) * 8, _resources[at + 4] * 8, _resources[at + 5] * 8), _resources[at + 7], _resources[at + 3] >> 7));
            }
            bool ParentsMatch(int index)
            {
                for (var steps = 0; all[index].Parent != 0; steps++)
                {
                    if (steps > all.Count || all[index].Parent > all.Count) return false;
                    if (all[index].ParentState != 0) return false;
                    index = all[index].Parent - 1;
                }
                return true;
            }
            for (var index = 0; index < all.Count; index++)
                if (all[index].Label.Width > 0 && all[index].Label.Height > 0 && ParentsMatch(index)) labels.Add(all[index].Label);
        }
        catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException) { }
        return labels;
    }

    /// <summary>The global scripts (<c>DSCR</c> directory, <c>SCRP</c> blocks): number, room that stores it, position in the resource file and bytecode.</summary>
    public IReadOnlyList<ScummV5Script> ReadGlobalScripts()
    {
        var scripts = new List<ScummV5Script>();
        foreach (var (id, room, position) in ReadDirectory("DSCR"))
        {
            if (position + 8 > _resources.Length || Encoding.Latin1.GetString(_resources, position, 4) != "SCRP") continue;
            var size = (int)BinaryPrimitives.ReadUInt32BigEndian(_resources.AsSpan(position + 4));
            if (size < 8 || position + size > _resources.Length) continue;
            scripts.Add(new ScummV5Script("SCRP", id, room, position, _resources.AsSpan(position + 8, size - 8).ToArray()));
        }
        return scripts;
    }

    /// <summary>A room's own code: its entry (<c>ENCD</c>) and exit (<c>EXCD</c>) scripts and its local scripts (<c>LSCR</c>, a script number byte and the code).</summary>
    public IReadOnlyList<ScummV5Script> ReadRoomScripts(int number)
    {
        var scripts = new List<ScummV5Script>();
        try
        {
            var room = Room(number);
            foreach (var block in Blocks(_resources, room.Start + 8, room.Start + room.Size))
            {
                if (block.Tag is "ENCD" or "EXCD")
                    scripts.Add(new ScummV5Script(block.Tag, number, number, block.Start, _resources.AsSpan(block.Start + 8, block.Size - 8).ToArray()));
                else if (block.Tag == "LSCR" && block.Size > 9)
                    scripts.Add(new ScummV5Script("LSCR", _resources[block.Start + 8], number, block.Start, _resources.AsSpan(block.Start + 9, block.Size - 9).ToArray()));
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException) { }
        return scripts;
    }

    /// <summary>
    /// The verb scripts of a room's objects (<c>OBCD</c> > <c>VERB</c>: (verb, word offset) entries ending in 0, offsets from the start of the <c>VERB</c> block, then the code);
    /// each carries the whole VERB block as its code and the entry point in <see cref="ScummV5ObjectVerb.StartOffset"/>, because a verb may jump back into earlier code.
    /// </summary>
    public IReadOnlyList<ScummV5ObjectVerb> ReadObjectVerbs(int number)
    {
        var verbs = new List<ScummV5ObjectVerb>();
        try
        {
            var room = Room(number);
            foreach (var obcd in Blocks(_resources, room.Start + 8, room.Start + room.Size).Where(b => b.Tag == "OBCD"))
            {
                var parts = Blocks(_resources, obcd.Start + 8, obcd.Start + obcd.Size);
                var objectId = parts.FirstOrDefault(b => b.Tag == "CDHD") is { Size: >= 10 } header ? BinaryPrimitives.ReadUInt16LittleEndian(_resources.AsSpan(header.Start + 8)) : 0;
                var nameBlock = parts.FirstOrDefault(b => b.Tag == "OBNA");
                string? name = null;
                if (nameBlock is not null && Array.IndexOf(_resources, (byte)0, nameBlock.Start + 8, nameBlock.Size - 8) is var end && end > nameBlock.Start + 8)
                    name = new string(Encoding.Latin1.GetString(_resources, nameBlock.Start + 8, end - nameBlock.Start - 8).Where(c => c >= ' ').ToArray());
                if (parts.FirstOrDefault(b => b.Tag == "VERB") is not { } verb) continue;
                var code = _resources.AsSpan(verb.Start, verb.Size).ToArray();
                for (var p = 8; p + 2 < code.Length && code[p] != 0; p += 3)
                    verbs.Add(new ScummV5ObjectVerb(objectId, name, code[p], BinaryPrimitives.ReadUInt16LittleEndian(code.AsSpan(p + 1)), verb.Start, code));
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException) { }
        return verbs;
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
