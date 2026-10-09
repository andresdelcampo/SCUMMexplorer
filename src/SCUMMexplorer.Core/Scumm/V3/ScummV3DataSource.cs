using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>The old-v3 games' data (LOOM EGA, Indy3 EGA): the readers the session used directly before the seam, behind <see cref="IScummDataSource"/>.</summary>
public sealed class ScummV3DataSource : IScummDataSource
{
    private readonly GameLibrary _game;
    private readonly ScummScriptReader _reader;
    private readonly ScummObjectVerbReader _verbs;
    private readonly ScummRoomObjectDirectory _directory = new();

    public ScummV3DataSource(GameLibrary game)
    {
        _game = game;
        var dialect = ScummScriptDialects.For(game.Id);
        _reader = new ScummScriptReader(dialect);
        _verbs = new ScummObjectVerbReader(dialect);
        GlobalScriptCount = ReadGlobalScriptCount(game);
    }

    public int GlobalScriptCount { get; }

    public IReadOnlyList<int> GlobalScriptIds() =>
        _game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue)
            .Select(item => item.ResourceId!.Value).Distinct().Order().ToArray();

    public ScummScriptDisassembly? GlobalScript(int number) =>
        _game.Resources.FirstOrDefault(item => item.Kind == ResourceKind.Script && item.ResourceId == number) is { } resource
            ? _reader.Decode(resource) : null;

    public ScummScriptDisassembly? RoomEntryScript(int room) => _reader.DecodeRoomEntry(Room(room));

    public ScummScriptDisassembly? RoomExitScript(int room) => _reader.DecodeRoomExit(Room(room));

    public IReadOnlyList<int> RoomLocalScriptIds(int room) =>
        FindRoom(room) is { } resource ? ScummScriptReader.ReadRoomLocalScriptIds(File.ReadAllBytes(resource.FilePath), GlobalScriptCount) : [];

    public ScummScriptDisassembly? RoomLocalScript(int room, int number) =>
        _reader.DecodeRoomLocalScript(Room(room), number, GlobalScriptCount);

    public ScummCostume? ReadCostume(int id) => ScummCostume.Load(_game, id);

    public ScummRoomData ReadRoom(int room)
    {
        var bytes = File.ReadAllBytes(Room(room).FilePath);
        var objects = _directory.Decode(bytes);
        var names = ScummRoomObjectDirectory.ReadObjectNameBytes(LflEncoding.DecodeXorFF(bytes), objects);
        ScummWalkBoxSet? boxes = null;
        string? error = null;
        try { boxes = ScummWalkBoxReader.Read(bytes); }
        catch (InvalidDataException ex) { error = ex.Message; }
        return new ScummRoomData(objects, names, boxes, error,
            (bytes[4] ^ 0xFF) | ((bytes[5] ^ 0xFF) << 8), (bytes[6] ^ 0xFF) | ((bytes[7] ^ 0xFF) << 8));
    }

    public IReadOnlyList<RoomObjectImage> ReadObjectImages(int room) =>
        FindRoom(room) is { } resource ? new ScummRoomObjects().Decode(File.ReadAllBytes(resource.FilePath)) : [];

    public IReadOnlyDictionary<int, IReadOnlyList<ScummObjectVerbEntry>> ReadObjectVerbs(int room, IReadOnlyList<ScummRoomObjectRecord> records) =>
        _verbs.ReadAll(File.ReadAllBytes(Room(room).FilePath), records);

    public ScummScriptDisassembly ReadObjectVerb(int room, ScummRoomObjectRecord record, int verbId, out ScummObjectVerbEntry selectedEntry) =>
        _verbs.Decode(Room(room), record, verbId, out selectedEntry);

    private GameResource? FindRoom(int room) => _game.Rooms.FirstOrDefault(item => item.ResourceId == room);

    private GameResource Room(int room) => FindRoom(room) ?? throw new InvalidDataException($"Room {room:00} is not in this game.");

    private static int ReadGlobalScriptCount(GameLibrary game)
    {
        var indexPath = Path.Combine(game.FolderPath, "00.LFL");
        try
        {
            if (File.Exists(indexPath)) return new LflIndexReader().ReadDeclaredScriptCount(indexPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException) { }
        return game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue)
                   .Select(item => item.ResourceId!.Value).DefaultIfEmpty(-1).Max() + 1;
    }
}
