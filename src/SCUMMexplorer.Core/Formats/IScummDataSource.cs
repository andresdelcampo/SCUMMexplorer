using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// What the exploration session reads from one room: its object records, the stored name bytes of each object,
/// its walk boxes (null with <see cref="WalkBoxError"/> when they cannot be read) and its size.
/// </summary>
public sealed record ScummRoomData(
    IReadOnlyList<LoomRoomObjectRecord> Objects,
    IReadOnlyDictionary<int, byte[]> ObjectNames,
    LoomWalkBoxSet? WalkBoxes,
    string? WalkBoxError,
    int Width,
    int Height);

/// <summary>
/// Where the session and its script executor get game data: scripts (entry, exit, local, global), a room's objects,
/// images and verb code. Rooms are numbers. The EGA game reads the old-v3 LFL files (<see cref="LoomEgaDataSource"/>),
/// the CD game its DISK01.LEC blocks (<see cref="LoomCdDataSource"/>). Methods throw <see cref="IOException"/> or
/// <see cref="InvalidDataException"/> for data they cannot read, as the readers they replace did.
/// </summary>
public interface IScummDataSource
{
    /// <summary>Scripts below this number are global; local scripts of a room are numbered from it.</summary>
    int GlobalScriptCount { get; }

    /// <summary>The numbers of the game's global scripts, ascending.</summary>
    IReadOnlyList<int> GlobalScriptIds();

    /// <summary>The numbered global script, or null when the game has none under that number.</summary>
    LoomScriptDisassembly? GlobalScript(int number);

    /// <summary>The room's entry script, or null when it has none.</summary>
    LoomScriptDisassembly? RoomEntryScript(int room);

    LoomScriptDisassembly? RoomExitScript(int room);

    /// <summary>The numbers of the room's local scripts, in directory order (empty when they cannot be read).</summary>
    IReadOnlyList<int> RoomLocalScriptIds(int room);

    /// <summary>A local script of the room, or null when the room has none under that number.</summary>
    LoomScriptDisassembly? RoomLocalScript(int room, int number);

    ScummRoomData ReadRoom(int room);

    /// <summary>The room's object images, in the game's palette indices (what <see cref="LoomRoomObjects"/> returns for EGA).</summary>
    IReadOnlyList<RoomObjectImage> ReadObjectImages(int room);

    /// <summary>The verb tables of the room's objects, by object id.</summary>
    IReadOnlyDictionary<int, IReadOnlyList<LoomObjectVerbEntry>> ReadObjectVerbs(int room, IReadOnlyList<LoomRoomObjectRecord> records);

    /// <summary>The code of the object's entry for the verb (or its fallback entry); throws when it has neither.</summary>
    LoomScriptDisassembly ReadObjectVerb(int room, LoomRoomObjectRecord record, int verbId, out LoomObjectVerbEntry selectedEntry);
}

public static class ScummDataSources
{
    /// <summary>The data source for a game: the CD archive for <see cref="GameId.LoomVga"/>, the LFL files otherwise.</summary>
    public static IScummDataSource For(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.Id == GameId.LoomVga ? new LoomCdDataSource(game) : new LoomEgaDataSource(game);
    }
}
