using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// 14 November 2026 (user: the Grail trials, "a 'go to' non-object place in the screen"): a room whose entry installs its own input script answers
/// clicks itself; a click there is a trigger, played by the game's own code. Record: <c>.verification/click-scenes-20261114/README.md</c>.
/// </summary>
public sealed class ClickScenesTests
{
    private static GameLibrary? Indy3Ega()
    {
        var folder = Path.Combine(TestGames.Root, "indy3 ega en");
        return File.Exists(Path.Combine(folder, "00.LFL")) ? new GameFolderScanner().Scan(folder) : null;
    }

    [Fact]
    public void TheBreathOfGodPassesOnTheKneelingPlaceAndKillsElsewhere()
    {
        // Room 83's entry installs local script 200; it compares the clicked object with 878 (+0x00A7). Passed: on to Room 84. Elsewhere past x 288:
        // the blades, and Room 82 again ("Be back in a jiffy!").
        if (Indy3Ega() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var clicks = planner.Groups.Select(g => g.First).Where(t => t.Room == 83 && t.IsClick).ToList();
        Assert.Contains(clicks, t => t.ObjectId == 878);
        var pass = planner.PlanSetup(clicks.First(t => t.ObjectId == 878));
        Assert.True(pass.Result.Begun, pass.Result.Failure);
        Assert.Contains(84, pass.Result.RoomsEntered);
        // The floor points past x 288 play the same death: one entry offered, the others "play as" it.
        var deaths = clicks.Where(t => t.ObjectId == 0 && t.Click!.Value.X > 288).ToList();
        Assert.True(deaths.Count >= 2);
        foreach (var death in deaths) Assert.Contains(82, planner.PlanSetup(death).Result.RoomsEntered);
        Assert.Single(deaths, death => planner.NotOfferedReason(death) is null);
    }

    [Fact]
    public void AVersion3PrintShowsTheStringItNames()
    {
        // Room 84 local script 205 +0x00A1: "Jehovah is spelled `" then code 7 of var[0x035] (0x20, the string the entry loaded). It showed nothing.
        if (Indy3Ega() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var fall = planner.Groups.Select(g => g.First).First(t => t.Room == 84 && t.IsClick);
        ScummSession? last = null;
        var mark = 0;
        ScummCutsceneRun.Run(game, () =>
        {
            last = ScummGameSession.BootForPlay(game, out _);
            ScummCutscenePlanner.IntoControl(last);
            mark = last.Messages.Log.Count;
            return last;
        }, fall);
        Assert.Contains(last!.Messages.Log.Skip(mark), m => m.Text.Contains("spelled `", StringComparison.Ordinal) && !m.Text.Contains("``", StringComparison.Ordinal));
    }
}
