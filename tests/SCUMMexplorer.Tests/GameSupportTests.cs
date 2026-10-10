using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;

namespace SCUMMexplorer.Tests;

public class GameSupportTests
{
    private static GameLibrary Game(GameId id, ScummVersion version) => new("t", "f", version, "DOS", [], "", id);

    [Fact]
    public void EachGameStatesWhatItSupports()
    {
        var loom = GameSupport.For(Game(GameId.Loom, ScummVersion.V3));
        Assert.True(loom.LiveSession && loom.Cutscenes && loom.Texts && loom.Status && loom.Audio);

        var cd = GameSupport.For(Game(GameId.LoomVga, ScummVersion.V4));
        Assert.True(cd.LiveSession && cd.Cutscenes && cd.Status && cd.Audio);
        Assert.False(cd.Texts); // Game Texts is not offered for the CD game

        var indy = GameSupport.For(Game(GameId.IndianaJones3, ScummVersion.V3));
        Assert.True(indy.Rooms && indy.Texts);
        Assert.False(indy.LiveSession || indy.Cutscenes || indy.Status || indy.Audio);

        var unknown = GameSupport.For(Game(GameId.Unknown, ScummVersion.Unknown));
        Assert.False(unknown.Rooms);
        Assert.Contains("not supported", unknown.Subtitle);
    }
}

public class LoomDraftEvidenceTests
{
    private static LoomDraftStatus Draft(int slot, int forward, int reverse) => new(slot, null, [], null, null, forward, reverse, "");

    [Fact]
    public void ReadingsAreMarkedAndUnnamedDraftsHaveNoEvidence()
    {
        Assert.StartsWith("reading", LoomDraftNames.Evidence(Draft(13, 0x50, 0x51)));
        Assert.StartsWith("decoded", LoomDraftNames.Evidence(Draft(12, 0x4E, 0x4F)));
        Assert.Contains("reverse", LoomDraftNames.Evidence(Draft(12, 0x4E, 0x4F)));
        Assert.StartsWith("decoded", LoomDraftNames.Evidence(Draft(0, 0x3A, 0x3B)));
        Assert.Null(LoomDraftNames.Evidence(Draft(0, 0x01, 0x02))); // slot and verbs do not match the table: shown as "Draft 1"
    }
}
