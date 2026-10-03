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

    [Fact]
    public void ABonusScriptStartRunsRoom34sLookoutSceneToItsEnd()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 34));
        var sequence = new LoomReplaySequence(session, [
            new("script", 200, X: 0x35),
            new("scene", EndBoundary: "endCutscene at Room 34 local script 200 +0x0056", ExpectedRoom: 34)]);
        for (var frame = 0; sequence.Continue() && frame < 3000; frame++) { session.AdvanceFrame(); sequence.FrameAdvanced(); }
        Assert.True(sequence.Complete, sequence.Failure);
        Assert.Contains(session.SessionTrace, line => line.StartsWith("cutscene at Room 34 local script 200 +0x0035", StringComparison.Ordinal));
        Assert.Contains("started directly at +0x0035", sequence.LastAction!.Input);
    }

    private static LoomReplayStep[] RustyPrefix() =>
    [
        new("visit", 34),
        new("verb", 561, 0x44), new("scene", EndBoundary: "endCutscene at Script 85 +0x05CE", ExpectedRoom: 34),
        new("verb", 561, 0x4C), new("scene", EndBoundary: "endCutscene at Room 34 local script 202 +0x015F", ExpectedRoom: 34),
        new("visit", 36), new("staff", 1), new("frames", Frames: 8)
    ];

    [Fact]
    public void TheFirewoodSequenceStartsWithBobbinInRustysLook()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 36));
        var sequence = new LoomReplaySequence(session, [.. RustyPrefix(), new("verb", 599, 0x38)]);
        Assert.True(sequence.RunSilently(8), sequence.Failure);
        Assert.Equal(8, sequence.Stage);
        Assert.True(session.GameStatus.StaffHeld);
        Assert.Equal(103, session.Actors.Single(a => a.Number == 1).Look!.Costume);
        Assert.Equal(36, session.Snapshot.RoomId);
        Assert.Null(sequence.LastAction is { VerbId: 0x38 } ? "sent early" : null);
    }

    [Fact]
    public void RustysGhostSceneStartsAfterTheDreamWithBobbinAsHimself()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 34));
        var sequence = new LoomReplaySequence(session, [.. RustyPrefix(),
            new("verb", 599, 56), new("frames", Frames: 32), new("click", X: 109, Y: 102),
            new("scene", EndBoundary: "endCutscene at Script 86 +0x055C", ExpectedRoom: 38),
            new("verb", 610, 54), new("scene", EndBoundary: "endCutscene at Script 87 +0x09B9", ExpectedRoom: 38),
            new("visit", 34), new("frames", Frames: 40),
            new("verb", 559, 55), new("scene", EndBoundary: "endCutscene at Script 98 +0x0618", ExpectedRoom: 34)]);
        Assert.True(sequence.RunSilently(16), sequence.Failure);
        // Before the ghost scene: Rusty is the ghost (costume 0x78, untouchable chico, touchable Rusty), Bobbin is himself.
        Assert.Equal(0x78, session.Actors.Single(a => a.Number == 3).Look!.Costume);
        Assert.Equal(1, session.Actors.Single(a => a.Number == 1).Look!.Costume);
        Assert.True(session.Snapshot.IsUntouchable(561));
        Assert.False(session.Snapshot.IsUntouchable(586));
        for (var frame = 0; sequence.Continue() && frame < 3000; frame++) { session.AdvanceFrame(); sequence.FrameAdvanced(); }
        Assert.True(sequence.Complete, sequence.Failure);
    }
}
