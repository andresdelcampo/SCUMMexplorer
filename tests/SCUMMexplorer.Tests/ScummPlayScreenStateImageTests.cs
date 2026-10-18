using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>Shared play addition of S6 Monkey2 (17 October 2026): the play screen picks each v5 object's image by its current state. Skips without <c>mi2 talkie en</c>.</summary>
public sealed class ScummPlayScreenStateImageTests
{
    private static string? Folder()
    {
        var path = Path.Combine(TestGames.Root, "mi2 talkie en");
        return Directory.Exists(path) ? path : null;
    }

    /// <summary>
    /// Shared play addition (S6 Monkey2, 17 October 2026): the play screen draws each v5 object with the image of its current state (object.cpp
    /// <c>drawObject</c>: <c>getObjectImage(ptr, getState(obj))</c>), as the first visit does, not its first stored image. Room 51's entry script puts
    /// object 0x24D in state 3 (+0x0007); its IM03 differs from its IM01.
    /// </summary>
    [Fact]
    public void ThePlayScreenDrawsEachObjectInItsCurrentState()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out var objects);
        var screen = new ScummPlayScreen(game, session, objects.States);
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 51));
        for (var i = 0; i < 5; i++) session.AdvanceFrame();
        Assert.Equal(51, session.CurrentRoomId);
        Assert.Equal(3, session.Snapshot.ObjectStates.GetValueOrDefault(0x24D));
        var data = (ScummV5DataSource)ScummDataSources.For(game);
        var first = data.ReadObjectImages(51).Single(image => image.Id == 0x24D);
        var third = data.ReadObjectImages(51, new Dictionary<int, byte> { [0x24D] = 3 }).Single(image => image.Id == 0x24D);
        Assert.NotEqual(first.Pixels, third.Pixels);
        var shown = Assert.Single(screen.Scene()!.Images, image => image.Id == 0x24D);
        Assert.Equal(third.Pixels, shown.Pixels);
    }
}
