using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomCdRunningSetupTests
{
    [Fact]
    public void AnEarnedForemanPreludeIsNotRepeatedAndLaterStatusIsRetained()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom vga en"));
        var game = new GameFolderScanner().Scan(folder);
        var data = ScummDataSources.For(game);
        var triggers = LoomCdCutsceneIndex.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value));
        var planner = new LoomCdSceneSetupPlanner(game, triggers, data);
        var trigger = triggers.Single(t => t.Room == 41 && t.ObjectId == 559 && t.VerbId == 0x4B);
        var setup = planner.Derive(trigger);
        var step = Assert.Single(setup.Before);
        Assert.Equal("bit[0x077]", step.ResultVariable);
        Assert.Equal(1, step.ResultValue);
        var session = new LoomExplorationSession(game, game.Vga!.ReadIndexedObjects());
        session.VisitRoomById(4);
        LoomCdSceneSetupPlanner.Settle(session);
        session.UpdateStaffPossession(true);
        session.UpdateAvailableNotes(8);
        session.UpdateDraftLearned(15, false);
        // Actual source conversation earns the prelude result; no story edit.
        LoomCdSceneSetupPlanner.ApplyBefore(session, game, setup.Before);
        Assert.Equal((ushort)1, session.Snapshot.KnownGlobalVariables[step.ResultVariable!]);
        var adapted = LoomCdSceneSetupPlanner.Adapt(setup, session);
        Assert.Empty(adapted.Before);
        var trace = session.SessionTrace.Count;
        LoomCdSceneSetupPlanner.ApplyBefore(session, game, setup.Before);
        Assert.Equal(trace, session.SessionTrace.Count);
        Assert.Equal(8, session.GameStatus.AvailableNoteCount);
        Assert.False(session.GameStatus.Drafts[15].Learned);
    }

    [Fact]
    public void MissingRepeatedSourceStagesAndUnknownResultsStayInTheSetup()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom vga en"));
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, game.Vga!.ReadIndexedObjects());
        var repeated = new LoomCdSetupStep("scene", 41, 557, 0x36, 0, "Room 41 local 207 +0x0000", ResultVariable: "bit[0x077]", ResultValue: 1);
        var unknown = repeated with { ResultVariable = null, ResultValue = null };
        var setup = new LoomCdStartSetup([repeated, repeated, unknown], [], [], true);
        Assert.Equal(setup.Before, LoomCdSceneSetupPlanner.Adapt(setup, session).Before);
    }
}
