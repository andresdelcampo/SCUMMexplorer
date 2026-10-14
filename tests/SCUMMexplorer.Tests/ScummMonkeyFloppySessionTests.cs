using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S4b MonkeyFloppy (14 October 2026): the data-driven <see cref="ScummGameSession"/> boots The Secret of Monkey Island (floppy, SCUMM v4, four
/// disks) with the game's own scripts: the copy-protection screen (Room 90, a labelled assumed answer read from the game's own variable), the
/// title credits (Room 10), the opening (Room 38, then Room 96's title music) and the dock (Room 33) where the player has control.
/// Record: <c>.verification/monkey1-session-20261014/README.md</c>. Skips when the game folder is absent.
/// </summary>
public sealed class ScummMonkeyFloppySessionTests
{
    private static string? Folder()
    {
        var path = Path.Combine(SupportMatrixTests.RepositoryRoot(), "Games", "monkey1 ega en");
        return File.Exists(Path.Combine(path, "DISK01.LEC")) ? path : null;
    }

    private static ScummGameSession Boot(string folder, out GameLibrary game)
    {
        game = new GameFolderScanner().Scan(folder);
        var data = (ScummV4DiskSetDataSource)ScummDataSources.For(game);
        return new ScummGameSession(game, data.ReadIndexedObjects(), 0, null, data);
    }

    [Fact]
    public void TheProfileGivesTheV4DialectAndOneSourceOverEveryDisk()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);

        Assert.Equal(ScummScriptDialect.V4, ScummProfile.For(game).Dialect);
        var data = Assert.IsType<ScummV4DiskSetDataSource>(ScummDataSources.For(game));
        Assert.Equal(4, data.DiskCount);
        // A resource lives on exactly one disk: the boot script, a script of a later disk's room and the first room all come through the one source.
        Assert.NotNull(data.GlobalScript(1));
        Assert.NotNull(data.RoomEntryScript(33));
        Assert.Contains(game.Rooms, room => room.ResourceId == 96);
        Assert.NotNull(data.RoomEntryScript(96));
        var onDisks = game.Rooms.Select(room => room.ContainerFileNumber).Distinct().Order().ToArray();
        Assert.Equal([1, 2, 3, 4], onDisks);
        Assert.Throws<InvalidDataException>(() => data.ReadRoom(250));
    }

    [Fact]
    public void TheBootReachesTheDockInABoundedNumberOfFramesWithNothingUnsupported()
    {
        if (Folder() is not { } folder) return;

        var session = Boot(folder, out _);
        var report = session.BootReport!;

        Assert.False(report.HitFrameLimit);
        Assert.InRange(report.Frames, 1000, 4000);
        Assert.Equal(33, report.FinalRoom);
        // Protection (90), the cleared screen (0), the title credits (10), no room, the opening (38), the title music (96), then the Mêlée dock (33).
        Assert.Equal([90, 0, 10, 0, 0, 38, 96, 33], report.RoomsEntered);
        Assert.Equal(33, session.Snapshot.RoomId);
        Assert.True(session.PlayerHasControl);
        Assert.False(session.BootRunning);
        Assert.False(session.InCutscene);
        // The boot ended script 1 by following the ego into its room (setCameraFollows starts the scene), as ScummVM does.
        Assert.Contains(session.Snapshot.Uncertainties, item => item.Contains("Room 38 entered by Script 01 +0x0E26 (actorFollowCamera)"));

        // R-9.19: every operation the run could not follow would be listed here. Display-only operations are named: they change no game state.
        string[] unsupported = ["not modeled", "could not", "disassembly stops", "undecoded", "cannot", "needs a session", "unknown", "depends on"];
        Assert.DoesNotContain(session.Snapshot.Uncertainties, item => unsupported.Any(item.Contains) && !item.Contains("seeded pick"));

        // The ego stands on the dock with its own costume, put there by the game's scripts (loadRoomWithEgo in Room 96's title script).
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal(33, ego.Room);
        Assert.Equal(1, ego.Look.Costume);
        Assert.True(ego.Visible);
        // The interface loop (script 23) and the verb script the engine runs for every click and key (script 4) are set by script 1.
        Assert.Equal(4, session.Snapshot.KnownGlobalVariables[ScummVars.VerbScript]);
        Assert.Contains("Script 23 +0x0155", session.PendingScriptSummary);
    }

    [Fact]
    public void TheCopyProtectionIsAnsweredWithTheGamesOwnYearAndSaysSo()
    {
        if (Folder() is not { } folder) return;
        var session = Boot(folder, out _);

        // R-15.5, R-1.11: Enter for the "press ENTER" screen, then the four digits of the year the game's script derived (var[0x10A]); each key
        // is a labelled assumed input and goes through the game's own input script (script 153, then 155).
        Assert.Contains(session.SessionTrace, item => item.StartsWith("boot: Player input assumed: key", StringComparison.Ordinal));
        var digits = session.SessionTrace.Where(item => item.StartsWith("boot: Player input assumed: the digits of var[0x10A]", StringComparison.Ordinal)).ToArray();
        var answer = Assert.Single(digits);
        Assert.Contains("the answer the game's own script derived", answer);
        Assert.Equal(5, session.SessionTrace.Count(item => item.StartsWith("key: ", StringComparison.Ordinal)));
        Assert.Contains(session.Snapshot.Uncertainties, item => item.Contains("sent to the game's input script as assumed input"));
        // The right answer lets the game go on (a wrong one four times quits); the game's own check is script 152 +0x01C6.
        Assert.DoesNotContain(session.SessionTrace, item => item.Contains("quit"));
    }

    [Fact]
    public void TheDockReachesAStableStateWithNothingUnsupported()
    {
        if (Folder() is not { } folder) return;
        var session = Boot(folder, out _);

        for (var frame = 0; frame < 900; frame++) session.AdvanceFrame();

        Assert.Equal(33, session.CurrentRoomId);
        Assert.True(session.PlayerHasControl);
        string[] unsupported = ["not modeled", "could not", "disassembly stops", "undecoded", "cannot", "needs a session", "depends on"];
        Assert.DoesNotContain(session.Snapshot.Uncertainties, item => unsupported.Any(item.Contains) && !item.Contains("seeded pick"));
        // The boot's script is gone; the room's ambient script and the interface loop run on.
        Assert.DoesNotContain(session.PendingScriptSummary, item => item.StartsWith("Script 01 ", StringComparison.Ordinal));
    }

    [Fact]
    public void TheBootIsTheSameEveryRun()
    {
        if (Folder() is not { } folder) return;

        var first = Boot(folder, out _).BootReport!;
        var second = Boot(folder, out _).BootReport!;

        Assert.Equal(first.Frames, second.Frames);
        Assert.Equal(first.RoomsEntered, second.RoomsEntered);
    }

    [Fact]
    public void VersionFourReadsOneValueForAnActorsScale()
    {
        // o5_actorOps case 17 (script_v5.cpp): version 4 reads one value for both scales, later versions two. Raw sub-opcode 0x13 is case 17 through
        // the small-header table. Bytes: actorOps (0x13) actor 3, scale (sub 0x13) 0xB4, terminator 0xFF, then stopObjectCode.
        byte[] code = [0x13, 0x03, 0x13, 0xB4, 0xFF, 0xA0];

        var v4 = new ScummScriptReader(ScummScriptDialect.V4).DecodeCode(code, 1, 1, 0);

        Assert.Equal("actorOps", v4.Instructions[0].Operation);
        Assert.Equal("0x03, [scale, 0xB4]", v4.Instructions[0].Operands);
        Assert.Equal("stopObjectCode", v4.Instructions[1].Operation);
    }
}
