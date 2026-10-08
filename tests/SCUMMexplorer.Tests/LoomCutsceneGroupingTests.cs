using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomCutsceneGroupingTests
{
    [Fact]
    public void SharedResponsesAreCountedAcrossDraftVerbsWithoutMergingDistinctSpells()
    {
        var triggers = Enumerable.Range(0, 24).Select(i =>
            new LoomCdCutscene(4, 100 + i / 3, "hole", 0x39 + i % 3, ["global script 41"], 1, 0, 10, "response"))
            .Concat(new[] {
                new LoomCdCutscene(5, 150, "dye", 0x39, ["Room 5 local script 200"], 3, 0, 40, "dye"),
                new LoomCdCutscene(5, 150, "dye", 0x3A, ["Room 5 local script 200"], 3, 0, 40, "dye")
            }).ToArray();
        var groups = LoomCdCutsceneIndex.Group(triggers);
        Assert.Equal(2, groups.Count);
        Assert.Equal(new[] { 0x39, 0x3A }, groups.Select(g => g.First.VerbId));
        Assert.All(groups, g => Assert.Equal("Room 5 local script 200", Assert.Single(g.First.Scripts)));
    }
}
