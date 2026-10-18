namespace SCUMMexplorer.Tests;

public sealed class ScummManiacClassicFidelityTests
{
    [Fact]
    public void GenuineDoorbellInputContinuesThroughScript174AndEdsOutdoorResponse()
    {
        var folder = Path.Combine(TestGames.Root, "mmansion ega en");
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var observed = new List<ScummScriptObservation>();
        var session = ManiacClassicStateRoute.AcquireKey(folder,
            new ScummScriptObserver(observed.Add, site => site.Script == 174,
                key => key is "var[0x0C8]" or "var[0x0C9]"));
        var game = new GameFolderScanner().Scan(folder);
        var bell = ScummDataSources.For(game).ReadRoom(1).Objects.Single(obj => obj.Id == 394);
        for (var frame = 0; frame < 1200 && !session.PlayerHasControl; frame++) session.AdvanceFrame();
        Assert.True(session.PlayerHasControl);
        var push = session.Verbs.Single(verb => verb.VerbId == 9 && verb.Mode == 1);
        void Click(int x, int y)
        { Assert.NotNull(session.ClickScreen(x, y, out _)); session.AdvanceFrame(); }
        Click(push.X!.Value + 4, push.Y!.Value + 4);
        Assert.Equal(9, session.Snapshot.KnownGlobalVariables[ScummVars.V2.SentenceVerb]);
        var x = bell.X + bell.Width / 2 - session.ViewLeft;
        Assert.InRange(x, 0, 319);
        var y = bell.Y + bell.Height / 2 + session.MainScreen.Top;
        Click(x, y); Click(x, y);
        var messages = new HashSet<string>();
        for (var frame = 0; frame < 3000; frame++)
        {
            foreach (var message in session.Messages.OnScreen) messages.Add(message.Text);
            session.AdvanceFrame();
        }
        Assert.Contains(messages, message => message.Contains("Oh, the doorbell!"));
        Assert.Contains(messages, message => message.Contains("Can't you read the sign?"));
        Assert.Contains(messages, message => message.Contains("NOW GET OUT OF HERE!"));
        Assert.Contains(observed, observation => observation.Site is { Offset: 0, Operation: "getClosestObjActor" });
        Assert.Contains(observed, observation => observation.Site is { Stage: "instruction", Offset: 0x2E, Operation: "stopObjectCode" });
        Assert.DoesNotContain(session.Snapshot.Uncertainties, line => line.Contains("unsupported v2"));
        Assert.False(session.InCutscene);
        Assert.True(session.PlayerHasControl);
        Assert.Equal(3, session.Snapshot.ObjectOwners[55]);
        Assert.Equal(26, session.Actors.Single(actor => actor.Number == 11).Room);
    }
}
