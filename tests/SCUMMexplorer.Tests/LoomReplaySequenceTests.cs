using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomReplaySequenceTests
{
    [Fact]
    public void AnUnmatchedStageNeverSendsItsFollowingInput()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 10));
        var sequence = new LoomReplaySequence(session, [
            new("verb", 295, 0x38),
            new("scene", Frames: 40, EndBoundary: "endCutscene at Script 53 +0x02AC", ExpectedRoom: 12),
            new("verb", 295, 0x36)]);
        while (sequence.Continue()) { session.AdvanceFrame(); sequence.FrameAdvanced(); }
        Assert.NotNull(sequence.Failure);
        Assert.False(sequence.Complete);
        Assert.Equal(1, sequence.Stage);
        Assert.Equal(0x38, sequence.LastAction!.VerbId);
    }

    [Fact]
    public void CheckingAWaitTwiceDoesNotAdvanceItsFrameCount()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        var sequence = new LoomReplaySequence(session, [new("frames", Frames: 1)]);
        Assert.True(sequence.Continue()); Assert.True(sequence.Continue());
        session.AdvanceFrame(); sequence.FrameAdvanced();
        Assert.False(sequence.Continue()); Assert.True(sequence.Complete);
    }
}
