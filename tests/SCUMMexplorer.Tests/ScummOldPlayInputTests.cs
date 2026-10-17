using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

public sealed class ScummOldPlayInputTests
{
    [Fact]
    public void QueuedScreenClicksReachNativePartyMenuThenVerbKeysReachScriptFour()
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        Assert.Equal(45, session.CurrentRoomId);
        foreach (var point in new[] { (76, 104), (196, 104), (292, 60) })
        {
            Assert.NotNull(session.ClickScreen(point.Item1, point.Item2 + session.MainScreen.Top, out var refused));
            Assert.Null(refused);
            for (var frame = 0; frame < 100; frame++) session.AdvanceFrame();
        }
        for (var frame = 0; frame < 5000 && !session.PlayerHasControl; frame++) session.AdvanceFrame();
        Assert.Equal(44, session.CurrentRoomId);
        Assert.True(session.PlayerHasControl);
        var verb = session.Verbs.First(v => v.Mode == 1 && v.Key != 0);
        var input = session.PressPlayKey(verb.Key, out var why);
        Assert.Null(why);
        Assert.Equal(verb.VerbId, input!.Value);
        session.AdvanceFrame();
        Assert.Equal(verb.VerbId, session.Snapshot.KnownGlobalVariables[ScummVars.V2.SentenceVerb]);
        Assert.Equal(input, session.DeliveredInput.Last());
        Assert.Equal(4, session.DeliveredInput.Count);
        Assert.DoesNotContain(session.Snapshot.Uncertainties, s => s.Contains("unsupported v2"));
    }
}
