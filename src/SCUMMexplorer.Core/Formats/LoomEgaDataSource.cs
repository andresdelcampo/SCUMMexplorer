using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>The old-v3 games' data (LOOM EGA, Indy3 EGA): the readers the session used directly before the seam, behind <see cref="IScummDataSource"/>.</summary>
public sealed class LoomEgaDataSource : IScummDataSource
{
    private readonly GameLibrary _game;
    private readonly LoomScriptReader _reader;
    private readonly LoomObjectVerbReader _verbs;
    private readonly LoomRoomObjectDirectory _directory = new();

    public LoomEgaDataSource(GameLibrary game)
    {
        _game = game;
        var dialect = ScummScriptDialects.For(game.Id);
        _reader = new LoomScriptReader(dialect);
        _verbs = new LoomObjectVerbReader(dialect);
        GlobalScriptCount = ReadGlobalScriptCount(game);
    }

    public int GlobalScriptCount { get; }

    public LoomScriptDisassembly? GlobalScript(int number) =>
        _game.Resources.FirstOrDefault(item => item.Kind == ResourceKind.Script && item.ResourceId == number) is { } resource
            ? _reader.Decode(resource) : null;

    public LoomScriptDisassembly? RoomEntryScript(int room) => _reader.DecodeRoomEntry(Room(room));

    public LoomScriptDisassembly? RoomExitScript(int room) => _reader.DecodeRoomExit(Room(room));

    public IReadOnlyList<int> RoomLocalScriptIds(int room) =>
        FindRoom(room) is { } resource ? LoomScriptReader.ReadRoomLocalScriptIds(File.ReadAllBytes(resource.FilePath), GlobalScriptCount) : [];

    public LoomScriptDisassembly? RoomLocalScript(int room, int number) =>
        _reader.DecodeRoomLocalScript(Room(room), number, GlobalScriptCount);

    public ScummRoomData ReadRoom(int room)
    {
        var bytes = File.ReadAllBytes(Room(room).FilePath);
        var objects = _directory.Decode(bytes);
        var names = LoomTextExtractor.ReadObjectNameBytes(LflEncoding.DecodeXorFF(bytes), objects);
        LoomWalkBoxSet? boxes = null;
        string? error = null;
        try { boxes = LoomWalkBoxReader.Read(bytes); }
        catch (InvalidDataException ex) { error = ex.Message; }
        return new ScummRoomData(objects, names, boxes, error,
            (bytes[4] ^ 0xFF) | ((bytes[5] ^ 0xFF) << 8), (bytes[6] ^ 0xFF) | ((bytes[7] ^ 0xFF) << 8));
    }

    public IReadOnlyList<RoomObjectImage> ReadObjectImages(int room) =>
        FindRoom(room) is { } resource ? new LoomRoomObjects().Decode(File.ReadAllBytes(resource.FilePath)) : [];

    public IReadOnlyDictionary<int, IReadOnlyList<LoomObjectVerbEntry>> ReadObjectVerbs(int room, IReadOnlyList<LoomRoomObjectRecord> records) =>
        _verbs.ReadAll(File.ReadAllBytes(Room(room).FilePath), records);

    public LoomScriptDisassembly ReadObjectVerb(int room, LoomRoomObjectRecord record, int verbId, out LoomObjectVerbEntry selectedEntry) =>
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
