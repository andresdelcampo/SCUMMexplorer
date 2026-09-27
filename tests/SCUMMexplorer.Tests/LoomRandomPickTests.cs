using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// getRandomNr is modelled as a labelled, seeded pick. What follows from a pick
/// is an estimate: it must never count as a reached game state.
/// </summary>
public sealed class LoomRandomPickTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    [Fact]
    public void SeededPickStaysInRangeAndRepeatsForTheSameSeed()
    {
        var first = new LoomSeededRandom(7);
        var again = new LoomSeededRandom(7);
        var values = Enumerable.Range(0, 200).Select(_ => first.Pick("Script 42", 0x26, 4)).ToArray();

        Assert.All(values, pick => Assert.InRange(pick.Value, 0, 4));
        Assert.Equal(5, values.Select(pick => pick.Value).Distinct().Count());
        Assert.Equal(values.Select(pick => pick.Value),
            Enumerable.Range(0, 200).Select(_ => again.Pick("Script 42", 0x26, 4).Value));
        Assert.Equal((7, 199), (values[^1].Seed, values[^1].Draw));
        // A pick does not depend on draws taken at other instructions.
        var interleaved = new LoomSeededRandom(7);
        interleaved.Pick("Room 04 local script 203", 0, 0x20);
        Assert.Equal(values[0].Value, interleaved.Pick("Script 42", 0x26, 4).Value);
    }

    [Fact]
    public void PickNeedsASessionSeedAndIsRecordedWithItsRange()
    {
        var script = Script(
            new(0, 0, "getRandomNr", "local[0x001], 0x03", "16 01 03"),
            new(3, 0, "stopObjectCode", "", "00"));
        var executor = new LoomBoundedScriptExecutor();

        var alone = executor.Execute(script, new Dictionary<int, byte>(), []);
        Assert.Contains("seeded random source", alone.UnresolvedReason, StringComparison.Ordinal);

        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>()) { Random = new LoomSeededRandom(3) };
        var run = executor.Execute(script, runtime.ObjectStates, [], runtime: runtime);

        var pick = Assert.Single(run.RandomPicks);
        Assert.Equal((0, 3, 3), (pick.Offset, pick.Max, pick.Seed));
        Assert.Equal((ushort)pick.Value, run.KnownVariables["local[0x001]"]);
        Assert.Contains("local[0x001]", run.EstimatedLocals);
        Assert.Contains("seeded pick", pick.Describe(), StringComparison.Ordinal);
        Assert.True(run.ReachedStop);
    }

    [Fact]
    public void StatesAndLoadsBehindABranchOnAPickAreEstimatesNotReached()
    {
        var script = Script(
            new(0, 0, "setState", "0x000B, 0x0001", "00 00 00 00"),
            new(4, 0, "getRandomNr", "local[0x001], 0x00", "16 01 00"),
            new(7, 0, "move", "var[0x010] = local[0x001]", "00 00 00 00 00"),
            new(12, 0, "equalZero", "local[0x001], branch +0x0009 -> 0x001A", "00 00 00 00 00"),
            new(17, 0, "setState", "0x000A, 0x0001", "00 00 00 00"),
            new(21, 0, "loadRoom", "0x05", "72 05"),
            new(23, 0, "stopObjectCode", "", "00"),
            new(26, 0, "stopObjectCode", "", "00"));
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte> { [10] = 0, [11] = 0 });
        var events = new List<LoomScriptEvent>();

        var run = new LoomBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime,
            onEvent: (item, _) => { events.Add(item); return ""; });

        // Max 0 always picks 0, so the branch falls through to the second setState.
        Assert.Equal(1, run.ObjectStates[10]);
        Assert.True(run.PathEstimated);
        Assert.Contains(11, runtime.StatesSetInRoom);
        Assert.DoesNotContain(10, runtime.StatesSetInRoom);
        Assert.Contains(10, runtime.EstimatedStates);
        Assert.Contains("var[0x010]", runtime.EstimatedVariables);
        var load = Assert.Single(events, item => item.Kind == LoomScriptEventKind.RoomLoaded);
        Assert.Equal(LoomScriptEventCertainty.Conditional, load.Certainty);
    }

    [Fact]
    public void IndexedOperandsReadAndWriteTheVariableTheyAddress()
    {
        var script = Script(
            new(0, 0, "move", "local[0x002] = 0x0002", "00 00 00 00 00"),
            new(5, 0, "move", "var[0x0B5] = 0x0077", "00 00 00 00 00"),
            new(10, 0, "move", "local[0x003] = var[0x0B3+local[0x002]]", "00 00 00 00 00 00 00"),
            new(17, 0, "move", "local[0x004+0x001] = 0x0009", "00 00 00 00 00 00 00"),
            new(24, 0, "stopObjectCode", "", "00"));
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());

        var run = new LoomBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime);

        Assert.Null(run.UnresolvedReason);
        Assert.Equal((ushort)0x77, run.KnownVariables["local[0x003]"]);
        Assert.Equal((ushort)9, run.KnownVariables["local[0x005]"]);
    }

    [Fact]
    public void Room04VisitPicksThroughLocalScript203AndStaysAStatePreview()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")), randomSeed: 4);

        var picksBeforeVisit = session.Snapshot.RandomPicks.Count;
        var snapshot = session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 4));

        // The boot's opening cutscene takes picks of its own (scripts 42, 113, 114).
        var pick = Assert.Single(snapshot.RandomPicks.Skip(picksBeforeVisit));
        Assert.Equal(("Room 04 local script 203", 0x0000, 0x20, 4), (pick.Source, pick.Offset, pick.Max, pick.Seed));
        Assert.DoesNotContain(snapshot.Uncertainties, line => line.Contains("'getRandomNr' is not modeled", StringComparison.Ordinal));
        Assert.Contains(snapshot.Uncertainties, line => line.Contains("seeded pick, seed 4", StringComparison.Ordinal));
        Assert.False(snapshot.IsFaithful);
        // The countdown of 4..36 frames is pending, not finished.
        Assert.True(snapshot.PendingScripts > 0);
    }

    [Fact]
    public void Script42PicksActor2PositionAndItsTimeOnScreen()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var resource = game.Resources.Single(item => item.Kind == ResourceKind.Script && item.ResourceId == 42);
        var script = new LoomScriptReader(ScummScriptDialects.For(game.Id)).Decode(resource);
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>()) { Random = new LoomSeededRandom(1) };
        var executor = new LoomBoundedScriptExecutor();
        // Room 03 entry +0x0114 starts it as startScript 0x2A, [0x0005].
        var locals = new Dictionary<string, ushort> { ["local[0x000]"] = 5 };

        var first = executor.Execute(script, runtime.ObjectStates, [], runtime: runtime, initialLocals: locals);
        Assert.Equal(0x0021, first.ResumeOffset);

        var resumed = executor.Execute(script, runtime.ObjectStates, [], runtime: runtime, startOffset: 0x0021,
            initialLocals: first.KnownVariables.Where(pair => pair.Key.StartsWith("local[", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value));

        // +0x0026 picks 0..4 (never the previous 5), +0x004F picks 0..2 for the animation.
        Assert.Equal([0x0026, 0x004F], resumed.RandomPicks.Select(pick => pick.Offset));
        Assert.Equal(0x0085, resumed.ResumeOffset);
        Assert.Equal(1, (runtime.GlobalVariables["var[0x0D7]"] >> 10) & 1);
        // The bit is set after a loop that branches on the pick, so it is an estimate.
        Assert.True(resumed.PathEstimated);
        Assert.Contains("bit[0xD7A]", runtime.EstimatedVariables);
    }

    private static LoomScriptDisassembly Script(params LoomScriptInstruction[] instructions) =>
        new(1, 1, 0, 32, 32, instructions, null, null, [], []);
}
