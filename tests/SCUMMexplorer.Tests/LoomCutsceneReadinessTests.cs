using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

public sealed class LoomCutsceneReadinessTests
{
    private static string Folder => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));

    [Fact]
    public void AStateDependentSceneGetsTheStaffItReadsAndAFreshGameHasNot()
    {
        var game = new GameFolderScanner().Scan(Folder);
        var snapshot = new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL"));
        var readiness = new LoomCutsceneReadiness(game, () => new LoomExplorationSession(game, snapshot));
        // Room 02, object 156 (the sky), verb 0x3A (Opening): the fresh game prints 3 lines; with the staff held, 27 (checked 11 October).
        var plan = readiness.Plan(2, 156, 0x3A, [91]);
        Assert.True(plan.Staff, plan.Note);
        Assert.True(plan.Result.Lines > plan.Base.Lines, plan.Note);

        var session = new LoomExplorationSession(game, snapshot);
        Assert.True(LoomCutsceneReadiness.Settle(session));
        Assert.Contains("staff held", LoomCutsceneReadiness.Apply(session, plan));
        Assert.True(session.GameStatus.StaffHeld);
        // A session that has it already is left alone: nothing is added, nothing lowered.
        Assert.Empty(LoomCutsceneReadiness.Apply(session, plan));
        Assert.True(session.GameStatus.StaffHeld);
    }

    [Fact]
    public void ASceneThatReadsNoStateAddsNone()
    {
        var game = new GameFolderScanner().Scan(Folder);
        var snapshot = new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL"));
        var readiness = new LoomCutsceneReadiness(game, () => new LoomExplorationSession(game, snapshot));
        Assert.False(readiness.Plan(2, 154, 0x36, []).HasEdits);
    }
}
