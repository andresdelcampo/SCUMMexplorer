using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomSceneSetupPlannerTests
{
    private static GameLibrary Game() =>
        new GameFolderScanner().Scan(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative)));

    private static LoomExplorationSession Session(GameLibrary game) =>
        new(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL")));

    [Fact]
    public void TheStaffWritersAreFoundInTheGamesOwnCodeWithObjectVerbsFirst()
    {
        var planner = new LoomSceneSetupPlanner(Game());
        var writers = planner.StaffHeldWriters;
        Assert.Contains(writers, w => w is { Room: 9, ObjectId: 271, VerbId: 0x38 });
        Assert.Equal((9, 271), (writers[0].Room, writers[0].ObjectId ?? 0));
        Assert.Contains(writers, w => w.ObjectId is null && w.Where.StartsWith("Room 13 entry", StringComparison.Ordinal));
    }

    [Fact]
    public void ASceneThatClearsTheStaffRunsTheRealPickupUnseenAndStartsWithItHeld()
    {
        var game = Game();
        var planner = new LoomSceneSetupPlanner(game);
        // Room 43 object 643 (script 091) and object 650 (script 093) both clear bit D61.
        foreach (var (room, script) in new[] { (43, 91), (43, 93) })
        {
            var setup = planner.Derive(room, [script]);
            Assert.True(setup.HasSteps, $"script {script:000}");
            Assert.Equal(("visit", 9), (setup.Steps[0].Kind, setup.Steps[0].ObjectId));
            Assert.Equal(("verb", 271, 0x38), (setup.Steps[1].Kind, setup.Steps[1].ObjectId, setup.Steps[1].Verb));
            Assert.Equal(("visit", room), (setup.Steps[3].Kind, setup.Steps[3].ObjectId)); // the pickup, then the scene's room, then any speakers
            Assert.Contains(setup.Notes, n => n.Contains("clears bit D61", StringComparison.Ordinal));
            var session = Session(game);
            session.VisitRoomById(room);
            var sequence = new LoomReplaySequence(session, setup.Steps);
            Assert.True(sequence.RunSilently(setup.Steps.Count), sequence.Failure);
            Assert.True(session.GameStatus.StaffHeld, $"script {script:000}");
            Assert.Equal(room, session.Snapshot.RoomId);
        }
    }

    [Fact]
    public void SpeakersGetTheirNamesFromTheLinesAndUnknownOnesKeepTheirNumber()
    {
        Assert.Equal("Bobbin", LoomSpeakerNames.Display("Actor 1"));
        Assert.Equal("Bobbin", LoomSpeakerNames.Display("Ego"));
        Assert.Equal("Hetchel", LoomSpeakerNames.Display("Actor 5"));
        Assert.Equal("Actor 7", LoomSpeakerNames.Display("Actor 7"));
        Assert.Equal("Unknown speaker", LoomSpeakerNames.Display(null));
    }

    [Fact]
    public void ARunningSessionThatAlreadyHoldsTheStaffIsNotGrantedItAgain()
    {
        var game = Game();
        var planner = new LoomSceneSetupPlanner(game);
        var setup = planner.Derive(43, [91]);
        var fresh = Session(game);
        fresh.VisitRoomById(43);
        Assert.Same(setup, planner.Adapt(setup, fresh)); // not held: the pickup stays
        var held = Session(game);
        held.VisitRoomById(43);
        for (var i = 0; i < 40; i++) held.AdvanceFrame();
        held.UpdateStaffPossession(true);
        var adapted = planner.Adapt(setup, held);
        Assert.DoesNotContain(adapted.Steps, st => st.EndBoundary == LoomSceneSetupPlanner.StaffGrant);
        Assert.Contains(adapted.Steps, st => st is { Kind: "visit", ObjectId: 43 });
        Assert.Contains(adapted.Notes, n => n.Contains("already holds the distaff", StringComparison.Ordinal));
    }

    [Fact]
    public void ASceneThatDoesNotDependOnTheStaffKeepsTheFreshCatalogVisit()
    {
        var planner = new LoomSceneSetupPlanner(Game());
        foreach (var (room, script) in new[] { (28, 72), (22, 108), (34, 85) })
            Assert.False(planner.Derive(room, [script]).HasSteps, $"script {script:000}");
    }

    [Fact]
    public void ASceneThatSetsTheStaffItselfNeedsNoPickup()
    {
        // Room 66's entry sets D61 to 1 before anything reads it; scene 102 loads it.
        var setup = new LoomSceneSetupPlanner(Game()).Derive(51, [102]);
        Assert.False(setup.HasSteps);
    }

    [Fact]
    public void ReadsOfTheStaffAndStoryBitsAreLabelledNotInvented()
    {
        var planner = new LoomSceneSetupPlanner(Game());
        // Room 18 is outside the island: the staff is held, from the real pickup.
        var off = planner.Derive(18, [58, 59]);
        Assert.True(off.HasSteps);
        Assert.Contains(off.Notes, n => n.Contains("outside the island", StringComparison.Ordinal));
        // Room 9 is on it: the fresh state stays, labelled.
        var on = planner.Derive(9, [47]);
        Assert.False(on.HasSteps);
        Assert.Contains(on.Notes, n => n.Contains("reads bit D61", StringComparison.Ordinal));
    }

    [Fact]
    public void TheScannerSeesEgoCostumeChangesAndRoomEntryReads()
    {
        var scanner = new LoomSceneStateScanner(Game());
        // The disassembler prints "costume, 0x11" (a comma), which the first version of the scan missed.
        Assert.Contains(scanner.FindCostumeChanges(), c => c.Where.StartsWith("Room 10 local 203", StringComparison.Ordinal) && c.Operands.StartsWith("0x01, [costume, 0x11", StringComparison.Ordinal));
        Assert.Contains(scanner.FindCostumeChanges(), c => c.Where.StartsWith("Room 34 local 202", StringComparison.Ordinal) && c.Operands.Contains("costume, var[0x07D]", StringComparison.Ordinal));
        // Room 38's entry gives the ego costume 0x70 and branches on story bit D9D (set by the ghost scene's chain).
        var entry = scanner.ScanRoomEntry(38);
        Assert.Contains(entry.CostumeChanges, c => c.StartsWith("Room 38 entry", StringComparison.Ordinal) && c.Contains("costume, 0x70", StringComparison.Ordinal));
        Assert.Contains(entry.Touches, t => !t.Write && t.Variable == "bit[0xD9D]" && t.Where.StartsWith("Room 38 entry", StringComparison.Ordinal));
        Assert.Contains(scanner.FindWriters("var[0x07D]"), t => t.Where.StartsWith("Room 34 local 202", StringComparison.Ordinal));
        Assert.Contains(scanner.FindStoryBitMoves(0), t => t.Where.StartsWith("Script 093 +0x0005", StringComparison.Ordinal) && t.Instruction.Contains("bit[0xDB6]", StringComparison.Ordinal));
    }

    [Fact]
    public void StoryEventsListTheBitsTheGameSetsAndCanBeMarkedHappened()
    {
        var game = Game();
        var session = Session(game);
        session.VisitRoomById(43);
        for (var i = 0; i < 40; i++) session.AdvanceFrame();
        var events = session.StoryEvents;
        Assert.True(events.Count > 40, $"{events.Count} story bits");
        Assert.All(events, e => Assert.True(e.Bit >= LoomStoryEvents.FirstBitFor(game)));
        // Script 093 (the cage scene) sets DB5; the list names it as a scene.
        var db6 = Assert.Single(events, e => e.Key == "bit[0xDB5]");
        Assert.Contains(db6.Scenes, s => s.Where.StartsWith("Script 093", StringComparison.Ordinal));
        Assert.False(session.StoryBit("bit[0xDB5]") ?? false);
        Assert.True(session.GameStatus.CanEdit, "session settled");
        session.UpdateStoryBit("bit[0xDB5]", true);
        Assert.True(session.StoryBit("bit[0xDB5]"));
        Assert.Equal(1, session.Snapshot.KnownGlobalVariables.GetValueOrDefault("bit[0xDB5]"));
        session.UpdateStoryBit("bit[0xDB5]", false);
        Assert.False(session.StoryBit("bit[0xDB5]"));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdateStoryBit("bit[0xD61]", true));
        System.IO.File.WriteAllLines(Environment.GetEnvironmentVariable("SCUMM_EVENTS_DUMP") ?? System.IO.Path.GetTempPath() + "events.txt",
            events.Select(e => $"{e.Key} scenes=[{string.Join("; ", e.Scenes.Select(s => s.Where.Split(" +")[0]).Distinct().Take(4))}] all={e.Setters.Count}"));
    }

    [Fact(Skip = "Diagnostic: speakers with rooms and sample lines (SCUMM_SPEAKER_DUMP=path).")]
    public void DumpSpeakers()
    {
        var game = Game();
        var extraction = new LoomTextExtractor().Extract(game);
        var lines = extraction.Entries.Where(e => e.Speaker is not null).GroupBy(e => e.Speaker!).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}: rooms [{string.Join(",", g.Select(e => e.RoomId).Distinct().OrderBy(r => r).Take(12))}] n={g.Count()}  e.g. " +
                string.Join(" | ", g.Select(e => e.Text).Distinct().Take(4)));
        System.IO.File.WriteAllLines(Environment.GetEnvironmentVariable("SCUMM_SPEAKER_DUMP") ?? System.IO.Path.GetTempPath() + "speakers.txt", lines);
    }

    [Fact(Skip = "Diagnostic dump of every catalog scene's derived setup; run by hand (SCUMM_SETUP_DUMP=path).")]
    public void DumpCatalogSetups()
    {
        var game = Game();
        var planner = new LoomSceneSetupPlanner(game);
        var json = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/SCUMMexplorer.App/loom-replays.json"));
        using var doc = JsonDocument.Parse(File.ReadAllText(json));
        var lines = new List<string>();
        foreach (var r in doc.RootElement.GetProperty("replays").EnumerateArray())
        {
            if (r.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array) continue;
            var ids = r.GetProperty("scriptIds").EnumerateArray().Select(e => e.GetInt32()).ToArray();
            var setup = planner.Derive(r.GetProperty("room").GetInt32(), ids, r.GetProperty("objectId").GetInt32(), r.GetProperty("verb").GetInt32());
            if (setup.Notes.Count == 0) continue;
            lines.Add($"{r.GetProperty("id").GetString()} steps={string.Join(" ", setup.Steps.Select(s => $"{s.Kind}:{s.ObjectId}"))}");
            lines.AddRange(setup.Notes.Select(n => "   " + n));
        }
        File.WriteAllLines(Environment.GetEnvironmentVariable("SCUMM_SETUP_DUMP") ?? Path.Combine(Path.GetTempPath(), "setups.txt"), lines);
    }

    [Fact(Skip = "Diagnostic audit dump (costume changes, room-entry reads, scene presumptions); run by hand (SCUMM_AUDIT_DUMP=path).")]
    public void DumpCostumeAndEntryAudit()
    {
        var game = Game();
        var scanner = new LoomSceneStateScanner(game);
        var lines = new List<string> { "== costume changes ==" };
        lines.AddRange(scanner.FindCostumeChanges().Select(c => $"Room {c.Room:00} {c.Where}: {c.Operands}"));
        lines.Add("== writers of var[0x07D] (the saved ego costume) ==");
        lines.AddRange(scanner.FindWriters("var[0x07D]").Select(t => $"{t.Where}: {t.Instruction}"));
        lines.Add("== story bits cleared (set to 0) by scene-own code ==");
        lines.AddRange(scanner.FindStoryBitMoves(0).Select(t => $"{t.Where}: {t.Instruction}"));
        lines.Add("== ego (actor 1) costume after a fresh catalog visit of each room ==");
        foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).OrderBy(i => i))
        {
            try
            {
                var session = Session(game);
                session.VisitRoomById(room);
                for (var i = 0; i < 30; i++) session.AdvanceFrame();
                var ego = session.Actors.FirstOrDefault(a => a.Number == 1);
                lines.Add($"Room {room:00}: costume {ego?.Look.Costume?.ToString("X2") ?? "?"} room {ego?.Room} staff {session.GameStatus.StaffHeld}");
            }
            catch (Exception ex) { lines.Add($"Room {room:00}: {ex.GetType().Name}"); }
        }
        lines.Add("== room entries: reads of global vars/bits ==");
        foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).OrderBy(i => i))
        {
            var scan = scanner.ScanRoomEntry(room);
            var reads = scan.Touches.Where(t => !t.Write && !LoomSceneStateScanner.IsSystemCode(t.Where)).Select(t => $"{t.Variable}@{t.Where}").Distinct().ToList();
            lines.Add($"Room {room:00}: scripts [{string.Join(", ", scan.Scripts)}] reads [{string.Join(" ", reads)}] costume [{string.Join(" | ", scan.CostumeChanges)}]");
        }
        lines.Add("== scenes: story bits read but not written by the scene's own code, and who sets them ==");
        var json = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/SCUMMexplorer.App/loom-replays.json"));
        using var doc = JsonDocument.Parse(File.ReadAllText(json));
        static bool Story(string v) => v.StartsWith("bit[0x", StringComparison.Ordinal) && int.Parse(v.AsSpan(6, v.Length - 7), System.Globalization.NumberStyles.HexNumber) >= 0xD72;
        var writerCache = new Dictionary<string, IReadOnlyList<LoomStateSource>>();
        foreach (var r in doc.RootElement.GetProperty("replays").EnumerateArray())
        {
            var ids = r.GetProperty("scriptIds").EnumerateArray().Select(e => e.GetInt32()).Where(i => i >= LoomSceneStateScanner.SystemScriptLimit).ToArray();
            var source = r.GetProperty("source").GetString() ?? "";
            if (ids.Length == 0 && !source.Contains("cutscene at", StringComparison.Ordinal)) continue;
            var room = r.GetProperty("room").GetInt32();
            var scan = scanner.Scan(room, ids, r.GetProperty("objectId").GetInt32(), r.GetProperty("verb").GetInt32());
            var own = scan.OwnTouches.ToList();
            var written = own.Where(t => t.Write).Select(t => t.Variable).ToHashSet();
            var reads = own.Where(t => !t.Write && Story(t.Variable) && !written.Contains(t.Variable)).Select(t => t.Variable).Distinct().Order().ToList();
            var costumeReads = own.Where(t => !t.Write && (t.Variable == "var[0x07D]" || t.Instruction.StartsWith("getActorCostume", StringComparison.Ordinal))).Select(t => t.Where).Distinct().ToList();
            var ownCostume = scan.CostumeChanges.Where(c => c.Contains("costume, 0x", StringComparison.Ordinal) || c.Contains("costume, var", StringComparison.Ordinal)).Count();
            if (reads.Count == 0 && costumeReads.Count == 0) continue;
            lines.Add($"{r.GetProperty("id").GetString()} (scripts {string.Join(",", ids)}): reads [{string.Join(" ", reads)}] costumeReads [{string.Join(", ", costumeReads)}]");
            foreach (var bit in reads)
            {
                if (!writerCache.TryGetValue(bit, out var w)) writerCache[bit] = w = scanner.FindConstantWriters(bit, 1);
                lines.Add($"      {bit} set to 1 by: {(w.Count == 0 ? "(no code)" : string.Join("; ", w.Select(x => x.Where.Split(" +")[0]).Distinct().Take(5)))}");
            }
        }
        File.WriteAllLines(Environment.GetEnvironmentVariable("SCUMM_AUDIT_DUMP") ?? Path.Combine(Path.GetTempPath(), "audit.txt"), lines);
    }
}

public sealed class LoomSceneSetupSceneTests
{
    [Fact]
    public void ElMuertosLinesAreSpokenBecauseTheDerivedSetupBringsActor4IntoRoom09()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        var game = new GameFolderScanner().Scan(folder);
        var planner = new LoomSceneSetupPlanner(game);
        // Room 09 object 275, verb 0x56 starts local 221: actor 8's apparition, then two lines printed for actor 4.
        var setup = planner.Derive(9, [], 275, 0x56);
        var step = Assert.Single(setup.Steps, s => s.Kind == "actor");
        Assert.Equal(4, step.ObjectId);
        Assert.Contains(setup.Notes, n => n.Contains("actor 4", StringComparison.Ordinal) && n.Contains("estimate", StringComparison.Ordinal));

        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        session.VisitRoomById(9);
        Assert.True(new LoomReplaySequence(session, setup.Steps).RunSilently(setup.Steps.Count));
        Assert.Equal(9, session.Actors.Single(a => a.Number == 4).Room);
        session.ActivateObjectVerb(275, 0x56);
        for (var i = 0; i < 1500; i++) session.AdvanceFrame();
        Assert.Contains(session.Messages.Log, m => m.Actor == 4 && m.Text.Contains("Tu magia de Tejedor", StringComparison.Ordinal));
    }

    private static string Run(bool withSetup, int room, int obj, int verb, int script)
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        session.VisitRoomById(room);
        if (withSetup)
        {
            var setup = new LoomSceneSetupPlanner(game).Derive(room, [script]);
            new LoomReplaySequence(session, setup.Steps).RunSilently(setup.Steps.Count);
        }
        session.ActivateObjectVerb(obj, verb);
        for (var i = 0; i < 6000; i++) session.AdvanceFrame();
        var ends = session.SessionTrace.Where(t => t.StartsWith("endCutscene at ", StringComparison.Ordinal)).ToArray();
        return $"room={session.Snapshot.RoomId} inScene={session.InCutscene} ends=[{string.Join("; ", ends)}] unsup={session.Snapshot.Uncertainties.Count(t => t.Contains("unsupported", StringComparison.OrdinalIgnoreCase))}";
    }

    [Fact]
    public void TheCageScenesStillReachTheirOwnEndFromTheDerivedSetup()
    {
        Assert.Contains("endCutscene at Script 91 +0x0E83", Run(true, 43, 643, 0x3A, 91));
        Assert.Contains("endCutscene at Script 93 +0x05FB", Run(true, 43, 650, 0x38, 93));
        Assert.DoesNotContain("unsup=1", Run(true, 43, 650, 0x38, 93));
    }
}
