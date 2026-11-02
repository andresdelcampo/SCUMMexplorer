using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The user's Indy 3 256-colour reports of 2 November 2026, fixed as engine rules for every game: a picked room takes the screen its entry or the
/// opening gives it, a print with no position goes on where the last one ended, a picked scene does not replay a room's arrival, a room only one script
/// shows is shown by it, and the opening's leftover route ends on a pick. Record: <c>.verification/user-reports-20261102/README.md</c>.
/// </summary>
public sealed class UserReports20261102Tests(ITestOutputHelper output)
{
    private static GameLibrary? Indy3Vga()
    {
        var folder = Path.Combine(TestGames.Root, "indy3 vga en");
        return File.Exists(Path.Combine(folder, "00.LFL")) ? new GameFolderScanner().Scan(folder) : null;
    }

    private static GameResource Room(GameLibrary game, int id) => game.Rooms.First(r => r.ResourceId == id);

    [Fact]
    public void ADrawnStringWithNoPositionGoesOnWhereTheLastEnded()
    {
        // Room 75's entry +0x00A7 prints "follow the " at x 0xBA, then +0x00C4 "first" with no position: drawString leaves the slot's x at the text's
        // right edge (_string[a].xpos = _charset->_str.right), and version 3 keeps it as the default.
        if (Indy3Vga() is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        session.VisitRoom(Room(game, 75));
        for (var frame = 0; frame < 20; frame++) session.AdvanceFrame();
        var drawn = session.Messages.Drawn;
        foreach (var message in drawn) output.WriteLine($"({message.X}, {message.Y}) {message.Text}");
        var follow = drawn.First(m => m.Text.StartsWith("follow the", StringComparison.Ordinal));
        var ordinal = drawn.First(m => m.Text is "first" or "second" or "third");
        Assert.Equal(follow.Y, ordinal.Y);
        Assert.True(ordinal.X > follow.X + 40, $"the ordinal starts at {ordinal.X}, the line at {follow.X}");
    }

    [Fact]
    public void APickedSceneDoesNotReplayTheRoomsArrival()
    {
        // Room 82's entry starts local script 200, the arrival: a cutscene that beheads a soldier and walks Indy on to Room 83. Give Grail (object 873)
        // entering the room does not play it.
        if (Indy3Vga() is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        session.ArrivalsSkippedFor = new ScummCutscene(82, 873, "", 0x03, ["Room 82 object 873 verb 0x03"], 0, 0, 0, "");
        session.VisitRoom(Room(game, 82));
        session.ArrivalsSkippedFor = null;
        Assert.Contains(session.Snapshot.Uncertainties, line => line.Contains("its arrival (local script 200", StringComparison.Ordinal));
        for (var frame = 0; frame < 600; frame++) session.AdvanceFrame();
        Assert.Equal(82, session.CurrentRoomId);
    }

    [Fact]
    public void ARoomOnlyOneScriptLoadsIsShownByIt()
    {
        // Room 74 (the plane's manual) is loaded only by Script 90, which prints its instructions; the boot's logo room (76) is the opening's own.
        if (Indy3Vga() is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        Assert.Equal("global script 90", session.ShownOnlyBy(74));
        Assert.Null(session.ShownOnlyBy(76));
        Assert.Null(session.ShownOnlyBy(2));
    }

    [Fact]
    public void APickedRoomTakesTheScreenItsEntryOrTheOpeningGivesIt()
    {
        // Room 80's entry gives the room the whole screen (roomOps setScreen 8, 200): a still preview runs no entry code but takes that. Room 76 is shown
        // only by the boot (setScreen 16, 152, before any verb exists).
        if (Indy3Vga() is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        var zeppelin = session.PrepareAmbientRoom(Room(game, 80), requireAmbient: false).Session!;
        Assert.Equal((8, 200), zeppelin.MainScreen);
        var logo = session.PrepareAmbientRoom(Room(game, 76), requireAmbient: false).Session!;
        Assert.Equal((16, 152), logo.MainScreen);
        Assert.Empty(logo.PlayVerbArea().Pixels.Where(pixel => pixel != 0).Take(1));
    }

    [Fact]
    public void APickedRoomEndsTheOpeningsLeftoverRoute()
    {
        // Script 1 goes on after it hands over control (+0x040D "wet clothes", a walk, then loadRoom 73); a picked room does not continue it.
        if (Indy3Vga() is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        session.VisitRoom(Room(game, 13));
        for (var frame = 0; frame < 600; frame++) session.AdvanceFrame();
        Assert.Equal(13, session.CurrentRoomId);
    }
}
