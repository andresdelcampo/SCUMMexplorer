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
}
