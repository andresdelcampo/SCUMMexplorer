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
    public void ARoomThatWaitsForTheHeroToWalkIntoABoxGetsHimThere()
    {
        // LOOM Room 10's entry starts local script 200, which notes Bobbin's walk box (+0x0001) and waits for it to change; box 3 (+0x0032 isEqual
        // local[0x000], 0x0003) begins the scene. The shared engine stands the hero in box 3 a few frames after the room is entered.
        var folder = Path.Combine(TestGames.Root, TestGames.LoomRelative);
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 10, 0, 0));
        output.WriteLine(plan.Note);
        Assert.False(plan.Base.Begun);
        Assert.True(plan.Result.Begun);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.WalkBox, Value: 3 });
    }

    [Fact]
    public void ThePlannerRunsOnLoomsOwnSession()
    {
        // ROADMAP 2b: LOOM's scanned scenes play through the shared planner on LOOM's own session (the app's LoomExplorationSession), with the same
        // walk-box start for Room 10's entry as on the generic session.
        var folder = Path.Combine(TestGames.Root, TestGames.LoomRelative);
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var snapshot = new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL"));
        var planner = new ScummCutscenePlanner(game, () => new SCUMMexplorer.Core.Loom.LoomExplorationSession(game, snapshot));
        var plan = planner.PlanSetup(Trigger(planner, 10, 0, 0));
        output.WriteLine(plan.Note);
        Assert.True(plan.Result.Begun);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.WalkBox, Value: 3 });
    }

    [Fact]
    public void ARoomEntryThatTestsWhereTheHeroCameFromIsEnteredFromThatRoom()
    {
        // Indy 4 Room 10's entry +0x0148 tests var[0x0E0], which the exit hook (Script 23 +0x0014: var[0x0E0] = VAR_ROOM) fills with the room left;
        // setting the variable does nothing (the exit hook overwrites it), so the setup visits a room it tests first and the scene's room is entered from it.
        var folder = Path.Combine(TestGames.Root, "indy4 vga en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 10, 0, 0));
        output.WriteLine(plan.Note);
        Assert.True(plan.Result.Begun);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        // Since 1 November the arrival tested on the way to the cutscene is tried first (Room 12, +0x01A1); Room 15 begins it too.
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.CameFrom, Target: "var[0x0E0]", Value: 12 or 15 });
    }

    [Fact]
    public void ABookLookedAtIsHeldFirstWhenItsCodeAsksWhoOwnsThisObject()
    {
        // Indy 3 Room 17 object 241 verb 0x09 +0x0000: getObjectOwner of var[0x007] ("this object"), and only a book already held shows the inscription
        // (the cutscene at +0x0055); the setup gives the picked book to the hero.
        if (Indy3() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var plan = planner.PlanSetup(Trigger(planner, 17, 241, 0x09));
        output.WriteLine(plan.Note);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.Held, Target: "241" });
    }

    [Fact]
    public void AClassTestOfThisObjectReadsTheClassListNotTheVariable()
    {
        // Indy 4 Room 40 object 462 verb 0x09 +0x0000: ifClassOfIs var[0x007], [0x0088]; the class list is the second bracket (class 8 set), not the
        // "[0x007]" of the variable (which read as class 7 until 31 October).
        var folder = Path.Combine(TestGames.Root, "indy4 vga en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var candidates = planner.Setup.Candidates(Trigger(planner, 40, 462, 0x09));
        Assert.Contains(candidates, edit => edit is { Kind: ScummSetupKind.ObjectClass, Target: "462", Value: 8 });
        Assert.DoesNotContain(candidates, edit => edit is { Kind: ScummSetupKind.ObjectClass, Value: 7 or -7 });
    }

    [Fact]
    public void AVariableReadAgainIsTestedForWhatItHoldsNow()
    {
        // Indy 4 Room 44's entry reads object 529's state into var[0x000] (+0x0311), later Sophia's room (+0x032F getActorRoom var[0x000], 0x02) and
        // compares that with VAR_ROOM (+0x0333): the candidate is actor 2 in the room, not "object 529 state 44".
        var folder = Path.Combine(TestGames.Root, "indy4 vga en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var candidates = planner.Setup.Candidates(Trigger(planner, 44, 0, 0));
        Assert.Contains(candidates, edit => edit is { Kind: ScummSetupKind.ActorRoom, Target: "2", Value: 44 });
        Assert.DoesNotContain(candidates, edit => edit is { Kind: ScummSetupKind.ObjectState, Target: "529", Value: 44 });
    }

    [Fact]
    public void ARoomEntryThatTestsAPseudoRoomNumberIsEnteredUnderIt()
    {
        // Indy 3 Room 21's entry tells its uses apart by VAR_ROOM (+0x001C isEqual VAR_ROOM, 0x81, and 0x82, 0x83): each is a setup candidate, and a
        // session given one enters the room under that number.
        if (Indy3() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var candidates = planner.Setup.Candidates(Trigger(planner, 21, 0, 0));
        Assert.Contains(candidates, edit => edit is { Kind: ScummSetupKind.EnteredAs, Value: 0x82 });
        var session = ScummGameSession.BootForPlay(game, out _);
        Assert.Single(session.ApplySceneSetup([candidates.First(edit => edit is { Kind: ScummSetupKind.EnteredAs, Value: 0x82 })]));
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == 21));
        Assert.Equal(0x82, session.Snapshot.KnownGlobalVariables.GetValueOrDefault(ScummVars.Room));
    }

    [Fact]
    public void ACorridorSceneIsEnteredByTheDoorItsEntryAsksFor()
    {
        // Indy 3 Room 21's entry +0x00DB compares VAR_WALKTO_OBJ (var[0x026], which o5_loadRoomWithEgo sets while the room is entered) with the
        // door 311; only then does it set object 314 to state 1, which local script 200 tests before its cutscene. No literal arrival uses that door.
        if (Indy3() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var plan = planner.PlanSetup(Trigger(planner, 21, 0, 0));
        output.WriteLine(plan.Note);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.EnteredBy, Value: 311 });
    }

    [Fact]
    public void AWaitForTheHeroToWalkPastAPointStandsHimThere()
    {
        // Monkey Island 1 Room 72's local script 201 loops (+0x0000 to +0x0006: getActorX of the hero, isGreater 0xB4, back) until he is left of
        // x 180, then its cutscene; the loop's way back is not the way on, so x 179 is the value on the scene's side.
        var folder = Path.Combine(TestGames.Root, "monkey1 ega en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 72, 0, 0));
        output.WriteLine(plan.Note);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.ActorX, Value: 179 });
    }

    [Fact]
    public void ThisObjectPassedToAScriptIsThePickedObject()
    {
        // Monkey Island 2 Room 18, Close coffin: startScript 203, [var[0x007]]; local 203 reads the state of local[0x000] and closes it only in state 1.
        var folder = Path.Combine(TestGames.Root, "mi2 mt32 en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 18, 182, 0x03));
        output.WriteLine(plan.Note);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.ObjectState, Target: "182", Value: 1 });
    }

    [Fact]
    public void TheSecondObjectPassedToAScriptIsGivenToTheHero()
    {
        // Indy 4 Room 43, X marks the spot: chainScript 210, [0x0206, local[0x000]]; local 210 digs only when local[0x001] is object 1001.
        var folder = Path.Combine(TestGames.Root, "indy4 vga en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 43, 518, 0x08));
        output.WriteLine(plan.Note);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.SecondObject, Target: "1001" });
    }

    [Fact]
    public void AVerbThatPlaysOnlyInAnotherRoomIsUsedThereWithTheObjectHeld()
    {
        // Monkey Island 1 Room 30's sword, Use (+0x0000 isEqual VAR_ROOM, 0x24): "On those helpless dogs?" only in Room 36.
        var folder = Path.Combine(TestGames.Root, "monkey1 ega en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 30, 402, 0x08));
        output.WriteLine(plan.Note);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.PlayedIn, Target: "402", Value: 36 });
    }

    [Fact]
    public void ManiacsFrontDoorIsOpenedAndItsTimedSceneHurried()
    {
        // Maniac Mansion Room 1 object 61 Walk to: ifState var[0x009], IntrinsicOff jumps away (v2: the door must be open), then Script 148 waits
        // 3600 jiffies before its cutscene; Room 36's door starts Script 139, whose seven-minute timer leads to Script 140. Both play the game's way.
        var folder = Path.Combine(TestGames.Root, "mmansion ega en");
        if (!Directory.Exists(folder)) return;
        var planner = SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.Planner(new GameFolderScanner().Scan(folder));
        var door = planner.PlanSetup(Trigger(planner, 1, 61, 0x0D));
        output.WriteLine(door.Note);
        Assert.NotEqual(ScummPickedStart.AtCutscene, door.Result.Way);
        Assert.Contains(door.Edits, edit => edit is { Kind: ScummSetupKind.ObjectStateBit, Target: "61", Value: 8 });
        var round = planner.PlanSetup(Trigger(planner, 36, 124, 0x0D));
        Assert.NotEqual(ScummPickedStart.AtCutscene, round.Result.Way);
    }

    [Fact]
    public void AVersionTwoObjectSceneIsCountedByItsSlashedLabel()
    {
        // Zak McKracken Room 8 object 208 (the drunk, verb 0xFD): versions 1 and 2 label object code "Room 08 / Object 208 / Verb 0xFD"; its
        // cutscene now counts as the scene's own.
        var folder = Path.Combine(TestGames.Root, "zak ega en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 8, 208, 0xFD));
        Assert.True(plan.Result.Begun);
        Assert.NotEqual(ScummPickedStart.AtCutscene, plan.Result.Way);
    }

    [Fact]
    public void TwoVariablesComparedAndAValueSwappedIntoTheCombinedTry()
    {
        // Zak McKracken Room 12's loose dirt: isNotEqual var[0x093], var[0x0EA] (the squirrel) needs the two to differ; Room 27's tram needs the
        // token, object 451 on, pseudo room 185 and the tram locked together (the combined try with the lock swapped in).
        var folder = Path.Combine(TestGames.Root, "zak ega en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var dirt = planner.PlanSetup(Trigger(planner, 12, 267, 0x0B));
        Assert.NotEqual(ScummPickedStart.AtCutscene, dirt.Result.Way);
        Assert.Contains(dirt.Edits, edit => edit is { Kind: ScummSetupKind.VariableMatch, Value: 0 });
        var tram = planner.PlanSetup(Trigger(planner, 27, 445, 0x0B));
        output.WriteLine(tram.Note);
        Assert.NotEqual(ScummPickedStart.AtCutscene, tram.Result.Way);
    }

    [Fact]
    public void AJumpBackIsALoopNotTheWayOn()
    {
        // Indy 4 Room 44's entry (local script 203: a bit, then the hero's walk box and x, inside a loop): reading an unconditional jump back as
        // the loop running again lets its tests guard the cutscene, and the scene plays the game's own way.
        var folder = Path.Combine(TestGames.Root, "indy4 vga en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 44, 0, 0));
        output.WriteLine(plan.Note);
        Assert.NotEqual(ScummPickedStart.AtCutscene, plan.Result.Way);
    }

    [Fact]
    public void AnInputScreenIsAnsweredWithTheValueItCompares()
    {
        // Maniac Mansion Room 9's wall safe: Script 44, the keypad, waits for each key (isNotEqual var[0x044], 0xFF, back) and compares it with the
        // code digit (isNotEqual var[0x044], var[0x03F]); while the pick waits, each key is given the digit, and the safe opens its own way.
        var folder = Path.Combine(TestGames.Root, "mmansion ega en");
        if (!Directory.Exists(folder)) return;
        var planner = SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.Planner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 9, 219, 0x01));
        output.WriteLine(plan.Note);
        Assert.NotEqual(ScummPickedStart.AtCutscene, plan.Result.Way);
        Assert.Contains(19, plan.Result.RoomsEntered);
    }

    [Fact]
    public void AWaitForTwoActorsToComeCloseStandsOneNextToTheOther()
    {
        // Zak McKracken Room 19's entry: Script 42 loops on getDist of the hero and the guru (actor 10) until it is 15 or less; the hero stood
        // next to the guru, the scene plays the game's own way.
        var folder = Path.Combine(TestGames.Root, "zak ega en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 19, 0, 0));
        output.WriteLine(plan.Note);
        Assert.NotEqual(ScummPickedStart.AtCutscene, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.ActorNear, Target: "ego|10" });
    }

    [Fact]
    public void ThePianoIsPlayedByAKidWhoCanPlayIt()
    {
        // Maniac Mansion Room 17's piano (verb 0x0B, +0x0018: isGreaterEqual var[0x000], 2, else "I can't play that thing!"): the hero switched
        // to actor 2, as the game's kid switch does.
        var folder = Path.Combine(TestGames.Root, "mmansion ega en");
        if (!Directory.Exists(folder)) return;
        var planner = SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.Planner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 17, 365, 0x0B));
        output.WriteLine(plan.Note);
        Assert.NotEqual(ScummPickedStart.AtCutscene, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.HeroIs, Value: 2 });
    }

    [Fact]
    public void AVersionTwoSecondObjectIsReadFromActiveObject2()
    {
        // Zak McKracken Room 21's sand pile (verb 0x0B, +0x003F: isEqual var[0x00A], 0x44): versions 1 and 2 keep the second object in
        // VAR_ACTIVE_OBJECT2; the setup gives object 0x44 and the direct run sets the variable.
        var folder = Path.Combine(TestGames.Root, "zak ega en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 21, 391, 0x0B));
        output.WriteLine(plan.Note);
        Assert.NotEqual(ScummPickedStart.AtCutscene, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.SecondObject, Target: "68" });
    }

    [Fact]
    public void TheGrailDiaryOpensOnAPageInARoomItsCodeNames()
    {
        // Indy 3 Room 2 object 68 Open: the page shown depends on VAR_ROOM_RESOURCE (+0x002E: rooms 18, 62, 48, 81), which startScene sets
        // (var 22); the diary held and opened in Room 18, with bit[0x5DF] kept.
        if (Indy3() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var plan = planner.PlanSetup(Trigger(planner, 2, 68, 0x01));
        output.WriteLine(plan.Note);
        Assert.True(plan.Result.Begun);
        Assert.NotEqual(ScummPickedStart.AtCutscene, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.PlayedIn, Target: "68", Value: 18 });
    }

    [Fact]
    public void ADirectRunPassesTheSecondObjectAndAVariableIsTriedBesideItsConstant()
    {
        // Indy 4 Room 72's peg (verb 0x06) is used with object 934; the sentence script refuses it (Script 9: the peg has no class 1 to 3), so the
        // verb's code is run directly with the second object as local[0x000], as startObject passes it. Indy 3 Room 24's first aid kit plays only
        // when var[0x0C1] is not 1000.
        var folder = Path.Combine(TestGames.Root, "indy4 vga en");
        if (Directory.Exists(folder))
        {
            var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
            var plan = planner.PlanSetup(Trigger(planner, 72, 992, 0x06));
            Assert.Equal(ScummPickedStart.Direct, plan.Result.Way);
            Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.SecondObject, Target: "934" });
        }
        if (Indy3() is not { } game) return;
        var indy3 = new ScummCutscenePlanner(game);
        var kit = indy3.PlanSetup(Trigger(indy3, 24, 360, 0x08));
        Assert.NotEqual(ScummPickedStart.AtCutscene, kit.Result.Way);
        Assert.Contains(kit.Edits, edit => edit is { Kind: ScummSetupKind.Variable, Target: "var[0x0C1]", Value: 999 });
    }

    [Fact]
    public void AScriptThatMustBeRunningIsStartedAndACameraWaitIsMet()
    {
        // Indy 3 Room 32's painting (object 489 verb 0x0A) starts Script 115 only while Script 114 runs; Room 86's local script 201 waits for the
        // camera to be left of x 0x186 before its cutscene.
        if (Indy3() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var painting = planner.PlanSetup(Trigger(planner, 32, 489, 0x0A));
        Assert.Equal(ScummPickedStart.Game, painting.Result.Way);
        Assert.Contains(painting.Edits, edit => edit is { Kind: ScummSetupKind.ScriptRunning, Target: "114" });
        var knight = planner.PlanSetup(Trigger(planner, 86, 0, 0));
        Assert.Equal(ScummPickedStart.Game, knight.Result.Way);
        Assert.Contains(knight.Edits, edit => edit is { Kind: ScummSetupKind.CameraX, Value: 389 });
    }

    [Fact]
    public void ASceneStartedAtItsCutsceneGetsTheValuesThatLetItsLinesRun()
    {
        // Monkey Island 2 Room 79, Wally: local script 207 starts with its cutscene; its first lines follow notEqualZero bit[0x237] (+0x0037).
        var folder = Path.Combine(TestGames.Root, "mi2 mt32 en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 79, 777, 0x0A));
        Assert.Equal(ScummPickedStart.AtCutscene, plan.Result.Way);
        Assert.True(plan.Result.Lines > 0);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.Bit, Target: "bit[0x237]" });
    }

    [Fact]
    public void AnObjectAnotherActorMustHoldIsGivenToThatActor()
    {
        // Indy 3 Room 82's cracked seal, Look (+0x0000 getObjectOwner of the Grail, 873; +0x0005 isEqual 3): the cutscene plays when Elsa (actor 3)
        // holds it, not the hero.
        if (Indy3() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var plan = planner.PlanSetup(Trigger(planner, 82, 864, 0x09));
        output.WriteLine(plan.Note);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.OwnedBy, Target: "873", Value: 3 });
    }

    [Fact]
    public void TheScriptsOnTheWayToTheCutsceneAreReadForTheirTests()
    {
        // Monkey Island 2 Room 56's entry starts local script 200, whose bit tests decide whether local 207 runs; the scan lists only 207 and
        // global script 25 (the scripts the scene reaches), so 200's tests were never candidates and the scene showed nothing.
        var folder = Path.Combine(TestGames.Root, "mi2 mt32 en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var trigger = Trigger(planner, 56, 0, 0);
        Assert.Contains(planner.Setup.Candidates(trigger), edit => edit is { Kind: ScummSetupKind.Bit, Target: "bit[0x0D2]" });
        // With bit[0x0D2] it plays its lines (22, started at its cutscene); before, it showed nothing.
        Assert.True(planner.PlanSetup(trigger).Result.Lines > 0);
    }

    [Fact]
    public void AChainedScriptsLocalsAreTheArgumentsItsCallerGives()
    {
        // Indy 4 Room 7 object 116 verb 0x09: chainScript 206, [0x0074, 0x0009]; local script 206 +0x0007 tests the class of local[0x000], which is
        // object 116 (class 18 set: "Another cat idol, it looks odd").
        var folder = Path.Combine(TestGames.Root, "indy4 vga en");
        if (!Directory.Exists(folder)) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        var plan = planner.PlanSetup(Trigger(planner, 7, 116, 0x09));
        output.WriteLine(plan.Note);
        Assert.Equal(ScummPickedStart.Game, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.ObjectClass, Target: "116", Value: 18 });
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
        // Old v3 keeps bit n in bit (n & 15) of global word n >> 4; a bit already set is kept (never lowered). A variable is set to the value its test
        // compares with (31 October: Indy 4 Room 10's entry compares var[0x0E0] with room numbers), and left alone when it already holds it.
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
        Assert.Empty(session.ApplySceneSetup([new(ScummSetupKind.Bit, $"bit[0x{bit:X3}]", 1, "test"), new(ScummSetupKind.Variable, variable, 1000, "test")]));
        Assert.Single(session.ApplySceneSetup([new(ScummSetupKind.Variable, variable, 7, "test")]));
        Assert.Equal(7, session.Snapshot.KnownGlobalVariables[variable]);
    }

    [Fact]
    public void APickedObjectThatIsNotInTheRoomYetIsBroughtIn()
    {
        // R-8.36/R-8.38: an object of the room's data that is not a hotspot at the first visit (owned elsewhere, untouchable, its parent in another state)
        // becomes one for its picked interaction; one that is present already is left as it is.
        if (Indy3() is not { } game) return;
        foreach (var roomId in new[] { 2, 13, 26, 39 })
        {
            var session = ScummGameSession.BootForPlay(game, out _);
            session.VisitRoom(game.Rooms.First(r => r.ResourceId == roomId));
            var records = ScummDataSources.For(game).ReadRoom(roomId).Objects;
            bool Present(ScummRoomObjectRecord record) => RoomHotspotRules.IsHotspot(record, records, session.Snapshot);
            var absent = records.Where(r => r.Width > 0 && r.Height > 0 && (r.X, r.Y) != (0, 0) && !Present(r)).ToList();
            var present = records.FirstOrDefault(Present);
            output.WriteLine($"Room {roomId}: absent [{string.Join(", ", absent.Select(r => r.Id))}], present {present?.Id}");
            if (present is not null) Assert.Empty(session.BringObjectIntoRoom(present.Id));
            foreach (var record in absent)
            {
                var changed = session.BringObjectIntoRoom(record.Id);
                output.WriteLine($"  object {record.Id}: {string.Join("; ", changed)}");
                Assert.True(Present(record), $"Room {roomId} object {record.Id} is still not in the room");
            }
        }
    }

    [Fact]
    public void ADoorThatTestsItsOwnStateIsOpenedForItsClose()
    {
        // Room 23 object 323 verb 0x02 +0x0000 ifState var[0x007], 0x01: "this object", which the sentence script sets; the room's entry closes its
        // doors, so the setup is applied again after the visit. The scene then begins the game's own way, not at its cutscene.
        if (Indy3() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var plan = planner.PlanSetup(Trigger(planner, 23, 323, 0x02));
        output.WriteLine(plan.Note);
        Assert.True(plan.Result.Begun);
        Assert.NotEqual(ScummPickedStart.AtCutscene, plan.Result.Way);
        Assert.Contains(plan.Edits, edit => edit is { Kind: ScummSetupKind.ObjectState, Target: "323", Value: 1 });
    }

    [Fact]
    public void ADerivationKeptBetweenLaunchesNeedsNoNewRuns()
    {
        // ScenePlanCache (31 October 2026): the runs and plans saved as JSON and loaded into a new planner make every scene derived without running it.
        if (Indy3() is not { } game) return;
        var first = new ScummCutscenePlanner(game);
        var jar = Trigger(first, 2, 71, 0x08);
        var plan = first.PlanSetup(jar);
        var json = System.Text.Json.JsonSerializer.Serialize(first.ExportScenes().ToList());
        var kept = System.Text.Json.JsonSerializer.Deserialize<List<ScummCutscenePlanner.SavedScene>>(json)!;
        var second = new ScummCutscenePlanner(game);
        second.ImportScenes(kept);
        Assert.True(second.IsDerived(jar));
        var again = second.PlanSetup(jar);
        Assert.Equal(plan.Result.Way, again.Result.Way);
        Assert.Equal(plan.Edits.Select(e => e.Describe()), again.Edits.Select(e => e.Describe()));
    }
}
