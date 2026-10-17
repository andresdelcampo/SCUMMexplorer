using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Tests;

public sealed class ScummActorCapacityTests
{
    [Fact]
    public void DefaultTableRetainsTwelveScriptActors()
    {
        var actors = new ScummActorTable();
        Assert.Equal(13, actors.Capacity);
        Assert.Equal(12, actors.Snapshot().Count);
        Assert.True(actors.Contains(12));
        Assert.False(actors.Contains(13));
        Assert.False(actors.Contains(0));
        Assert.False(actors.Contains(-1));
    }

    [Fact]
    public void LargerTableTracksLastActorWithoutChangingDefaultTable()
    {
        var actors = new ScummActorTable(actorCount: 25);
        var place = new ScummActorTable.Placement(1, null, null, null);
        actors.Put(24, 80, 40, 2, place, ego: 3);
        actors.SetTalkColor(24, 6);
        Assert.True(actors.Contains(24));
        Assert.False(actors.Contains(25));
        Assert.Equal(24, actors.Snapshot().Count);
        Assert.Equal((80, 40, 2), (actors[24].X, actors[24].Y, actors[24].Room));
        Assert.Equal(6, actors.TalkColor(24));
        Assert.Equal(12, new ScummActorTable().Snapshot().Count);
    }

    [Theory]
    [InlineData("mmansion ega en", 25)]
    [InlineData("mmansionE ega es", 25)]
    [InlineData("zak ega en", 13)]
    [InlineData("zakE ega en", 13)]
    public void ProfileUsesEngineActorAllocation(string folder, int expected)
    {
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return;
        var game = new GameFolderScanner().Scan(path);
        Assert.Equal(expected, ScummProfile.For(game).ActorCount);
    }
}
