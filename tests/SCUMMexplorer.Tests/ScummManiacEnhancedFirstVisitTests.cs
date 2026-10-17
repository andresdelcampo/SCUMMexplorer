using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Maniac;

namespace SCUMMexplorer.Tests;

public sealed class ScummManiacEnhancedFirstVisitTests
{
    [Theory]
    [InlineData(1, new int[] { 56, 57, 62, 63, 64 })]
    [InlineData(2, new int[] { })]
    [InlineData(44, new int[] { })]
    public void ReviewedPartyPreviewPinsThreeRooms(int room, int[] active)
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var scene = ManiacFirstVisit.Run(game, room);
        Assert.Equal(ScummSettleOutcome.Settled, scene.Outcome);
        Assert.InRange(scene.Frames, 90, 600);
        Assert.True(ScummProfile.For(game).HasFirstVisitScenes);
        Assert.Equal(active, scene.Images.Where(scene.Selection.IsActive).Select(image => image.Id).Order().ToArray());
        Assert.Empty(scene.Gaps());
        Assert.Contains(scene.Notes, note => note.StartsWith("Player input assumed:"));
        Assert.Equal(room == 44, scene.IsFirstRoomOfTheGame);
        Assert.NotEmpty(scene.ActorLayer().Actors);
        foreach (var record in scene.Records.Where(scene.IsHotspot))
            Assert.Equal(0, scene.Snapshot.ObjectStates.GetValueOrDefault(record.Id) & 2);
        foreach (var image in scene.Images.Where(scene.Selection.IsActive))
            Assert.NotEqual(0, scene.Snapshot.ObjectStates[image.Id] & 8);
        Assert.Equal(scene.Selection.PaintOrder().Select(i => i.Id), scene.NewSelection().PaintOrder().Select(i => i.Id));
    }
}
