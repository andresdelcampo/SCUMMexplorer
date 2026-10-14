using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

/// <summary>A CD room as the game's own entry script leaves it: the background, the object records and images, the session snapshot and the picture selection built from it.</summary>
public sealed record LoomCdScene(RoomImage Room, IReadOnlyList<ScummRoomObjectRecord> Records, IReadOnlyList<RoomObjectImage> Images,
    ScummSessionSnapshot Snapshot, RoomObjectSelection Selection, int CameraX,
    IReadOnlyList<ScummActorSnapshot> Actors, ScummWalkBoxSet? WalkBoxes, Func<int, ScummCostume?> Costumes)
{
    public IReadOnlyList<byte>? ShadowPalette { get; init; }
    /// <summary>The room's pixels with the objects the entry script made visible painted over the background.</summary>
    public byte[] Compose() => Selection.Compose(Room);
}

/// <summary>
/// First-visit state of a CD room. The index (0O) gives every object state 0, so what a room shows comes from running its entry script in a
/// fresh session, as the EGA room cards do. Where the script branches on a value the session does not know (a seeded pick), the session's
/// uncertainties say so; nothing is estimated beyond that (no artwork gap filling: the CD backgrounds carry the permanent scenery).
/// </summary>
public static class LoomCdRoomScene
{
    public static LoomCdScene FirstVisit(GameLibrary game, int roomId, int randomSeed = 0)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.Id != GameId.LoomVga || game.V4Archive is not { } vga) throw new InvalidDataException("This is not the LOOM CD game.");
        var resource = game.Rooms.FirstOrDefault(item => item.ResourceId == roomId)
            ?? throw new InvalidDataException($"Room {roomId:00} is not in this game.");
        var indexed = vga.ReadIndexedObjects();
        var session = new LoomExplorationSession(game, indexed, randomSeed, completeBoot: false);
        var snapshot = session.VisitRoom(resource);
        var data = ScummDataSources.For(game);
        var records = data.ReadRoom(roomId).Objects;
        var images = data.ReadObjectImages(roomId);
        var room = vga.DecodeRoom(roomId);
        var selection = new RoomObjectSelection(images, snapshot.ObjectStates, snapshot.ObjectPositions, records, room, indexed.States, snapshot.DrawOrder);
        return new LoomCdScene(room, records, images, snapshot, selection, session.CameraX ?? 0, session.Actors, session.CurrentWalkBoxes, session.ReadCostume)
            { ShadowPalette = session.ShadowPalette.ToArray() };
    }
}
