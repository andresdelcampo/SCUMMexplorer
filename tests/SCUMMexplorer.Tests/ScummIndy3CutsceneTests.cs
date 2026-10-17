using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S7 Cutscenes for Indiana Jones and the Last Crusade, EGA and 256 colours (17 October 2026): the engine's scan (<see cref="ScummCutsceneIndex"/>) lists
/// the game's cutscene triggers from its scripts, titled with the verb names its own scripts give (Script 96), and three scenes begin from a fresh start
/// in a headless run of the generic session (<see cref="ScummCutsceneRun"/>): the Room 73 light switch (object 837, Push, Pull, Use and Turn off share one
/// code block), Room 22's entry ("Meanwhile, elsewhere in the castle") and Room 80's entry ("Dad! How'd you get here before me?"). Registry: Cutscenes ◐
/// for both families. Record: <c>.verification/indy3-cutscenes-20261017/README.md</c>. Skips when a game folder is absent.
/// </summary>
public sealed class ScummIndy3CutsceneTests(ITestOutputHelper output)
{
    private static string? EgaFolder()
    {
        var overridden = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        var folder = string.IsNullOrWhiteSpace(overridden) ? Path.Combine(TestGames.Root, "indy3 ega en") : overridden;
        return File.Exists(Path.Combine(folder, "00.LFL")) ? folder : null;
    }

    public static TheoryData<string> Releases => ["ega", "vga"];

    private static readonly Dictionary<string, Lazy<ScummCutscenePlanner?>> Planners = new()
    {
        ["ega"] = new(() => EgaFolder() is { } folder ? new ScummCutscenePlanner(new GameFolderScanner().Scan(folder)) : null),
        ["vga"] = new(() => ScummIndy3VgaSessionTests.Folder() is { } folder ? new ScummCutscenePlanner(new GameFolderScanner().Scan(folder)) : null)
    };

    private static ScummCutsceneGroup GroupOf(ScummCutscenePlanner planner, int room, int objectId, int verb) =>
        Assert.Single(planner.Groups, g => g.First.Room == room && (g.First.ObjectId == objectId && g.First.VerbId == verb ||
                                                                   g.Others.Any(o => o.ObjectId == objectId && o.VerbId == verb)));

    [Theory]
    [MemberData(nameof(Releases))]
    public void TheScanListsTheGamesCutscenesWithItsOwnVerbNames(string release)
    {
        if (Planners[release].Value is not { } planner) return;
        output.WriteLine($"{release}: {planner.Triggers.Count} triggers, {planner.Groups.Count} groups, {planner.Groups.Count(g => g.IsScene)} scenes");
        // Script 96 names the play verbs; the titles take them from the booted session, not from a table.
        Assert.Equal("Push", planner.Verbs.WordFor(0x06));
        Assert.Equal("Pick up", planner.Verbs.WordFor(0x0B));
        Assert.True(planner.Groups.Count >= 130, $"only {planner.Groups.Count} groups");

        // The light switch: four verbs, one code block (+0x0002 in EGA), one entry in the list.
        var lights = GroupOf(planner, 73, 837, 0x06);
        Assert.Equal(4, lights.Others.Count + 1);
        Assert.Equal("HEY!! WHO TURNED OUT THE LIGHTS!!", lights.First.FirstText);
        Assert.Equal("Room 73 · Push light switch", ScummCutsceneTitles.Title(lights.Others.Append(lights.First).Single(t => t.VerbId == 0x06)));

        var castle = GroupOf(planner, 22, 0, 0);
        Assert.Equal("Room 22 · Entry", ScummCutsceneTitles.Title(castle.First));
        Assert.True(castle.IsScene);
        Assert.StartsWith("Meanwhile, elsewhere in the castle", castle.First.FirstText, StringComparison.Ordinal);

        var grail = GroupOf(planner, 80, 0, 0);
        Assert.StartsWith("Dad!", grail.First.FirstText, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Releases))]
    public void ThreeScenesBeginFromAFreshStart(string release)
    {
        if (Planners[release].Value is not { } planner) return;
        foreach (var (room, objectId, verb) in new[] { (73, 837, 0x06), (22, 0, 0), (80, 0, 0) })
        {
            var group = GroupOf(planner, room, objectId, verb);
            var run = planner.Run(group.First);
            output.WriteLine($"{release} {ScummCutsceneTitles.Title(group.First)}: begun {run.Begun}, {run.Frames} frames, rooms [{string.Join(", ", run.RoomsEntered)}] {run.Failure}");
            Assert.True(run.Begun, $"{release} Room {room} object {objectId} verb 0x{verb:X2}: {run.Failure}");
            Assert.StartsWith("A fresh start begins its cutscene", planner.StartNote(group.First), StringComparison.Ordinal);
        }
    }
}
