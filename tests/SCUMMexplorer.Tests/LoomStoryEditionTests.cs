using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomStoryEditionTests
{
    [Theory]
    [InlineData("loom ega mt32 en", 0xD71, "bit[0xD9C]", "bit[0xDC7]")]
    [InlineData("ALTERNATIVE LOOMS/loom ega mt32 es", 0xD72, "bit[0xD9D]", "bit[0xDC8]")]
    [InlineData("loom vga en", 0x12, "bit[0x03D]", "bit[0x068]")]
    public void StoryControlsUseTheirOwnEditionAndChangeRustysSourceBranch(string edition, int first, string ghost, string restored)
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", edition));
        var game = new GameFolderScanner().Scan(folder);
        var data = ScummDataSources.For(game);
        var index = game.Vga?.ReadIndexedObjects() ?? new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL"));
        var session = new LoomExplorationSession(game, index);
        Assert.Equal(first, LoomStoryEvents.FirstBitFor(game));
        Assert.Contains(session.StoryEvents, e => e.Key == ghost);
        Assert.Contains(session.StoryEvents, e => e.Key == restored);
        Assert.All(session.StoryEvents, e => Assert.True(e.Bit >= first));
        Assert.Contains(data.RoomEntryScript(34)!.Instructions, i => i.Operation == "notEqualZero" && i.Operands.StartsWith(ghost + ","));
        session.VisitRoomById(5);
        LoomCdSceneSetupPlanner.Settle(session);
        session.UpdateStoryBit(ghost, true);
        session.UpdateStoryBit(restored, false);
        session.VisitRoomById(34);
        for (int n = 0; n < 40; n++) session.AdvanceFrame();
        Assert.Equal(120, session.Actors.Single(a => a.Number == 3).Look.Costume);
        session.VisitRoomById(5);
        LoomCdSceneSetupPlanner.Settle(session);
        session.UpdateStoryBit(restored, true);
        session.VisitRoomById(34);
        Assert.Equal(0, session.Actors.Single(a => a.Number == 3).Room);
    }
}
