namespace SCUMMexplorer.Tests;

public sealed class ScummManiacClassicSessionTests
{
    [Fact]
    public void PartyClicksRunTheClassicIntroductionToStablePlayerControl()
    {
        var folder = Path.Combine(TestGames.Root, "mmansion ega en");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = new ScummV1DataSource(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), data: data);
        Assert.Equal(45, session.CurrentRoomId);
        // Assumed player choices, delivered through the native input script and object verbs.
        foreach (var point in new[] { (76, 104), (196, 104), (292, 60) })
        {
            session.Click(point.Item1, point.Item2);
            for (var frame = 0; frame < 100; frame++) session.AdvanceFrame();
        }
        for (var frame = 0; frame < 5000 && !session.PlayerHasControl; frame++) session.AdvanceFrame();
        Assert.Equal(44, session.CurrentRoomId);
        Assert.True(session.PlayerHasControl);
        Assert.False(session.InCutscene);
        Assert.Equal(1, session.Snapshot.KnownGlobalVariables["var[0x030]"]);
        Assert.Equal(5, session.Snapshot.KnownGlobalVariables["var[0x031]"]);
        Assert.Contains(session.SessionTrace, line => line.Contains("Room 49 after loadRoom"));
        Assert.DoesNotContain(session.Snapshot.Uncertainties, line => line.Contains("unsupported v2") || line.Contains("no indexed destination"));
        Assert.Contains(session.Verbs, verb => verb.Mode == 1 && verb.Name is not null);
        for (var frame = 0; frame < 120; frame++) session.AdvanceFrame();
        Assert.Equal(44, session.CurrentRoomId);
        Assert.True(session.PlayerHasControl);
        Assert.False(session.InCutscene);
    }
}
