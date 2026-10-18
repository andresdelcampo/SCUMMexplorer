using SCUMMexplorer.Core.Maniac;

namespace SCUMMexplorer.Tests;

public sealed class ScummManiacClassicEditionBoundaryTests
{
    private static string Folder => Path.Combine(TestGames.Root, "mmansion ega en");

    [Fact]
    public void UnreviewedMenuBytesRejectAutomaticInputsBeforeChangingTheSession()
    {
        if (!Directory.Exists(Folder)) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        Assert.Equal(45, session.CurrentRoomId);
        // Substitute only the in-memory resource descriptor, never the fixture bytes.
        // Room 1 is intentionally not a menu: this is a negative boundary, not another release.
        var other = game.Rooms.Single(room => room.ResourceId == 1);
        var unreviewed = game with { Resources = game.Resources.Select(resource =>
            resource.Kind == ResourceKind.Room && resource.ResourceId == 45
                ? resource with { FilePath = other.FilePath } : resource).ToArray() };
        var trace = session.SessionTrace.ToArray();
        var variables = session.Snapshot.KnownGlobalVariables.OrderBy(pair => pair.Key).ToArray();
        var error = Assert.Throws<InvalidDataException>(() => ManiacFirstVisit.Prepare(unreviewed, session));
        Assert.Contains("no reviewed first-visit input recipe", error.Message);
        Assert.Equal(45, session.CurrentRoomId);
        Assert.Empty(session.DeliveredInput);
        Assert.False(session.HasPendingPlayInput);
        Assert.Equal(trace, session.SessionTrace);
        Assert.Equal(variables, session.Snapshot.KnownGlobalVariables.OrderBy(pair => pair.Key).ToArray());
    }

    [Fact]
    public void EditionLabelAloneDoesNotEnableSpanishPreviewConversion()
    {
        if (!Directory.Exists(Folder)) return;
        var game = new GameFolderScanner().Scan(Folder);
        Assert.Null(ManiacPreviewText.For(game));
        Assert.DoesNotContain("not read for this game yet", game.DetectionNote);
        Assert.Null(ManiacPreviewText.For(game with { Edition = "Spanish, synthetic label only" }));
        Assert.Equal(ScummVersion.V1, ScummProfile.For(game).EngineVersion);
        Assert.Equal(ScummScriptDialect.V1, ScummProfile.For(game).Dialect);
    }
}
