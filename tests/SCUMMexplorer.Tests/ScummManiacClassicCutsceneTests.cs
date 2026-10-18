using SCUMMexplorer.Core.Maniac;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>Three independently selected source triggers; previews are not an earned story path.</summary>
public sealed class ScummManiacClassicCutsceneTests(ITestOutputHelper output)
{
    private static string Folder => Path.Combine(TestGames.Root, "mmansion ega en");
    private static readonly Lazy<ScummCutscenePlanner?> Planner = new(() =>
        File.Exists(Path.Combine(Folder, "00.LFL"))
            ? ManiacCutsceneSetup.Planner(new GameFolderScanner().Scan(Folder)) : null);

    [Fact]
    public void NativeCallsAndTextsProduceAnEditionOwnedList()
    {
        if (Planner.Value is not { } planner) return;
        Assert.Equal(59, planner.Triggers.Count);
        Assert.Equal(27, planner.Groups.Count);
        Assert.Equal(25, planner.Groups.Count(group => group.IsScene));
        Assert.Equal("Push", planner.Verbs.WordFor(9));
        Assert.Equal("Turn off", planner.Verbs.WordFor(5));
        Assert.Equal("Turn on", planner.Verbs.WordFor(4));
        var bell = Assert.Single(planner.Groups, group => group.First.Room == 1 && group.First.ObjectId == 394);
        Assert.Equal(9, bell.First.VerbId);
        Assert.Equal("Room 01 · Push doorbell", ScummCutsceneTitles.Title(bell.First));
        Assert.Contains("global script 87", bell.First.Scripts);
        Assert.Equal("Oh, the doorbell!", bell.First.FirstText);
        Assert.Contains("Syd, Bernard", planner.StartNote(bell.First));
    }

    [Theory]
    [InlineData(1, 394, 9, 87, 0x001F, 26, "Oh, the doorbell!")]
    [InlineData(8, 157, 5, 47, 0x0001, 31, "Oh great, someone shut the power off!")]
    [InlineData(17, 190, 4, 112, 0x0000, 40, "Don't forget the stamp!")]
    public void FreshPreviewRunsTheSelectedObjectsOwnScene(int room, int objectId, int verb, int script,
        int offset, int visitedRoom, string expectedLine)
    {
        if (Planner.Value is not { } planner) return;
        var group = Assert.Single(planner.Groups, g => g.First.Room == room && g.First.ObjectId == objectId && g.First.VerbId == verb);
        var run = planner.Run(group.First);
        output.WriteLine($"{ScummCutsceneTitles.Title(group.First)}: begun {run.Begun}, {run.Frames} frames; {run.Failure}");
        Assert.True(run.Begun);
        Assert.Null(run.Failure);
        Assert.InRange(run.Frames, 1, ScummCutsceneRun.EndFrames - 1);
        Assert.Contains(visitedRoom, run.RoomsEntered);

        var game = new GameFolderScanner().Scan(Folder);
        var session = ManiacCutsceneSetup.NewSession(game);
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == room));
        var traceStart = session.SessionTrace.Count;
        session.ActivateObjectVerb(objectId, verb);
        // Native direct invocation needs the verb's active object context, not a stale startup choice.
        Assert.Equal(objectId, session.Snapshot.KnownGlobalVariables["var[0x009]"]);
        var sawLine = false;
        for (var frame = 0; frame <= run.Frames; frame++)
        {
            sawLine |= session.Messages.OnScreen.Any(message => message.Text.Contains(expectedLine, StringComparison.Ordinal));
            if (frame < run.Frames) session.AdvanceFrame();
        }
        Assert.True(sawLine, $"Expected native line was not displayed: {expectedLine}");
        Assert.Contains(session.SessionTrace.Skip(traceStart), line =>
            line.StartsWith($"cutscene at Script {script} +0x{offset:X4}", StringComparison.Ordinal));
        Assert.False(session.InCutscene);
        Assert.Equal(room, session.CurrentRoomId);
        var unsupported = session.Snapshot.Uncertainties.Where(line => line.Contains("unsupported v2", StringComparison.Ordinal)).ToArray();
        foreach (var limitation in unsupported) output.WriteLine(limitation);
        // The doorbell returns to Room 1 and starts Ed's later background movement.
        // Its closest-object query remains unsupported; this proof claims the scene's begin/end and lines only.
        Assert.All(unsupported, line => Assert.True(room == 1 && line.Contains("Script 174 +0x0000: unsupported v2 getClosestObjActor", StringComparison.Ordinal), line));
    }
}
