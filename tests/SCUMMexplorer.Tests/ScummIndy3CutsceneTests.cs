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

    public static TheoryData<string> Releases => ["ega", "vga", "ega es", "vga es"];

    private static string? Spanish(string folderName)
    {
        var folder = Path.Combine(TestGames.Root, folderName);
        return File.Exists(Path.Combine(folder, "00.LFL")) && File.Exists(Path.Combine(folder, "73.LFL")) ? folder : null;
    }

    /// <summary>
    /// What the list shows, per copy (S9, 17 October 2026): the Spanish copies (<c>Games/indy3 ega es</c>, <c>Games/indy3 vga es</c>) give the same triggers,
    /// groups and runs through the same ids; only the verb names and lines are the copy's own (CP437: 0xAD and 0xA8 are the inverted marks).
    /// Record: <c>.verification/indy3-editions-20261017/README.md</c>.
    /// </summary>
    private sealed record Texts(string Push, string PickUp, string Lights, string LightsTitle, string Castle, string Grail);

    private static readonly Texts English = new("Push", "Pick up", "HEY!! WHO TURNED OUT THE LIGHTS!!", "Room 73 · Push light switch", "Meanwhile, elsewhere in the castle", "Dad!");
    // The two Spanish copies are separate translations (Walk to is "Andar hacia" in EGA, "Ir a" in 256 colours). The scan's first text replaces every
    // escaped byte with a space (ScummCutsceneIndex, owned by the S7 Monkey Island work), so the switch's line and the grail line are checked by a
    // fragment without accented letters or inverted marks, which holds before and after that is fixed.
    private static readonly Dictionary<string, Texts> TextsByRelease = new()
    {
        ["ega"] = English,
        ["vga"] = English,
        ["ega es"] = new("Empujar", "Coger", "QUIEN HA APAGADO LAS LUCES", "Room 73 · Empujar interruptor", "Entre tanto, en otra parte del castillo", "Como has llegado aqui"),
        ["vga es"] = new("Empujar", "Coger", "QUIEN APAGO LAS LUCES", "Room 73 · Empujar interruptor", "Mientras,en otra parte delCastillo", "Como llegaste antes")
    };
    private static Texts TextsOf(string release) => TextsByRelease[release];

    private static readonly Dictionary<string, Lazy<ScummCutscenePlanner?>> Planners = new()
    {
        ["ega"] = new(() => EgaFolder() is { } folder ? new ScummCutscenePlanner(new GameFolderScanner().Scan(folder)) : null),
        ["vga"] = new(() => ScummIndy3VgaSessionTests.Folder() is { } folder ? new ScummCutscenePlanner(new GameFolderScanner().Scan(folder)) : null),
        ["ega es"] = new(() => Spanish("indy3 ega es") is { } folder ? new ScummCutscenePlanner(new GameFolderScanner().Scan(folder)) : null),
        ["vga es"] = new(() => Spanish("indy3 vga es") is { } folder ? new ScummCutscenePlanner(new GameFolderScanner().Scan(folder)) : null)
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
        var texts = TextsOf(release);
        Assert.Equal(texts.Push, planner.Verbs.WordFor(0x06));
        Assert.Equal(texts.PickUp, planner.Verbs.WordFor(0x0B));
        Assert.True(planner.Groups.Count >= 130, $"only {planner.Groups.Count} groups");

        // The light switch: four verbs, one code block (+0x0002 in EGA), one entry in the list.
        var lights = GroupOf(planner, 73, 837, 0x06);
        Assert.Equal(4, lights.Others.Count + 1);
        Assert.Contains(texts.Lights, lights.First.FirstText, StringComparison.Ordinal);
        Assert.Equal(texts.LightsTitle, ScummCutsceneTitles.Title(lights.Others.Append(lights.First).Single(t => t.VerbId == 0x06)));

        var castle = GroupOf(planner, 22, 0, 0);
        Assert.Equal("Room 22 · Entry", ScummCutsceneTitles.Title(castle.First));
        Assert.True(castle.IsScene);
        Assert.StartsWith(texts.Castle, castle.First.FirstText, StringComparison.Ordinal);

        var grail = GroupOf(planner, 80, 0, 0);
        Assert.Contains(texts.Grail, grail.First.FirstText, StringComparison.Ordinal);
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
