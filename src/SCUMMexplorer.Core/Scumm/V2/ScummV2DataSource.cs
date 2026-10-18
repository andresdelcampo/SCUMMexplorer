using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>Enhanced v2 LFL resources. Layout: ScummVM resource_v2.cpp, room.cpp and object.cpp; 17 October 2026.</summary>
public class ScummV2DataSource : IScummDataSource
{
    private readonly GameLibrary _game;
    private readonly ScummScriptReader _reader;
    public ScummV2DataSource(GameLibrary game) : this(game, ScummScriptDialect.V2, ReadScriptCount(game)) { }
    protected ScummV2DataSource(GameLibrary game, ScummScriptDialect dialect, int globalScriptCount)
    {
        _game = game;
        _reader = new(dialect);
        GlobalScriptCount = globalScriptCount;
    }
    private static int ReadScriptCount(GameLibrary game)
    {
        if (game.Version != ScummVersion.V2) throw new ArgumentException("The data source requires a v2 game.", nameof(game));
        var index = LflEncoding.DecodeXorFF(File.ReadAllBytes(Path.Combine(game.FolderPath, "00.LFL")));
        var position = 4 + Word(index, 2);
        for (var directory = 0; directory < 2; directory++) position += 1 + index[position] * 3;
        return index[position];
    }
    // v2 has no room-local script directory (room.cpp setupRoomSubBlocks).
    public int GlobalScriptCount { get; }
    public IReadOnlyList<int> GlobalScriptIds() => _game.Resources.Where(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue)
        .Select(r => r.ResourceId!.Value).Distinct().Order().ToArray();
    public ScummScriptDisassembly? GlobalScript(int number) => _game.Resources.FirstOrDefault(r => r.Kind == ResourceKind.Script && r.ResourceId == number) is { } resource
        ? _reader.Decode(resource) : null;
    public ScummScriptDisassembly? RoomEntryScript(int room) => RoomScript(room, "Entry script");
    public ScummScriptDisassembly? RoomExitScript(int room) => RoomScript(room, "Exit script");
    public IReadOnlyList<int> RoomLocalScriptIds(int room) => [];
    public ScummScriptDisassembly? RoomLocalScript(int room, int number) => null;
    public ScummCostume? ReadCostume(int id) => ScummCostume.Load(_game, id);
    protected byte[] RoomBytes(int room) => LflEncoding.DecodeXorFF(File.ReadAllBytes(
        (_game.Rooms.FirstOrDefault(r => r.ResourceId == room) ?? throw new InvalidDataException($"Room {room:00} is not in this game.")).FilePath));
    private ScummScriptDisassembly? RoomScript(int room, string kind)
    {
        var source = ScummOldScripts.ReadRoomScripts(RoomBytes(room), room).FirstOrDefault(s => s.ObjectId is null && s.Label.EndsWith(kind));
        return source is null ? null : Decode(source, room);
    }
    private ScummScriptDisassembly Decode(OldScriptSource source, int id) =>
        _reader.DecodeCode(source.Code, id, source.Container, source.Offset, source.StartOffset) with { Context = source.Label, EntryOffset = source.StartOffset };

    public virtual ScummIndexedObjectSnapshot ReadIndexedObjects()
    {
        var bytes = LflEncoding.DecodeXorFF(File.ReadAllBytes(Path.Combine(_game.FolderPath, "00.LFL")));
        if (bytes.Length < 4 || Word(bytes, 0) != 0x0100) throw new InvalidDataException("Invalid v2 index.");
        var count = Word(bytes, 2);
        if (4 + count > bytes.Length) throw new InvalidDataException("Truncated v2 object table.");
        var states = new Dictionary<int, byte>();
        var owners = new Dictionary<int, byte>();
        for (var id = 0; id < count; id++) { states[id] = (byte)(bytes[4 + id] >> 4); owners[id] = (byte)(bytes[4 + id] & 15); }
        return new(states, owners);
    }
    public ScummRoomData ReadRoom(int room)
    {
        var bytes = RoomBytes(room);
        if (bytes.Length < 28 || Word(bytes, 0) > bytes.Length) throw new InvalidDataException("Invalid v2 room header.");
        var count = bytes[20];
        if (28 + count * 4 > bytes.Length) throw new InvalidDataException("Truncated v2 object directory.");
        var records = new List<ScummRoomObjectRecord>();
        var names = new Dictionary<int, byte[]>();
        for (var i = 0; i < count; i++)
        {
            var image = Word(bytes, 28 + i * 2);
            var code = Word(bytes, 28 + count * 2 + i * 2);
            if (code == 0 || code + 15 > bytes.Length) continue;
            var id = Word(bytes, code + 4);
            if (id == 0) continue;
            records.Add(new(id, i + 1, bytes[code + 7] * 8, (bytes[code + 8] & 127) * 8, bytes[code + 9] * 8,
                bytes[code + 13] & 248, bytes[code + 10], (byte)((bytes[code + 8] >> 7) * 8), bytes[code + 11] * 8,
                (bytes[code + 12] & 31) * 8, (byte)(bytes[code + 13] & 7), code, image > 0 && image < code ? image : null)
                { Preposition = (byte)(bytes[code + 12] >> 5), V2ClassMask = bytes[code + 6] });
            var start = code + bytes[code + 14];
            var end = start < bytes.Length ? Array.IndexOf(bytes, (byte)0, start) : -1;
            if (end >= start) names[id] = bytes[start..end];
        }
        ScummWalkBoxSet? boxes = null;
        string? error = null;
        try { boxes = ScummWalkBoxReader.ReadV2(bytes); } catch (InvalidDataException ex) { error = ex.Message; }
        var (width, height) = ReadDimensions(bytes);
        return new(records, names, boxes, error, width, height);
    }
    protected virtual (int Width, int Height) ReadDimensions(byte[] bytes) => (Word(bytes, 4), Word(bytes, 6));
    public virtual RoomImage ReadBackground(int room) => new ScummV2RoomDecoder().Decode(File.ReadAllBytes(
        _game.Rooms.First(r => r.ResourceId == room).FilePath));
    public virtual IReadOnlyList<RoomObjectImage> ReadObjectImages(int room) => ScummOldRoomObjects.ReadV2Images(RoomBytes(room));
    public IReadOnlyDictionary<int, IReadOnlyList<ScummObjectVerbEntry>> ReadObjectVerbs(int room, IReadOnlyList<ScummRoomObjectRecord> records)
    {
        var sources = ScummOldScripts.ReadRoomScripts(RoomBytes(room), room).Where(s => s.ObjectId.HasValue).ToArray();
        return records.GroupBy(r => r.Id).Select(group => group.Last()).ToDictionary(r => r.Id, r => (IReadOnlyList<ScummObjectVerbEntry>)sources.Where(s => s.ObjectId == r.Id && s.Offset == r.CodeOffset)
            .Select(s => Entry(s)).ToArray());
    }
    private static ScummObjectVerbEntry Entry(OldScriptSource s) => new(s.ObjectId!.Value, s.VerbId!.Value, s.Offset,
        s.Offset + 15, s.Offset + s.StartOffset, s.Offset + s.Code.Length);
    public ScummScriptDisassembly ReadObjectVerb(int room, ScummRoomObjectRecord record, int verbId, out ScummObjectVerbEntry selectedEntry)
    {
        var selected = ReadRoom(room).Objects.Last(r => r.Id == record.Id); // getObjectIndex scans backwards.
        var sources = ScummOldScripts.ReadRoomScripts(RoomBytes(room), room).Where(s => s.ObjectId == record.Id && s.Offset == selected.CodeOffset).ToArray();
        var source = sources.FirstOrDefault(s => s.VerbId == verbId) ?? sources.FirstOrDefault(s => s.VerbId == 255)
            ?? throw new InvalidDataException($"Object {record.Id} has no verb 0x{verbId:X2} or fallback entry.");
        selectedEntry = Entry(source);
        // Keep the object's offsets: v2 relative jumps are within this code resource, whose entry is not necessarily its first byte.
        return Decode(source, room);
    }
    private static int Word(byte[] data, int offset) => data[offset] | data[offset + 1] << 8;
}
