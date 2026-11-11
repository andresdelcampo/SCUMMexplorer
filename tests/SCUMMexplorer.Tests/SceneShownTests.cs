using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Roadmap step 3 (11 November 2026): scenes judged by what they show. Record: <c>.verification/picture-checks-20261110/README.md</c>.
/// </summary>
public sealed class SceneShownTests
{
    private static GameLibrary? Indy3Ega()
    {
        var folder = Path.Combine(TestGames.Root, "indy3 ega en");
        return File.Exists(Path.Combine(folder, "00.LFL")) ? new GameFolderScanner().Scan(folder) : null;
    }

    private static ScummCutscene Scene(ScummCutscenePlanner planner, int room, int obj, int verb) =>
        planner.Groups.SelectMany(g => new[] { g.First }.Concat(g.Others)).First(t => t.Room == room && t.ObjectId == obj && t.VerbId == verb);

    [Fact]
    public void AFullerSecondObjectReplacesTheFirstThatBegins()
    {
        // Indy 3 Room 82, Give Grail: with object 3 (verb 0x03 +0x0019) the scene begins and plays a short branch; with object 10 (+0x0000) it is the
        // ending, on to Room 94. A sentence has one second object, so the search tries the other in its place and keeps the one that shows more.
        if (Indy3Ega() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var plan = planner.PlanSetup(Scene(planner, 82, 873, 0x03));
        Assert.True(plan.Result.Begun, plan.Result.Failure);
        Assert.Contains(94, plan.Result.RoomsEntered);
        Assert.Single(plan.Edits, edit => edit.Kind is ScummSetupKind.SecondObject or ScummSetupKind.SecondActor);
        Assert.Contains(plan.Edits, edit => edit.Kind == ScummSetupKind.SecondObject && edit.Target == "10");
    }

    [Fact]
    public void AnActorTheGamesOwnScriptsDressedAndPlacedIsNoSuspect()
    {
        // Indy 3's boot (Script 118 +0x0045, +0x00E8) dresses actor 4 as Marcus and puts him in Room 1: his costume is "from before the run" in every
        // scene of Room 1, and right.
        if (Indy3Ega() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var plan = planner.PlanSetup(Scene(planner, 1, 21, 0x01));
        Assert.True(plan.Result.Begun, plan.Result.Failure);
        Assert.NotNull(plan.Result.Picture);
        Assert.DoesNotContain(plan.Result.Picture!, suspect => suspect.Kind == ScummPictureSuspectKind.CostumeFromBefore);
    }
}
