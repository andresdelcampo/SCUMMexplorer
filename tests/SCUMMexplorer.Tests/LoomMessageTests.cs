using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// A reached <c>print</c> becomes a message (ScummVM <c>decodeParseString</c>,
/// <c>actorTalk</c>, <c>displayDialog</c>): its text, text slot position and
/// colour, and its talk delay, which the frames count down while
/// <c>VAR_HAVE_MSG</c> (<c>var[0x003]</c>) holds it on the screen.
/// </summary>
public sealed class LoomMessageTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));

    private static (GameLibrary Game, LoomExplorationSession Session)? Start()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return null;
        var game = new GameFolderScanner().Scan(Folder);
        return (game, new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL"))));
    }

    private static ushort? Var(LoomExplorationSession session, int number) =>
        session.Snapshot.KnownGlobalVariables.TryGetValue($"var[0x{number:X3}]", out var value) ? value : null;

    /// <summary>Frames until a talk message from <paramref name="source"/> shows (at most <paramref name="limit"/>); returns how many ran.</summary>
    private static int FramesUntilTalk(LoomExplorationSession session, string source, int limit)
    {
        var frames = 0;
        while (session.Messages.Talk?.Source != source && frames < limit)
        {
            session.AdvanceFrame();
            frames++;
        }
        return frames;
    }

    [Fact]
    public void LaterDrawnStringsDoNotEvictTheScoreAndRepeatedGlyphsReplaceTheirColour()
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        LoomPrintRequest Glyph(int x, int color) => new(0xFE,
            [new("position", x, 169), new("color", color)], [0x12], "Score", 0, 2, null);
        // More than the old 32-entry cap, at distinct screen locations.
        for (var i = 0; i < 40; i++) runtime.Messages.Print(Glyph(i * 8, 7), runtime);
        Assert.Equal(40, runtime.Messages.Drawn.Count);
        Assert.Contains(runtime.Messages.Drawn, message => message.X == 0 && message.Color == 7);
        // A note recolours the same glyph; retaining the old gray paint is unnecessary.
        runtime.Messages.Print(Glyph(0, 15), runtime);
        Assert.Equal(40, runtime.Messages.Drawn.Count);
        Assert.Equal(15, Assert.Single(runtime.Messages.Drawn, message => message.X == 0).Color);
        runtime.Messages.DrawBox(0, 169, 8, 177, 0);
        Assert.DoesNotContain(runtime.Messages.Drawn, message => message.X == 0);
        runtime.Messages.RoomEntered(runtime);
        Assert.Empty(runtime.Messages.Drawn);
    }

    [Fact]
    public void ADrawnBoxCoversTheNamesUnderIt()
    {
        // Script 008 +0x000E erases the close-up and name area (232,152)–(312,192) before
        // script 007 +0x001E prints the next object's name centred at (272, 181).
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        var table = runtime.Messages;
        LoomPrintRequest Name(string text) => new(0xFE,
            [new("center"), new("position", 0x110, 0xB5), new("color", 0x8F)],
            System.Text.Encoding.ASCII.GetBytes(text), "Script 07", 0x1E, 2, null);
        table.Print(Name("cielo"), runtime);
        table.Print(Name("arbol"), runtime);
        Assert.Equal(["cielo", "arbol"], table.Drawn.Select(message => message.Text));

        var trace = table.DrawBox(0xE8, 0x98, 0x138, 0xC0, 0);
        Assert.Empty(table.Drawn);
        Assert.Contains("2 drawn string(s) covered", trace, StringComparison.Ordinal);

        // A string the box only partly covers stays, with the box recorded over it.
        table.Print(Name("cielo"), runtime);
        table.DrawBox(0xE8, 0x98, 0x110, 0xC0, 0);
        var partly = Assert.Single(table.Drawn);
        Assert.Equal(new LoomScreenBox(0xE8, 0x98, 0x110, 0xC0, 0), Assert.Single(partly.ErasedBy));
    }

    [Fact]
    public void TheSkysDescriptionIsATalkMessageWithTheGamesDelay()
    {
        if (Start() is not { } started) return;
        var session = started.Session;
        Assert.Equal(2, session.Snapshot.RoomId);
        // Script 001 +0x0012 sets VAR_CHARINC to 5; +0x0017 sets VAR_TIMER_NEXT to 6.
        Assert.Equal((ushort?)5, Var(session, 0x025));
        Assert.Equal(6, session.FrameJiffies);
        // The opening cutscene's last print (script 044 +0x06AA, actor 1, an empty text)
        // is still on the screen after the boot; it set the slot's position and height.
        Assert.Equal("Script 44 +0x06AA", session.Messages.Talk?.Source);
        Assert.Equal("", session.Messages.Talk!.Text);

        // Room 02 object 156 verb 0x36 +0x0000: print 0x01, [center; text, "Amanecer."].
        session.ActivateObjectVerb(156, 0x36);
        var message = session.Messages.Talk;
        Assert.NotNull(message);
        Assert.Equal("Amanecer.", message.Text);
        Assert.Equal(LoomMessageKind.Talk, message.Kind);
        Assert.Equal(1, message.Actor);
        Assert.True(message.Center);
        Assert.Equal("Room 02 Object 156 Verb 0x36 +0x0000", message.Source);
        // displayDialog: 60 jiffies, plus VAR_CHARINC for each of the 9 characters.
        Assert.Equal(60 + 9 * 5, message.TalkDelay);
        // actorTalk sets VAR_HAVE_MSG to 0xFF; displayDialog leaves _haveMsg at 1 at the text's end.
        Assert.Equal((ushort?)0xFF, Var(session, 0x003));
        Assert.Equal(1, session.Messages.HaveMsg);
        Assert.Contains(message, session.Snapshot.ScreenMessages);

        // Each frame takes VAR_TIMER_NEXT (6) jiffies off before the scripts run, and
        // displayDialog ends the message (stopTalk) in the frame that reaches 0:
        // 105 jiffies stay on the screen for 17 frames and go in the 18th.
        for (var frame = 1; frame <= 17; frame++)
        {
            session.AdvanceFrame();
            Assert.Equal("Amanecer.", session.Messages.Talk?.Text);
        }
        session.AdvanceFrame();
        Assert.Null(session.Messages.Talk);
        Assert.Equal((ushort?)0, Var(session, 0x003));
        Assert.Empty(session.Snapshot.ScreenMessages);
    }

    [Fact]
    public void ADoubleClickOnTheSkyShowsItsDescription()
    {
        if (Start() is not { } started) return;
        var session = started.Session;
        // Room 02 object 156 (class 1) covers (700, 40). Script 006 walks the ego to it,
        // then script 007 runs verb 0x38 and, for the double click, verb 0x36.
        session.Click(700, 40, doubleClick: true);
        var frames = FramesUntilTalk(session, "Room 02 Object 156 Verb 0x36 +0x0000", 12);
        var message = session.Messages.Talk;
        Assert.NotNull(message);
        Assert.Equal("Amanecer.", message.Text);
        Assert.Equal("Room 02 Object 156 Verb 0x36 +0x0000", message.Source);
        Assert.True(frames < 12);
        Assert.Equal(2, session.Snapshot.RoomId);
    }

    [Fact]
    public void TheLeafsDescriptionFollowsAWalkAndACutscene()
    {
        if (Start() is not { } started) return;
        var session = started.Session;
        // Room 02 object 154 verb 0x36: a cutscene, actor 5 walks, 8 frames, the ego turns,
        // two more frames, then print 0x01, [center; position, 0x00A0, var[0x0A6]; text, "Ultima hoja del a\xA4o."].
        session.ActivateObjectVerb(154, 0x36);
        Assert.NotEqual("Room 02 Object 154 Verb 0x36 +0x0037", session.Messages.Talk?.Source);
        FramesUntilTalk(session, "Room 02 Object 154 Verb 0x36 +0x0037", 20);
        var message = session.Messages.Talk;
        Assert.NotNull(message);
        Assert.Equal("Ultima hoja del año.", message.Text);
        Assert.Equal(160, message.X);
        Assert.Equal(Var(session, 0x0A6), (ushort)message.Y);
        Assert.Equal(60 + 20 * 5, message.TalkDelay);
        Assert.Equal("Room 02 Object 154 Verb 0x36 +0x0037", message.Source);
    }

    [Fact]
    public void AWaitForMessageHoldsTheScriptUntilTheMessageEnds()
    {
        if (Start() is not { } started) return;
        var session = started.Session;
        // print 0xFF, [text, "Hi"]; waitFor message; var[0x0F0] = 1.
        session.RunTopLevelScript(new LoomScriptDisassembly(250, 1, 0, 32, 16,
            [
                new(0, 0x14, "print", "0xFF, [text, \"Hi\"]", "14 FF 0F 48 69 00"),
                new(6, 0xAE, "waitFor", "message", "AE 02"),
                new(8, 0x1A, "move", "var[0x0F0] = 0x0001", "1A F0 00 01 00"),
                new(13, 0, "stopObjectCode", "", "00")
            ],
            null, null, [], [], "Synthetic test script"));
        Assert.Equal("Hi", session.Messages.Talk?.Text);
        Assert.True(session.Messages.Talk!.IsSystemLine);
        Assert.Equal((ushort?)0, Var(session, 0x0F0));
        // 60 + 2 * 5 = 70 jiffies: the 12th frame of 6 jiffies reaches 0 and ends the
        // message before the scripts run, so the script goes on in that frame.
        var frames = 0;
        while (Var(session, 0x0F0) != 1 && frames < 40)
        {
            session.AdvanceFrame();
            frames++;
        }
        Assert.Equal(12, frames);
        Assert.Null(session.Messages.Talk);
    }
}
