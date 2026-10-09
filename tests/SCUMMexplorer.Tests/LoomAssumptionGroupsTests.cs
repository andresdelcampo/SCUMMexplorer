using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

public sealed class LoomAssumptionGroupsTests
{
    [Fact]
    public void AssumptionsAreGroupedByWhatTheyMeanForTheScreen()
    {
        var groups = LoomAssumptionGroups.Group([
            "Room 02 was selected from the catalog: the ego's size (var[0x07C], script 017) is estimated as 2 from the assumed arrival's route.",
            "Room 02 entry script +0x0000: presentation operation not applied: resourceRoutines",
            "Script 14 +0x0000: presentation operation not applied: lights",
            "Camera x (var[0x002]) = 540 is an estimate: the camera is put on the ego.",
            "Object 154 verb 0x36 was activated directly; input dispatch, actor walk, and class-based hit selection were not simulated.",
            "The artwork of 1 object(s) is estimated.",
            "Script 06 +0x01BB: findObject reads the virtual mouse: assumed input: no pointer over the room",
            "Something unexpected"
        ]);
        Assert.Equal(["Arrived by a shortcut", "Estimates", "Effects not drawn", "Other"], groups.Select(g => g.Title));
        Assert.Equal(2, groups[2].Items.Count);
        Assert.Equal(3, groups[1].Items.Count);
        Assert.All(groups, g => Assert.False(string.IsNullOrWhiteSpace(g.Meaning)));
    }
}
