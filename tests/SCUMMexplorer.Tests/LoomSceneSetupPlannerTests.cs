using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomSceneSetupPlannerTests
{
    private static GameLibrary Game() =>
        new GameFolderScanner().Scan(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom")));

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
            Assert.Equal(("visit", room), (setup.Steps[^1].Kind, setup.Steps[^1].ObjectId));
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
        var setup = new LoomSceneSetupPlanner(Game()).Derive(18, [58, 59]);
        Assert.False(setup.HasSteps);
        Assert.Contains(setup.Notes, n => n.Contains("reads bit D61", StringComparison.Ordinal));
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
            var setup = planner.Derive(r.GetProperty("room").GetInt32(), ids);
            if (setup.Notes.Count == 0) continue;
            lines.Add($"{r.GetProperty("id").GetString()} steps={string.Join(" ", setup.Steps.Select(s => $"{s.Kind}:{s.ObjectId}"))}");
            lines.AddRange(setup.Notes.Select(n => "   " + n));
        }
        File.WriteAllLines(Environment.GetEnvironmentVariable("SCUMM_SETUP_DUMP") ?? Path.Combine(Path.GetTempPath(), "setups.txt"), lines);
    }
}

public sealed class LoomSceneSetupSceneTests
{
    private static string Run(bool withSetup, int room, int obj, int verb, int script)
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));
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
