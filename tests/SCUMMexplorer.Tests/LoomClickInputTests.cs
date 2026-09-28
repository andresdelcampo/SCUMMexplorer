using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// A click sent as assumed input: runInputScript runs VAR_VERB_SCRIPT (script
/// 005, set by the boot), which chains a room click into the input loop (script
/// 006); script 006 walks the ego to the clicked object and script 007 runs the
/// sentence (verb 0x38, or 0x36 after a double click).
/// </summary>
public sealed class LoomClickInputTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    private static (GameLibrary Game, LoomExplorationSession Session)? Start()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return null;
        var game = new GameFolderScanner().Scan(Folder);
        return (game, new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL"))));
    }

    private static ushort? Var(LoomExplorationSession session, int number) =>
        session.Snapshot.KnownGlobalVariables.TryGetValue($"var[0x{number:X3}]", out var value) ? value : null;

    private static (int X, int Y) Ego(LoomExplorationSession session)
    {
        var ego = session.Actors.Single(actor => actor.Number == 1);
        return (ego.X!.Value, ego.Y!.Value);
    }

    /// <summary>Frames until no actor in the room moves, then a few more for the scripts that waited.</summary>
    private static void Settle(LoomExplorationSession session)
    {
        // A cutscene's delay can leave a frame without a walk between a putActor and the next walk.
        for (int frame = 0, still = 0; frame < 400 && still < 10; frame++)
        {
            session.AdvanceFrame();
            still = session.ActorsMoving ? 0 : still + 1;
        }
    }

    private static void Frames(LoomExplorationSession session, int count, int room)
    {
        for (var frame = 0; frame < count && session.Snapshot.RoomId == room; frame++) session.AdvanceFrame();
    }

    [Fact]
    public void AClickOnRoom02sExitWalksTheEgoAndRunsTheSentence()
    {
        if (Start() is not { } started) return;
        var session = started.Session;
        // Script 001 +0x016B sets VAR_VERB_SCRIPT (var[0x020]) to 5, and +0x0247 turns input on.
        Assert.Equal((ushort?)5, Var(session, 0x020));

        // Room 02 object 153 (class 2, box (376, 8) 176x120, walk point (540, 108)).
        var click = session.Click(450, 60);
        Assert.True(click.IsGameInput);
        Assert.Contains(session.SessionTrace, line => line.Contains("runs script 5 with [2, 0, 0]", StringComparison.Ordinal));
        Assert.Contains(session.SessionTrace, line => line.Contains("Script 05 chains to script 6", StringComparison.Ordinal));
        // Script 006 +0x014C: a class-2 object is the exit the player arrives through.
        Assert.Equal((ushort?)153, Var(session, 0x071));
        // The walk starts from the cliff (828, 60) and goes down the path frame by frame.
        Assert.Equal((828, 60), Ego(session));
        Frames(session, 10, 2);
        Assert.True(Ego(session).X > 828);

        // Script 045 watches the ego's box: entering box 4 runs its cutscene, which puts the ego
        // at (798, 118) (the smaller Bobbin) and walks it to (758, 103). That putActor ends the
        // click's walk, so script 007 finds the ego too far from the exit (getDist > 3).
        Settle(session);
        Assert.Equal((756, 107), Ego(session));
        Assert.Equal(2, session.Snapshot.RoomId);
        // A second click stops at box 10's change of scene ((627, 124), walk to (609, 110)).
        session.Click(450, 60);
        Settle(session);
        Assert.Equal((609, 110), Ego(session));
        // The third click reaches the walk point; script 007 finds the ego close and runs verb 0x38.
        session.Click(450, 60);
        Frames(session, 200, 2);
        Assert.Equal(3, session.Snapshot.RoomId);
        Assert.Contains(session.SessionTrace, line => line.Contains("Script 07 +0x002E: startObject runs object 153 verb 0x38", StringComparison.Ordinal));
        Assert.True(session.LastAction!.IsGameInput);
        Assert.Equal(3, session.LastAction.DestinationRoomId);
    }

    [Fact]
    public void AClickAfterACatalogVisitReachesTheExitFromAnyStart()
    {
        if (Start() is not { } started) return;
        var (game, session) = started;
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 3));
        // Room 03 object 162 covers (0, 0) 72x72; its verb 0x38 starts local 200, which loads Room 04.
        // Every Room 03 box reaches the object's walk box, so the walk's end does not depend on the assumed arrival.
        session.Click(24, 24);
        Frames(session, 200, 3);
        Assert.Equal(4, session.Snapshot.RoomId);
        Assert.Contains("Room 03 local script 200", session.LastAction!.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void ADoubleClickRunsTheSecondSentence()
    {
        if (Start() is not { } started) return;
        var session = started.Session;
        // Room 02 object 156 (class 1): the second click, 10 jiffies after the first at
        // the same point, sets script 006's local[1], so script 007 also runs verb 0x36.
        session.Click(700, 40, doubleClick: true);
        // The ego walks to object 156's walk point in 5 frames before script 007 runs.
        Frames(session, 12, 2);
        Assert.Contains(session.SessionTrace, line => line.Contains("startObject runs object 156 verb 0x36", StringComparison.Ordinal));
        Assert.Equal((ushort?)156, Var(session, 0x067));
        Assert.Contains(session.Snapshot.Uncertainties, line => line.Contains("the second click of a double click", StringComparison.Ordinal));
        Assert.Equal(2, session.Snapshot.RoomId);
    }

    [Fact]
    public void AClickIsNotSentWhileUserInputIsOffOrOutsideThePicture()
    {
        if (Start() is not { } started) return;
        var session = started.Session;
        var outside = session.Click(10, 500);
        Assert.Contains(outside.Uncertainties, line => line.Contains("not sent, because the point is outside the room picture", StringComparison.Ordinal));

        // Script 002 (the cutscene start hook) turns user input off.
        var script = new LoomScriptReader().Decode(started.Game.Resources.Single(item => item.Kind == ResourceKind.Script && item.ResourceId == 2));
        session.RunTopLevelScript(script);
        var ignored = session.Click(450, 60);
        Assert.Contains(ignored.Uncertainties, line => line.Contains("user input is off", StringComparison.Ordinal));
        Assert.DoesNotContain(session.SessionTrace, line => line.Contains("runs script 5", StringComparison.Ordinal));
    }

    [Fact]
    public void StartingARunningScriptAgainEndsTheRunningCopy()
    {
        if (Start() is not { } started) return;
        var session = started.Session;
        // Script 006 runs forever; the click's chainScript starts it again (runScript, not recursive).
        var before = session.Snapshot.PendingScripts;
        session.Click(10, 10);
        Assert.Contains(session.SessionTrace, line => line.Contains("script 6 started again: runScript ended its running copy first", StringComparison.Ordinal));
        Assert.True(session.Snapshot.PendingScripts <= before);
    }
}
