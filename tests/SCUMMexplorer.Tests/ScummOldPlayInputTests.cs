using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

public sealed class ScummOldPlayInputTests
{
    [Fact]
    public void OldBoxMatrixUsesItsRowOffsetsAndSignedUnreachableEntries()
    {
        var room = new byte[64];
        room[0x15] = 24;
        room[24] = 2;
        // count row offsets, then two rows. Deliberately reversed offsets test lookup semantics.
        new byte[] { 2, 0, 255, 1, 0, 1 }.CopyTo(room, 41);
        var boxes = ScummWalkBoxReader.ReadV2(room);
        Assert.Equal(1, boxes.GetNextBox(0, 1));
        Assert.Equal(-1, boxes.GetNextBox(1, 0));
        Assert.Equal(-1, boxes.Clone().GetNextBox(1, 0));
    }
    [Fact]
    public void QueuedScreenClicksReachNativePartyMenuThenVerbKeysReachScriptFour()
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out var indexed);
        Assert.IsType<ScummV2Charset>(session.PlayCharset());
        Assert.Equal(4, session.PlayInputScript);
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
        Assert.NotEmpty(session.PlaySentenceGlyphs());
        var rendered = new ScummPlayScreen(game, session, indexed.States).Compose(0);
        Assert.Contains(rendered.Verbs.Items, item => item.IsText);
        Assert.Contains(rendered.Verbs.Pixels.Take(8 * 320), color => color == 13);
        Assert.Null(session.PressPlayKey(319, out var saveRefusal));
        Assert.NotNull(saveRefusal);
        Assert.Null(session.PressPlayKey(322, out var restartRefusal));
        Assert.NotNull(restartRefusal);
        Assert.DoesNotContain(session.Snapshot.Uncertainties, s => s.Contains("unsupported v2"));
        var walk = session.Verbs.First(v => v.VerbId == 13 && v.Mode == 1);
        session.PressPlayKey(walk.Key, out _);
        session.AdvanceFrame();
        session.ClickScreen(8 - session.ViewLeft, 120 + session.MainScreen.Top, out _);
        for (var frame = 0; frame < 1000 && session.CurrentRoomId == 44; frame++) session.AdvanceFrame();
        Assert.Equal(1, session.CurrentRoomId);
        Assert.DoesNotContain(session.Snapshot.Uncertainties, s => s.Contains("unsupported v2"));
    }
}
