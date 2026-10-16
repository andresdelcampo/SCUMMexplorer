namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The data of a game stored as one plain file per room (<c>00.LFL</c> the index, <c>NN.LFL</c> room NN: the 256-colour Indiana Jones and the Last Crusade)
/// behind <see cref="IScummDataSource"/> (S4b Indy3Vga, 15 October 2026). The room files hold the same blocks as a v4 disk's LF blocks, so the reading is
/// <see cref="ScummV4DataSource"/>'s over <see cref="ScummV4Archive.LoadRoomFiles"/>; what differs is said here: the scripts are read in the game's own dialect
/// (version 3, <see cref="ScummProfile.Dialect"/>), walk boxes are 18 bytes and costume poses store one byte per limb command (<see cref="ScummV4Archive.IsRoomFileGame"/>),
/// and a script or object names the room file that holds it (<c>NN.LFL</c>, with the offset in that file) instead of a disk.
/// </summary>
public sealed class ScummV4RoomFileDataSource : IScummDataSource
{
    private readonly ScummV4Archive _archive;
    private readonly ScummV4DataSource _source;
    private readonly Dictionary<int, ScummV4Room> _rooms;

    public ScummV4RoomFileDataSource(ScummV4Archive archive, ScummScriptDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(archive);
        if (!archive.IsRoomFileGame) throw new ArgumentException("The archive is not read from room files.", nameof(archive));
        _archive = archive;
        _source = new ScummV4DataSource(archive, dialect, 1, ScummV4Archive.RoomFileIndex);
        _rooms = archive.Rooms.ToDictionary(room => room.Number);
    }

    /// <summary>The object table of the index (<c>00.LFL</c>, block "0O"): every object's starting state, owner and class.</summary>
    public ScummIndexedObjectSnapshot ReadIndexedObjects() => _archive.ReadIndexedObjects();

    public int GlobalScriptCount => _source.GlobalScriptCount;

    public IReadOnlyList<int> GlobalScriptIds() => _source.GlobalScriptIds();

    public ScummScriptDisassembly? GlobalScript(int number) =>
        _source.GlobalScript(number) is { } script && _archive.ReadGlobalScript(number) is { } stored ? InRoomFile(script, stored.Room) : null;

    public ScummScriptDisassembly? RoomEntryScript(int room) => InRoomFile(_source.RoomEntryScript(room), room);

    public ScummScriptDisassembly? RoomExitScript(int room) => InRoomFile(_source.RoomExitScript(room), room);

    public IReadOnlyList<int> RoomLocalScriptIds(int room) => _source.RoomLocalScriptIds(room);

    public ScummScriptDisassembly? RoomLocalScript(int room, int number) => InRoomFile(_source.RoomLocalScript(room, number), room);

    public ScummRoomData ReadRoom(int room) => _source.ReadRoom(room);

    /// <summary>A costume of the game: its CO block read as the 256-colour format with one byte per limb command (ScummVM <c>costumeDecodeData</c>, version 3).</summary>
    public ScummCostume? ReadCostume(int id) => _archive.FindCostume(id) is { } costume ? ScummCostume.ReadVga(costume.Body, id, byteCommands: true) : null;

    public IReadOnlyList<RoomObjectImage> ReadObjectImages(int room) => _source.ReadObjectImages(room);

    public IReadOnlyDictionary<int, IReadOnlyList<ScummObjectVerbEntry>> ReadObjectVerbs(int room, IReadOnlyList<ScummRoomObjectRecord> records) =>
        _source.ReadObjectVerbs(room, records);

    public ScummScriptDisassembly ReadObjectVerb(int room, ScummRoomObjectRecord record, int verbId, out ScummObjectVerbEntry selectedEntry) =>
        InRoomFile(_source.ReadObjectVerb(room, record, verbId, out selectedEntry), room)!;

    // The archive is the room files one after another, each behind an 8-byte LF header: an offset in it is (room's start + 8) plus the offset in the file.
    private ScummScriptDisassembly? InRoomFile(ScummScriptDisassembly? script, int room)
    {
        if (script is null || !_rooms.TryGetValue(room, out var stored)) return script;
        return script with
        {
            ContainerName = $"{room:00}.LFL",
            CodeFileOffset = script.CodeFileOffset is int offset ? offset - stored.Offset - 8 : null
        };
    }
}
