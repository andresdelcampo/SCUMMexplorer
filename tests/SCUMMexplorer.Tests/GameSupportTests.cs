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
        Assert.True(cd.Texts); // the extractor reads the CD scripts and objects too

        var indy = GameSupport.For(Game(GameId.IndianaJones3, ScummVersion.V3));
        Assert.True(indy.Rooms && indy.Texts);
        Assert.False(indy.LiveSession || indy.Cutscenes || indy.Status || indy.Audio);

        // Any other old-v3 game whose index parsed gets the same limited look: room pictures and texts.
        var otherV3 = GameSupport.For(Game(GameId.Unknown, ScummVersion.V3));
        Assert.True(otherV3.Rooms && otherV3.Texts);
        Assert.False(otherV3.LiveSession || otherV3.Cutscenes || otherV3.Status || otherV3.Audio || otherV3.Scripts);

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
