namespace SCUMMexplorer.Tests;

/// <summary>S5 source-derived previews of this edition; no route or original-game picture match is claimed.</summary>
public sealed class ScummZakEnhancedFirstVisitTests
{
    private static GameLibrary? Game()
    {
        var folder = Path.Combine(TestGames.Root, "zakE ega en");
        return File.Exists(Path.Combine(folder, "00.LFL")) ? new GameFolderScanner().Scan(folder) : null;
    }

    [Theory]
    [InlineData(1, new int[] { 90, 95, 102 })]
    [InlineData(5, new int[] { 169, 170 })]
    [InlineData(7, new int[] { 183, 184, 185, 186, 187, 188 })]
    public void BedroomAndSourceConnectedRoomsKeepTheirSettledObjectStates(int room, int[] expected)
    {
        if (Game() is not { } game) return;
        Assert.True(ScummProfile.For(game).HasFirstVisitScenes);
        var scene = ScummRoomFirstVisit.Run(game, room);
        Assert.Equal(ScummSettleOutcome.Settled, scene.Outcome);
        Assert.Equal(room, scene.Snapshot.KnownGlobalVariables[ScummVars.Room]);
        Assert.Equal(expected, scene.Images.Where(scene.Selection.IsActive).Select(image => image.Id).Distinct().Order().ToArray());
        Assert.Equal(room == 1, scene.IsFirstRoomOfTheGame);
        Assert.Empty(scene.Gaps());
        Assert.Equal(8, scene.IntrinsicStateMask);
        Assert.Equal(scene.Selection.PaintOrder().Select(image => image.Id), scene.NewSelection().PaintOrder().Select(image => image.Id));
        if (room == 1)
        {
            var layer = scene.ActorLayer();
            Assert.Contains(layer.Actors, actor => actor.Number == 1);
            Assert.Contains(Enumerable.Range(0, layer.Pixels.Length), layer.IsDrawn);
        }
        foreach (var record in scene.Records.Where(scene.IsHotspot))
        {
            Assert.Equal(0, scene.Snapshot.ObjectStates.GetValueOrDefault(record.Id) & 2);
            Assert.Equal(15, scene.Snapshot.ObjectOwners.GetValueOrDefault(record.Id, (byte)15));
        }
        if (room != 1)
            Assert.Contains(scene.Snapshot.Uncertainties, note => note.Contains("Assumed arrival", StringComparison.Ordinal));
    }
}
