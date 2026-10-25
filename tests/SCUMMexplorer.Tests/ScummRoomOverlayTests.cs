using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Maniac;

namespace SCUMMexplorer.Tests;

public sealed class ScummRoomOverlayTests
{
    [Fact]
    public void ScreenProjectionIncludesNativeCameraAndExcludesTheVerbStrip()
    {
        var viewport = new ScummRoomViewport(336, 16, 144);
        Assert.Equal((400, 84), viewport.ToRoom(64, 100));
        Assert.Equal((64, 100), viewport.ToScreen(400, 84));
        Assert.Null(viewport.ToRoom(0, 15));
        Assert.Null(viewport.ToRoom(0, 144));
        Assert.Null(viewport.ToRoom(-1, 100));
        Assert.Null(viewport.ToRoom(320, 100));
    }

    [Fact]
    public void WideFrameProjectionIsTheWholeRoomAtItsOwnWidth()
    {
        // R-6.49 wide mode: the room rows show the whole room, so room x is frame x and the viewport is as wide as the frame.
        var viewport = new ScummRoomViewport(0, 16, 144, 480);
        Assert.Equal((450, 84), viewport.ToRoom(450, 100));
        Assert.Null(viewport.ToRoom(480, 100));
        Assert.Null(new ScummRoomViewport(0, 16, 144).ToRoom(450, 100));
    }

    [Theory]
    [InlineData("mmansion ega en")]
    [InlineData("mmansionE ega es")]
    [InlineData("zak ega en")]
    [InlineData("zakE ega en")]
    public void NativeFirstRoomOverlaysSplitSourceDoorwaysAndMatchTheActiveDepthMask(string fixture)
    {
        var folder = Path.Combine(TestGames.Root, fixture);
        Assert.True(Directory.Exists(folder), fixture);
        var game = new GameFolderScanner().Scan(folder);
        var scene = GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced ? ManiacFirstVisit.Run(game, 1) : ScummRoomFirstVisit.Run(game, 1);
        var data = ScummDataSources.For(game);
        var finder = new ScummDoorwayFinder(game, data);
        var resource = game.Rooms.Single(room => room.ResourceId == 1);
        var verbs = data.ReadObjectVerbs(1, scene.Records);
        var targets = scene.Records.Select(record => (record.Id, Targets: verbs.GetValueOrDefault(record.Id, [])
                .SelectMany(verb => finder.Find(resource, data.ReadObjectVerb(1, record, verb.VerbId, out _)))
                .Select(target => target.RoomId).Distinct().Order().ToArray()))
            .Where(item => item.Targets.Length > 0).ToDictionary(item => item.Id, item => item.Targets);
        Assert.NotEmpty(targets);
        var objects = ScummRoomOverlays.Objects(scene, targets);
        Assert.NotEmpty(objects);
        Assert.Contains(objects, obj => obj.IsDoorway);
        Assert.All(objects, obj => Assert.True(scene.IsHotspot(scene.Records.Single(record => record.Id == obj.ObjectId))));
        Assert.DoesNotContain(objects.Where(obj => !obj.IsDoorway), obj => targets.ContainsKey(obj.ObjectId));
        Assert.NotNull(scene.Room.DepthMask);
        Assert.NotNull(scene.State.WalkBoxes);
        Assert.NotEmpty(scene.State.WalkBoxes.Boxes);
        var viewport = new ScummRoomViewport(0, 16, 144);
        var tint = ScummRoomOverlays.DepthPixels(scene, viewport);
        var mask = scene.Selection.ComposeDepthMask(scene.Room)!;
        for (var y = 0; y < 200; y++)
            for (var x = 0; x < 320; x++)
            {
                var at = viewport.ToRoom(x, y);
                var expected = at is { } p && p.X < scene.Room.Width && p.Y < scene.Room.Height && mask[p.Y * scene.Room.Width + p.X] != 0;
                Assert.Equal(expected, tint[y * 320 + x] != 0);
            }
    }

    [Theory]
    [InlineData("mmansion ega en")]
    [InlineData("mmansionE ega es")]
    [InlineData("zak ega en")]
    [InlineData("zakE ega en")]
    public void OverlayEligibilityUsesLiveOwnershipUntouchablePositionAndParentState(string fixture)
    {
        var game = new GameFolderScanner().Scan(Path.Combine(TestGames.Root, fixture));
        var scene = GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced ? ManiacFirstVisit.Run(game, 1) : ScummRoomFirstVisit.Run(game, 1);
        var record = scene.Records.First(scene.IsHotspot) with { ParentLocalIndex = 0 };
        var snapshot = scene.Snapshot;
        var records = new[] { record };
        ScummRoomScene Changed(ScummSessionSnapshot state) => scene with { Records = records, State = scene.State with { Snapshot = state } };
        Assert.Single(ScummRoomOverlays.Objects(Changed(snapshot), new Dictionary<int, int[]>()));
        var owners = new Dictionary<int, byte>(snapshot.ObjectOwners) { [record.Id] = 1 };
        Assert.Empty(ScummRoomOverlays.Objects(Changed(snapshot with { ObjectOwners = owners }), new Dictionary<int, int[]>()));
        var positions = new Dictionary<int, RoomObjectPosition>(snapshot.ObjectPositions) { [record.Id] = new(0, 0) };
        Assert.Empty(ScummRoomOverlays.Objects(Changed(snapshot with { ObjectPositions = positions }), new Dictionary<int, int[]>()));
        positions[record.Id] = new(456, 80);
        var moved = Assert.Single(ScummRoomOverlays.Objects(Changed(snapshot with { ObjectPositions = positions }), new Dictionary<int, int[]>()));
        Assert.Equal((456, 80), (moved.X, moved.Y));
        var states = new Dictionary<int, byte>(snapshot.ObjectStates) { [record.Id] = 2 };
        Assert.Empty(ScummRoomOverlays.Objects(Changed(snapshot with { ObjectStates = states }), new Dictionary<int, int[]>()));
        var classes = new Dictionary<int, uint>(snapshot.ObjectClasses) { [record.Id] = 1u << (snapshot.UntouchableClass - 1) };
        Assert.Empty(ScummRoomOverlays.Objects(Changed(snapshot with { Classes = classes }), new Dictionary<int, int[]>()));
        var parent = record with { Id = 9999, LocalIndex = 99, ParentLocalIndex = 0 };
        var child = record with { ParentLocalIndex = 99, ParentRequiredState = 8 };
        var badParent = scene with { Records = new[] { parent, child }, State = scene.State with { Snapshot = snapshot } };
        Assert.DoesNotContain(ScummRoomOverlays.Objects(badParent, new Dictionary<int, int[]>()), obj => obj.ObjectId == child.Id);
    }
}
