using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S4b Indy3Vga (15 October 2026): the data-driven <see cref="ScummGameSession"/> boots Indiana Jones and the Last Crusade in 256 colours (one file per
/// room, version 3 scripts) with the game's own scripts: script 001 plays the opening (Rooms 76, 79), the lecture (Rooms 6, 1) and hands over to script 119
/// in Room 73. This release has no copy-protection room (no Room 92, and script 001 loads none). Record: <c>.verification/indy3vga-session-20261015/README.md</c>.
/// Skips when the game folder is absent (set <c>SCUMM_INDY3_VGA_TEST_ROOT</c> for another copy).
/// </summary>
public sealed class ScummIndy3VgaSessionTests
{
    internal static string? Folder()
    {
        var overridden = Environment.GetEnvironmentVariable("SCUMM_INDY3_VGA_TEST_ROOT");
        var path = string.IsNullOrWhiteSpace(overridden) ? Path.Combine(TestGames.Root, "indy3 vga en") : overridden;
        return File.Exists(Path.Combine(path, "00.LFL")) && File.Exists(Path.Combine(path, "73.LFL")) ? path : null;
    }

    internal static ScummGameSession Boot(string folder, out GameLibrary game)
    {
        game = new GameFolderScanner().Scan(folder);
        var data = (ScummV4RoomFileDataSource)ScummDataSources.For(game);
        return new ScummGameSession(game, data.ReadIndexedObjects(), 0, null, data);
    }

    private static readonly string[] Unsupported = ["not modeled", "could not", "disassembly stops", "undecoded", "cannot", "needs a session", "unknown", "depends on"];

    [Fact]
    public void TheProfileGivesTheVersionThreeEngineAndTheRoomFileSource()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var profile = ScummProfile.For(game);

        // v4-style blocks, version 3 scripts and engine (ScummVM: indy3 VGA is version 3).
        Assert.Equal(ScummVersion.V4, game.Version);
        Assert.Equal(ScummScriptDialect.V3Indy3, profile.Dialect);
        Assert.Equal(ScummVersion.V3, profile.EngineVersion);
        Assert.False(profile.UsesV4Walking);
        Assert.False(profile.SeparateBitVariables);
        Assert.Empty(profile.EngineVariablesAtStart);
        Assert.Equal(3, profile.ExtraTimerVariables.Count);
        Assert.Equal(19, profile.VideoMode);
        Assert.True(profile.HasFirstVisitScenes);

        var data = Assert.IsType<ScummV4RoomFileDataSource>(ScummDataSources.For(game));
        Assert.Equal(ScummV4Archive.LocalScriptBase, data.GlobalScriptCount);
        Assert.NotNull(data.GlobalScript(1));
        // A script names the room file that holds it.
        var entry = data.RoomEntryScript(73);
        Assert.NotNull(entry);
        Assert.Equal("73.LFL", entry.ContainerName);
        Assert.InRange(entry.CodeFileOffset!.Value, 8, (int)new FileInfo(Path.Combine(folder, "73.LFL")).Length - 1);
        // Walk boxes are 18 bytes in this layout: Room 73's boxes read with corners on screen.
        var boxes = data.ReadRoom(73).WalkBoxes;
        Assert.NotNull(boxes);
        Assert.NotEmpty(boxes.Boxes);
        Assert.All(boxes.Boxes, box => Assert.InRange(box.UpperLeft.Y, 0, 200));
        // Costume poses store one byte per limb command (version 3): the ego's standing animation starts.
        var costume = data.ReadCostume(1);
        Assert.NotNull(costume);
        var state = new ScummCostumeAnimationState();
        Assert.True(costume.DecodeAnimation(state, 1, 2));
        var cels = costume.Draw(state, 2);
        Assert.NotEmpty(cels);
        Assert.All(cels, cel => Assert.InRange(cel.Height, 1, 100));
        // Over every animation of every costume, the byte layout draws a plausible figure far more often than the word layout of LOOM CD (15 October: 1720 and
        // 1408 of 1752; 953 differ).
        int byteDraws = 0, wordDraws = 0, differ = 0, total = 0;
        foreach (var body in new GameFolderScanner().Scan(folder).V4Disks!.Disks.OfType<ScummV4Archive>().Single().ReadCostumes())
        {
            var b = ScummCostume.ReadVga(body.Body, body.Id, byteCommands: true);
            var w = ScummCostume.ReadVga(body.Body, body.Id);
            for (var frame = 0; frame * 4 <= b.AnimationLimit; frame++)
                for (var dir = 0; dir < 4; dir++)
                {
                    var sb = new ScummCostumeAnimationState(); var sw = new ScummCostumeAnimationState();
                    if (!b.DecodeAnimation(sb, frame, dir)) continue;
                    w.DecodeAnimation(sw, frame, dir);
                    total++;
                    var cb = b.Draw(sb, dir); var cw = w.Draw(sw, dir);
                    if (cb.Count > 0 && cb.All(c => c.Height <= 150)) byteDraws++;
                    if (cw.Count > 0 && cw.All(c => c.Height <= 150)) wordDraws++;
                    if (!cb.Select(c => (c.Limb, c.Width, c.Height)).SequenceEqual(cw.Select(c => (c.Limb, c.Width, c.Height)))) differ++;
                }
        }
        Assert.True(byteDraws >= total * 97 / 100, $"byte {byteDraws} of {total}");
        Assert.True(wordDraws < byteDraws - total / 10, $"word {wordDraws}, byte {byteDraws}");
        Assert.True(differ > total / 3, $"differ {differ} of {total}");
    }

    [Fact]
    public void TheBootReachesTheFirstPlayableRoomInABoundedNumberOfFramesWithNothingUnsupported()
    {
        if (Folder() is not { } folder) return;

        var session = Boot(folder, out _);
        var report = session.BootReport!;

        Assert.False(report.HitFrameLimit);
        Assert.InRange(report.Frames, 500, 2000);
        Assert.Equal(73, report.FinalRoom);
        // Opening (76, 79), the cleared screen (0), the lecture (6, 1), then the college gym (73). No copy-protection room.
        Assert.Equal([76, 79, 0, 6, 1, 73], report.RoomsEntered);
        Assert.Equal(73, session.Snapshot.RoomId);
        Assert.Equal(73, session.Snapshot.KnownGlobalVariables[ScummVars.Room]);
        Assert.False(session.BootRunning);
        Assert.False(session.InCutscene);
        Assert.Contains("Script 01 chains to script 119", session.SessionTrace);

        // R-9.19: nothing the run could not follow, no display operation left unapplied, no assumed player input.
        Assert.DoesNotContain(session.Snapshot.Uncertainties, item => Unsupported.Any(item.Contains) && !item.Contains("seeded pick"));
        Assert.DoesNotContain(session.Snapshot.Uncertainties, item => item.Contains("presentation operation not applied:"));
        Assert.DoesNotContain(session.Snapshot.Uncertainties, item => item.StartsWith("Player input assumed", StringComparison.Ordinal));

        // The first room (the college gym): the ego with its own costume, the boxer in the ring (actor 9, costume 38), and the interface scripts set by the game.
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal(73, ego.Room);
        Assert.Equal(1, ego.Look.Costume);
        Assert.True(ego.Visible);
        Assert.Contains(session.Actors, actor => actor is { Number: 9, Room: 73, Visible: true });
        Assert.Equal(4, session.Snapshot.KnownGlobalVariables[ScummVars.VerbScript]);
        Assert.Equal(12, session.Snapshot.KnownGlobalVariables[ScummVars.InventoryScript]);
        Assert.Equal(2, session.Snapshot.KnownGlobalVariables[ScummVars.SentenceScript]);
    }

    [Fact]
    public void TheFirstRoomReachesAStableStateWithNothingUnsupported()
    {
        if (Folder() is not { } folder) return;
        var session = Boot(folder, out _);

        for (var frame = 0; frame < 900; frame++) session.AdvanceFrame();

        Assert.Equal(73, session.CurrentRoomId);
        Assert.DoesNotContain(session.Snapshot.Uncertainties, item => Unsupported.Any(item.Contains) && !item.Contains("seeded pick") && !item.Contains("unknown"));
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
