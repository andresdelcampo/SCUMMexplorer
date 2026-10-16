using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S6 Play for The Secret of Monkey Island, four-disk 256-colour floppy (SCUMM v4; 16 October 2026; REQUIREMENTS §7.1, Proposed R-7.7 to R-7.18): the
/// acceptance route of R-7.18 from the dock (Room 33), played headless through the game's own interface. Clicks and keys go to the input script the game
/// holds in <c>VAR_VERB_SCRIPT</c> (Script 4 in play, Script 14 in a dialogue) with the version 4 click mode (1), sentences run only through the game's
/// sentence script (Script 2), the verb area, sentence line (Script 12) and inventory (Script 9) are the game's text verbs drawn with its v4 charsets.
/// Record: <c>.verification/monkey1-play-20261016/README.md</c>. Skips when the game folder is absent.
/// </summary>
public sealed class ScummMonkeyFloppyPlayTests(ITestOutputHelper output)
{
    private static string? Folder()
    {
        var path = Path.Combine(TestGames.Root, "monkey1 ega en");
        return File.Exists(Path.Combine(path, "DISK01.LEC")) ? path : null;
    }

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

        public string Sentence => Glyphs(0x64);

        public string Glyphs(int verbId) => Session.Verbs.FirstOrDefault(item => item.VerbId == verbId) is { } verb
            ? ScummTextDecoder.DecodeGlyphs(Session.VerbGlyphs(verb)).Trim() : "";

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

        private int ScreenX(int roomX)
        {
            var screenX = roomX - Session.ViewLeft;
            Assert.InRange(screenX, 0, 319);
            return screenX;
        }

        /// <summary>Moves the pointer over a room point (the camera's view) and plays two frames: Script 23 finds the object under it.</summary>
        public void Hover(int roomX, int roomY)
        {
            Session.PointAtScreen((ScreenX(roomX), roomY + Session.MainScreen.Top));
            Run(2);
        }

        /// <summary>Points at a room point, lets the interface loop see it for a frame, then clicks there.</summary>
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

        /// <summary>Walks the ego (a click in the room with "Walk to") towards a room x until the camera shows it.</summary>
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

    private static ScummGameSession Boot(string folder, out GameLibrary game)
    {
        game = new GameFolderScanner().Scan(folder);
        var data = (ScummV4DiskSetDataSource)ScummDataSources.For(game);
        return new ScummGameSession(game, data.ReadIndexedObjects(), 0, null, data);
    }

    [Fact]
    public void TheAcceptanceRouteFromTheDockRunsThroughTheGamesOwnInterface()
    {
        if (Folder() is not { } folder) return;
        var play = new Player(Boot(folder, out var game), output);
        var session = play.Session;
        Assert.True(ScummProfile.For(game).PlaysThroughVerbs);
        // Room 96 local script 200 +0x0020 brings Guybrush in at the cliffside (object 435) walking to (346, 133): the player has control while he walks.
        Assert.True(play.RunUntil(() => !session.EgoMoving, 600), "Guybrush walks in from the cliffside");
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal((33, 346, 133), (ego.Room, ego.X, ego.Y));
        play.Log("after the boot");
        // The verb screen is the game's: Script 22's twelve verbs and the sentence line (verb 0x64, Script 12) drawn as text with the game's v4 charset
        // (CHAR blocks of the disks); the room screen is rows 0 to 143; the input script is Script 4; the cursor and user input are on, and from version 4
        // cursorCommand keeps them in VAR_CURSORSTATE/VAR_USERPUT, which Script 23 reads (+0x0000).
        Assert.Equal((0, 144), session.MainScreen);
        Assert.True(session.CursorShown && session.UserInputOn);
        Assert.Equal((1, 1), (play.Var(ScummVars.CursorState), play.Var(ScummVars.UserPut)));
        var area = session.PlayVerbArea();
        foreach (var item in area.Items) output.WriteLine($"verb 0x{item.VerbId:X2} mode {item.Mode} at ({item.X},{item.Y}) {item.Width}x{item.Height} \"{play.Glyphs(item.VerbId)}\"");
        Assert.Equal(12, area.Items.Count(item => item.IsText && item.Mode == 1 && item.VerbId is >= 0x01 and <= 0x0D and not 0x0C));
        Assert.Equal(["Open", "Close", "Push", "Pull", "Walk to", "Pick up", "Talk to", "Give", "Use", "Look at", "Turn on", "Turn off"],
            new[] { 1, 2, 6, 7, 0x0A, 0x0B, 0x0D, 3, 8, 9, 4, 5 }.Select(play.Glyphs).ToArray());
        Assert.Equal("Walk to", play.Sentence);
        Assert.Equal(4, play.Var(ScummVars.VerbScript));
        Assert.All(area.Items.Where(item => item.IsText), item => Assert.True(item.Width > 0 && item.Height == 8));
        Assert.Contains(area.Pixels, pixel => pixel != 0);

        // (1) Compose a sentence: "Look at" (verb 9), then the pointer over the poster (object 438): Script 23 finds it and Script 12 names the line.
        play.ClickVerb(0x09);
        Assert.Equal("Look at", play.Sentence);
        play.Hover(268, 104);
        play.Log("pointer on the poster");
        Assert.Equal("Look at poster", play.Sentence);
        play.ClickRoom(268, 104);
        // Script 4 (area 2, mode 1): no second object needed, so the click runs doSentence (+0x044B); the sentence script walks Guybrush to the poster.
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x09 with objects 438", StringComparison.Ordinal));

        // (5) The poster's first look is a cutscene with an override (Room 33 object 438 verb 0x09 +0x00CF..+0x0134): Esc skips it.
        Assert.True(play.RunUntil(() => session.InCutscene && session.CutsceneSkippable, 900), "the poster's cutscene reaches its override");
        play.Log("poster cutscene");
        Assert.Contains(session.Messages.OnScreen, message => message.Text.StartsWith("Re-elect Governor Marley.", StringComparison.Ordinal));
        session.PressKey(0x1B);
        Assert.True(play.RunUntil(() => play.Idle, 600), "the skipped cutscene ends");
        play.Log("cutscene skipped");
        Assert.DoesNotContain(session.Messages.OnScreen, message => message.Text.Contains("only one candidate", StringComparison.Ordinal));

        // (2) Leave the room: "Open" the door (object 437), then a click on it (Walk to): loadRoomWithEgo 0x142, 0x1C (Room 28).
        play.BringIntoView(716, 108);
        play.ClickVerb(0x01);
        play.ClickRoom(716, 108);
        Assert.True(play.RunUntil(() => session.Snapshot.ObjectStates.GetValueOrDefault(437) == 1 && play.Idle, 900), "the door opens");
        play.Log("door open");
        play.ClickRoom(716, 108);
        Assert.True(play.RunUntil(() => session.CurrentRoomId != 33, 900), "the door leads out");
        play.Log("left the dock");
        Assert.Equal(28, session.CurrentRoomId);
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x0A with objects 437", StringComparison.Ordinal));
        Assert.True(play.RunUntil(() => play.Idle, 1500));
        play.Log("in the next room");

        // (4) A dialogue: "Talk to" the important-looking pirates (object 332).
        play.BringIntoView(428, 104);
        play.ClickVerb(0x0D);
        play.Hover(428, 104);
        play.Log("pointer on the pirates");
        Assert.Equal("Talk to important-looking pirates", play.Sentence);
        play.ClickRoom(428, 104);
        Assert.True(play.RunUntil(() => session.Verbs.Any(verb => verb.VerbId == 0x78 && verb.Mode == 1) && session.UserInputOn, 3000), "the dialogue offers its choices");
        play.Log("choices");
        output.WriteLine("running: " + string.Join(" ; ", session.PendingScriptSummary));
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x0D with objects 332", StringComparison.Ordinal));
        // The conversation installs Script 14 as the input script and offers its lines as verbs 0x78 up, drawn with charset 1 (Script 17 case 5).
        Assert.Equal(["I want to be a fireman.", "I mean to kill you all!", "I want to be a pirate."], new[] { 0x78, 0x79, 0x7A }.Select(play.Glyphs).ToArray());
        Assert.Equal(3, session.PlayVerbArea().Items.Count(item => item.VerbId is >= 0x78 and <= 0x7A && item.IsText && item.Width > 0));
        Assert.Equal(14, play.Var(ScummVars.VerbScript));
        Assert.Equal("I want to be a pirate.", play.Glyphs(0x7A));
        play.ClickVerb(0x7A);
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(message => message.Actor == 1 && message.Text.Length > 1), 300), "the ego says the choice");
        play.Log("the ego's line");
        Assert.True(play.RunUntil(() => play.Var("var[0x0C1]") == 0x7A, 300), "the choice reaches var[0x0C1]");
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(message => message.Actor != 1 && message.Text.Length > 1), 900), "the pirates answer");
        play.Log("the pirates answer");
        var said = new List<string>();
        Assert.True(play.RunUntil(() =>
        {
            foreach (var message in session.Messages.OnScreen) if (said.Count == 0 || said[^1] != message.Text) said.Add(message.Text);
            return play.Idle && play.Var(ScummVars.VerbScript) != 14 || session.Verbs.Any(verb => verb.VerbId == 0x78 && verb.Mode == 1) && session.UserInputOn && play.Var(ScummVars.VerbScript) == 14 && said.Count > 3;
        }, 6000), "the conversation goes on");
        foreach (var line in said) output.WriteLine("said: " + line);
        play.Log("after the answer");
        Assert.Equal("I'll just be running along now.", play.Glyphs(0x7F));
        play.ClickVerb(0x7F);
        Assert.True(play.RunUntil(() => play.Idle && play.Var(ScummVars.VerbScript) != 14, 1500), "the conversation ends");
        play.Log("conversation over");

        // Out of the bar: "Open" the door (object 322), then "Walk to" it (Room 28 object 322 verb 0x0A: loadRoomWithEgo 0x1B5, 0x21).
        play.BringIntoView(52, 96);
        play.ClickVerb(0x01);
        play.ClickRoom(52, 96);
        Assert.True(play.RunUntil(() => session.Snapshot.ObjectStates.GetValueOrDefault(322) == 1 && play.Idle, 900), "the bar's door opens");
        play.ClickRoom(52, 96);
        Assert.True(play.RunUntil(() => session.CurrentRoomId == 33 && play.Idle, 3000), "back on the dock");
        play.Log("back on the dock");

        // Up the street: the archway (Room 33 object 436 verb 0x0A: loadRoomWithEgo 0x1CA, 0x23, Room 35).
        play.BringIntoView(990, 100);
        play.ClickRoom(990, 100);
        Assert.True(play.RunUntil(() => session.CurrentRoomId == 35 && play.Idle, 3000), "the archway leads to the street");
        play.Log("in the street");

        // The door of Room 35 (object 452): "Open", then "Walk to" (verb 0x0A +0x0031 loadRoomWithEgo 0x17D, 0x1D, Room 29).
        play.BringIntoView(232, 80);
        play.ClickVerb(0x01);
        play.ClickRoom(232, 80);
        Assert.True(play.RunUntil(() => session.Snapshot.ObjectStates.GetValueOrDefault(452) == 1 && play.Idle, 900), "the street door opens");
        play.ClickRoom(232, 80);
        Assert.True(play.RunUntil(() => session.CurrentRoomId == 29 && play.Idle, 3000), "the door leads in");
        play.Log("inside");

        // (3) Take an object: "Pick up" the rubber chicken (Room 29 object 391 verb 0x0B: pickupObject var[0x007]).
        play.BringIntoView(248, 116);
        play.ClickVerb(0x0B);
        play.Hover(248, 116);
        play.Log("pointer on the chicken");
        play.ClickRoom(248, 116);
        Assert.True(play.RunUntil(() => session.Snapshot.Inventory.Contains(391), 1500), "the chicken reaches the inventory");
        play.Run(5);
        play.Log("chicken taken");
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x0B with objects 391", StringComparison.Ordinal));
        // pickupObject runs the inventory script (VAR_INVENTORY_SCRIPT = 9), which names slot 0x65 with code 6 of var[0x086] and turns it on.
        Assert.Equal("chicken", play.Glyphs(0x65));
        Assert.Equal(1, session.Verbs.Single(verb => verb.VerbId == 0x65).Mode);
        Assert.Contains(session.PlayVerbArea().Items, item => item.VerbId == 0x65 && item.IsText && item.Width > 0);

        // The app chose nothing: every input was a click or a verb's own key; no key reached area 4 on the route, and the boot's assumed protection
        // answer is not among the player's inputs (R-15.7).
        Assert.DoesNotContain(session.DeliveredInput, input => input.Area == ScummSession.KeyClickArea);
        Assert.All(session.DeliveredInput, input => Assert.Equal(1, input.Mode));
        foreach (var item in session.SoundEvents)
            output.WriteLine($"sound: seq {item.Sequence} room {item.RoomId} {item.Event.Kind} {item.Event.TargetId} {item.Event.SourceLabel} +0x{item.Event.Offset:X4} {item.Event.Operation} {item.Event.Certainty} {item.Event.Executed}");
        output.WriteLine("active at the end: " + string.Join(",", session.Snapshot.ActiveSounds));
        AssertRouteSounds(session);

        // The play screen: the room from the camera, the verb screen below row 144 and the version 4 cross-hair (cursor 0 of resetCursors, hotspot 8, 7)
        // in colour 15 of the room palette at the pointer.
        var screen = new ScummPlayScreen(game, session, ((ScummV4DiskSetDataSource)ScummDataSources.For(game)).ReadIndexedObjects().States).Compose(0);
        Assert.Equal(29, screen.RoomId);
        var (px, py) = session.ScreenPointer!.Value;
        var white = screen.Pixels[(py - 7) * ScummPlayFrame.Width + px];
        Assert.Equal(white, screen.Pixels[(py - 6) * ScummPlayFrame.Width + px]);
        Assert.Equal(white, screen.Pixels[py * ScummPlayFrame.Width + px - 7]);
        Assert.Equal(white, screen.Pixels[py * ScummPlayFrame.Width + px + 7]);
    }

    /// <summary>
    /// S6 sound (16 October 2026): the sound commands the route's scripts executed, which the app plays through the chosen device. Ids are the
    /// non-Roland table of Script 1 (VAR_SOUNDCARD 3): bar music <c>var[0x04B]</c> = 0x65 (+0x0466), title <c>var[0x051]</c> = 104.
    /// </summary>
    private static void AssertRouteSounds(ScummSession session)
    {
        var events = session.SoundEvents;
        bool Has(ScummScriptEventKind kind, int? id, string source, int offset) => events.Any(item => item.Event.Kind == kind && item.Event.TargetId == id &&
            item.Event.SourceLabel == source && item.Event.Offset == offset && item.Event.Executed);
        // Boot: the credits' music (Script 149 +0x001D), the lookout's (Room 38 local 203 +0x000E), and the title music of Room 96, which local script 200
        // awaits with isSoundRunning (+0x000D) and then stops (+0x0017): the wait ends, the boot reaches the dock.
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 110, "Script 149", 0x001D));
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 108, "Room 38 local script 203", 0x000E));
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 104, "Room 96 local script 200", 0x0009));
        Assert.True(Has(ScummScriptEventKind.SoundStopped, 104, "Room 96 local script 200", 0x0017));
        // iMuse's Monkey Island workaround (Sound::triggerSound): the title music's start stops the lookout's music first, which no script stops.
        var title = events.Select((item, index) => (item, index)).First(pair => pair.item.Event.TargetId == 104).index;
        Assert.Equal(ScummScriptEventKind.MusicStopped, events[title - 1].Event.Kind);
        Assert.Equal("triggerSound", events[title - 1].Event.Operation);
        // The route: the dock's door opens with sound 2 (Script 25 +0x0034); the SCUMM Bar's entry starts its music 101 (Room 28 entry +0x008A, after
        // isSoundRunning +0x0080 found it silent); the bar's door stops it as Guybrush leaves (object 322 verb 0x0A +0x0006); the first exit's Script 117
        // plays sound 100 and stops it (+0x0031, +0x053E); Room 29's local script 200 starts sound 115 (+0x0021), still playing at the end.
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 2, "Script 25", 0x0034));
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 101, "Room 28 entry script", 0x008A));
        Assert.True(Has(ScummScriptEventKind.SoundStopped, 101, "Room 28 Object 322 Verb 0x0A", 0x0006));
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 100, "Script 117", 0x0031));
        Assert.True(Has(ScummScriptEventKind.SoundStopped, 100, "Script 117", 0x053E));
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 115, "Room 29 local script 200", 0x0021));
        Assert.Contains(115, session.Snapshot.ActiveSounds);
        Assert.All(events.Where(item => item.Event.Kind is ScummScriptEventKind.SoundStarted), item => Assert.NotNull(item.Event.TargetId));
    }

    [Fact]
    public void ARightClickOnAnObjectRunsItsDefaultVerb()
    {
        // R-7.16 (Rule, 16 Oct): from version 4 a right click reaches the input script as the click with mode 2 (ScummVM checkExecVerbs). Monkey's
        // default two-button interface (bit[0x223] = 0) reads it: Script 4 +0x0000 equalZero bit[0x223], +0x0005 isEqual local[0x002], 2,
        // +0x000C equalZero var[0x06E] (no second object pending), +0x0011 notEqualZero var[0x0B5], +0x0016 move var[0x06B] = var[0x0B5]: the verb
        // becomes the hovered object's default verb (Script 23 +0x0128..+0x0142 from the object's verb 0x5A; the poster's is 9, "Look at"); the area 2
        // path then runs doSentence var[0x06B], var[0x06C], var[0x06D] (+0x044B), and sentence Script 2 runs the poster's verb 0x09.
        if (Folder() is not { } folder) return;
        var play = new Player(Boot(folder, out var game), output);
        var session = play.Session;
        Assert.True(ScummProfile.For(game).RightClickReachesGame);
        Assert.True(play.RunUntil(() => !session.EgoMoving && play.Idle, 600), "Guybrush walks in from the cliffside");
        Assert.Equal(0, play.Var("bit[0x223]"));
        Assert.Equal("Walk to", play.Sentence);

        play.Hover(268, 104);
        Assert.Equal(438, play.Var("var[0x0B4]"));
        Assert.Equal(9, play.Var("var[0x0B5]"));
        var screenX = 268 - session.ViewLeft;
        var input = session.ClickScreen(screenX, 104 + session.MainScreen.Top, true, out var refused);
        Assert.True(input is not null, refused);
        Assert.Equal((ScummSession.SceneClickArea, 0, 2), (input!.Area, input.Value, input.Mode));
        play.Run(2);
        play.Log("after the right click");
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x09 with objects 438", StringComparison.Ordinal));
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(message => message.Text.StartsWith("Re-elect Governor Marley.", StringComparison.Ordinal)), 900),
            "the poster is looked at");
        // The app chose no verb: the only input was the right click.
        Assert.Equal(2, Assert.Single(session.DeliveredInput).Mode);
    }

    [Fact]
    public void ThePlayCursorIsTheCrossHairOfResetCursors()
    {
        // ScummEngine_v5::resetCursors cursor 0 (versions 3 and 4 inherit it): arms 2 to 7 pixels from the hotspot (8, 7) across and up/down, none at it.
        var points = ScummPlayScreen.CursorPoints;
        Assert.Equal(24, points.Count);
        Assert.DoesNotContain((0, 0), points);
        foreach (var d in Enumerable.Range(2, 6))
        {
            Assert.Contains((d, 0), points);
            Assert.Contains((-d, 0), points);
            Assert.Contains((0, d), points);
            Assert.Contains((0, -d), points);
        }
        // Not the 23 by 21 crosshair of ScummEngine_v2 (arms to 11 across, 10 down).
        Assert.DoesNotContain((8, 0), points);
        Assert.DoesNotContain((0, 8), points);
    }

    [Fact]
    public void TheMainMenuKeyAndTheEnginesKeysAreNotDelivered()
    {
        if (Folder() is not { } folder) return;
        var session = Boot(folder, out var game);
        // Script 1 +0x077F puts F5 (0x13F) in VAR_MAINMENU_KEY (50, version 4): the engine opens its save/load menu with it, the scripts never see it.
        Assert.Equal(ScummVars.MainMenuKey, ScummProfile.For(game).MainMenuKeyVariable);
        Assert.Equal(0x13F, session.Snapshot.KnownGlobalVariables[ScummVars.MainMenuKey]);
        Assert.Null(session.PressPlayKey(0x13F, out var refused));
        Assert.Contains("save/load", refused);
        Assert.Null(session.PressPlayKey(0x20, out _));
        Assert.Null(session.PressPlayKey(0x142, out _));
        // A verb's own key is that verb's click (checkExecVerbs): 'l' is Look at (Script 22).
        var look = session.PressPlayKey('l', out _);
        Assert.Equal((ScummSession.VerbClickArea, 9, 1), (look!.Area, look.Value, look.Mode));
    }
}