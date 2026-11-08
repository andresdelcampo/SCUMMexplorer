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
    public void ASecondObjectsClassNamesTheObjectsThatHaveIt()
    {
        // Indy 3 Room 26's hot coals: Use tests its second object for class 12 (a container) and class 10 (full). The index puts objects 337 and 520 in
        // class 12; they are offered held, with class 10 set, and the scene begins the game's own way.
        if (Copy("indy3 vga en") is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var trigger = planner.Groups.SelectMany(g => new[] { g.First }.Concat(g.Others)).First(t => t.Room == 26 && t.ObjectId == 401 && t.VerbId == 0x08);
        var plan = planner.PlanSetup(trigger);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.SecondObject, Target: "337" or "520" });
    }

    [Fact]
    public void WhatTheRoomDoesAsTheHeroArrivesPlaysBeforeThePickedScene()
    {
        // LOOM Room 23 (ROADMAP 2b, the English catalog as the answer key): local script 201 begins the shepherds' first meeting (local 202, 212: 40 lines)
        // once Bobbin is left of x 125, and their walk point is x 121. It plays before the draft, as the walk there would; the reaction alone is the scene
        // (the catalog's Other draft response, 70 frames less its idle tail).
        if (Copy("loom ega mt32 en") is not { } game) return;
        var snapshot = new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
        var planner = new ScummCutscenePlanner(game, () => new SCUMMexplorer.Core.Loom.LoomExplorationSession(game, snapshot));
        var trigger = planner.Groups.SelectMany(g => new[] { g.First }.Concat(g.Others)).First(t => t.Room == 23 && t.ObjectId == 422 && t.VerbId == 0x56);
        var plan = planner.PlanSetup(trigger);
        Assert.True(plan.Result.Begun, plan.Result.Failure);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.InRange(plan.Result.Frames, 30, 120);
        Assert.InRange(plan.Result.Lines, 1, 3);
    }

    [Fact]
    public void APickedSceneGetsTheStateOfTheCodeBeforeIt()
    {
        // ROADMAP 3c, scene order: Monkey Island 2 Room 5's entry needs bit[0x0A5], which only global script 52 sets; what that script
        // leaves (bit[0x30D]) comes with it, labelled as earlier code, and the scene still begins and reaches its rooms.
        if (Copy("mi2 mt32 en") is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var trigger = planner.Groups.SelectMany(g => new[] { g.First }.Concat(g.Others)).First(t => t.Room == 5 && t.IsRoomEntry);
        var plan = planner.PlanSetup(trigger);
        Assert.True(plan.Result.Begun);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.Bit, Target: "bit[0x0A5]" });
        Assert.Contains(plan.Edits, edit => edit.Target == "bit[0x30D]" && edit.Where.Contains("earlier code", StringComparison.Ordinal));
    }

    [Fact]
    public void ASceneAFreshStartBeginsGetsWhatEveryWayIntoItsRoomNeeds()
    {
        // ROADMAP 3c, second step: the only way into Monkey Island 2's Room 43 the code reads (global script 48 +0x2171) tests var[0x0AF] = 1 first,
        // so the book of pirate quotations there, which a fresh start already begins, gets it as the state of its place in the story.
        if (Copy("mi2 mt32 en") is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        Assert.Contains(planner.Setup.RoomNeeds(43), need => need is { Kind: ScummSetupKind.Variable, Target: "var[0x0AF]", Value: 1 });
        var trigger = planner.Groups.SelectMany(g => new[] { g.First }.Concat(g.Others)).First(t => t is { Room: 43, ObjectId: 444, VerbId: 0x05 });
        var plan = planner.PlanSetup(trigger);
        Assert.True(plan.Base.Begun);
        Assert.True(plan.Result.Begun);
        Assert.Contains(plan.Edits, edit => edit.Target == "var[0x0AF]" && edit.Where.Contains("every way in", StringComparison.Ordinal));
        Assert.StartsWith("Story point (estimate)", plan.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void EarlierCodeBringsOnlyStoryState()
    {
        // ROADMAP 3c: Maniac Mansion's microwave (Room 7 object 85, Open) gets the state of Room 6's object 139, which sets var[0x0A6]; the engine's
        // second-object variable on the way there (var[0x00A]) is that code's sentence, and set it spoiled the window's pick (8 November 2026).
        if (Copy("mmansion ega en") is not { } game) return;
        var planner = SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.Planner(game);
        var trigger = planner.Groups.SelectMany(g => new[] { g.First }.Concat(g.Others)).First(t => t is { Room: 7, ObjectId: 85, VerbId: 0x01 });
        var plan = planner.PlanSetup(trigger);
        Assert.True(plan.Result.Begun);
        Assert.Contains(plan.Edits, edit => edit.Target == "var[0x0A8]" && edit.Where.Contains("earlier code", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Edits, edit => edit.Target == "var[0x00A]");
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
