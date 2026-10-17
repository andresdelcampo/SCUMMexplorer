using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

public sealed class ScummOldFirstVisitRulesTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(4, false)]
    [InlineData(8, true)]
    [InlineData(10, true)]
    public void OldObjectFlagsOnlyDrawTheIntrinsicBit(byte flags, bool visible)
    {
        var image = new RoomObjectImage(1, 8, 8, 1, 1, [3]);
        var selection = new RoomObjectSelection([image], new Dictionary<int, byte> { [1] = flags }, intrinsicStateMask: 8);
        Assert.Equal(visible, selection.IsActive(image));
    }

    [Fact]
    public void V2PseudoRoomInstructionsAcceptTheirHexadecimalOperands()
    {
        var folder = Path.Combine(TestGames.Root, "zakE ega en");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var index = ScummEgoArrivalIndex.For(game);
        Assert.NotEmpty(index.PseudoNumbersOf(23));
        Assert.NotEmpty(index.PseudoNumbersOf(36));
    }
}
