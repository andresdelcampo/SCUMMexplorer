using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S4b Indy4 (17 October 2026): the data-driven <see cref="ScummGameSession"/> boots Indiana Jones and the Fate of Atlantis (talkie CD, SCUMM v5,
/// <c>ATLANTIS.000</c>/<c>.001</c> on the disc image, iMUSE music) with the game's own scripts: the LucasArts logo (Room 68, Script 74), no room, then
/// Barnett College (Room 4), whose local script 206 plays the opening line and turns user input on. Script 1 takes the same path for boot parameter 0 and
/// for ScummVM's copy-protection bypass value -7873, so no protection question is met and no input is assumed.
/// Record: <c>.verification/indy4-session-20261017/README.md</c>. Skips when the disc image is absent.
/// </summary>
public sealed class ScummIndy4SessionTests
{
    private static readonly string[] Unsupported = ["unsupported", "not modeled", "could not", "disassembly stops", "undecoded", "cannot", "needs a session", "unknown", "depends on"];

    private static string? Folder()
    {
        var path = Path.Combine(TestGames.Root, "indy4 vga en");
        return File.Exists(Path.Combine(path, "ATLANTIS.cue")) ? path : null;
    }

    private static ScummGameSession Boot(string folder, out GameLibrary game)
    {
        game = new GameFolderScanner().Scan(folder);
        var data = (ScummV5DataSource)ScummDataSources.For(game);
        return new ScummGameSession(game, data.ReadIndexedObjects(), 0, null, data);
    }

    private static void AssertNothingUnsupported(ScummGameSession session) =>
        Assert.DoesNotContain(session.Snapshot.Uncertainties, item => Unsupported.Any(item.Contains) && !item.Contains("seeded pick"));

    [Fact]
    public void TheProfileGivesTheV5DialectAndTheV5SourceWithoutBootInput()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var profile = ScummProfile.For(game);

        Assert.Equal(GameFamily.Indy4, GameSupport.FamilyOf(game));
        Assert.Equal(ScummScriptDialect.V5, profile.Dialect);
        Assert.Null(profile.BootInput);
        Assert.True(profile.BootEndsWhenPlayerHasControl);
        // vars.cpp ScummEngine_v5::resetScummVars: _scummVars[74] = 1225 is GID_MONKEY only; nothing is set for GID_INDY4.
        Assert.Empty(profile.GameVariablesAtStart);
        Assert.Contains((ScummVars.InputMode, (ushort)3), profile.EngineVariablesAtStart);

        var data = Assert.IsType<ScummV5DataSource>(ScummDataSources.For(game));
        Assert.NotNull(data.GlobalScript(1));
        Assert.NotNull(data.RoomEntryScript(4));
        Assert.Contains(206, data.RoomLocalScriptIds(4));
        Assert.Equal(15, data.ReadRoom(4).Objects.Count);
    }

    [Fact]
    public void TheBootReachesBarnettCollegeWithThePlayerInControlAndNothingUnsupported()
    {
        if (Folder() is not { } folder) return;

        var session = Boot(folder, out _);
        var report = session.BootReport!;

        Assert.False(report.HitFrameLimit);
        Assert.InRange(report.Frames, 300, 2000);
        // The logo (68), no room (Script 74 +0x005D), Barnett College (4).
        Assert.Equal([68, 0, 4], report.RoomsEntered);
        Assert.Equal(4, report.FinalRoom);
        Assert.True(session.PlayerHasControl);
        Assert.False(session.InCutscene);
        Assert.DoesNotContain(session.SessionTrace, item => item.StartsWith("boot: Player input assumed", StringComparison.Ordinal));
        AssertNothingUnsupported(session);
        Assert.Contains(session.SessionTrace, item => item.Contains("Room 04 entry script chains to script 206"));
        Assert.Contains(session.SessionTrace, item => item.StartsWith("endCutscene at Room 04 local script 206", StringComparison.Ordinal));
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal(4, ego.Room);
        Assert.Equal(2, ego.Look.Costume);
        // iMUSE lengths come from the S3 player's default path and are labelled estimates.
        Assert.Contains(session.Snapshot.Uncertainties, item => item.StartsWith("Sound 21 status is an estimate: iMUSE default path", StringComparison.Ordinal));
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
    public void TheSpanishTalkieBootsTheSameWayWithoutInput()
    {
        // S9 (18 October 2026): the Spanish talkie folder (ATLANTIS.000/.001 and MONSTER.SOU) takes the English disc's path: no protection, no input.
        if (ScummIndy4PlayTests.Folder("indy4 talkie vga es") is not { } folder) return;
        var session = Boot(folder, out var game);

        Assert.Equal(GameFamily.Indy4, GameSupport.FamilyOf(game));
        Assert.NotNull(game.Speech);
        Assert.False(session.BootReport!.HitFrameLimit);
        Assert.Equal([68, 0, 4], session.BootReport.RoomsEntered);
        Assert.True(session.PlayerHasControl);
        Assert.DoesNotContain(session.SessionTrace, item => item.StartsWith("boot: Player input assumed", StringComparison.Ordinal));
        AssertNothingUnsupported(session);
    }

    [Fact]
    public void TheSpanishFloppysStoneDiskIsAnsweredWithTheArgumentItsOwnRoomCodePasses()
    {
        // S9 (18 October 2026): the floppy's Script 1 loads the stone-disk screen (Room 81, +0x06A1) and waits for var[0x1BC] (+0x06AE), which only Room 81
        // local script 207 sets (+0x005A). Started with local 0 = 0 (Esc, a click elsewhere: local 205 +0x0026, +0x018A) it does nothing (+0x000C jumps to
        // the end); the room's own submit (local 205 +0x017A) starts it with 1, which reaches the write. The copy's three comparisons there (+0x0040 to
        // +0x004E) branch by 0, so the submitted wheel is accepted as it stands. R-15.5: a later stage's labelled assumption; nothing else is chosen.
        if (ScummIndy4PlayTests.Folder("indy4 classic vga es") is not { } folder) return;
        var session = Boot(folder, out var game);

        Assert.Null(game.Speech);
        Assert.False(session.BootReport!.HitFrameLimit);
        Assert.Equal([81, 68, 0, 4], session.BootReport.RoomsEntered);
        Assert.True(session.PlayerHasControl);
        Assert.Contains(session.Snapshot.Uncertainties, item => item.StartsWith("Player input assumed: script 001 +0x06AE waits for var[0x1BC], which only Room 81 local script 207 sets (+0x005A)", StringComparison.Ordinal));
        Assert.Contains(session.Snapshot.Uncertainties, item => item.StartsWith("Player input assumed: starting Room 81 local script 207 with local 0 = 0 did not release the boot; the room's own code also starts it with local 0 = 1 (local 205 +0x017A)", StringComparison.Ordinal));
        Assert.DoesNotContain(session.SessionTrace, item => item.Contains("quit"));
        AssertNothingUnsupported(session);
    }

    [Fact]
    public void SoundKludgeStartsAndStopsSoundsThroughImuseCommandsEightAndNine()
    {
        if (Folder() is not { } folder) return;
        var session = Boot(folder, out _);

        // Room 68's exit script ends the logo music with soundKludge [0x0009, 0x0096] (iMUSE doCommand param 0, command 9: stopSound 150).
        Assert.Contains(session.SoundEvents, item => item.Event is { Kind: ScummScriptEventKind.SoundStopped, TargetId: 150, Offset: 0x0024 });
        Assert.DoesNotContain(session.Snapshot.Uncertainties, item => item.Contains("'soundKludge' is not modeled"));
    }
}
