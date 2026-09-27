using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Actor rooms, positions and walk boxes (ScummVM <c>Actor::putActor</c>,
/// <c>adjustActorPos</c>, <c>startWalkActor</c>, <c>startScene</c> and
/// <c>o5_loadRoomWithEgo</c> for v3), and the queries that read them.
/// </summary>
public sealed class LoomActorTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    private static LoomWalkBoxSet Room02Boxes() =>
        LoomWalkBoxReader.Read(File.ReadAllBytes(Path.Combine(Folder, "02.LFL")));

    [Fact]
    public void PutInTheCurrentRoomAdjustsIntoABoxAndElsewhereKeepsTheRawPoint()
    {
        if (!File.Exists(Path.Combine(Folder, "02.LFL"))) return;
        var table = new LoomActorTable();
        var place = new LoomActorTable.Placement(2, Room02Boxes(), null, null);

        // (820, 70) is 10 pixels below box 0's segment (811..830, y 60).
        table.Put(3, 820, 70, 2, place, ego: 1);
        var shown = table[3];
        Assert.Equal((2, 820, 60, 0, true), (shown.Room, shown.X, shown.Y, shown.WalkBox, shown.Visible));
        Assert.False(table.EgoPositioned);

        // Another room: hidden, raw point kept, box unchanged; putting the ego sets egoPositioned.
        table.Put(1, 5, 7, 9, place, ego: 1);
        var away = table[1];
        Assert.Equal((9, 5, 7, false, 0), (away.Room, away.X, away.Y, away.Visible, away.WalkBox));
        Assert.True(table.EgoPositioned);

        // startWalkActor outside the current room puts the actor at the destination exactly, not as an estimate.
        table.Walk(1, 100, 110, place, "walk");
        Assert.Equal((100, 110, (string?)null), (table[1].X, table[1].Y, table[1].Estimate));
    }

    [Fact]
    public void AWalkInTheCurrentRoomEndsInItsBoxAsAnEstimate()
    {
        if (!File.Exists(Path.Combine(Folder, "02.LFL"))) return;
        var table = new LoomActorTable();
        var place = new LoomActorTable.Placement(2, Room02Boxes(), null, null);
        table.Put(1, 820, 60, 2, place, ego: 1);

        // Box 4 (837..888, y 118) is reached from box 0 along the chain.
        table.Walk(1, 860, 125, place, "walk");
        var walked = table[1];
        Assert.Equal((860, 118, 4), (walked.X, walked.Y, walked.WalkBox));
        Assert.NotNull(walked.Estimate);

        // An unknown destination makes the actor unknown and drops the estimate.
        table.Walk(1, null, 125, place, "walk");
        Assert.Null(table[1].WalkBox);
        Assert.Null(table[1].Estimate);
        Assert.NotNull(table[1].Unknown);
    }

    [Fact]
    public void TheBootLeavesTheEgoOnTheCliffInRoom02AndScript45Runs()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));

        Assert.Equal(2, session.Snapshot.RoomId);
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal((2, 828, 60, 0, true), (ego.Room, ego.X, ego.Y, ego.WalkBox, ego.Visible));
        Assert.Null(ego.Estimate);
        // Script 045 (Room 02's walk-box watcher) no longer stops at getActorWalkBox.
        Assert.DoesNotContain(session.Snapshot.Uncertainties, line => line.StartsWith("Script 45 ", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadRoomWithEgoPlacesTheEgoAndACatalogVisitLeavesItUnknown()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));

        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 3));
        var browsed = session.Actors.Single(actor => actor.Number == 1);
        Assert.Null(browsed.WalkBox);
        Assert.Contains("selected from the catalog", browsed.Unknown);

        // Local 200 +0x0008: loadRoomWithEgo with walkTo; the walk is an estimate.
        session.ActivateObjectVerb(162, 0x36);
        Assert.True(session.ContinueUntilRoomChanges(8));
        Assert.Equal(4, session.Snapshot.RoomId);
        var arrived = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal((4, true), (arrived.Room, arrived.Visible));
        Assert.NotNull(arrived.WalkBox);
        Assert.Contains("taken to end", arrived.Estimate);
    }

    [Fact]
    public void TheCameraFollowsTheEgoAfterTheBootAndLoadRoomWithEgo()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));

        // Script 001 +0x0233 actorFollowCamera: the ego at x 828 is past the triggers,
        // so the camera moves onto it, kept half a screen from Room 02's right edge (960 - 160).
        Assert.Equal(800, session.CameraX);
        Assert.False(session.CameraEstimated);

        // startScene: a catalog visit starts at the left edge.
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 3));
        Assert.Equal(160, session.CameraX);

        // o5_loadRoomWithEgo puts the camera on the ego, which then walks to (580, 132);
        // Room 04 is 640 wide, so the camera stops at 480, an estimate like the walk.
        session.ActivateObjectVerb(162, 0x36);
        Assert.True(session.ContinueUntilRoomChanges(8));
        Assert.Equal(4, session.Snapshot.RoomId);
        Assert.Equal(480, session.CameraX);
        Assert.True(session.CameraEstimated);
        Assert.Contains(session.Snapshot.Uncertainties, item => item.StartsWith("Camera x (var[0x002]) = 480 is an estimate", StringComparison.Ordinal));
    }
}
