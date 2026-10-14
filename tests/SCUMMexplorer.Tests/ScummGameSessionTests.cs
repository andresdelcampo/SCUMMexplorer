using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S4b (14 October 2026): the data-driven <see cref="ScummGameSession"/> boots Indiana Jones and the Last Crusade EGA (English, the family's
/// fixture) with the game's own scripts: script 001 plays the opening and the lecture, the copy-protection screen (Room 92, an assumed and
/// labelled answer) and hands over to script 119 in Room 73. Record: <c>.verification/indy3-session-20261014/README.md</c>.
/// The test skips when the game folder is absent (set <c>SCUMM_INDY3_EGA_TEST_ROOT</c> for another copy).
/// </summary>
public sealed class ScummGameSessionTests
{
    private static string? Folder()
    {
        var overridden = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        var folder = string.IsNullOrWhiteSpace(overridden) ? Path.Combine(SupportMatrixTests.RepositoryRoot(), "Games", "indy3 ega en") : overridden;
        return File.Exists(Path.Combine(folder, "00.LFL")) ? folder : null;
    }

    private static ScummGameSession Boot(string folder, out GameLibrary game)
    {
        game = new GameFolderScanner().Scan(folder);
        var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL"));
        return new ScummGameSession(game, objects);
    }

    [Fact]
    public void ProfileGivesTheIndianaJonesDialectAndTheLfLDataSource()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);

        Assert.Equal(ScummScriptDialect.V3Indy3, ScummProfile.For(game).Dialect);
        Assert.IsType<ScummV3DataSource>(ScummDataSources.For(game));
        // The index of the English fixture declares 139 global scripts (00.LFL); a room's local scripts are numbered above that (Room 73 has 200 to 204).
        Assert.Equal(139,ScummDataSources.For(game).GlobalScriptCount);
        Assert.Equal(ScummAdlibTiming.Standard, ScummProfile.For(game).AdlibTiming);
    }

    [Fact]
    public void TheBootReachesTheFirstPlayableRoomInABoundedNumberOfFramesWithNothingUnsupported()
    {
        if (Folder() is not { } folder) return;

        var session = Boot(folder, out var game);
        var report = session.BootReport!;

        Assert.False(report.HitFrameLimit);
        Assert.InRange(report.Frames, 100, 2000);
        Assert.Equal(73, report.FinalRoom);
        // Opening (76, 79), the cleared screen (0), the lecture (6, 1), the copy-protection screen (92) and back, then the first room.
        Assert.Equal([76, 79, 0, 6, 1, 92, 1, 73], report.RoomsEntered);
        Assert.Equal(73, session.Snapshot.RoomId);
        Assert.Equal(73, session.Snapshot.KnownGlobalVariables[ScummVars.Room]);
        Assert.False(session.BootRunning);
        Assert.False(session.InCutscene);
        Assert.Contains("Script 01 chains to script 119", session.SessionTrace);

        // Every operation the run could not follow would be listed here (R-9.19). Display-only operations are named: they change no game state.
        string[] unsupported = ["not modeled", "could not", "disassembly stops", "undecoded", "cannot", "needs a session", "unknown", "depends on"];
        Assert.DoesNotContain(session.Snapshot.Uncertainties, item => unsupported.Any(item.Contains) && !item.Contains("seeded pick"));
        var presentation = session.Snapshot.Uncertainties.SelectMany(item => item.Split("; "))
            .Where(item => item.Contains("presentation operation not applied:"))
            .Select(item => item[(item.LastIndexOf(':') + 1)..].Trim()).Distinct().Order().ToArray();
        // S5: the display operations are applied (lights, room colours, strings) or have no lasting effect on the room (transitions, resource routines).
        Assert.Empty(presentation);

        // The copy-protection screen is the only assumed input, and says so.
        var assumed = Assert.Single(session.Snapshot.Uncertainties, item => item.StartsWith("Player input assumed", StringComparison.Ordinal));
        Assert.Contains("Room 92 local script 204", assumed);
        // The answer is 0 (wrong): the game's own rule lets play go on after three wrong rounds of its four questions (var[0x06B] = 3) and no more answers follow.
        Assert.Equal(12, session.SessionTrace.Count(item => item.StartsWith("boot: starts Room 92 local script 204", StringComparison.Ordinal)));
        Assert.Equal(3, session.Snapshot.KnownGlobalVariables["var[0x06B]"]);
        Assert.Equal(1, session.Snapshot.KnownGlobalVariables["var[0x039]"]);

        // The first room: the ego stands in it, with the game's own look (costume 39) and the inventory script set by the game.
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal(73, ego.Room);
        Assert.Equal(39, ego.Look.Costume);
        Assert.Equal(12, session.Snapshot.KnownGlobalVariables[ScummVars.InventoryScript]);
        Assert.Equal(2, session.Snapshot.KnownGlobalVariables[ScummVars.SentenceScript]);
        Assert.Equal(game.Rooms.Single(room => room.ResourceId == 73).ResourceId, session.CurrentRoomId);
    }

    [Fact]
    public void TheFirstRoomReachesAStableStateWithNothingUnsupported()
    {
        if (Folder() is not { } folder) return;
        var session = Boot(folder, out _);

        for (var frame = 0; frame < 900; frame++) session.AdvanceFrame();

        Assert.Equal(73, session.CurrentRoomId);
        string[] unsupported = ["not modeled", "could not", "disassembly stops", "undecoded", "cannot", "needs a session", "depends on"];
        Assert.DoesNotContain(session.Snapshot.Uncertainties, item => unsupported.Any(item.Contains) && !item.Contains("seeded pick"));
        // Room 73's own scripts (the ambient local scripts) are still running; the boot's script and the sentence queue are not.
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
}
