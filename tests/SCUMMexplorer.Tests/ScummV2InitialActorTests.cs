namespace SCUMMexplorer.Tests;

public sealed class ScummV2InitialActorTests
{
    [Theory]
    [InlineData("mmansion ega en", true)]
    [InlineData("zak ega en", true)]
    [InlineData("mmansionE ega es", false)]
    public void NewGameUsesNativeOldActorDefaultsBeforeBoot(string fixture, bool classic)
    {
        var folder = Path.Combine(TestGames.Root, fixture);
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = (ScummV2DataSource)ScummDataSources.For(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), data: data, boot: false);
        Assert.All(session.Actors, actor =>
        {
            var look = actor.Look;
            Assert.Equal(classic ? actor.Number : 0, look.Costume);
            Assert.Equal((2, 0, 1, 5, 4), (look.InitFrame, look.WalkFrame, look.StandFrame, look.TalkStartFrame, look.TalkStopFrame));
            Assert.Equal((8, 2), (look.WalkSpeedX, look.WalkSpeedY));
            Assert.Equal(0, actor.Room);
            Assert.False(actor.Visible);
            Assert.All(look.Palette, color => Assert.Equal(0, color));
        });
    }

    [Fact]
    public void GenericPreviewRetainsV3Defaults()
    {
        Assert.All(new ScummActorTable().Snapshot(), actor =>
        {
            var look = actor.Look;
            Assert.Equal(0, look.Costume);
            Assert.Equal((1, 2, 3, 4, 5), (look.InitFrame, look.WalkFrame, look.StandFrame, look.TalkStartFrame, look.TalkStopFrame));
            Assert.Equal((8, 2), (look.WalkSpeedX, look.WalkSpeedY));
        });
    }
}
