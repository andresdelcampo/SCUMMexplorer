using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// What the exploration session reads from one room: its object records, the stored name bytes of each object,
/// its walk boxes (null with <see cref="WalkBoxError"/> when they cannot be read) and its size.
/// </summary>
public sealed record ScummRoomData(
    IReadOnlyList<ScummRoomObjectRecord> Objects,
    IReadOnlyDictionary<int, byte[]> ObjectNames,
    ScummWalkBoxSet? WalkBoxes,
    string? WalkBoxError,
    int Width,
    int Height);

/// <summary>
/// Where the session and its script executor get game data: scripts (entry, exit, local, global), a room's objects,
/// images and verb code. Rooms are numbers. The EGA game reads the old-v3 LFL files (<see cref="ScummV3DataSource"/>),
/// the CD game its DISK01.LEC blocks (<see cref="ScummV4DataSource"/>). Methods throw <see cref="IOException"/> or
/// <see cref="InvalidDataException"/> for data they cannot read, as the readers they replace did.
/// </summary>
public interface IScummDataSource
{
    /// <summary>Scripts below this number are global; local scripts of a room are numbered from it.</summary>
    int GlobalScriptCount { get; }

    /// <summary>The numbers of the game's global scripts, ascending.</summary>
    IReadOnlyList<int> GlobalScriptIds();

    /// <summary>The numbered global script, or null when the game has none under that number.</summary>
    ScummScriptDisassembly? GlobalScript(int number);

    /// <summary>The room's entry script, or null when it has none.</summary>
    ScummScriptDisassembly? RoomEntryScript(int room);

    ScummScriptDisassembly? RoomExitScript(int room);

    /// <summary>The numbers of the room's local scripts, in directory order (empty when they cannot be read).</summary>
    IReadOnlyList<int> RoomLocalScriptIds(int room);

    /// <summary>A local script of the room, or null when the room has none under that number.</summary>
    ScummScriptDisassembly? RoomLocalScript(int room, int number);

    ScummRoomData ReadRoom(int room);

    /// <summary>The costume with this number, or null when the game does not index it.</summary>
    ScummCostume? ReadCostume(int id);

    /// <summary>The room's object images, in the game's palette indices (what <see cref="ScummRoomObjects"/> returns for EGA).</summary>
    IReadOnlyList<RoomObjectImage> ReadObjectImages(int room);

    /// <summary>The verb tables of the room's objects, by object id.</summary>
    IReadOnlyDictionary<int, IReadOnlyList<ScummObjectVerbEntry>> ReadObjectVerbs(int room, IReadOnlyList<ScummRoomObjectRecord> records);

    /// <summary>The code of the object's entry for the verb (or its fallback entry); throws when it has neither.</summary>
    ScummScriptDisassembly ReadObjectVerb(int room, ScummRoomObjectRecord record, int verbId, out ScummObjectVerbEntry selectedEntry);
}

public static class ScummDataSources
{
    /// <summary>The data source for a game: the CD archive for <see cref="GameId.LoomVga"/>, the LFL files otherwise.</summary>
    public static IScummDataSource For(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.Id == GameId.LoomVga ? new ScummV4DataSource(game) : new ScummV3DataSource(game);
    }
}
