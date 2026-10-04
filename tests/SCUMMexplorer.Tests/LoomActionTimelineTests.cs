using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomActionTimelineTests
{
    private static string Folder => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));

    [Fact]
    public void TheLastActionReadsAsATimeline()
    {
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));
        for (var i = 0; i < 3000 && !session.GameStatus.CanEdit; i++) session.AdvanceFrame();
        var action = session.ActivateObjectVerb(154, 0x36);
        var steps = LoomActionTimeline.Build(action, new LoomScriptPresenter(LoomGlossary.Load(game)), "hoja", "Look at");
        Assert.StartsWith("hoja → Look at was run directly", steps[0]);
        Assert.True(steps.Any(s => s.Contains("last leaf of Room 02 walks to")), string.Join(" | ", steps));
        Assert.Equal("Result: no scene change followed", steps[^1]);
    }
}
