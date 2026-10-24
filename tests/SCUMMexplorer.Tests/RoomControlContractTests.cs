using SCUMMexplorer.Core.Scumm;
using Xunit;

namespace SCUMMexplorer.Tests;

public class RoomControlContractTests
{
    private static RoomCutsceneEntry Entry(string title) => new(title, "Scenes", "", () => { });

    [Theory]
    [InlineData(0, false, "Play Cutscene ▴")]
    [InlineData(1, false, "Play Cutscene")]
    [InlineData(3, false, "Play Cutscene ▴")]
    [InlineData(0, true, "Play Cutscene")]
    public void RoomCutsceneCaptionFollowsLoom(int entries, bool listing, string expected) =>
        Assert.Equal(expected, RoomControlText.RoomCutsceneCaption(entries, listing));

    [Fact]
    public void RoomCutsceneTipFollowsLoom()
    {
        Assert.Equal(RoomControlText.RoomCutsceneListingTip, RoomControlText.RoomCutsceneTip(true, true, []));
        Assert.Equal(RoomControlText.GlobalCutsceneBusyTip, RoomControlText.RoomCutsceneTip(false, false, [Entry("A")]));
        Assert.Equal("Play Room 02 · Look at leaf", RoomControlText.RoomCutsceneTip(false, true, [Entry("Room 02 · Look at leaf")]));
        Assert.Equal("Choose a cutscene that happens in this room.", RoomControlText.RoomCutsceneTip(false, true, [Entry("A"), Entry("B")]));
    }

    [Fact]
    public void SkipAndPauseWordingFollowLoom()
    {
        Assert.Equal("No cutscene is playing.", RoomControlText.SkipTip(false, false));
        Assert.StartsWith("Skip the cutscene (Escape)", RoomControlText.SkipTip(true, true));
        Assert.Contains("has not set its skip point", RoomControlText.SkipTip(true, false));
        Assert.Equal("▶ Play", RoomControlText.PauseCaption(true));
        Assert.Equal("⏸ Pause", RoomControlText.PauseCaption(false));
    }

    [Fact]
    public void InteractCaptionNamesTheSelectedObject()
    {
        Assert.Equal("Interact ▴", RoomControlText.InteractCaption(null));
        Assert.Equal("Interact · door ▴", RoomControlText.InteractCaption("door"));
    }

    [Fact]
    public void HiddenStateOffersNothing()
    {
        var state = RoomControlState.Hidden;
        Assert.False(state.BarVisible);
        Assert.Empty(state.RoomCutscenes);
        Assert.False(state.Transport.PauseEnabled);
    }
}
