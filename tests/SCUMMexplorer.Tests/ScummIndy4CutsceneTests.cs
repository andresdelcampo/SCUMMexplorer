using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S7 Cutscenes for Indiana Jones and the Fate of Atlantis (talkie CD, SCUMM v5; 18 October 2026): the engine's scan (<see cref="ScummCutsceneIndex"/>)
/// lists the game's cutscene triggers from its resource file. The play boot ends in the playable credits (Room 4, Script 78 as the input script, no verb on),
/// so the planner takes the verb names from the state after the game's own skips (<see cref="ScummCutscenePlanner.UntilAVerbIsOn"/>: two Esc, New York,
/// Script 19's full interface). Three scenes begin from a fresh start in a headless run of the generic session (<see cref="ScummCutsceneRun"/>): Smith's scene
/// (Room 1's entry, "I still think it's a fake."), Sophia's lecture in New York (Room 11's entry, "Here, my friends, is ATLANTIS", which goes on backstage to
/// Room 12) and, later in the game, the diving suit (Room 82's entry, "Oh boy… I've got maybe three minutes of air in my suit", which goes on to Room 42).
/// The scan's first line is the scene's first spoken line: its text follows the voice's code 10, whose operand bytes are not text. Registry: Cutscenes ◐.
/// Record: <c>.verification/indy4-cutscenes-20261018/README.md</c>. Skips when the disc image is absent.
/// </summary>
public sealed class ScummIndy4CutsceneTests(ITestOutputHelper output)
{
    private static string? Folder()
    {
        var path = Path.Combine(TestGames.Root, "indy4 vga en");
        return File.Exists(Path.Combine(path, "ATLANTIS.cue")) ? path : null;
    }

    private static readonly Lazy<ScummCutscenePlanner?> Planner =
        new(() => Folder() is { } folder ? new ScummCutscenePlanner(new GameFolderScanner().Scan(folder)) : null);

    private static ScummCutsceneGroup GroupOf(ScummCutscenePlanner planner, int room, int objectId, int verb) =>
        Assert.Single(planner.Groups, g => g.First.Room == room && (g.First.ObjectId == objectId && g.First.VerbId == verb ||
                                                                   g.Others.Any(o => o.ObjectId == objectId && o.VerbId == verb)));

    private static string Clean(string text) => text.Replace("\\x10", " ").Replace("\x10", " ");

    [Fact]
    public void TheScanTitlesWithTheVerbNamesTheGameShowsAfterTheCredits()
    {
        if (Planner.Value is not { } planner) return;
        output.WriteLine($"{planner.Triggers.Count} triggers, {planner.Groups.Count} groups, {planner.Groups.Count(g => g.IsScene)} scenes; {planner.VerbSource}");
        // The boot shows no verb (the credits); two of the game's own skips (Room 4 local 221, Room 5 local 214's overrides) reach New York, where Script 19
        // with 2 has named the verbs.
        Assert.StartsWith("the boot (Room 04) shows no verb; the verb names are those of Room 10 after 2 of the game's own skips", planner.VerbSource, StringComparison.Ordinal);
        Assert.Equal(["Open", "Close", "Give", "Push", "Pull", "Use", "Look at", "Walk to", "Pick up", "Talk to"],
            Enumerable.Range(3, 10).Select(verb => planner.Verbs.WordFor(verb)).ToArray());
        Assert.True(planner.Groups.Count >= 400, $"only {planner.Groups.Count} groups");

        Assert.Equal("Room 10 · Use fire escape", ScummCutsceneTitles.Title(GroupOf(planner, 10, 138, 0x08).First));
        Assert.Equal("Room 10 · Look at marquee", ScummCutsceneTitles.Title(GroupOf(planner, 10, 142, 0x09).First));
        Assert.Equal("Room 10 · Open back door", ScummCutsceneTitles.Title(GroupOf(planner, 10, 136, 0x03).First));
        // Only the verb the game never names keeps its number.
        Assert.All(planner.Groups.Select(g => ScummCutsceneTitles.Title(g.First)).Where(title => title.Contains(" verb 0x", StringComparison.Ordinal)),
            title => Assert.Contains(" verb 0x50 ", title, StringComparison.Ordinal));

        // The first lines are spoken (code 10 and its operands before the text) and still read as text.
        Assert.Equal("I still think it's a fake.", GroupOf(planner, 1, 0, 0).First.FirstText);
        Assert.StartsWith("Here, my friends, is ATLANTIS", GroupOf(planner, 11, 0, 0).First.FirstText, StringComparison.Ordinal);
        Assert.StartsWith("Oh boy", GroupOf(planner, 82, 0, 0).First.FirstText, StringComparison.Ordinal);
    }

    [Fact]
    public void ThreeScenesBeginFromAFreshStart()
    {
        if (Planner.Value is not { } planner) return;
        foreach (var (room, objectId, verb) in new[] { (1, 0, 0), (11, 0, 0), (82, 0, 0) })
        {
            var group = GroupOf(planner, room, objectId, verb);
            Assert.True(group.IsScene);
            var run = planner.Run(group.First);
            output.WriteLine($"{ScummCutsceneTitles.Title(group.First)}: begun {run.Begun}, {run.Frames} frames, rooms [{string.Join(", ", run.RoomsEntered)}] {run.Failure}");
            Assert.True(run.Begun, $"Room {room} object {objectId} verb 0x{verb:X2}: {run.Failure}");
            Assert.StartsWith("A fresh start begins its cutscene", planner.StartNote(group.First), StringComparison.Ordinal);
        }
        // The lecture goes on backstage (Room 12); the diving suit goes on to Room 42.
        Assert.Contains(12, planner.Run(GroupOf(planner, 11, 0, 0).First).RoomsEntered);
        Assert.Contains(42, planner.Run(GroupOf(planner, 82, 0, 0).First).RoomsEntered);
    }

    /// <summary>
    /// S9 (18 October 2026): the Spanish talkie folder and the Spanish floppy list the same 666 triggers, 426 entries and 182 scenes as the English disc,
    /// titled with their own verb and object names and first lines (CP437 accents and inverted marks), and the three acceptance scenes begin from a fresh
    /// start in each. The floppy's list differs from the disc's in three entries' data (Room 2's entry chains to local 228, not 227; Room 4's to 220, not
    /// 216; Room 72's object is 985, not 984). Record: <c>.verification/indy4-editions-20261018/README.md</c>.
    /// </summary>
    [Theory]
    [InlineData("indy4 talkie vga es")]
    [InlineData("indy4 classic vga es")]
    public void TheSpanishCopiesListTheSameScenesWithTheirOwnTexts(string copy)
    {
        if (ScummIndy4PlayTests.Folder(copy) is not { } folder) return;
        var planner = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder));
        output.WriteLine($"{planner.Triggers.Count} triggers, {planner.Groups.Count} groups, {planner.Groups.Count(g => g.IsScene)} scenes; {planner.VerbSource}");

        Assert.Equal((666, 426, 182), (planner.Triggers.Count, planner.Groups.Count, planner.Groups.Count(g => g.IsScene)));
        Assert.Equal(["Abrir", "Cerrar", "Dar", "Empujar", "Tirar", "Usar", "Mirar", "Ir a", "Coger", "Hablar"],
            Enumerable.Range(3, 10).Select(verb => planner.Verbs.WordFor(verb)).ToArray());
        Assert.Equal("Room 10 · Usar salida de incendios", ScummCutsceneTitles.Title(GroupOf(planner, 10, 138, 0x08).First));
        Assert.Equal("Room 10 · Mirar luminoso", ScummCutsceneTitles.Title(GroupOf(planner, 10, 142, 0x09).First));
        Assert.Equal("Room 10 · Abrir puerta trasera", ScummCutsceneTitles.Title(GroupOf(planner, 10, 136, 0x03).First));
        Assert.Equal("Sigo pensando que es falsa.", GroupOf(planner, 1, 0, 0).First.FirstText);
        Assert.StartsWith("Amigos, aquí está la ATLANTIDA", GroupOf(planner, 11, 0, 0).First.FirstText, StringComparison.Ordinal);
        Assert.StartsWith("¿Qué quieres, tío?", GroupOf(planner, 10, 136, 0x03).First.FirstText, StringComparison.Ordinal);

        foreach (var (room, objectId, verb) in new[] { (1, 0, 0), (11, 0, 0), (82, 0, 0) })
        {
            var run = planner.Run(GroupOf(planner, room, objectId, verb).First);
            Assert.True(run.Begun, $"Room {room}: {run.Failure}");
        }
        Assert.Contains(12, planner.Run(GroupOf(planner, 11, 0, 0).First).RoomsEntered);
        Assert.Contains(42, planner.Run(GroupOf(planner, 82, 0, 0).First).RoomsEntered);
    }

    [Fact]
    public void TheLecturePlaysWithItsSpokenLines()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameFamily.Indy4, GameSupport.FamilyOf(game));
        Assert.NotNull(game.Speech);
        // As the window plays it from the list: the play session (the boot), Room 11 entered for the scene.
        var session = ScummGameSession.BootForPlay(game, out _);
        var speech = session.Messages.SpeechEvents.Count;
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == 11));
        var frames = 0;
        while (frames < 600 && !session.Messages.OnScreen.Any(m => Clean(m.Text).Contains("Here, my friends, is ATLANTIS", StringComparison.Ordinal)))
        {
            session.AdvanceFrame();
            frames++;
        }
        Assert.True(frames < 600, "the lecture's first line never showed");
        Assert.True(session.InCutscene);
        var lines = session.Messages.SpeechEvents.Skip(speech).Where(item => !item.IsStop).ToList();
        foreach (var line in lines) output.WriteLine($"speech 0x{line.Offset:X8} {line.Length} {line.DurationJiffies} jiffies");
        // The line carries its voice from MONSTER.SOU, and after the boot it lasts as long as the sample.
        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.True(line.DurationJiffies > 0));
    }
}
