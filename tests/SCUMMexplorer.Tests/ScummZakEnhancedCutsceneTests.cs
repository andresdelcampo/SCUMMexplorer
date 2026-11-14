using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>Zak v2 S7: derived triggers, source spans and fresh-start exploration runs; not a played story route.</summary>
public sealed class ScummZakEnhancedCutsceneTests(ITestOutputHelper output)
{
    private static string Folder => Path.Combine(TestGames.Root, "zakE ega en");
    private static readonly Lazy<ScummCutscenePlanner?> Planner = new(() =>
        Directory.Exists(Folder) ? new ScummCutscenePlanner(new GameFolderScanner().Scan(Folder)) : null);

    [Fact]
    public void ScanFollowsNativeCallsAndKeepsSourceTitles()
    {
        if (Planner.Value is not { } planner) return;
        // 8 November 2026: scenes that take control by hiding the interface count too (ScummSceneMarks).
        // 9 November 2026: the widened candidates (R-8.41) are entries too.
        // 13 November 2026: an object whose only code is its fallback verb is a trigger (Room 44's fence).
        // 14 November 2026: an object's fallback verb is scanned beside its other verbs (one more trigger here).
        Assert.Equal(286, planner.Triggers.Count);
        Assert.Equal(182, planner.Groups.Count);
        Assert.Equal(43, planner.Groups.Count(g => g.IsScene));
        Assert.Equal("Read", planner.Verbs.WordFor(12));
        Assert.Contains(planner.Groups, g => ScummCutsceneTitles.Title(g.First) == "Room 03 · Push doorbell");
        Assert.Contains(planner.Groups, g => g.First.Room == 2 && g.First.IsRoomEntry && g.First.Scripts.Contains("global script 126"));
        // Buy is created by Room 7, not by boot. An unknown word stays honest rather than borrowing another edition's table.
        Assert.Equal("verb 0x11", planner.Verbs.WordFor(17));
    }

    [Theory]
    [InlineData(8, 0, 0, 140, 0x0004)]
    [InlineData(3, 147, 9, 39, 0x0000)]
    [InlineData(7, 34, 17, 24, 0x0000)]
    public void SourceEstablishedTriggerBeginsFresh(int room, int objectId, int verb, int scriptId, int offset)
    {
        if (Planner.Value is not { } planner) return;
        var group = Assert.Single(planner.Groups, g => g.First.Room == room && g.First.ObjectId == objectId && g.First.VerbId == verb);
        Assert.Contains($"global script {scriptId}", group.First.Scripts);
        var data = ScummDataSources.For(new GameFolderScanner().Scan(Folder));
        Assert.Contains(data.GlobalScript(scriptId)!.Instructions, i => i.Offset == offset && i.Operation == "cutscene");
        var run = planner.Run(group.First);
        output.WriteLine($"{ScummCutsceneTitles.Title(group.First)}: begun {run.Begun}, frames {run.Frames}, rooms {string.Join(',', run.RoomsEntered)}, failure {run.Failure}");
        Assert.True(run.Begun, run.Failure ?? "No attributable cutscene began");
        Assert.Null(run.Failure);
        Assert.True(run.Frames < ScummCutsceneRun.EndFrames, "Scene reached the end-frame bound");
    }
}
