using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S6 Play for Indiana Jones and the Fate of Atlantis (talkie CD, SCUMM v5; 17 October 2026; REQUIREMENTS Â§7.1, R-7.7 to R-7.18): the acceptance route of
/// R-7.18, played headless through the game's own interface. Record: <c>.verification/indy4-play-20261017/README.md</c>. Skips when the disc image is absent.
/// </summary>
public sealed class ScummIndy4PlayTests(ITestOutputHelper output)
{
    private static string? Folder()
    {
        var path = Path.Combine(TestGames.Root, "indy4 vga en");
        return File.Exists(Path.Combine(path, "ATLANTIS.cue")) ? path : null;
    }

    // Script 19 (local[0] 2) and the boot's verbOps: the verbs of the full interface.
    private const int Open = 0x03, Give = 0x05, Use = 0x08, LookAt = 0x09, WalkTo = 0x0A, PickUp = 0x0B, TalkTo = 0x0C, Sentence = 0x64;

    private sealed class Player(ScummGameSession session, ITestOutputHelper output)
    {
        public ScummGameSession Session { get; } = session;
        public int Frames { get; private set; }

        public void Run(int frames)
        {
            for (var i = 0; i < frames; i++) { Session.AdvanceFrame(); Frames++; }
        }

        public bool RunUntil(Func<bool> condition, int limit = 1500)
        {
            for (var i = 0; i < limit; i++)
            {
                if (condition()) return true;
                Run(1);
            }
            if (condition()) return true;
            foreach (var line in Session.SessionTrace.Where(l => !l.Contains("delay") && !l.Contains("waits") && !l.Contains("getRandomNr")).TakeLast(40)) output.WriteLine("trace " + line);
            return false;
        }

        public bool Idle => Session.UserInputOn && !Session.InCutscene;

        public string Sentence => Glyphs(Indy4Sentence);
        private const int Indy4Sentence = 0x64;

        // The verb's glyphs as drawString shows them: an escape and its code draw nothing, codes 9, 10, 12, 13 and 14 skip their two operand bytes.
        public string Glyphs(int verbId) => Session.Verbs.FirstOrDefault(item => item.VerbId == verbId) is { } verb
            ? ScummTextDecoder.DecodeGlyphs(Shown(Session.VerbGlyphs(verb))).Replace("\x7F", "").Replace("\\x10", " ").Replace("\x10", " ").Trim() : "";

        private static List<byte> Shown(IReadOnlyList<byte> glyphs)
        {
            var shown = new List<byte>();
            for (var i = 0; i < glyphs.Count; i++)
            {
                if (glyphs[i] is 0xFF or 0xFE) { if (++i < glyphs.Count && glyphs[i] is 9 or 10 or 12 or 13 or 14) i += 2; continue; }
                shown.Add(glyphs[i]);
            }
            return shown;
        }

        public ushort Var(string name) => Session.Snapshot.KnownGlobalVariables.GetValueOrDefault(name);

        public void ClickVerb(int verbId)
        {
            var item = Session.PlayVerbArea().Items.Single(entry => entry.VerbId == verbId);
            var input = Session.ClickScreen(item.X + 1, item.Y + 1, out var refused);
            Assert.True(input is not null, refused);
            Assert.Equal((ScummSession.VerbClickArea, verbId, 1), (input!.Area, input.Value, input.Mode));
            output.WriteLine($"frame {Frames}: {input.Description}");
            Run(2);
        }

        public int ScreenX(int roomX)
        {
            var screenX = roomX - Session.ViewLeft;
            Assert.InRange(screenX, 0, 319);
            return screenX;
        }

        public void Hover(int roomX, int roomY)
        {
            Session.PointAtScreen((ScreenX(roomX), roomY + Session.MainScreen.Top));
            Run(2);
        }

        public void ClickRoom(int roomX, int roomY)
        {
            var screenX = ScreenX(roomX);
            Session.PointAtScreen((screenX, roomY + Session.MainScreen.Top));
            Run(1);
            var input = Session.ClickScreen(screenX, roomY + Session.MainScreen.Top, out var refused);
            Assert.True(input is not null, refused);
            Assert.Equal((ScummSession.SceneClickArea, 1), (input!.Area, input.Mode));
            output.WriteLine($"frame {Frames}: {input.Description}");
            Run(2);
        }

        public void BringIntoView(int roomX, int roomY)
        {
            for (var tries = 0; tries < 8 && (roomX - Session.ViewLeft is < 16 or > 303); tries++)
            {
                var target = roomX > Session.ViewLeft + 160 ? Session.ViewLeft + 310 : Session.ViewLeft + 10;
                ClickRoom(target, roomY);
                RunUntil(() => !Session.EgoMoving && !Session.CameraMoving, 600);
                Log($"walked towards x {target}");
            }
            Assert.InRange(roomX - Session.ViewLeft, 16, 303);
        }

        public void Log(string what) => output.WriteLine($"frame {Frames} [{what}] room {Session.CurrentRoomId}, view {Session.ViewLeft}, input script {Var(ScummVars.VerbScript)}, " +
                                                         $"sentence \"{Sentence}\", inventory [{string.Join(", ", Session.Snapshot.Inventory)}], cutscene {Session.InCutscene}, input {Session.UserInputOn}, " +
                                                         $"messages [{string.Join(" | ", Session.Messages.OnScreen.Select(message => message.Text))}]");
    }

    private static string Clean(string text) => text.Replace("\\x10", " ").Replace("\x10", " ");

    /// <summary>
    /// The route of R-7.18 from Barnett College. (1) The prologue: the boot ends in Room 4 with input script 78 (Room 4 local 206 +0x019D) and no verbs; a click on
    /// the peculiar statue (object 63) runs Script 78, which runs the object's verb 0x0A, and Indy falls into Room 5. (2) Skip: Esc in Room 5 takes the override of
    /// Room 5 local 214 (+0x000D beginOverride, +0x001D): the credits sequence is skipped, Script 4 becomes the input script (+0x008B) and the game goes on through
    /// Room 1 (Smith) to New York, Room 10. (3) Compose a sentence: "Look at" the marquee (object 142). (4) Take an object: "Pick up" the newspaper (141); Script 10
    /// gives an inventory slot its icon from Room 98. (5) A dialogue: "Talk to" the ticket taker (143); Room 10 local 208 loads the lines as strings 0x29 up and turns
    /// on verbs 0x78 up, input script 17; the route picks 0x78, which Indy says with its spoken line. (6) Leave the room: push the crates (145, 137, 139), "Use" the
    /// fire escape (138); Room 10 local 207 is a cutscene with an override: Esc skips the climb and Room 12 follows.
    /// </summary>
    [Fact]
    public void TheAcceptanceRouteFromBarnettCollegeRunsThroughTheGamesOwnInterface()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.True(ScummProfile.For(game).PlaysThroughVerbs);
        Assert.True(ScummProfile.For(game).RightClickReachesGame);
        var play = new Player(ScummGameSession.BootForPlay(game, out var objects), output);
        Assert.NotEmpty(objects.States);
        var session = play.Session;
        Assert.Equal(4, session.CurrentRoomId);
        Assert.True(play.Idle);
        // The prologue screen: the whole screen is the room (Room 4 entry +0x0032 setScreen 0, 200) and Script 78 is the input script; no verb is on.
        Assert.Equal((0, 200), session.MainScreen);
        Assert.Equal(78, play.Var(ScummVars.VerbScript));
        Assert.DoesNotContain(session.Verbs, verb => verb.Mode == 1);
        // Marcus' first line (Room 4 local 206 +0x011C) carries its spoken line: offset 0x0507BB6E in MONSTER.SOU. The boot keeps the text's timing (no length).
        var marcus = session.Messages.SpeechEvents.First();
        Assert.Equal((0x0507BB6E, 14, false, (int?)null), (marcus.Offset, marcus.Length, marcus.IsStop, marcus.DurationJiffies));

        // (1) The prologue's click: Script 79 names the object under the pointer, Script 78 +0x0061 runs object 63's verb 0x0A, and Indy goes on to Room 5.
        play.Hover(80, 92);
        Assert.Contains(session.Messages.OnScreen, message => message.Text.Contains("peculiar statue", StringComparison.Ordinal));
        play.ClickRoom(80, 92);
        Assert.True(play.RunUntil(() => session.CurrentRoomId == 5 && play.Idle, 1500), "the statue leads to Room 5");
        Assert.Contains(session.SessionTrace, line => line.StartsWith("Script 78 +0x0061: startObject runs object 63 verb 0x0A", StringComparison.Ordinal));
        play.Log("room 5");
        // Room 5 is entered once: Script 21's actorFollowCamera (run nested from endCutscene at Room 4 local 200 +0x0092) starts the scene; the caller's own follow
        // at +0x0093 then finds Indy in the current room (setCameraFollows starts a scene only for an actor elsewhere).
        Assert.Single(session.SessionTrace, line => line == "Room 05 entry script chains to script 207");

        // (2) Skip: Esc takes Room 5 local 214's override; the full interface (Script 19 with 2) comes up in Room 10.
        Assert.True(session.CutsceneSkippable);
        session.PressKey(0x1B);
        Assert.Contains(session.SessionTrace, line => line.StartsWith("abortCutscene: Room 05 local script 214 continues at its skip jump +0x000F", StringComparison.Ordinal));
        Assert.True(play.RunUntil(() => play.Idle && session.CurrentRoomId == 10 && session.Verbs.Any(v => v.VerbId == Open && v.Mode == 1), 3000), "New York");
        play.Log("new york");
        Assert.Equal(4, play.Var(ScummVars.VerbScript));
        Assert.Equal((0, 144), session.MainScreen);
        Assert.Equal(["Give", "Open", "Close", "Pick up", "Talk to", "Look at", "Use", "Push", "Pull"],
            new[] { Give, Open, 4, PickUp, TalkTo, LookAt, Use, 6, 7 }.Select(play.Glyphs).ToArray());
        Assert.Equal("Walk", play.Sentence);

        // (3) Compose and run a sentence: "Look at" + the marquee (object 142); sentence Script 2 runs verb 9.
        play.ClickVerb(LookAt);
        Assert.Equal("Look at", play.Sentence);
        play.Hover(116, 16);
        Assert.Equal("Look at marquee", play.Sentence);
        play.ClickRoom(116, 16);
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(m => m.Actor == 1 && Clean(m.Text).StartsWith("Imagine the suckers", StringComparison.Ordinal)), 900), "Indy reads the marquee");
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x09 with objects 142", StringComparison.Ordinal));
        Assert.True(play.RunUntil(() => play.Idle && session.Messages.Talk is null, 1500));

        // (4) Take an object: "Pick up" the newspaper (141). Script 10 gives the second inventory slot (verb 0x66; 0x65 holds the magazine, object 23) its icon.
        play.ClickVerb(PickUp);
        play.Hover(168, 80);
        Assert.Equal("Pick up newspaper", play.Sentence);
        play.ClickRoom(168, 80);
        Assert.True(play.RunUntil(() => session.Snapshot.Inventory.Contains(141), 1500), "the newspaper reaches the inventory");
        play.Run(5);
        play.Log("newspaper taken");
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x0B with objects 141", StringComparison.Ordinal));
        var slot = session.Verbs.Single(verb => verb.VerbId == 0x66);
        Assert.Equal((1, 98), (slot.Mode, slot.ImageRoom));
        Assert.Contains(session.PlayVerbArea().Items, item => item.VerbId == 0x66 && !item.IsText && item.Width > 0);
        Assert.True(play.RunUntil(() => play.Idle && session.Messages.Talk is null, 1500));

        // (5) A dialogue: "Talk to" the ticket taker (143). Room 10 local 208 +0x0113 up loads the lines (strings 0x29 up, each with its speech codes) and turns on
        // verbs 0x78 up, whose names are code 7 of those strings; Script 17 is the input script.
        play.ClickVerb(TalkTo);
        play.Hover(116, 48);
        Assert.Equal("Talk to ticket taker", play.Sentence);
        play.ClickRoom(116, 48);
        Assert.True(play.RunUntil(() => play.Var(ScummVars.VerbScript) == 17 && session.UserInputOn && session.Verbs.Any(v => v.VerbId == 0x78 && v.Mode == 1), 3000), "the choices");
        play.Log("choices");
        Assert.Equal(["But--", "Now wait--", "Hold on--", "Um--"], new[] { 0x78, 0x79, 0x7A, 0x7B }.Select(id => Clean(play.Glyphs(id))).ToArray());
        Assert.Equal(4, session.PlayVerbArea().Items.Count(item => item.VerbId is >= 0x78 and <= 0x7B && item.IsText && item.Width > 0 && item.Y >= 144));
        var spoken = session.Messages.SpeechEvents.Count;
        play.ClickVerb(0x78);
        // Script 17 +0x00D9 prints the chosen verb's name (code 5): Indy says the line, and its speech codes start the spoken line (0x040B3E4B).
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(m => m.Actor == 1 && m.Text.EndsWith("But--", StringComparison.Ordinal)), 300), "Indy says the choice");
        Assert.Contains(session.Messages.SpeechEvents.Skip(spoken), e => e is { Offset: 0x040B3E4B, IsStop: false, DurationJiffies: > 0 });
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(m => m.Actor != 1 && Clean(m.Text).StartsWith("No seats", StringComparison.Ordinal)), 900), "the ticket taker answers");
        Assert.True(play.RunUntil(() => play.Idle && play.Var(ScummVars.VerbScript) == 4, 1500), "the conversation ends");

        // (6) Leave the room: the crates (Room 10 objects 145, then 137 and 139, verb 6) clear the fire escape's class 6 (object 139 verb 6 +0x0118), and "Use" on
        // the fire escape (138) chains to Room 10 local 207: a cutscene with an override. Esc skips the climb; loadRoomWithEgo (+0x008A) enters Room 12.
        foreach (var (x, y) in new[] { (496, 108), (560, 92), (560, 92) })
        {
            play.BringIntoView(x, y);
            play.ClickVerb(6);
            play.Hover(x, y);
            Assert.Equal("Push crate", play.Sentence);
            play.ClickRoom(x, y);
            Assert.True(play.RunUntil(() => play.Idle && !session.EgoMoving && session.Messages.Talk is null, 900));
            play.Run(10);
        }
        Assert.Equal(0, session.Snapshot.ObjectStates.GetValueOrDefault(139));
        play.BringIntoView(536, 32);
        play.ClickVerb(Use);
        play.Hover(536, 32);
        Assert.Equal("Use fire escape", play.Sentence);
        play.ClickRoom(536, 32);
        Assert.True(play.RunUntil(() => session.InCutscene && session.CutsceneSkippable, 900), "the climb is a cutscene with an override");
        play.Log("climbing");
        session.PressKey(0x1B);
        Assert.Contains(session.SessionTrace, line => line.StartsWith("abortCutscene: Room 10 local script 207", StringComparison.Ordinal));
        Assert.True(play.RunUntil(() => session.CurrentRoomId == 12 && play.Idle, 1500), "the fire escape leads to Room 12");
        play.Log("backstage");

        foreach (var input in session.DeliveredInput) output.WriteLine($"input ({input.Area}, {input.Value}, {input.Mode}) {input.Description}");
        // Every input was the player's: 18 clicks with mode 1 and the two Esc presses (the skips); no verb, object or line was chosen by the app.
        Assert.Equal(18, session.DeliveredInput.Count);
        Assert.All(session.DeliveredInput, input => Assert.Equal(1, input.Mode));
        foreach (var e in session.Messages.SpeechEvents) output.WriteLine($"speech {e}");

        // The play screen composes the room with the verb screen below row 144.
        var screen = new ScummPlayScreen(game, session, objects.States).Compose(0);
        Assert.Equal(12, screen.RoomId);
    }

    [Fact]
    public void TheMainMenuKeyIsNotDeliveredAndAVerbsKeyIsItsClick()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        session.PressKey(0x1B);
        for (var i = 0; i < 3000 && !(session.CurrentRoomId == 10 && session.UserInputOn && !session.InCutscene && session.Verbs.Any(v => v.VerbId == Open && v.Mode == 1)); i++)
            session.AdvanceFrame();
        Assert.Equal(10, session.CurrentRoomId);
        var menu = session.Snapshot.KnownGlobalVariables[ScummVars.MainMenuKey];
        output.WriteLine($"VAR_MAINMENU_KEY = {menu}");
        if (menu != 0) Assert.Null(session.PressPlayKey(menu, out _));
        // Script 19's verbs carry their keys ('l' for Look at, verb 9).
        var keyed = session.Verbs.Single(verb => verb.VerbId == LookAt);
        Assert.Equal('l', keyed.Key);
        var input = session.PressPlayKey(keyed.Key, out _);
        Assert.Equal((ScummSession.VerbClickArea, LookAt, 1), (input!.Area, input.Value, input.Mode));
    }

    [Fact]
    public void ATalkiesLineLastsAsLongAsItsSpokenSample()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        // Marcus' line (Room 4 local 206 +0x011C) is sample 0x0507BB6E: its voice file's samples over its rate give 215 jiffies.
        var wave = game.Speech!.ReadWave(0x0507BB6E, 14);
        Assert.Equal(215, (int)Math.Ceiling((wave.Length - 44) / 2.0 * 60 / BitConverter.ToInt32(wave, 24)));
        var session = ScummGameSession.BootForPlay(game, out _);
        Assert.Contains(session.Messages.Log, message => message.Text.Contains("Alright, Jones", StringComparison.Ordinal));
        Assert.NotNull(session.Messages.SpeechJiffies);
        // After the boot, a line waits for its sample (processSfxQueues: finished && _talkDelay == 0). Skip the prologue: Smith's scene in Room 1 speaks.
        session.PressKey(0x1B);
        var waited = false;
        for (var i = 0; i < 1500 && !waited; i++)
        {
            session.AdvanceFrame();
            if (session.Messages.Talk is { } talk && session.Messages.TalkDelay == 0 && session.Messages.SpeechPlaying) waited = true;
        }
        Assert.True(waited, "a line stays on screen after its text delay while its sample plays");
        var line = session.Messages.SpeechEvents.Last(e => !e.IsStop && e.DurationJiffies is not null);
        var sample = game.Speech.ReadWave(line.Offset, line.Length);
        Assert.Equal((int)Math.Ceiling((sample.Length - 44) / 2.0 * 60 / BitConverter.ToInt32(sample, 24)), line.DurationJiffies);
    }
}