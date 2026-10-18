using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S7 Cutscenes for Monkey Island 1, four-disk 256-colour floppy (17 October 2026): the engine's scan (<see cref="ScummCutsceneIndex"/>) lists the
/// game's cutscene triggers from its scripts, titled with the verb names its own scripts give, and three scenes begin from a fresh start in a headless
/// run of the generic session (<see cref="ScummCutsceneRun"/>): the poster's first Look at in Room 33 (object 438 verb 0x09, <c>cutscene</c> +0x00CF to
/// <c>endCutscene</c> +0x0134, "Re-elect Governor Marley."), Room 37's entry (global script 60, "Hey!  I don't like visitors!") and, later in the game,
/// Room 19's entry on the ship (local script 200, which goes on to Room 7). A cutscene the room's own background scripts begin (the SCUMM Bar's doors,
/// global scripts 25/26) is not the trigger's. Registry: Cutscenes ◐. Record: <c>.verification/monkey1-cutscenes-20261017/README.md</c>. Skips when
/// the game folder is absent.
/// </summary>
public sealed class ScummMonkeyFloppyCutsceneTests(ITestOutputHelper output)
{
    private static string? Folder()
    {
        var path = Path.Combine(TestGames.Root, "monkey1 ega en");
        return File.Exists(Path.Combine(path, "DISK01.LEC")) ? path : null;
    }

    private static readonly Lazy<ScummCutscenePlanner?> Planner =
        new(() => Folder() is { } folder ? new ScummCutscenePlanner(new GameFolderScanner().Scan(folder)) : null);

    private static ScummCutsceneGroup GroupOf(ScummCutscenePlanner planner, int room, int objectId, int verb) =>
        Assert.Single(planner.Groups, g => g.First.Room == room && (g.First.ObjectId == objectId && g.First.VerbId == verb ||
                                                                   g.Others.Any(o => o.ObjectId == objectId && o.VerbId == verb)));

    [Fact]
    public void TheScanListsTheGamesCutscenesWithItsOwnVerbNames()
    {
        if (Planner.Value is not { } planner) return;
        output.WriteLine($"{planner.Triggers.Count} triggers, {planner.Groups.Count} groups, {planner.Groups.Count(g => g.IsScene)} scenes");
        // The boot's verb script names the play verbs; the titles take them from the booted session, not from a table.
        Assert.Equal("Look at", planner.Verbs.WordFor(0x09));
        Assert.Equal("Talk to", planner.Verbs.WordFor(0x0D));
        Assert.Equal("Pick up", planner.Verbs.WordFor(0x0B));
        Assert.True(planner.Groups.Count >= 150, $"only {planner.Groups.Count} groups");

        Assert.Equal("Room 33 · Look at poster", ScummCutsceneTitles.Title(GroupOf(planner, 33, 438, 0x09).First));

        var meathook = GroupOf(planner, 37, 0, 0);
        Assert.Equal("Room 37 · Entry", ScummCutsceneTitles.Title(meathook.First));
        Assert.True(meathook.IsScene);
        Assert.StartsWith("Hey!  I don't like visitors!", meathook.First.FirstText, StringComparison.Ordinal);

        var ship = GroupOf(planner, 19, 0, 0);
        Assert.True(ship.IsScene);
        Assert.StartsWith("First of all", ship.First.FirstText, StringComparison.Ordinal);
    }

    [Fact]
    public void ThreeScenesBeginFromAFreshStart()
    {
        if (Planner.Value is not { } planner) return;
        foreach (var (room, objectId, verb) in new[] { (33, 438, 0x09), (37, 0, 0), (19, 0, 0) })
        {
            var group = GroupOf(planner, room, objectId, verb);
            var run = planner.Run(group.First);
            output.WriteLine($"{ScummCutsceneTitles.Title(group.First)}: begun {run.Begun}, {run.Frames} frames, rooms [{string.Join(", ", run.RoomsEntered)}] {run.Failure}");
            Assert.True(run.Begun, $"Room {room} object {objectId} verb 0x{verb:X2}: {run.Failure}");
            Assert.StartsWith("A fresh start begins its cutscene", planner.StartNote(group.First), StringComparison.Ordinal);
        }
        // The ship's scene goes on in Room 7.
        Assert.Contains(7, planner.Run(GroupOf(planner, 19, 0, 0).First).RoomsEntered);
    }

    [Fact]
    public void ThePostersCutsceneShowsItsLineFromAFreshStart()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == 33));
        session.ActivateObjectVerb(438, 0x09);
        var frames = 0;
        while (frames < 120 && !session.Messages.OnScreen.Any(m => m.Text.StartsWith("Re-elect Governor Marley.", StringComparison.Ordinal)))
        {
            session.AdvanceFrame();
            frames++;
        }
        Assert.True(frames < 120, "the poster's line never showed");
        Assert.True(session.InCutscene);
        Assert.Contains(session.SessionTrace, line => line.StartsWith("cutscene at Room 33 Object 438 Verb 0x09 +0x00CF", StringComparison.Ordinal));
    }

    [Fact]
    public void TheScansFirstLineKeepsTheCopysCharactersAndSpacesItsControls()
    {
        // ScummTextDecoder.DecodeLine (17 October 2026, the S9 open item of Indy3): a glyph code from 0x80 is the copy's character in CP437; a text
        // control (0xFF, or 0xFE as ScummVM reads it up to v6, its code and the two operand bytes of codes such as 0x0A) and other escapes are spaces.
        Assert.Equal("Treasure of Mêlée Island  and", ScummTextDecoder.DecodeLine(@"Treasure of M\x88l\x82e Island\x0F and"));
        Assert.Equal("The three trials!    ", ScummTextDecoder.DecodeLine(@"The three trials!\xFE\x01\xFF\x02"));
        Assert.Equal("¡EH! ¿QUIEN", ScummTextDecoder.DecodeLine(@"\xADEH! \xA8QUIEN"));
        Assert.Equal("a    b", ScummTextDecoder.DecodeLine(@"a\xFF\x0A\x80\x00b"));
        Assert.Equal("one  two", ScummTextDecoder.DecodeLine(@"one\xFF\x03two"));
    }

    [Fact]
    public void ACutsceneTheRoomsOwnScriptsBeginIsNotTheTriggers()
    {
        if (Planner.Value is not { } planner) return;
        // The SCUMM Bar's pirates go through the kitchen door (global scripts 25/26, each a short cutscene) about 480 frames after a visit;
        // closing the front door begins nothing of its own.
        var run = planner.Run(GroupOf(planner, 28, 322, 0x02).First);
        Assert.False(run.Begun);
        Assert.Equal("global 25", ScummCutsceneRun.ScriptKey("Script 25 +0x0019 runs script 18"));
        Assert.Equal("global 25", ScummCutsceneRun.ScriptKey("global script 25"));
        Assert.Equal("local 12/200", ScummCutsceneRun.ScriptKey("Room 12 local script 200"));
        Assert.Equal("object 438", ScummCutsceneRun.ScriptKey("Room 33 Object 438 Verb 0x09 +0x00CF"));
        Assert.Equal("entry 35", ScummCutsceneRun.ScriptKey("Room 35 entry script +0x0133"));
    }

    /// <summary>
    /// S9 (17 October 2026): the Spanish eight-disk 16-colour release (<c>Games/monkey1 ega es</c>) gives the same list as English (294 triggers, 164
    /// entries, 63 scenes, the same keys, scripts and groups; 104 begin from a fresh start in both, record
    /// <c>.verification/monkey1-editions-20261017</c>); its titles and lines are its own, with the accents its texts hold (CP437 glyph codes, decoded by
    /// <see cref="ScummTextDecoder"/> since the same day). The poster's span starts at +0x00D9 there (English +0x00CF): its lines are longer.
    /// </summary>
    // S9 (18 October 2026): the Spanish four-disk 256-colour floppy (<c>Games/monkey1 vga es</c>, record <c>.verification/monkeycd-editions-20261018</c>) too.
    [Theory]
    [InlineData("monkey1 ega es", "DISK08.LEC")]
    [InlineData("monkey1 vga es", "DISK04.LEC")]
    public void TheSpanishReleaseListsTheSameScenesWithItsOwnAccentedTexts(string copy, string lastDisk)
    {
        var folder = Path.Combine(TestGames.Root, copy);
        if (!File.Exists(Path.Combine(folder, lastDisk))) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        output.WriteLine($"{planner.Triggers.Count} triggers, {planner.Groups.Count} groups, {planner.Groups.Count(g => g.IsScene)} scenes");
        Assert.Equal("Mirar", planner.Verbs.WordFor(0x09));
        Assert.Equal("Room 33 · Mirar poster", ScummCutsceneTitles.Title(GroupOf(planner, 33, 438, 0x09).First));
        Assert.Equal("Room 19 · Empujar tripulación amotinada", ScummCutsceneTitles.Title(GroupOf(planner, 19, 267, 0x06).First));
        Assert.Equal("Room 18 · Usar árbol", ScummCutsceneTitles.Title(GroupOf(planner, 18, 161, 0x08).First));
        Assert.StartsWith("¡Oye!  ¡No me gustan las visitas!", GroupOf(planner, 37, 0, 0).First.FirstText, StringComparison.Ordinal);
        if (Planner.Value is { } english)
        {
            static string Key(ScummCutsceneGroup g) => $"{g.First.Room}/{g.First.ObjectId}/{g.First.VerbId} {g.IsScene} {string.Join(";", g.First.Scripts)} " +
                                                        string.Join(",", g.Others.Select(o => $"{o.Room}/{o.ObjectId}/{o.VerbId}"));
            Assert.Equal(english.Triggers.Count, planner.Triggers.Count);
            Assert.Equal(english.Groups.Select(Key), planner.Groups.Select(Key));
        }
        var poster = planner.Run(GroupOf(planner, 33, 438, 0x09).First);
        Assert.True(poster.Begun, poster.Failure);
    }
}