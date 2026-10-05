using System.Text;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// The CD game's data (SCUMM v4, DISK01.LEC) behind <see cref="IScummDataSource"/>: scripts and objects as <see cref="LoomVgaArchive"/>
/// reads them, decoded with the <see cref="ScummScriptDialect.LoomCd"/> dialect. Offsets in the records it returns count from the start
/// of the object's OC block (the EGA game's count from the room). Walk boxes come from the room's BX block.
/// </summary>
public sealed class LoomCdDataSource : IScummDataSource
{
    private const int Container = 1;
    private readonly LoomVgaArchive _archive;
    private readonly LoomScriptReader _reader = new(ScummScriptDialects.For(GameId.LoomVga));
    private readonly Dictionary<int, IReadOnlyList<LoomVgaScript>> _scripts = [];
    private readonly Dictionary<int, IReadOnlyList<LoomVgaObject>> _objects = [];

    public LoomCdDataSource(GameLibrary game)
    {
        _archive = game.Vga ?? throw new InvalidDataException("The CD game has no archive.");
    }

    public int GlobalScriptCount => LoomVgaArchive.LocalScriptBase;

    public LoomScriptDisassembly? GlobalScript(int number) =>
        _archive.ReadGlobalScript(number) is { } script
            ? _reader.DecodeCode(script.Code, number, Container, script.BlockOffset) with { Context = $"Global script {number}" }
            : null;

    public LoomScriptDisassembly? RoomEntryScript(int room) => RoomScript(room, "EN", "entry script");

    public LoomScriptDisassembly? RoomExitScript(int room) => RoomScript(room, "EX", "exit script");

    public IReadOnlyList<int> RoomLocalScriptIds(int room) =>
        Scripts(room).Where(s => s.Kind == "LS").Select(s => s.Number).Distinct().ToArray();

    public LoomScriptDisassembly? RoomLocalScript(int room, int number)
    {
        if (number < GlobalScriptCount) return null;
        return Scripts(room).FirstOrDefault(s => s.Kind == "LS" && s.Number == number) is { } script
            ? _reader.DecodeCode(script.Code, number, Container, script.BlockOffset) with { Context = $"Room {room:00} local script {number:00}" }
            : null;
    }

    public ScummRoomData ReadRoom(int room)
    {
        var records = Objects(room);
        var images = _archive.ReadObjectImages(room).Select(image => image.Id).ToHashSet();
        var objects = new List<LoomRoomObjectRecord>(records.Count);
        var names = new Dictionary<int, byte[]>();
        for (var i = 0; i < records.Count; i++)
        {
            var o = records[i];
            objects.Add(new LoomRoomObjectRecord(o.Id, i + 1, o.X, o.Y, o.Width, o.Height, o.Parent, (byte)o.ParentState, o.WalkX, o.WalkY,
                (byte)o.Direction, o.BlockOffset, images.Contains(o.Id) ? o.BlockOffset : null));
            if (o.Name.Length > 0) names[o.Id] = Encoding.Latin1.GetBytes(o.Name);
        }
        var (width, height) = _archive.ReadRoomSize(room);
        LoomWalkBoxSet? boxes = null;
        string? boxError = null;
        try
        {
            if (_archive.ReadWalkBoxBlock(room) is { } block) boxes = LoomWalkBoxReader.ReadBlockBody(block);
            else boxError = $"Room {room:00} has no BX block";
        }
        catch (InvalidDataException ex) { boxError = ex.Message; }
        return new ScummRoomData(objects, names, boxes, boxError, width, height);
    }

    public IReadOnlyList<RoomObjectImage> ReadObjectImages(int room) =>
        _archive.ReadObjectImages(room).Select(i => new RoomObjectImage(i.Id, i.X, i.Y, i.Width, i.Height, i.Pixels)).ToArray();

    public IReadOnlyDictionary<int, IReadOnlyList<LoomObjectVerbEntry>> ReadObjectVerbs(int room, IReadOnlyList<LoomRoomObjectRecord> records)
    {
        var tables = new Dictionary<int, IReadOnlyList<LoomObjectVerbEntry>>();
        foreach (var record in records)
            if (Objects(room).FirstOrDefault(o => o.Id == record.Id) is { } o) tables[record.Id] = VerbEntries(o);
        return tables;
    }

    public LoomScriptDisassembly ReadObjectVerb(int room, LoomRoomObjectRecord record, int verbId, out LoomObjectVerbEntry selectedEntry)
    {
        var o = Objects(room).FirstOrDefault(item => item.Id == record.Id)
            ?? throw new InvalidDataException($"Object {record.Id} is not in Room {room:00}.");
        var entries = VerbEntries(o);
        var entry = entries.FirstOrDefault(item => item.VerbId == verbId) ?? entries.FirstOrDefault(item => item.IsFallback)
            ?? throw new InvalidDataException($"Object {record.Id} has no verb 0x{verbId:X2} or fallback entry.");
        if (entry.ScriptOffset >= o.Block.Length)
            throw new InvalidDataException($"Object {record.Id} verb 0x{entry.VerbId:X2} points outside its block.");
        selectedEntry = entry;
        var code = o.Block.AsSpan(entry.ScriptOffset).ToArray();
        return _reader.DecodeCode(code, room, Container, o.BlockOffset + entry.ScriptOffset)
            with { Context = $"Room {room:00} Object {record.Id} {entry.Label}" };
    }

    private static LoomObjectVerbEntry[] VerbEntries(LoomVgaObject o) =>
        o.Verbs.Select((v, i) => new LoomObjectVerbEntry(o.Id, v.VerbId, 0, 19 + i * 3, v.Offset, o.Block.Length)).ToArray();

    private LoomScriptDisassembly? RoomScript(int room, string kind, string label) =>
        Scripts(room).FirstOrDefault(s => s.Kind == kind) is { } script
            ? _reader.DecodeCode(script.Code, room, Container, script.BlockOffset) with { Context = $"Room {room:00} {label}" }
            : null;

    private IReadOnlyList<LoomVgaScript> Scripts(int room)
    {
        if (!_scripts.TryGetValue(room, out var list)) _scripts[room] = list = Read(() => _archive.ReadScripts(room));
        return list;
    }

    private IReadOnlyList<LoomVgaObject> Objects(int room)
    {
        if (!_objects.TryGetValue(room, out var list)) _objects[room] = list = Read(() => _archive.ReadObjects(room));
        return list;
    }

    private static T Read<T>(Func<T> read)
    {
        try { return read(); }
        catch (KeyNotFoundException ex) { throw new InvalidDataException(ex.Message, ex); }
    }
}
