using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The user's report of 7 November 2026: Indy 3's castle plans (Rooms 20, 28, 32) "no longer show correctly, Indy won't move". In the EGA copy a picked plan
/// took over half a minute to open while the background scene derivation reread every script from its file. Record: <c>.verification/user-reports-20261107/README.md</c>.
/// </summary>
public sealed class UserReports20261107Tests
{
    private static GameLibrary? Indy3Ega()
    {
        var folder = Path.Combine(TestGames.Root, "indy3 ega en");
        return File.Exists(Path.Combine(folder, "00.LFL")) ? new GameFolderScanner().Scan(folder) : null;
    }

    private static GameLibrary? Copy(string folder) =>
        Directory.Exists(Path.Combine(TestGames.Root, folder)) ? new GameFolderScanner().Scan(Path.Combine(TestGames.Root, folder)) : null;

    private static ScummCutsceneRunResult RunScene(GameLibrary game, int room, int obj, int verb, params ScummSetupEdit[] setup)
    {
        var trigger = new ScummCutscenePlanner(game).Groups.SelectMany(g => new[] { g.First }.Concat(g.Others)).First(t => t.Room == room && t.ObjectId == obj && t.VerbId == verb);
        return ScummCutsceneRun.RunPicked(game, () => ScummGameSession.BootForPlay(game, out _), trigger, setup);
    }

    [Fact]
    public void TheDiaryUsedInAnotherRoomKeepsTheHeroThere()
    {
        // Indy 3 Room 2's diary, Open in Room 18: closing Room 18's arrival ran the cutscene-end hook (Script 23 +0x0022, actorFollowCamera of the hero), which
        // took the game back to the hero's room; the hero is now in Room 18 before the arrival is closed, and the game's own sentence begins the scene.
        if (Copy("indy3 vga en") is not { } game) return;
        var result = RunScene(game, 2, 68, 0x01, new(ScummSetupKind.Bit, "bit[0x5DF]", 1, "test", 2), new(ScummSetupKind.PlayedIn, "68", 18, "test", 2));
        Assert.True(result.Begun, result.Failure);
        Assert.Equal(ScummPickedStart.Game, result.Way);
    }

    [Fact]
    public void AHeldObjectComesFirstWhereTheGameNeedsIt()
    {
        // Indy 4 Room 33: Use the Sunstone (934) with the pedestal (377). Script 2 refuses the pedestal in front (Script 9: no class 1 to 3); the Sunstone's
        // own code hands over to the pedestal (+0x0057), so the sentence with the held object first begins the scene the game's own way.
        if (Copy("indy4 vga en") is not { } game) return;
        var result = RunScene(game, 33, 377, 0x08, new ScummSetupEdit(ScummSetupKind.SecondObject, "934", 1, "test", 68));
        Assert.True(result.Begun, result.Failure);
        Assert.Equal(ScummPickedStart.GameHeldFirst, result.Way);
    }

    [Fact]
    public void AnotherVerbOfTheSameCodeReachesTheScene()
    {
        // Indy 4 Room 48: the stone's Push (0x06) and Use (0x08) share their code; the scan names the scene by Push, which the game never runs with the
        // Sunstone. The Sunstone's Use (+0x0057) starts the stone's Use with itself, so the scene begins the game's own way through Use.
        if (Copy("indy4 vga en") is not { } game) return;
        var result = RunScene(game, 48, 589, 0x06, new ScummSetupEdit(ScummSetupKind.SecondObject, "934", 1, "test", 68));
        Assert.True(result.Begun, result.Failure);
        Assert.Equal(ScummPickedStart.GameHeldFirst, result.Way);
        Assert.Equal(0x08, result.Verb);
    }

    [Fact]
    public void AnObjectNoOneCanFindIsHeldForItsVerb()
    {
        // Monkey Island 2 Room 14: the Cheese Squigglies (132) are owned by neither the room nor an inventory, so the game finds no verb code for them
        // (whereIsObject: not found) and answers "I can't reach it"; a pick gives them to the hero first, and Use with the box begins the game's own way.
        if (Copy("mi2 mt32 en") is not { } game) return;
        var result = RunScene(game, 14, 132, 0x07, new(ScummSetupKind.SecondObject, "131", 1, "test", 14), new(ScummSetupKind.ObjectState, "131", 2, "test", 14));
        Assert.True(result.Begun, result.Failure);
        Assert.Equal(ScummPickedStart.Game, result.Way);
    }

    [Fact]
    public void TheHerosRoomInAVerbIsWhereTheObjectIsUsed()
    {
        // Monkey Island 2 Room 28's fishing pole: global script 56 +0x0046 tests the hero's room against Room 59, so the pole is used there, held; the
        // search offers that, with bit[0x02A] from the scene's side of its tests, and the scene begins the game's own way.
        if (Copy("mi2 mt32 en") is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var trigger = planner.Groups.SelectMany(g => new[] { g.First }.Concat(g.Others)).First(t => t.Room == 28 && t.ObjectId == 269 && t.VerbId == 0x07);
        var plan = planner.PlanSetup(trigger);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.PlayedIn, Value: 59 });
    }

    [Fact]
    public void AnEgaScriptIsDecodedOncePerGame()
    {
        // ScummDataSources.For makes a new source per call; the decoded scripts are shared by every source of the same game.
        if (Indy3Ega() is not { } game) return;
        var first = ScummDataSources.For(game);
        var second = ScummDataSources.For(game);
        Assert.Same(first.GlobalScript(25), second.GlobalScript(25));
        Assert.Same(first.RoomEntryScript(20), second.RoomEntryScript(20));
        Assert.Same(first.RoomLocalScript(20, 200), second.RoomLocalScript(20, 200));
        // Another scan of the folder is another game: it reads its own.
        var again = ScummDataSources.For(new GameFolderScanner().Scan(game.FolderPath));
        Assert.NotSame(first.GlobalScript(25), again.GlobalScript(25));
        Assert.Equal(first.GlobalScript(25)!.Instructions.Count, again.GlobalScript(25)!.Instructions.Count);
    }

    [Fact]
    public void TheEgaCastlePlansAreLiveAndIndyWalks()
    {
        // From the boot as the app picks them: each plan stands, and a click on his own corridor walks him (Script 28 +0x0032, walkActorTo).
        if (Indy3Ega() is not { } game) return;
        var boot = ScummGameSession.BootForPlay(game, out _);
        foreach (var (plan, x, y) in new[] { (20, 100, 80), (28, 60, 110), (32, 60, 110) })
        {
            var prepared = boot.PrepareManualRoom(game.Rooms.First(r => r.ResourceId == plan));
            Assert.True(prepared.Succeeded, prepared.Reason);
            var session = prepared.Session!;
            var start = session.Actors.First(a => a.Number == 1);
            Assert.NotNull(session.ClickScreen(x, y, out var refused));
            for (var frame = 0; frame < 15; frame++) session.StepFrame();
            var hero = session.Actors.First(a => a.Number == 1);
            Assert.Equal(plan, session.CurrentRoomId);
            Assert.NotEqual((start.X, start.Y), (hero.X, hero.Y));
        }
    }
}
