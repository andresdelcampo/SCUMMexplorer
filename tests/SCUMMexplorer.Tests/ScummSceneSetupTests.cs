using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Picked scenes in every game (REQUIREMENTS §8.6, R-8.35 to R-8.38; 31 October 2026), first on Indiana Jones and the Last Crusade EGA: a scene a fresh
/// start does not begin gets the state its own code tests (a bit, a story variable, an object's state, an object held, the second object of a
/// two-object verb, an actor's costume or room), and where none makes the game's code begin it, its cutscene is started directly. Nothing is refused.
/// Record: <c>.verification/scene-setup-20261031/README.md</c>.
/// </summary>
public sealed class ScummSceneSetupTests(ITestOutputHelper output)
{
    private static GameLibrary? Indy3()
    {
        var overridden = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        var folder = string.IsNullOrWhiteSpace(overridden) ? Path.Combine(TestGames.Root, "indy3 ega en") : overridden;
        return File.Exists(Path.Combine(folder, "00.LFL")) ? new GameFolderScanner().Scan(folder) : null;
    }

    private static ScummCutscene Trigger(ScummCutscenePlanner planner, int room, int objectId, int verb) =>
        planner.Groups.Select(g => g.First).Single(t => t.Room == room && t.ObjectId == objectId && t.VerbId == verb);

    [Fact]
    public void TheJarIsUsedWithTheObjectItsCodeTests()
    {
        // Room 2 object 71 verb 0x08 (the jar) +0x0000 isEqual local[0x000], 0x0085: its scene runs only when used with object 133, which the
        // setup gives the hero and passes to the sentence script as object B.
        if (Indy3() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var plan = planner.PlanSetup(Trigger(planner, 2, 71, 0x08));
        output.WriteLine(plan.Note);
        Assert.False(plan.Base.Begun);
        Assert.True(plan.Result.Begun);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.SecondObject, Target: "133" });
    }

    [Fact]
    public void TheHatchSceneGoesOnThroughItsRoomsOnceItsStateIsSet()
    {
        // Room 39 object 586 verb 0x0A (the hatch) +0x0000 tests its own state: with state 1 the scene plays its 18 lines through Rooms 41, 42 and 8.
        if (Indy3() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var plan = planner.PlanSetup(Trigger(planner, 39, 586, 0x0A));
        output.WriteLine(plan.Note);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.ObjectState, Target: "586", Value: 1 });
        Assert.True(plan.Result.Begun);
        Assert.Contains(41, plan.Result.RoomsEntered);
    }

    [Fact]
    public void EveryIndy3SceneBeginsWhenPicked()
    {
        // R-8.35: the game's own way, a setup, or the cutscene started directly; none is left out.
        if (Indy3() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var ways = planner.Groups.Select(group => planner.PlanSetup(group.First)).GroupBy(plan => (plan.Result.Begun, plan.HasEdits, plan.Result.Way))
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var (key, count) in ways) output.WriteLine($"{key}: {count}");
        Assert.Equal(planner.Groups.Count, ways.Where(pair => pair.Key.Begun).Sum(pair => pair.Value));
    }

    [Fact]
    public void ASetupRaisesOnlyWhatTheSessionLacks()
    {
        // Old v3 keeps bit n in bit (n & 15) of global word n >> 4; a bit already set and a variable already non-zero are kept (never lowered).
        if (Indy3() is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        var known = session.Snapshot.KnownGlobalVariables;
        var bit = Enumerable.Range(0x5C0, 0x40).First(n => (known.GetValueOrDefault($"var[0x{n >> 4:X3}]") & (1 << (n & 15))) == 0);
        var variable = Enumerable.Range(0xC0, 0x30).Select(n => $"var[0x{n:X3}]").First(key => known.GetValueOrDefault(key) == 0);
        var applied = session.ApplySceneSetup([new(ScummSetupKind.Bit, $"bit[0x{bit:X3}]", 1, "test"), new(ScummSetupKind.Variable, variable, 1000, "test")]);
        Assert.Equal(2, applied.Count);
        known = session.Snapshot.KnownGlobalVariables;
        Assert.NotEqual(0, known[$"var[0x{bit >> 4:X3}]"] & (1 << (bit & 15)));
        Assert.Equal(1000, known[variable]);
        Assert.Empty(session.ApplySceneSetup([new(ScummSetupKind.Bit, $"bit[0x{bit:X3}]", 1, "test"), new(ScummSetupKind.Variable, variable, 7, "test")]));
        Assert.Equal(1000, session.Snapshot.KnownGlobalVariables[variable]);
    }
}
