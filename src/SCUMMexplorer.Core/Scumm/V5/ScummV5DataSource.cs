using System.Buffers.Binary;
using System.Text;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// A SCUMM v5 game (<c>NAME.000</c>/<c>NAME.001</c>; first one: The Secret of Monkey Island CD) behind <see cref="IScummDataSource"/> (S4b MonkeyCd,
/// 17 October 2026): global scripts from the <c>DSCR</c> directory (<c>SCRP</c>), a room's <c>ENCD</c>, <c>EXCD</c> and <c>LSCR</c> blocks, its objects
/// (<c>OBCD</c> &gt; <c>CDHD</c>, <c>VERB</c>, <c>OBNA</c>; <c>OBIM</c>), walk boxes (<c>BOXD</c> + <c>BOXM</c>) and scale slots (<c>SCAL</c>), all read through
/// <see cref="ScummV5Archive"/> and decoded in the profile's dialect. Positions count from the start of the resource file; an object's code offset is its
/// <c>OBCD</c> block's place in the room, its verbs' offsets count from the start of its <c>VERB</c> block (8-byte header included), as ScummVM's.
/// </summary>
public sealed class ScummV5DataSource : IScummDataSource
{
    /// <summary>Local scripts of a room are numbered from 200 (ScummVM: <c>_numGlobalScripts</c> is 200 for version 5).</summary>
    public const int LocalScriptBase = 200;

    private const string ContainerName = "the resource file";
    private readonly ScummV5Archive _archive;
    private readonly ScummScriptReader _reader;
    private readonly Lazy<IReadOnlyDictionary<int, ScummV5Script>> _globals;
    private readonly Dictionary<int, IReadOnlyList<ScummV5Script>> _roomScripts = [];
    private readonly Dictionary<int, BrowsedObjects> _objects = [];
    private readonly Dictionary<int, IReadOnlyList<ScummV5ObjectVerb>> _verbs = [];

    public ScummV5DataSource(ScummV5Archive archive, ScummScriptDialect dialect)
    {
        _archive = archive ?? throw new ArgumentNullException(nameof(archive));
        _reader = new(dialect);
        _globals = new(() => _archive.ReadGlobalScripts().GroupBy(s => s.Number).ToDictionary(g => g.Key, g => g.First()));
    }

    /// <summary>The object table of the index (<c>DOBJ</c>): starting states, owners and classes.</summary>
    public ScummIndexedObjectSnapshot ReadIndexedObjects() => _archive.ReadIndexedObjects();

    public int GlobalScriptCount => LocalScriptBase;

    public IReadOnlyList<int> GlobalScriptIds() => _globals.Value.Keys.Order().ToArray();

    public ScummScriptDisassembly? GlobalScript(int number) =>
        _globals.Value.TryGetValue(number, out var script) ? Decode(script, number, 8) : null;

    public ScummScriptDisassembly? RoomEntryScript(int room) => RoomScript(room, "ENCD", "entry script");

    public ScummScriptDisassembly? RoomExitScript(int room) => RoomScript(room, "EXCD", "exit script");

    public IReadOnlyList<int> RoomLocalScriptIds(int room) =>
        Scripts(room).Where(s => s.Kind == "LSCR").Select(s => s.Number).Distinct().ToArray();

    public ScummScriptDisassembly? RoomLocalScript(int room, int number)
    {
        if (number < GlobalScriptCount) return null;
        return Scripts(room).FirstOrDefault(s => s.Kind == "LSCR" && s.Number == number) is { } script
            ? Decode(script, number, 9) with { Context = $"Room {room:00} local script {number:00}" }
            : null;
    }

    public ScummCostume? ReadCostume(int id) => _archive.FindCostume(id) is { } costume ? ScummCostume.ReadVga(costume.Body, id) : null;

    public ScummRoomData ReadRoom(int room)
    {
        var browsed = Objects(room);
        var names = browsed.Names.Where(pair => pair.Value.Length > 0).ToDictionary(pair => pair.Key, pair => Encoding.Latin1.GetBytes(pair.Value));
        var (width, height) = Read(() => _archive.ReadRoomSize(room));
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
            else boxError = $"Room {room:00} has no BOXD block";
        }
        catch (InvalidDataException ex) { boxError = ex.Message; }
        return new ScummRoomData(browsed.Records, names, boxes, boxError, width, height);
    }

    /// <summary>
    /// One image per object (the room-object views key on the object id): its first state's (<c>IM01</c>). A v5 object can store one image per state
    /// (Monkey Island 2 rooms do; S4b Monkey2, 17 October 2026); <see cref="ReadObjectImages(int, IReadOnlyDictionary{int, byte})"/> picks by state.
    /// </summary>
    public IReadOnlyList<RoomObjectImage> ReadObjectImages(int room) => ReadObjectImages(room, new Dictionary<int, byte>());

    /// <summary>
    /// One image per object, the one its state shows: ScummVM <c>drawObject</c> finds the image block <c>IMnn</c> with n the object's state (state 0 draws
    /// nothing, so its first image stands in; the object is inactive anyway). An object whose state has no image of its own keeps its first.
    /// </summary>
    public IReadOnlyList<RoomObjectImage> ReadObjectImages(int room, IReadOnlyDictionary<int, byte> states) =>
        Read(() => _archive.ReadObjectImages(room)).GroupBy(i => i.Id)
            .Select(group => group.FirstOrDefault(i => states.TryGetValue(i.Id, out var state) && (state & 0x0F) == i.State) ?? group.First())
            .Select(i => new RoomObjectImage(i.Id, i.X, i.Y, i.Width, i.Height, i.Pixels) { Mask = i.Mask, Transparent = i.Transparent }).ToArray();

    public IReadOnlyDictionary<int, IReadOnlyList<ScummObjectVerbEntry>> ReadObjectVerbs(int room, IReadOnlyList<ScummRoomObjectRecord> records)
    {
        var tables = new Dictionary<int, IReadOnlyList<ScummObjectVerbEntry>>();
        var all = Verbs(room);
        foreach (var record in records)
        {
            var entries = Entries(all, record.Id);
            if (entries.Length > 0) tables[record.Id] = entries;
        }
        return tables;
    }

    public ScummScriptDisassembly ReadObjectVerb(int room, ScummRoomObjectRecord record, int verbId, out ScummObjectVerbEntry selectedEntry)
    {
        var verbs = Verbs(room).Where(v => v.ObjectId == record.Id).ToArray();
        if (verbs.Length == 0) throw new InvalidDataException($"Object {record.Id} of Room {room:00} has no verb 0x{verbId:X2}: it has no VERB block.");
        var entries = Entries(verbs, record.Id);
        var entry = entries.FirstOrDefault(item => item.VerbId == verbId) ?? entries.FirstOrDefault(item => item.IsFallback)
            ?? throw new InvalidDataException($"Object {record.Id} has no verb 0x{verbId:X2} or fallback entry.");
        var block = verbs[0].Block;
        if (entry.ScriptOffset >= block.Length) throw new InvalidDataException($"Object {record.Id} verb 0x{entry.VerbId:X2} points outside its block.");
        selectedEntry = entry;
        var position = verbs[0].Position + entry.ScriptOffset;
        return _reader.DecodeCode(block.AsSpan(entry.ScriptOffset).ToArray(), room, 1, position)
            with { Context = $"Room {room:00} Object {record.Id} {entry.Label}", ContainerName = ContainerName, CodeFileOffset = position };
    }

    private static ScummObjectVerbEntry[] Entries(IEnumerable<ScummV5ObjectVerb> verbs, int objectId) =>
        verbs.Where(v => v.ObjectId == objectId)
            .Select((v, i) => new ScummObjectVerbEntry(objectId, v.VerbId, 0, 8 + i * 3, v.StartOffset, v.Block.Length)).ToArray();

    private ScummScriptDisassembly? RoomScript(int room, string kind, string label) =>
        Scripts(room).FirstOrDefault(s => s.Kind == kind) is { } script
            ? Decode(script, room, 8) with { Context = $"Room {room:00} {label}" }
            : null;

    private ScummScriptDisassembly Decode(ScummV5Script script, int id, int header) =>
        _reader.DecodeCode(script.Code, id, 1, script.Position) with
        {
            ContainerName = ContainerName,
            CodeFileOffset = script.Position + header,
            ResourceLength = script.Code.Length + header
        };

    private IReadOnlyList<ScummV5Script> Scripts(int room)
    {
        if (!_roomScripts.TryGetValue(room, out var list))
        {
            RequireRoom(room);
            _roomScripts[room] = list = _archive.ReadRoomScripts(room);
        }
        return list;
    }

    private BrowsedObjects Objects(int room)
    {
        if (!_objects.TryGetValue(room, out var objects)) _objects[room] = objects = Read(() => _archive.ReadBrowsedObjects(room));
        return objects;
    }

    private IReadOnlyList<ScummV5ObjectVerb> Verbs(int room)
    {
        if (!_verbs.TryGetValue(room, out var list))
        {
            RequireRoom(room);
            _verbs[room] = list = _archive.ReadObjectVerbs(room);
        }
        return list;
    }

    private void RequireRoom(int room)
    {
        if (_archive.Rooms.All(r => r.Number != room)) throw new InvalidDataException($"Room {room:00} is not in the game's resource file.");
    }

    private static T Read<T>(Func<T> read)
    {
        try { return read(); }
        catch (KeyNotFoundException ex) { throw new InvalidDataException(ex.Message, ex); }
    }
}
