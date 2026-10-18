using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S7 Cutscenes for The Secret of Monkey Island CD (SCUMM v5, read from its disc image; 17 October 2026): the engine's scan (<see cref="ScummCutsceneIndex"/>)
/// lists the game's cutscene triggers from the v5 resource file, titled with the verb names the CD's own verb script gives after the boot (2 Open … 8 Look at,
/// 0x0A Talk to, 0x0B Walk to; the floppy's numbers differ), and three scenes begin from a fresh start in a headless run of the generic session
/// (<see cref="ScummCutsceneRun"/>): the poster's Look at on the dock (Room 33 object 429 verb 0x08, <c>cutscene</c> +0x00CF to <c>endCutscene</c> +0x0134,
/// "Re-elect Governor Marley."), Room 37's entry (global script 60, "Hey!  I don't like visitors!") and, later in the game, Room 19's entry on the ship (local
/// script 200, which goes on to Room 7). Room 37's scene starts its music from the disc. Registry: Cutscenes ◐. Record:
/// <c>.verification/monkeycd-cutscenes-20261017/README.md</c>. Skips when the disc image is absent.
/// </summary>
public sealed class ScummMonkeyCdCutsceneTests(ITestOutputHelper output)
{
    private static string? Folder()
    {
        var path = Path.Combine(TestGames.Root, "monkey1 vga en");
        return File.Exists(Path.Combine(path, "The_Secret_of_Monkey_Island.cue")) ? path : null;
    }

    private const int LookAt = 0x08;

    private static readonly Lazy<ScummCutscenePlanner?> Planner =
        new(() => Folder() is { } folder ? new ScummCutscenePlanner(new GameFolderScanner().Scan(folder)) : null);

    private static ScummCutsceneGroup GroupOf(ScummCutscenePlanner planner, int room, int objectId, int verb) =>
        Assert.Single(planner.Groups, g => g.First.Room == room && (g.First.ObjectId == objectId && g.First.VerbId == verb ||
                                                                   g.Others.Any(o => o.ObjectId == objectId && o.VerbId == verb)));

    [Fact]
    public void TheScanReadsTheCdAndTitlesWithItsOwnVerbNames()
    {
        if (Planner.Value is not { } planner) return;
        output.WriteLine($"{planner.Triggers.Count} triggers, {planner.Groups.Count} groups, {planner.Groups.Count(g => g.IsScene)} scenes");
        // The CD's verb script (run by the boot) names its verbs; the numbers are the CD's, not the floppy's.
        Assert.Equal("Look at", planner.Verbs.WordFor(0x08));
        Assert.Equal("Talk to", planner.Verbs.WordFor(0x0A));
        Assert.Equal("Walk to", planner.Verbs.WordFor(0x0B));
        Assert.Equal("Pick up", planner.Verbs.WordFor(0x09));
        Assert.True(planner.Groups.Count >= 150, $"only {planner.Groups.Count} groups");

        Assert.Equal("Room 33 · Look at poster", ScummCutsceneTitles.Title(GroupOf(planner, 33, 429, LookAt).First));
        Assert.Equal("Room 19 · Talk to mutinous crew", ScummCutsceneTitles.Title(GroupOf(planner, 19, 259, 0x0A).First));
        // A verb the game's verb script does not name keeps its number (the code an inventory object reaches when given or used on someone).
        Assert.Equal("Room 61 · verb 0x50 Sword Master", ScummCutsceneTitles.Title(GroupOf(planner, 61, 744, 0x50).First));

        var meathook = GroupOf(planner, 37, 0, 0);
        Assert.Equal("Room 37 · Entry", ScummCutsceneTitles.Title(meathook.First));
        Assert.True(meathook.IsScene);
        Assert.StartsWith("Hey!  I don't like visitors!", meathook.First.FirstText, StringComparison.Ordinal);

        var ship = GroupOf(planner, 19, 0, 0);
        Assert.True(ship.IsScene);
        Assert.StartsWith("First of all", ship.First.FirstText, StringComparison.Ordinal);
    }

    [Fact]
    public void ThreeScenesBeginFromAFreshStart()
    {
        if (Planner.Value is not { } planner) return;
        foreach (var (room, objectId, verb) in new[] { (33, 429, LookAt), (37, 0, 0), (19, 0, 0) })
        {
            var group = GroupOf(planner, room, objectId, verb);
            var run = planner.Run(group.First);
            output.WriteLine($"{ScummCutsceneTitles.Title(group.First)}: begun {run.Begun}, {run.Frames} frames, rooms [{string.Join(", ", run.RoomsEntered)}] {run.Failure}");
            Assert.True(run.Begun, $"Room {room} object {objectId} verb 0x{verb:X2}: {run.Failure}");
            Assert.StartsWith("A fresh start begins its cutscene", planner.StartNote(group.First), StringComparison.Ordinal);
        }
        // The ship's scene goes on in Room 7.
        Assert.Contains(7, planner.Run(GroupOf(planner, 19, 0, 0).First).RoomsEntered);
    }

    [Fact]
    public void ThePostersCutsceneShowsItsLineFromAFreshStart()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == 33));
        session.ActivateObjectVerb(429, LookAt);
        var frames = 0;
        while (frames < 120 && !session.Messages.OnScreen.Any(m => m.Text.StartsWith("Re-elect Governor Marley.", StringComparison.Ordinal)))
        {
            session.AdvanceFrame();
            frames++;
        }
        Assert.True(frames < 120, "the poster's line never showed");
        Assert.True(session.InCutscene);
        Assert.Contains(session.SessionTrace, line => line.StartsWith("cutscene at Room 33 Object 429 Verb 0x08 +0x00CF", StringComparison.Ordinal));
    }

    [Fact]
    public void AnEntrySceneStartsItsMusicFromTheDisc()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameFamily.MonkeyCd, GameSupport.FamilyOf(game));
        var cd =ScummCdAudioTracks.For(game)!;
        // Room 19's scene (the ship, Part II), started from the list as the window does: its local script 200 starts sound 102 at +0x0009, a track of the
        // disc (ScummVM playCDTrackFromSoundID); the lookout's sound 98 (an iMuse sound in the resource file) is stopped by Room 38's exit.
        var session = ScummGameSession.BootForPlay(game, out _);
        var at = session.SoundEvents.Count;
        session.EnterRoomAgain(game.Rooms.First(r => r.ResourceId == 19));
        for (var frame = 0; frame < 300; frame++) session.AdvanceFrame();
        var events = session.SoundEvents.Skip(at).ToList();
        foreach (var item in events)
            output.WriteLine($"{item.Event.Kind} {item.Event.TargetId} {item.Event.SourceLabel} +0x{item.Event.Offset:X4} executed {item.Event.Executed} disc {item.Event.TargetId is { } id && cd.Plays(id)}");
        Assert.Contains(events, item => item.Event.Kind == ScummScriptEventKind.SoundStarted && item.Event.TargetId == 102 &&
                                        item.Event.SourceLabel == "Room 19 local script 200" && item.Event.Offset == 0x0009 && item.Event.Executed);
        Assert.True(cd.Plays(102));
        Assert.True(session.InCutscene || session.SessionTrace.Any(t => t.StartsWith("cutscene at Room 19 local script 200", StringComparison.Ordinal)));
    }
}
