using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S6 Play for The Secret of Monkey Island CD (SCUMM v5; 17 October 2026; REQUIREMENTS §7.1, R-7.7 to R-7.18): the acceptance route of R-7.18, played headless
/// through the game's own interface. The boot ends at the lookout (Room 38) with the player in control; left alone, the game goes on to the title and the dock
/// (Room 33), where the route starts as on the floppy release. Ids, offsets and texts are the CD's own (its verbs are 2 Open, 3 Close, 4 Give, 5 Push, 6 Pull,
/// 7 Use, 8 Look at, 9 Pick up, 0x0A Talk to, 0x0B Walk to; the floppy's differ). Clicks and keys go to the input script in <c>VAR_VERB_SCRIPT</c> (Script 4,
/// Room 28's local script 202 in the bar, Script 14 in a dialogue) with the version 5 click mode (1, right click 2). Record:
/// <c>.verification/monkeycd-play-20261017/README.md</c>. Skips when the disc image is absent.
/// </summary>
public sealed class ScummMonkeyCdPlayTests(ITestOutputHelper output)
{
    private static string? Folder()
    {
        var path = Path.Combine(TestGames.Root, "monkey1 vga en");
        return File.Exists(Path.Combine(path, "The_Secret_of_Monkey_Island.cue")) ? path : null;
    }

    /// <summary>The disc read from one of its language folders (S9, 18 October 2026): null is the default (ENGLISH), "SPANISH" the disc's Spanish copy.</summary>
    internal static GameLibrary Scan(string folder, string? disc) => new GameFolderScanner { DiscFolder = disc }.Scan(folder);

    /// <summary>
    /// The route's texts per copy (S9, 18 October 2026): the disc's SPANISH folder goes through the same ids, positions and branches; its texts are its own data.
    /// Record: <c>.verification/monkeycd-editions-20261018/README.md</c>.
    /// </summary>
    internal sealed record RouteTexts(string[] Verbs, string WalkTo, string LookAt, string LookAtPoster, string PosterLine, string TalkToPirates,
        string[] Choices, string Trials, string Leave, string PickUpChicken);

    internal static RouteTexts TextsOf(string? disc) => disc is null
        ? new(["Give", "Pick up", "Use", "Open", "Look at", "Talk to", "Close", "Push", "Pull"], "Walk to", "Look at", "Look at poster", "Re-elect Governor Marley.",
            "Talk to important-looking pirates", ["I want to be a fireman.", "I mean to kill you all!", "I want to be a pirate."], "three trials",
            "I'll just be running along now.", "Pick up chicken")
        : new(["Dar", "Coger", "Usar", "Abrir", "Mirar", "Hablar a", "Cerrar", "Empujar", "Tirar"], "Caminar a", "Mirar", "Mirar cartel", "Re-eliga a la Gobernadora Marley.",
            "Hablar a piratas importantes", ["Quiero ser bombero.", "¡Os voy a matar a todos!", "Quiero ser un pirata."], "tres pruebas",
            "Pues yo me largo.", "Coger pollo");

    private const int Open = 0x02, Give = 0x04, LookAt = 0x08, PickUp = 0x09, TalkTo = 0x0A;

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

        public int ScreenX(int roomX)
        {
            var screenX = roomX - Session.ViewLeft;
            Assert.InRange(screenX, 0, 319);
            return screenX;
        }

        /// <summary>Moves the pointer over a room point and plays two frames: Script 23 finds the object under it.</summary>
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

        /// <summary>Walks the ego (a click in the room, the default "Walk to") towards a room x until the camera shows it.</summary>
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

    /// <summary>The play boot (<see cref="ScummGameSession.BootForPlay"/>, the window's path), then the frames the game plays alone from the lookout to the dock.</summary>
    private Player AtTheDock(string folder, out GameLibrary game, string? disc = null)
    {
        game = Scan(folder, disc);
        var play = new Player(ScummGameSession.BootForPlay(game, out var objects), output);
        Assert.NotEmpty(objects.States);
        var session = play.Session;
        Assert.Equal(38, session.CurrentRoomId);
        Assert.True(play.Idle);
        // Room 38's object 486 waits for the lookout's track (sound 108) to end, then the title (Room 96) leads to the dock: Guybrush walks in from the cliffside.
        Assert.True(play.RunUntil(() => session.CurrentRoomId == 33 && !session.EgoMoving && play.Idle, 6000), "the game goes on to the dock");
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal((33, 346, 133), (ego.Room, ego.X, ego.Y));
        play.Log("dock");
        return play;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("SPANISH")]
    public void TheAcceptanceRouteFromTheDockRunsThroughTheGamesOwnInterface(string? disc)
    {
        if (Folder() is not { } folder) return;
        var texts = TextsOf(disc);
        var play = AtTheDock(folder, out var game, disc);
        var session = play.Session;
        Assert.True(ScummProfile.For(game).PlaysThroughVerbs);
        Assert.Equal(32, ScummProfile.For(game).UntouchableClass);

        // The verb screen is the game's: Script 22's nine verbs and the sentence line (verb 0x64, Script 12), drawn as text with the CD's own charsets (the CHAR
        // blocks of MONKEY.001); the room screen is rows 0 to 143; the input script is Script 4; cursor and user input on, and kept in VAR_CURSORSTATE/VAR_USERPUT.
        Assert.Equal((0, 144), session.MainScreen);
        Assert.True(session.CursorShown && session.UserInputOn);
        Assert.Equal((1, 1), (play.Var(ScummVars.CursorState), play.Var(ScummVars.UserPut)));
        var area = session.PlayVerbArea();
        foreach (var item in area.Items) output.WriteLine($"verb 0x{item.VerbId:X2} mode {item.Mode} at ({item.X},{item.Y}) {item.Width}x{item.Height} \"{play.Glyphs(item.VerbId)}\"");
        Assert.Equal(texts.Verbs,
            new[] { Give, PickUp, 7, Open, LookAt, TalkTo, 3, 5, 6 }.Select(play.Glyphs).ToArray());
        Assert.Equal(9, area.Items.Count(item => item.IsText && item.Mode == 1 && item.VerbId is >= 2 and <= 0x0A && item.Width > 0));
        Assert.Equal(texts.WalkTo, play.Sentence);
        Assert.Equal(4, play.Var(ScummVars.VerbScript));
        Assert.Contains(area.Pixels, pixel => pixel != 0);

        // (1) Compose a sentence: "Look at" (verb 8), then the pointer over the poster (object 429): Script 23 finds it and Script 12 names the line, with string 49
        // (" ", stringOps loadString) between verb and object through code 7 (convertStringMessage).
        play.ClickVerb(LookAt);
        Assert.Equal(texts.LookAt, play.Sentence);
        play.Hover(268, 104);
        play.Log("pointer on the poster");
        Assert.Equal(texts.LookAtPoster, play.Sentence);
        play.ClickRoom(268, 104);
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x08 with objects 429", StringComparison.Ordinal));

        // (5) The poster's first look is a cutscene with an override (Room 33 object 429 verb 8 +0x00CF..+0x0134): Esc skips it.
        Assert.True(play.RunUntil(() => session.InCutscene && session.CutsceneSkippable, 900), "the poster's cutscene reaches its override");
        play.Log("poster cutscene");
        Assert.Contains(session.Messages.OnScreen, message => message.Text.StartsWith(texts.PosterLine, StringComparison.Ordinal));
        session.PressKey(0x1B);
        Assert.True(play.RunUntil(() => play.Idle, 600), "the skipped cutscene ends");
        play.Log("cutscene skipped");
        Assert.DoesNotContain(session.Messages.OnScreen, message => message.Text.Contains("only one candidate", StringComparison.Ordinal));

        // (2) Leave the room: "Open" the door (object 428), then a click on it (Walk to, verb 0x0B: loadRoomWithEgo to Room 28).
        play.BringIntoView(716, 108);
        play.ClickVerb(Open);
        play.ClickRoom(716, 108);
        Assert.True(play.RunUntil(() => session.Snapshot.ObjectStates.GetValueOrDefault(428) == 1 && play.Idle, 900), "the door opens");
        play.Log("door open");
        play.ClickRoom(716, 108);
        Assert.True(play.RunUntil(() => session.CurrentRoomId != 33, 900), "the door leads out");
        Assert.Equal(28, session.CurrentRoomId);
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x0B with objects 428", StringComparison.Ordinal));
        Assert.True(play.RunUntil(() => play.Idle, 1500));
        play.Log("in the SCUMM Bar");
        // The bar's entry hands input to its local script 202 (VAR_VERB_SCRIPT), which the CD release has and the floppy does not.
        Assert.Equal(202, play.Var(ScummVars.VerbScript));

        // (4) A dialogue: "Talk to" the important-looking pirates (object 322).
        play.BringIntoView(428, 104);
        play.ClickVerb(TalkTo);
        play.Hover(428, 104);
        Assert.Equal(texts.TalkToPirates, play.Sentence);
        play.ClickRoom(428, 104);
        Assert.True(play.RunUntil(() => play.Var(ScummVars.VerbScript) == 14 && session.UserInputOn && session.Verbs.Any(v => v.VerbId == 0x78 && v.Mode == 1), 3000),
            "the dialogue offers its choices");
        play.Log("choices");
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x0A with objects 322", StringComparison.Ordinal));
        // The conversation installs Script 14 as the input script and offers its lines as verbs 0x78 up, one text row each (rows 145 to 161, as the script places them).
        Assert.Equal(texts.Choices, new[] { 0x78, 0x79, 0x7A }.Select(play.Glyphs).ToArray());
        Assert.Equal(3, session.PlayVerbArea().Items.Count(item => item.VerbId is >= 0x78 and <= 0x7A && item.IsText && item.Width > 0 && item.Y >= 145));
        play.ClickVerb(0x7A);
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(message => message.Actor == 1 && message.Text.StartsWith(texts.Choices[2], StringComparison.Ordinal)), 300),
            "the ego says the choice");
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(message => message.Actor != 1 && message.Text.Length > 1), 900), "the pirates answer");
        var said = new List<string>();
        Assert.True(play.RunUntil(() =>
        {
            foreach (var message in session.Messages.OnScreen) if (said.Count == 0 || said[^1] != message.Text) said.Add(message.Text);
            return session.Verbs.Any(verb => verb.VerbId == 0x7F && verb.Mode == 1) && session.UserInputOn && play.Var(ScummVars.VerbScript) == 14;
        }, 6000), "the conversation goes on to its next choices");
        foreach (var line in said) output.WriteLine("said: " + line);
        Assert.Contains(said, line => line.Contains(texts.Trials, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(texts.Leave, play.Glyphs(0x7F));
        play.ClickVerb(0x7F);
        Assert.True(play.RunUntil(() => play.Idle && play.Var(ScummVars.VerbScript) != 14, 1500), "the conversation ends");
        play.Log("conversation over");

        // Out of the bar: "Open" the door (object 315), then "Walk to" it: the CD's Script 120 plays the lookout's flashback (Rooms 70, 72) and returns to the dock.
        play.BringIntoView(52, 96);
        play.ClickVerb(Open);
        play.ClickRoom(52, 96);
        Assert.True(play.RunUntil(() => session.Snapshot.ObjectStates.GetValueOrDefault(315) == 1 && play.Idle, 900), "the bar's door opens");
        play.ClickRoom(52, 96);
        Assert.True(play.RunUntil(() => session.CurrentRoomId == 33 && play.Idle, 3000), "back on the dock");
        play.Log("back on the dock");

        // Up the street: the archway (Room 33 object 427, Walk to: Room 35).
        play.BringIntoView(990, 100);
        play.ClickRoom(990, 100);
        Assert.True(play.RunUntil(() => session.CurrentRoomId == 35 && play.Idle, 3000), "the archway leads to the street");
        play.Log("in the street");

        // The door of Room 35 (object 444): "Open", then "Walk to" (Room 29).
        play.BringIntoView(232, 80);
        play.ClickVerb(Open);
        play.ClickRoom(232, 80);
        Assert.True(play.RunUntil(() => session.Snapshot.ObjectStates.GetValueOrDefault(444) == 1 && play.Idle, 900), "the street door opens");
        play.ClickRoom(232, 80);
        Assert.True(play.RunUntil(() => session.CurrentRoomId == 29 && play.Idle, 3000), "the door leads in");
        play.Log("inside");

        // (3) Take an object: "Pick up" the rubber chicken (Room 29 object 377). o5_pickupObject makes it untouchable (class 32, the version 5 number).
        play.BringIntoView(248, 116);
        play.ClickVerb(PickUp);
        play.Hover(248, 116);
        Assert.Equal(texts.PickUpChicken, play.Sentence);
        play.ClickRoom(248, 116);
        Assert.True(play.RunUntil(() => session.Snapshot.Inventory.Contains(377), 1500), "the chicken reaches the inventory");
        play.Run(5);
        play.Log("chicken taken");
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x09 with objects 377", StringComparison.Ordinal));
        Assert.True(session.Snapshot.IsUntouchable(377));
        // pickupObject runs the inventory script (Script 9), which gives the first inventory slot (verb 0xC8; Script 4 +0x0304 reads slots 200 to 207) the
        // object's icon: the object's verb 0x5B puts the icon's object number in var[0x178] (+0x00AA), and verbOps assignObject takes its image from Room 99
        // (+0x00C7). The 256-colour release shows its inventory as pictures, not names.
        foreach (var verb in session.Verbs.Where(v => v.VerbId is >= 0xC8 and <= 0xD1)) output.WriteLine($"slot 0x{verb.VerbId:X2} mode {verb.Mode} image {verb.ImageObject} room {verb.ImageRoom}");
        var slot = session.Verbs.Single(verb => verb.VerbId == 0xC8);
        Assert.Equal((1, 99), (slot.Mode, slot.ImageRoom));
        Assert.Equal(play.Var("var[0x178]"), slot.ImageObject);
        Assert.Contains(session.PlayVerbArea().Items, item => item.VerbId == 0xC8 && !item.IsText && item.Width > 0 && item.Height > 0);

        // The app chose nothing: every input was a click; no key reached area 4 on the route.
        Assert.DoesNotContain(session.DeliveredInput, input => input.Area == ScummSession.KeyClickArea);
        Assert.All(session.DeliveredInput, input => Assert.Equal(1, input.Mode));
        foreach (var item in session.SoundEvents)
            output.WriteLine($"sound: seq {item.Sequence} room {item.RoomId} {item.Event.Kind} {item.Event.TargetId} {item.Event.SourceLabel} +0x{item.Event.Offset:X4} {item.Event.Operation} {item.Event.Certainty} {item.Event.Executed}");
        AssertRouteSounds(session, game, disc is null);

        // The play screen: the room from the camera, the verb screen below row 144 and the version 5 cross-hair (cursor 0 of resetCursors, hotspot 8, 7) at the pointer.
        var screen = new ScummPlayScreen(game, session, ((ScummV5DataSource)ScummDataSources.For(game)).ReadIndexedObjects().States).Compose(0);
        Assert.Equal(29, screen.RoomId);
        var (px, py) = session.ScreenPointer!.Value;
        var white = screen.Pixels[(py - 7) * ScummPlayFrame.Width + px];
        Assert.Equal(white, screen.Pixels[(py - 6) * ScummPlayFrame.Width + px]);
        Assert.Equal(white, screen.Pixels[py * ScummPlayFrame.Width + px - 7]);
        Assert.Equal(white, screen.Pixels[py * ScummPlayFrame.Width + px + 7]);
    }

    /// <summary>
    /// S6 sound (17 October 2026): the route's sound commands, and which of them play a track of the disc (ScummVM <c>playCDTrackFromSoundID</c>): the music is CD
    /// audio, the door sounds are the game's own iMuse sounds (no CD track).
    /// </summary>
    private static void AssertRouteSounds(ScummSession session, GameLibrary game, bool offsets = true)
    {
        var events = session.SoundEvents;
        bool Has(ScummScriptEventKind kind, int id, string source, int offset) => events.Any(item => item.Event.Kind == kind && item.Event.TargetId == id &&
            item.Event.SourceLabel == source && (!offsets || item.Event.Offset == offset) && item.Event.Executed);
        var cd = ScummCdAudioTracks.For(game)!;
        // Boot: the credits (Script 152 +0x0049, sound 110, CD track 17), the lookout (Room 38 local script 203 +0x000E, sound 108: track 17 from frame 7173),
        // the title (Room 96 local script 200 +0x0027, sound 104, track 6, stopped at +0x0033).
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 110, "Script 152", 0x0049));
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 108, "Room 38 local script 203", 0x000E));
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 104, "Room 96 local script 200", 0x0027));
        Assert.True(Has(ScummScriptEventKind.SoundStopped, 104, "Room 96 local script 200", 0x0033));
        // The route: the dock's music 122 (Room 33 entry +0x0048, track 22, repeats), stopped by its exit (+0x0000); the door's creak, sound 2 (Script 25 +0x003A);
        // the bar's music 101 (Room 28 entry +0x0071, track 8) stopped as Guybrush leaves (object 315 verb 0x0B +0x000C); Script 120's sound 100 (+0x0036, track 12)
        // and its stop (+0x053B); the voodoo shop's sound 3 (Script 26 +0x0031) and music 115 (Room 29 local script 200 +0x0021, track 1), still playing at the end.
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 122, "Room 33 entry script", 0x0048));
        Assert.True(Has(ScummScriptEventKind.SoundStopped, 122, "Room 33 exit script", 0x0000));
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 2, "Script 25", 0x003A));
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 101, "Room 28 entry script", 0x0071));
        Assert.True(Has(ScummScriptEventKind.SoundStopped, 101, "Room 28 Object 315 Verb 0x0B", 0x000C));
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 100, "Script 120", 0x0036));
        Assert.True(Has(ScummScriptEventKind.SoundStopped, 100, "Script 120", 0x053B));
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 3, "Script 26", 0x0031));
        Assert.True(Has(ScummScriptEventKind.SoundStarted, 115, "Room 29 local script 200", 0x0021));
        Assert.Contains(115, session.Snapshot.ActiveSounds);
        // Music on the disc, effects in the resource file: what the window plays for each (MainWindow.Play RenderPlaySound).
        Assert.All(new[] { 100, 101, 104, 108, 110, 115, 122 }, id => Assert.True(cd.Plays(id)));
        Assert.All(new[] { 2, 3 }, id => Assert.False(cd.Plays(id)));
        Assert.True(cd.LoopsForever(122) && cd.LoopsForever(101) && cd.LoopsForever(115));
        Assert.False(cd.LoopsForever(104));
        // The dock's track as the window plays it: CD audio, 44.1 kHz stereo, the whole of track 22 (one pass; the player repeats it).
        var wave = cd.ReadWave(122);
        var track = game.AudioDisc!.AudioTracks[21];
        Assert.Equal(44 + track.Frames * 2352, wave.Length);
        Assert.Contains(wave.Skip(44 + 2352 * 75).Take(2352 * 75), b => b != 0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("SPANISH")]
    public void ARightClickOnAnObjectRunsItsDefaultVerb(string? disc)
    {
        // R-7.16 (Rule, 16 Oct): from version 4 a right click reaches the input script as the click with mode 2 (ScummVM checkExecVerbs). The CD's Script 4 reads it
        // like the floppy's, with its own variable: +0x0000 equalZero bit[0x223], +0x0005 isEqual local[0x002], 2, +0x000C equalZero var[0x06E], +0x0011 notEqualZero
        // var[0x0B6], +0x0016 move var[0x06B] = var[0x0B6] (the hovered object's default verb; the poster's is 8, "Look at"); then doSentence runs it.
        if (Folder() is not { } folder) return;
        var texts = TextsOf(disc);
        var play = AtTheDock(folder, out var game, disc);
        var session = play.Session;
        Assert.True(ScummProfile.For(game).RightClickReachesGame);
        Assert.Equal(0, play.Var("bit[0x223]"));
        Assert.Equal(texts.WalkTo, play.Sentence);

        play.Hover(268, 104);
        Assert.Equal(LookAt, play.Var("var[0x0B6]"));
        var input = session.ClickScreen(play.ScreenX(268), 104 + session.MainScreen.Top, true, out var refused);
        Assert.True(input is not null, refused);
        Assert.Equal((ScummSession.SceneClickArea, 0, 2), (input!.Area, input.Value, input.Mode));
        play.Run(2);
        play.Log("after the right click");
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x08 with objects 429", StringComparison.Ordinal));
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(message => message.Text.StartsWith(texts.PosterLine, StringComparison.Ordinal)), 900),
            "the poster is looked at");
        Assert.Equal(2, Assert.Single(session.DeliveredInput).Mode);
    }

    [Fact]
    public void TheMainMenuKeyAndTheEnginesKeysAreNotDeliveredAndAVerbsKeyIsItsClick()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        Assert.Equal(ScummVars.MainMenuKey, ScummProfile.For(game).MainMenuKeyVariable);
        var menu = session.Snapshot.KnownGlobalVariables[ScummVars.MainMenuKey];
        output.WriteLine($"VAR_MAINMENU_KEY = {menu}");
        if (menu != 0)
        {
            Assert.Null(session.PressPlayKey(menu, out var refused));
            Assert.Contains("save/load", refused);
        }
        Assert.Null(session.PressPlayKey(0x1B, out _));
        Assert.Null(session.PressPlayKey('.', out _));
        // A verb's own key is that verb's click (checkExecVerbs), from the keys Script 22 gave the verbs.
        var keyed = session.Verbs.First(verb => verb.Mode == 1 && verb.Key != 0 && verb.VerbId is >= 2 and <= 0x0A);
        var input = session.PressPlayKey(keyed.Key, out _);
        Assert.Equal((ScummSession.VerbClickArea, keyed.VerbId, 1), (input!.Area, input.Value, input.Mode));
    }

    [Fact]
    public void TheCdCharsetsAreReadFromTheResourceFile()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var archive = game.V4Disks!.Disks.OfType<ScummV5Archive>().Single();
        // DCHR lists charsets 1 to 6 (Script 22 sets 6 for the verbs, Script 17 sets 1 for the dialogue lines and 2 for the room's messages).
        foreach (var number in new[] { 1, 2, 6 })
        {
            var charset = archive.ReadCharset(number);
            Assert.NotNull(charset);
            output.WriteLine($"charset {number}: height {charset!.FontHeight}, {charset.BitsPerPixel} bpp, {charset.GlyphCount} glyphs, 'A' {charset.CharWidth('A')} wide");
            Assert.True(charset.CharWidth('A') > 0);
        }
        Assert.Null(archive.ReadCharset(99));

        // cursorCommand charsetColors (Script 22 +0x0003, after charsetSet 6): the verbs' charset draws pixel value 2 in colour 2, not the file's own entry.
        var session = ScummGameSession.BootForPlay(game, out _);
        var file = archive.ReadCharset(6)!;
        var played = Assert.IsType<ScummV4Charset>(session.PlayCharset(6));
        output.WriteLine($"charset 6 map: file [{string.Join(",", file.ColorMap)}], play [{string.Join(",", played.ColorMap)}]");
        Assert.Equal(2, played.ColorMap[1]);
        Assert.Equal(file.ColorMap.Skip(2), played.ColorMap.Skip(2));
    }

    /// <summary>S9 accents (18 October 2026): as the Indy4 test of the same name, for the disc's ENGLISH (null) and SPANISH folders.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("SPANISH")]
    public void EveryTextByteAboveAsciiHasAGlyphInTheCopysCharsets(string? disc)
    {
        if (Folder() is not { } folder) return;
        var game = Scan(folder, disc);
        var session = ScummGameSession.BootForPlay(game, out _);
        var found = ScummCopyTextBytes.Collect(game);
        output.WriteLine($"{disc ?? "ENGLISH"}: text bytes from 0x80: {ScummCopyTextBytes.Describe(found)}");
        var charsets = Enumerable.Range(0, 8).Select(n => (n, charset: session.PlayCharset(n))).Where(p => p.charset is not null).ToArray();
        Assert.NotEmpty(charsets);
        foreach (var (n, charset) in charsets)
            output.WriteLine($"charset {n}: no glyph for [{string.Join(" ", found.Keys.Where(code => charset!.CharWidth(code) == 0).Order().Select(code => code.ToString("X2")))}]");
        // Charsets 1 (dialogue lines) and 2 (room messages) draw every accent the SPANISH folder writes (0x82 é, 0x88 ê, 0x8A è, 0xA0 to 0xA5, 0xA8 ¿, 0xAD ¡);
        // 0xFA has no glyph in charset 1 of either folder (as on the floppy). Charsets 3 and 6 (the verbs) lack some Spanish glyphs: data, not used for them here.
        foreach (var number in new[] { 1, 2 })
            Assert.Empty(found.Keys.Where(code => code != 0xFA && session.PlayCharset(number)!.CharWidth(code) == 0));
        if (disc is not null) Assert.True(found.ContainsKey(0xA4) && found.ContainsKey(0xA8) && found.ContainsKey(0xAD), "the Spanish texts write ñ, ¿ and ¡");
    }
}
