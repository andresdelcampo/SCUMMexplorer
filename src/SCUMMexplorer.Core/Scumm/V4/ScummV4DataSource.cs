using System.Buffers.Binary;
using System.Text;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The CD game's data (SCUMM v4, DISK01.LEC) behind <see cref="IScummDataSource"/>: scripts and objects as <see cref="ScummV4Archive"/>
/// reads them, decoded with the <see cref="ScummScriptDialect.V4"/> dialect. Offsets in the records it returns count from the start
/// of the object's OC block (the EGA game's count from the room). Walk boxes come from the room's BX block.
/// </summary>
public sealed class ScummV4DataSource : IScummDataSource
{
    private readonly int _container;
    private readonly string _containerName;
    private readonly ScummV4Archive _archive;
    private readonly ScummScriptReader _reader;
    private readonly Dictionary<int, IReadOnlyList<ScummV4Script>> _scripts = [];
    private readonly Dictionary<int, IReadOnlyList<ScummV4Object>> _objects = [];

    public ScummV4DataSource(GameLibrary game)
    {
        _archive = game.V4Archive ?? throw new InvalidDataException("The CD game has no archive.");
        _reader = new(ScummScriptDialect.V4);
        _container = 1;
        _containerName = "DISK01.LEC";
    }

    /// <summary>One disk (or the room files) of a browsed v4 game: its scripts read in the game's own dialect, the disk number as the container file number.</summary>
    public ScummV4DataSource(ScummV4Archive archive, ScummScriptDialect dialect, int disk, string containerName)
    {
        _archive = archive;
        _reader = new(dialect);
        _container = disk;
        _containerName = containerName;
    }

    public int GlobalScriptCount => ScummV4Archive.LocalScriptBase;

    public IReadOnlyList<int> GlobalScriptIds() => _archive.GlobalScriptNumbers();

    /// <summary>A global script carries no context, as on EGA: the session treats a script without one as global (it survives a room load).</summary>
    public ScummScriptDisassembly? GlobalScript(int number) =>
        _archive.ReadGlobalScript(number) is { } script
            ? Decode(script, number)
            : null;

    public ScummScriptDisassembly? RoomEntryScript(int room) => RoomScript(room, "EN", "entry script");

    public ScummScriptDisassembly? RoomExitScript(int room) => RoomScript(room, "EX", "exit script");

    public IReadOnlyList<int> RoomLocalScriptIds(int room) =>
        Scripts(room).Where(s => s.Kind == "LS").Select(s => s.Number).Distinct().ToArray();

    public ScummScriptDisassembly? RoomLocalScript(int room, int number)
    {
        if (number < GlobalScriptCount) return null;
        return Scripts(room).FirstOrDefault(s => s.Kind == "LS" && s.Number == number) is { } script
            ? Decode(script, number) with { Context = $"Room {room:00} local script {number:00}" }
            : null;
    }

    public ScummCostume? ReadCostume(int id) => _archive.FindCostume(id) is { } costume ? ScummCostume.ReadVga(costume.Body, id) : null;

    public ScummRoomData ReadRoom(int room)
    {
        var records = Objects(room);
        var images = _archive.ReadObjectImages(room).Select(image => image.Id).ToHashSet();
        var objects = new List<ScummRoomObjectRecord>(records.Count);
        var names = new Dictionary<int, byte[]>();
        for (var i = 0; i < records.Count; i++)
        {
            var o = records[i];
            objects.Add(new ScummRoomObjectRecord(o.Id, i + 1, o.X, o.Y, o.Width, o.Height, o.Parent, (byte)o.ParentState, o.WalkX, o.WalkY,
                (byte)o.Direction, o.BlockOffset, images.Contains(o.Id) ? o.BlockOffset : null));
            if (o.Name.Length > 0) names[o.Id] = Encoding.Latin1.GetBytes(o.Name);
        }
        var (width, height) = _archive.ReadRoomSize(room);
        ScummWalkBoxSet? boxes = null;
        string? boxError = null;
        try
        {
            if (_archive.ReadWalkBoxBlock(room) is { } block)
            {
                boxes = ScummWalkBoxReader.ReadBlockBody(block);
                if (_archive.ReadScaleSlotBlock(room) is { Length: >= 8 } sa)
                    boxes.ScaleSlots = Enumerable.Range(0, sa.Length / 8).Select(i =>
                        ((int)BinaryPrimitives.ReadUInt16LittleEndian(sa.AsSpan(i * 8)), (int)BinaryPrimitives.ReadUInt16LittleEndian(sa.AsSpan(i * 8 + 2)),
                         (int)BinaryPrimitives.ReadUInt16LittleEndian(sa.AsSpan(i * 8 + 4)), (int)BinaryPrimitives.ReadUInt16LittleEndian(sa.AsSpan(i * 8 + 6)))).ToArray();
            }
            else boxError = $"Room {room:00} has no BX block";
        }
        catch (InvalidDataException ex) { boxError = ex.Message; }
        return new ScummRoomData(objects, names, boxes, boxError, width, height);
    }

    public IReadOnlyList<RoomObjectImage> ReadObjectImages(int room) =>
        _archive.ReadObjectImages(room).Select(i => new RoomObjectImage(i.Id, i.X, i.Y, i.Width, i.Height, i.Pixels) { Mask = i.Mask }).ToArray();

    public IReadOnlyDictionary<int, IReadOnlyList<ScummObjectVerbEntry>> ReadObjectVerbs(int room, IReadOnlyList<ScummRoomObjectRecord> records)
    {
        var tables = new Dictionary<int, IReadOnlyList<ScummObjectVerbEntry>>();
        foreach (var record in records)
            if (Objects(room).FirstOrDefault(o => o.Id == record.Id) is { } o) tables[record.Id] = VerbEntries(o);
        return tables;
    }

    public ScummScriptDisassembly ReadObjectVerb(int room, ScummRoomObjectRecord record, int verbId, out ScummObjectVerbEntry selectedEntry)
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
        return _reader.DecodeCode(code, room, _container, o.BlockOffset + entry.ScriptOffset)
            with { Context = $"Room {room:00} Object {record.Id} {entry.Label}", ContainerName = _containerName,
                CodeFileOffset = o.BlockOffset + entry.ScriptOffset };
    }

    private static ScummObjectVerbEntry[] VerbEntries(ScummV4Object o) =>
        o.Verbs.Select((v, i) => new ScummObjectVerbEntry(o.Id, v.VerbId, 0, 19 + i * 3, v.Offset, o.Block.Length)).ToArray();

    private ScummScriptDisassembly? RoomScript(int room, string kind, string label) =>
        Scripts(room).FirstOrDefault(s => s.Kind == kind) is { } script
            ? Decode(script, room) with { Context = $"Room {room:00} {label}" }
            : null;

    private ScummScriptDisassembly Decode(ScummV4Script script, int id) =>
        _reader.DecodeCode(script.Code, id, _container, script.BlockOffset) with
        {
            ContainerName = _containerName,
            CodeFileOffset = script.BlockOffset + (script.Kind == "LS" ? 7 : 6),
            ResourceLength = script.Code.Length + (script.Kind == "LS" ? 7 : 6)
        };

    private IReadOnlyList<ScummV4Script> Scripts(int room)
    {
        if (!_scripts.TryGetValue(room, out var list)) _scripts[room] = list = Read(() => _archive.ReadScripts(room));
        return list;
    }

    private IReadOnlyList<ScummV4Object> Objects(int room)
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
