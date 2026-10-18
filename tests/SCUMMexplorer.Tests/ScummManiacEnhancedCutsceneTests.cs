using SCUMMexplorer.Core.Maniac;

namespace SCUMMexplorer.Tests;

public sealed class ScummManiacEnhancedCutsceneTests
{
    private static readonly Lazy<ScummCutscenePlanner?> Planner = new(() =>
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        return Directory.Exists(folder) ? ManiacCutsceneSetup.Planner(new GameFolderScanner().Scan(folder)) : null;
    });

    [Fact]
    public void NativeStartupPreparationTracesItsOwnCutsceneAndPreservesAChosenParty()
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ManiacCutsceneSetup.NewSession(game);
        Assert.Contains(session.SessionTrace, line => line.StartsWith("cutscene at Script 131 +0x0006"));
        var before = session.SessionTrace.Count;
        Assert.False(ManiacCutsceneSetup.PrepareIfNeeded(game, session));
        Assert.Equal(before, session.SessionTrace.Count);
        var partial = ScummGameSession.BootForPlay(game, out _);
        partial.ClickScreen(76, 120, out _);
        Assert.True(partial.HasPendingPlayInput);
        Assert.Throws<InvalidOperationException>(() => ManiacCutsceneSetup.PrepareIfNeeded(game, partial));
        partial.AdvanceFrame();
        Assert.NotEmpty(partial.DeliveredInput);
        before = partial.SessionTrace.Count;
        Assert.Throws<InvalidOperationException>(() => ManiacCutsceneSetup.PrepareIfNeeded(game, partial));
        Assert.Equal(before, partial.SessionTrace.Count);
    }

    [Fact]
    public void ScanFollowsBareOldScriptCallsAndKeepsTheNativePreviewStartDisclosed()
    {
        if (Planner.Value is not { } planner) return;
        Assert.Equal(59, planner.Triggers.Count);
        Assert.Equal(27, planner.Groups.Count);
        Assert.Equal("Abrir", planner.Verbs.WordFor(1));
        Assert.Equal("Qué es", planner.Verbs.WordFor(15));
        Assert.Equal("teléfono", Assert.Single(planner.Triggers, t => t.Room == 5 && t.ObjectId == 136 && t.VerbId == 11).ObjectName);
        var bell = Assert.Single(planner.Triggers, t => t.Room == 1 && t.ObjectId == 394 && t.VerbId == 9);
        Assert.Contains("global script 87", bell.Scripts);
        Assert.True(bell.Texts > 0);
        Assert.Contains("timbre", bell.FirstText);
        Assert.Contains("Syd, Bernard", planner.StartNote(bell));
    }

    [Fact]
    public void DirectTelevisionActionBindsItsOwnNativeSentenceContext()
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ManiacCutsceneSetup.NewSession(game);
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 17));
        session.ActivateObjectVerb(190, 4);
        Assert.Equal(4, session.Snapshot.KnownGlobalVariables[ScummVars.V2.ActiveVerb]);
        Assert.Equal(190, session.Snapshot.KnownGlobalVariables[ScummVars.V2.ActiveObject1]);
        Assert.Equal(0, session.Snapshot.KnownGlobalVariables[ScummVars.V2.ActiveObject2]);
        Assert.NotEqual(0, session.Snapshot.ObjectStates[190] & 8);
    }

    [Theory]
    [InlineData(1, 394, 9, 87, 187, "el timbre")]
    [InlineData(8, 157, 5, 47, 888, "alguien apago la luz")]
    [InlineData(17, 190, 4, 112, 390, "futuros escritores")]
    public void ThreeOwnSourceTriggersBeginFinishAndReturnControlFromFreshPreparedStarts(
        int room, int obj, int verb, int script, int frames, string line)
    {
        if (Planner.Value is not { } planner) return;
        var trigger = Assert.Single(planner.Triggers, t => t.Room == room && t.ObjectId == obj && t.VerbId == verb);
        var run = planner.Run(trigger);
        Assert.True(run.Begun);
        Assert.Null(run.Failure);
        Assert.Equal(frames, run.Frames);

        var game = new GameFolderScanner().Scan(Path.Combine(TestGames.Root, "mmansionE ega es"));
        var session = ManiacCutsceneSetup.NewSession(game);
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == room));
        var trace = session.SessionTrace.Count;
        var messages = session.Messages.Log.Count;
        session.ActivateObjectVerb(obj, verb);
        for (var frame = 0; frame < frames; frame++) session.AdvanceFrame();
        Assert.Contains(session.SessionTrace.Skip(trace), t => t.StartsWith($"cutscene at Script {script} +0x"));
        Assert.Contains(session.Messages.Log.Skip(messages), m => m.SourceLabel == $"Script {script}" && m.Text.Contains(line));
        Assert.False(session.InCutscene);
        Assert.Equal(0, session.Messages.HaveMsg);
        Assert.True(session.PlayerHasControl);
        Assert.Equal(room, session.CurrentRoomId);
    }
}
