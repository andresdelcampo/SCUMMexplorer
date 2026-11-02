using SCUMMexplorer.Core.Scumm;
using Xunit;

namespace SCUMMexplorer.Tests;

public class RoomHotspotRulesTests
{
    private static ScummRoomObjectRecord Record(int id, int index, int x = 10, int y = 10, int w = 20, int h = 20, int parent = 0, byte parentState = 0) =>
        new(id, index, x, y, w, h, parent, parentState, 0, 0, 0, 0, null);

    private static ScummSessionSnapshot Snapshot(Dictionary<int, byte>? owners = null, Dictionary<int, byte>? states = null,
        Dictionary<int, RoomObjectPosition>? positions = null, Dictionary<int, uint>? classes = null) =>
        new(1, states ?? [], positions ?? [], owners ?? [], new Dictionary<string, ushort>(), [], true, [], 0, [], Classes: classes);

    [Fact]
    public void AnObjectNeedsABoxAPlaceOnTheSceneAndTheRoomAsOwner()
    {
        var records = new[] { Record(1, 1), Record(2, 2, w: 0), Record(3, 3, x: 0, y: 0), Record(4, 4) };
        var snapshot = Snapshot(owners: new() { [4] = 3 });
        Assert.True(RoomHotspotRules.IsHotspot(records[0], records, snapshot));
        Assert.False(RoomHotspotRules.IsHotspot(records[1], records, snapshot));
        // 0, 0 is off the scene only in LOOM (R-6.14, its draft previews); findObject has no such test (Maniac enhanced Room 44's exit lies there).
        Assert.True(RoomHotspotRules.IsHotspot(records[2], records, snapshot));
        Assert.False(RoomHotspotRules.IsHotspot(records[2], records, snapshot, originIsOffScene: true));
        Assert.False(RoomHotspotRules.IsHotspot(records[3], records, snapshot));
    }

    [Fact]
    public void AnUnlistedOwnerFollowsTheVersionNotTheGame()
    {
        var records = new[] { Record(1, 1) };
        var snapshot = Snapshot();
        Assert.True(RoomHotspotRules.IsHotspot(records[0], records, snapshot, ownerWhenUnlisted: 0x0F));
        Assert.False(RoomHotspotRules.IsHotspot(records[0], records, snapshot, ownerWhenUnlisted: 0));
    }

    [Fact]
    public void AParentMustBeInTheRequiredState()
    {
        var records = new[] { Record(1, 1), Record(2, 2, parent: 1, parentState: 2) };
        Assert.False(RoomHotspotRules.IsHotspot(records[1], records, Snapshot(states: new() { [1] = 1 })));
        Assert.True(RoomHotspotRules.IsHotspot(records[1], records, Snapshot(states: new() { [1] = 2 })));
        // Old engines keep the intrinsic bit only.
        Assert.True(RoomHotspotRules.IsHotspot(Record(5, 3, parent: 1, parentState: 8), [.. records, Record(5, 3, parent: 1, parentState: 8)],
            Snapshot(states: new() { [1] = 8 }), stateMask: 8));
    }

    [Fact]
    public void DoorwaysInOneRectangleShareABoxAndTheModeDecides()
    {
        var spots = new[]
        {
            new RoomHotspot(1, 1, "door", 5, 5, 10, 10, [3]), new RoomHotspot(2, 2, null, 5, 5, 10, 10, [7, 3]),
            new RoomHotspot(3, 3, "cup", 40, 5, 10, 10, []), new RoomHotspot(4, 4, "gate", 60, 5, 10, 10, [9])
        };
        var all = RoomHotspotRules.DoorwayBoxes(spots, _ => true, item => $"o{item.ObjectId}", _ => true);
        Assert.Equal(2, all.Count);
        Assert.Equal([3, 7], all[0].Targets);
        Assert.Equal("o1\no2", all[0].Tip);
        var hover = RoomHotspotRules.DoorwayBoxes(spots, group => group.Any(item => item.Contains(62, 6)), item => "", item => item.ObjectId != 4);
        var only = Assert.Single(hover);
        Assert.False(only.Live);
        Assert.Equal([9], only.Targets);
    }

    [Fact]
    public void RepeatedNamesCarryTheirIndexAndThePickPrefersATouchableObject()
    {
        var spots = new[]
        {
            new RoomHotspot(1, 1, "leaf", 0, 0, 30, 30, []), new RoomHotspot(2, 2, "leaf", 50, 0, 10, 10, []),
            new RoomHotspot(3, 3, null, 0, 0, 30, 30, []), new RoomHotspot(4, 4, "gate", 0, 0, 30, 30, [9])
        };
        Assert.Equal(["leaf"], RoomHotspotRules.RepeatedLabels(spots));
        Assert.Equal(1, RoomHotspotRules.PointedAt(spots, 5, 5)?.ObjectId);
        Assert.Equal(3, RoomHotspotRules.PointedAt(spots, 5, 5, item => item.ObjectId == 1)?.ObjectId);
        Assert.Null(RoomHotspotRules.PointedAt(spots, 200, 5));
    }
}
