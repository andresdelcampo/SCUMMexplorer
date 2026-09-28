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
    public void AWalkInTheCurrentRoomStepsAlongTheBoxPath()
    {
        if (!File.Exists(Path.Combine(Folder, "02.LFL"))) return;
        var table = new LoomActorTable();
        var place = new LoomActorTable.Placement(2, Room02Boxes(), null, null);
        table.Put(1, 820, 60, 2, place, ego: 1);

        // Box 4 (837..888, y 118) is reached from box 0 along the chain. startWalkActor only
        // sets MF_NEW_LEG; each walkActor moves the actor by its walk speed (8, 2) at most.
        table.Walk(1, 860, 125, place, "walk");
        Assert.Equal((820, 60, LoomActorTable.MoveNewLeg), (table[1].X, table[1].Y, table[1].Moving));
        var points = new List<(int, int)>();
        for (var frame = 0; frame < 200 && table[1].Moving != 0; frame++)
        {
            var (x, y) = (table[1].X!.Value, table[1].Y!.Value);
            table.StepWalks(place);
            Assert.True(Math.Abs(table[1].X!.Value - x) <= 8 && Math.Abs(table[1].Y!.Value - y) <= 2,
                $"frame {frame}: ({x}, {y}) to ({table[1].X}, {table[1].Y})");
            points.Add((table[1].X!.Value, table[1].Y!.Value));
        }
        var walked = table[1];
        Assert.Equal((860, 118, 4, 0), (walked.X, walked.Y, walked.WalkBox, walked.Moving));
        Assert.True(points.Distinct().Count() > 20);
        Assert.Null(walked.Estimate);

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
    public void LoadRoomWithEgoPlacesTheEgoAndACatalogVisitAssumesASourceBackedArrival()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));

        // From Room 02 (the boot), Room 02 object 153 verb 0x38 +0x002B is the only
        // arrival into Room 03 in Room 02's own code: object 160, walk to (250, 68).
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 3));
        var browsed = session.Actors.Single(actor => actor.Number == 1);
        // A catalog visit shows the state after the assumed arrival: its walk to (250, 68) ends at once (estimate).
        Assert.Equal((3, 250, true, 0), (browsed.Room, browsed.X, browsed.Visible, browsed.Moving));
        Assert.NotNull(browsed.WalkBox);
        Assert.Contains("assumed arrival at Room 02 Object 153 Verb 0x38 +0x002B", browsed.Estimate);
        Assert.True(session.CameraEstimated);
        Assert.Contains(session.Snapshot.Uncertainties, line =>
            line.Contains("Assumed arrival", StringComparison.Ordinal) && line.Contains("the only one in Room 02's own code", StringComparison.Ordinal));

        // Local 200 +0x0008: loadRoomWithEgo with walkTo; the walk is stepped from object 170's walk point.
        session.ActivateObjectVerb(162, 0x36);
        Assert.True(session.ContinueUntilRoomChanges(8));
        Assert.Equal(4, session.Snapshot.RoomId);
        var arrived = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal((4, true), (arrived.Room, arrived.Visible));
        Assert.NotEqual(0, arrived.Moving);
        for (var frame = 0; frame < 200 && session.ActorsMoving; frame++) session.AdvanceFrame();
        arrived = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal((580, 132, 0), (arrived.X, arrived.Y, arrived.WalkBox));
        Assert.Null(arrived.Estimate);
    }

    [Fact]
    public void CatalogArrivalsPreferThePreviousRoomAndPickAmongSeveral()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));

        // Room 17's objects 352 and 356 both lead into Room 16, at different objects:
        // a seeded pick between the two, with the other listed as the alternative.
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 17));
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 16));
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal(16, ego.Room);
        Assert.Contains("assumed arrival at Room 17 Object", ego.Estimate);
        var label = session.Snapshot.Uncertainties.Last(line => line.Contains("Assumed arrival", StringComparison.Ordinal));
        Assert.Contains("seeded pick", label);
        Assert.Contains("among those in Room 17's own code; alternatives: Room 17 Object", label);

        // No decoded loadRoomWithEgo brings the ego into Room 01 (the intro), so it stays unknown.
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 1));
        var unknown = session.Actors.Single(actor => actor.Number == 1);
        Assert.Null(unknown.X);
        Assert.Contains("no decoded loadRoomWithEgo", unknown.Unknown);
    }

    [Fact]
    public void RoomsWithoutLoadRoomWithEgoTakeALoadRoomThatPutsTheEgoIn()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var index = LoomEgoArrivalIndex.For(game, 200);

        // Script 072 +0x0024: loadRoom 0x41, setCameraAt 0xA0, putActorInRoom 1, 0x41,
        // putActorAtObject 1, 0x33B, walkActorTo 1, 0xE4, 0x2C (decoded).
        var room65 = Assert.Single(index.Into(65));
        Assert.False(room65.IsLoadRoomWithEgo);
        Assert.Equal("Script 072", room65.Source);
        Assert.Equal(0x0024, room65.Offset);
        Assert.Equal(1, room65.EgoActor);
        Assert.Equal(160, room65.CameraX);
        Assert.Equal([LoomEgoArrivalStepKind.PutAtObject, LoomEgoArrivalStepKind.Walk], room65.Steps!.Select(step => step.Kind));
        Assert.Equal(0x33B, room65.Steps![0].A);
        // Room 43: scripts 091 and 092 put the ego at (142, 93); 090, 093 and 094 load it
        // without putting the ego in straight-line code, so they are not arrivals.
        Assert.Equal(["Script 091", "Script 092"], index.Into(43).Select(item => item.Source));
        Assert.Equal(["Script 079", "Script 080"], index.Into(32).Select(item => item.Source));

        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 65));
        for (var frame = 0; frame < 200 && session.ActorsMoving; frame++) session.AdvanceFrame();
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal(65, ego.Room);
        Assert.Equal((228, 44), (ego.X, ego.Y));
        Assert.Contains("assumed arrival at Script 072 +0x0024", ego.Estimate);
        Assert.Equal(160, session.CameraX);
        Assert.True(session.CameraEstimated);
        var label = session.Snapshot.Uncertainties.Last(line => line.Contains("Assumed arrival", StringComparison.Ordinal));
        Assert.Contains("loadRoom, then actor 1 put at object 827, then walks to (228, 44)", label);
        Assert.Contains("no decoded loadRoomWithEgo brings the ego in", label);

        // A room with a doorway arrival keeps it: Room 30 has loadRoom arrivals too (script 072
        // +0x004B and others), but its loadRoomWithEgo arrivals come first.
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 30));
        var room30 = session.Snapshot.Uncertainties.Last(line => line.Contains("Assumed arrival", StringComparison.Ordinal));
        Assert.DoesNotContain("Script 072", room30);
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
        // moveCamera follows it 8 pixels a frame once it passes the trigger strips, and
        // Room 04 is 640 wide, so the camera stops at 480.
        session.ActivateObjectVerb(162, 0x36);
        Assert.True(session.ContinueUntilRoomChanges(8));
        Assert.Equal(4, session.Snapshot.RoomId);
        var cameras = new List<int>();
        for (var frame = 0; frame < 200 && (session.ActorsMoving || session.CameraMoving); frame++)
        {
            session.AdvanceFrame();
            cameras.Add(session.CameraX!.Value);
        }
        Assert.Equal(480, session.CameraX);
        Assert.All(cameras.Zip(cameras.Skip(1)), pair => Assert.True(Math.Abs(pair.Second - pair.First) <= 8));
        Assert.All(cameras, x => Assert.Equal(0, x % 8));
    }

    [Fact]
    public void RoomScrollLocksRoom04sCameraToTheHalfItsEntryPicks()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));
        (int? X, ushort Min, ushort Max) Camera() => (session.CameraX,
            session.Snapshot.KnownGlobalVariables["var[0x011]"], session.Snapshot.KnownGlobalVariables["var[0x012]"]);

        // startScene: VAR_CAMERA_MIN_X/MAX_X (vars 17, 18) are half a screen from Room 03's edges.
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 3));
        Assert.Equal((160, (ushort)160, (ushort)160), Camera());

        // Arriving from Room 03, the entry starts local 205: roomScroll 480, 488 (488 is kept at
        // 640 - 160) and setCameraAt 480, the right half.
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 4));
        Assert.Equal((480, (ushort)480, (ushort)480), Camera());

        // From Room 64 (var[0x070] = 0x40), local 204: roomScroll 152, 160 and setCameraAt 160.
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 64));
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 4));
        Assert.Equal((160, (ushort)160, (ushort)160), Camera());
    }
}
