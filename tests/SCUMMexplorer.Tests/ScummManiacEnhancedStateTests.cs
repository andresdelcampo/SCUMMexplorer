using SCUMMexplorer.Core.Maniac;

namespace SCUMMexplorer.Tests;

public sealed class ScummManiacEnhancedStateTests
{
    [Fact]
    public void SelectedPartyNamesComeFromReachedNamesAndOwnInputBindings()
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        Assert.False(session.CaptureInventoryStatus().CanEdit);
        ManiacCutsceneSetup.PrepareIfNeeded(game, session);
        for (var i = 0; i < 2000 && !session.CaptureInventoryStatus().CanEdit; i++) session.AdvanceFrame();
        var status = session.CaptureInventoryStatus();
        Assert.True(status.CanEdit, status.Refusal);
        Assert.Equal(new[] { 1, 3, 5 }, status.Characters.Select(x => x.ActorId));
        Assert.Equal(new[] { "Syd", "Dave", "Bernard" }, status.Characters.Select(x => x.Name));
        Assert.All(status.Characters, x => Assert.Contains("Script 4", x.Evidence));
    }
}
