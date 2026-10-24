using SCUMMexplorer.Core.Scumm;
using Xunit;

namespace SCUMMexplorer.Tests;

public class RoomOverlayRulesTests
{
    [Theory]
    [InlineData(RoomOverlayMode.Never, true, false)]
    [InlineData(RoomOverlayMode.Never, false, false)]
    [InlineData(RoomOverlayMode.Hover, true, true)]
    [InlineData(RoomOverlayMode.Hover, false, false)]
    [InlineData(RoomOverlayMode.Always, false, true)]
    public void BoxesFollowTheSelectedMode(RoomOverlayMode mode, bool underPointer, bool shown) =>
        Assert.Equal(shown, RoomOverlayRules.Shows(mode, underPointer));

    [Fact]
    public void DoorwayRowIsOfferedOnlyWithDoorwaysAndHotspotRowAlways()
    {
        Assert.False(RoomOverlayRules.DoorwayOptionOffered(0));
        Assert.True(RoomOverlayRules.DoorwayOptionOffered(1));
        Assert.True(RoomOverlayRules.HotspotOptionOffered());
    }

    [Fact]
    public void HotspotRowsUseLoomsPattern()
    {
        Assert.Equal("Hotspot 3", RoomControlText.HotspotLabel(null, 3, false));
        Assert.Equal("leaf", RoomControlText.HotspotLabel("leaf", 2, false));
        Assert.Equal("dime · 4", RoomControlText.HotspotLabel("dime", 4, true));
        Assert.Equal("Hotspot 2 · object 154 · stored name \"leaf\" · 800, 16 · 16 × 16. Choose a decoded interaction. Use Enter or Space to activate this hotspot.",
            RoomControlText.HotspotTip(2, 154, "leaf", 800, 16, 16, 16));
    }
}
