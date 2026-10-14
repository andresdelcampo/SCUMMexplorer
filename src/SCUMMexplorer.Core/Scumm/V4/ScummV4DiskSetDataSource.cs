using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The data of a v4 game whose rooms sit on several disks (<c>DISK01.LEC</c> to <c>DISK04.LEC</c> for Monkey Island's floppy release), behind one
/// <see cref="IScummDataSource"/>. A resource lives on exactly one disk: the index (<c>000.LFL</c>) names the room that holds it, and each disk's
/// <see cref="ScummV4Archive"/> lists only the rooms it stores, so the owning disk is the first one that has the room (or, for a global script
/// and for a costume, the one whose directory places it).
/// <para>
/// Assumption (labelled, S4b MonkeyFloppy, 14 October 2026): the game asks the player to change disks (ScummVM <c>openRoom</c> asks for the disk
/// of a room); here every disk is available at all times, so a resource is never "not inserted" and the boot never waits for a disk. The disk a
/// script came from is kept as the container file number of the script it returns.
/// </para>
/// </summary>
public sealed class ScummV4DiskSetDataSource : IScummDataSource
{
    private readonly IReadOnlyList<(ScummV4Archive Archive, ScummV4DataSource Source)> _disks;
    private readonly Dictionary<int, ScummV4DataSource> _roomOwner = [];

    public ScummV4DiskSetDataSource(ScummV4Disks disks, ScummScriptDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(disks);
        var list = new List<(ScummV4Archive, ScummV4DataSource)>();
        for (var i = 0; i < disks.Disks.Count; i++)
            if (disks.Disks[i] is ScummV4Archive archive)
                list.Add((archive, new ScummV4DataSource(archive, dialect, i + 1, ScummV4Archive.DiskFileName(i + 1))));
        if (list.Count == 0) throw new InvalidDataException("The game has no v4 disk.");
        _disks = list;
        foreach (var (archive, source) in list)
            foreach (var room in archive.Rooms)
                _roomOwner.TryAdd(room.Number, source);
    }

    /// <summary>The disks the set reads, in order (disk 1 first).</summary>
    public int DiskCount => _disks.Count;

    /// <summary>The index of the game (<c>000.LFL</c>) is the same on every disk: the object table is read from the first.</summary>
    public ScummIndexedObjectSnapshot ReadIndexedObjects() => _disks[0].Archive.ReadIndexedObjects();

    public int GlobalScriptCount => ScummV4Archive.LocalScriptBase;

    public IReadOnlyList<int> GlobalScriptIds() => _disks.SelectMany(disk => disk.Archive.GlobalScriptNumbers()).Distinct().Order().ToArray();

    public ScummScriptDisassembly? GlobalScript(int number)
    {
        foreach (var (_, source) in _disks)
            if (source.GlobalScript(number) is { } script) return script;
        return null;
    }

    public ScummScriptDisassembly? RoomEntryScript(int room) => Owner(room).RoomEntryScript(room);

    public ScummScriptDisassembly? RoomExitScript(int room) => Owner(room).RoomExitScript(room);

    public IReadOnlyList<int> RoomLocalScriptIds(int room) => Owner(room).RoomLocalScriptIds(room);

    public ScummScriptDisassembly? RoomLocalScript(int room, int number) => Owner(room).RoomLocalScript(room, number);

    public ScummRoomData ReadRoom(int room) => Owner(room).ReadRoom(room);

    public ScummCostume? ReadCostume(int id)
    {
        foreach (var (_, source) in _disks)
            if (source.ReadCostume(id) is { } costume) return costume;
        return null;
    }

    public IReadOnlyList<RoomObjectImage> ReadObjectImages(int room) => Owner(room).ReadObjectImages(room);

    public IReadOnlyDictionary<int, IReadOnlyList<ScummObjectVerbEntry>> ReadObjectVerbs(int room, IReadOnlyList<ScummRoomObjectRecord> records) =>
        Owner(room).ReadObjectVerbs(room, records);

    public ScummScriptDisassembly ReadObjectVerb(int room, ScummRoomObjectRecord record, int verbId, out ScummObjectVerbEntry selectedEntry) =>
        Owner(room).ReadObjectVerb(room, record, verbId, out selectedEntry);

    private ScummV4DataSource Owner(int room) =>
        _roomOwner.TryGetValue(room, out var source) ? source : throw new InvalidDataException($"Room {room:00} is on none of the game's {_disks.Count} disks.");
}
