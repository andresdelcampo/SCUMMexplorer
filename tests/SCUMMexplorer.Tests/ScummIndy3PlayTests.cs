using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S6 Play for Indiana Jones and the Last Crusade EGA (15 October 2026; REQUIREMENTS §7.1, Proposed R-7.7 to R-7.18): the acceptance route of R-7.18, played
/// headless from Room 73 through the game's own interface. Every click and key goes to the input script the game holds in <c>VAR_VERB_SCRIPT</c>
/// (Script 4 in play, Script 18 in a dialogue), sentences run only through the game's sentence script (Script 2), the verb area, sentence line and
/// inventory are the game's verbs drawn with their string escapes. Record: <c>.verification/indy3-play-20261015/README.md</c>.
/// </summary>
public sealed class ScummIndy3PlayTests(ITestOutputHelper output)
{
    private static string? Folder()
    {
        var overridden = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        var folder = string.IsNullOrWhiteSpace(overridden) ? Path.Combine(TestGames.Root, "indy3 ega en") : overridden;
        return File.Exists(Path.Combine(folder, "00.LFL")) ? folder : null;
    }

    private sealed class Player(ScummGameSession session, ITestOutputHelper output)
    {
        public ScummGameSession Session { get; } = session;
        public int Frames { get; private set; }

        public void Run(int frames)
        {
            for (var i = 0; i < frames; i++) { Session.AdvanceFrame(); Frames++; }
        }

        /// <summary>Plays frames until the condition holds (at most <paramref name="limit"/>).</summary>
        public bool RunUntil(Func<bool> condition, int limit = 1500)
        {
            for (var i = 0; i < limit; i++)
            {
                if (condition()) return true;
                Run(1);
            }
            if (condition()) return true;
            foreach (var line in Session.SessionTrace.Where(l => !l.Contains("delay") && !l.Contains("waits")).TakeLast(40)) output.WriteLine("trace " + line);
            return false;
        }

        public string Sentence => Glyphs(0x64);

        public string Glyphs(int verbId) => Session.Verbs.FirstOrDefault(item => item.VerbId == verbId) is { } verb
            ? ScummTextDecoder.DecodeGlyphs(Session.VerbGlyphs(verb)).Trim() : "";

        public ushort Var(string name) => Session.Snapshot.KnownGlobalVariables.GetValueOrDefault(name);

        public void ClickVerb(int verbId)
        {
            var item = Session.PlayVerbArea().Items.Single(entry => entry.VerbId == verbId);
            var input = Session.ClickScreen(item.X + 1, item.Y + 1, out var refused);
            Assert.True(input is not null, refused);
            Assert.Equal((ScummSession.VerbClickArea, verbId), (input!.Area, input.Value));
            output.WriteLine($"frame {Frames}: {input.Description}");
            Run(2);
        }

        public void ClickRoom(int roomX, int roomY)
        {
            var input = Session.ClickScreen(roomX - Session.ViewLeft, roomY + Session.MainScreen.Top, out var refused);
            Assert.True(input is not null, refused);
            Assert.Equal(ScummSession.SceneClickArea, input!.Area);
            output.WriteLine($"frame {Frames}: {input.Description}");
            Run(2);
        }

        public void Log(string what) => output.WriteLine($"frame {Frames} [{what}] room {Session.CurrentRoomId}, input script {Var(ScummVars.VerbScript)}, " +
                                                         $"sentence \"{Sentence}\", inventory [{string.Join(", ", Session.Snapshot.Inventory)}], cutscene {Session.InCutscene}, " +
                                                         $"messages [{string.Join(" | ", Session.Messages.OnScreen.Select(message => message.Text))}]");
    }

    [Fact]
    public void TheAcceptanceRouteFromRoom73RunsThroughTheGamesOwnInterface()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL"));
        RunAcceptanceRoute(game, new ScummGameSession(game, objects), output);
    }

    /// <summary>
    /// The R-7.18 route from Room 73, shared by the releases whose Room 73, Room 1 and interface scripts agree (EGA; the 256-colour release, S6 Indy3Vga,
    /// whose objects, positions and Scripts 2, 12, 16, 18, 20, 23, 24, 27, 29, 96 and 113 are the same instructions).
    /// </summary>
    internal static void RunAcceptanceRoute(GameLibrary game, ScummGameSession bootedSession, ITestOutputHelper output)
    {
        var play = new Player(bootedSession, output);
        var session = play.Session;
        Assert.True(ScummProfile.For(game).PlaysThroughVerbs);
        play.Run(2);
        play.Log("after the boot");

        // The verb screen is the game's: Script 96's twelve verbs drawn as text at their positions, Talk and Travel dim, the sentence line (verb 0x64,
        // Script 16) showing the default verb. Room screen rows 16 to 143 (Script 1 +0x021B roomOps setScreen 0x10, 0x90).
        Assert.Equal((16, 144), session.MainScreen);
        Assert.True(session.CursorShown && session.UserInputOn);
        var area = session.PlayVerbArea();
        Assert.Equal(12, area.Items.Count(item => item.IsText && item.Mode == 1 && item.VerbId is >= 0x01 and <= 0x0C));
        Assert.Contains(area.Items, item => item.VerbId == 0x0D && item.Mode == 2);
        Assert.Equal("Walk to", play.Sentence);
        Assert.Equal(4, play.Var(ScummVars.VerbScript));

        // (1) and (3): "Pick up" (verb 0x0B), then the mallet (object 836): Script 4 +0x0114 sets var[0x03B], Script 20 finds the object under the
        // pointer, Script 16 names the sentence line "Pick up mallet"; the second click on the same object runs it (+0x01B9, doSentence +0x025D).
        play.ClickVerb(0x0B);
        Assert.Equal("Pick up", play.Sentence);
        play.ClickRoom(176, 112);
        Assert.Equal("Pick up mallet", play.Sentence);
        play.ClickRoom(176, 112);
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x0B with objects 836", StringComparison.Ordinal));
        Assert.True(play.RunUntil(() => session.Snapshot.Inventory.Contains(836)), "the mallet reaches the inventory (Room 73 object 836 verb 0x0B: pickupObject)");
        play.Run(5);
        // Script 12 (the inventory script, runInventoryScript after pickupObject) names verb 0x65 with code 6 of var[0x053+1].
        Assert.Equal("mallet", play.Glyphs(0x65));
        play.Log("mallet taken");

        // (5) A cutscene skipped from its override point. The light switch (object 837) is a child of the door (object 830, parent state 0): the game's
        // findObject sees it only while the door is closed, so "Close" (0x02) the door first (object 830 verb 0x02: cutscene, Script 27), then "Push"
        // (0x06) the switch: cutscene, lights off, beginOverride +0x0008 whose skip jump goes to +0x005F (lights back on).
        Assert.True(play.RunUntil(() => session.UserInputOn && !session.InCutscene));
        play.ClickVerb(0x02);
        play.ClickRoom(56, 60);
        play.ClickRoom(56, 60);
        // The mallet is chained to the bell: the pick-up started Room 73 local script 205 (object 836 verb 0x0B +0x0000), which waits until the ego
        // walks near object 0x343, then (cutscene +0x000F) says so, gives the mallet back to the room (setOwnerOf 0x344, 0x0F, +0x001B) and makes it
        // untouchable; its endCutscene runs Script 23, which stops the sentence script (+0x0017). The game's own interruption: close the door again.
        Assert.True(play.RunUntil(() => !session.Snapshot.Inventory.Contains(836) && session.UserInputOn && !session.InCutscene), "the chain takes the mallet back");
        play.Log("the chain took the mallet back");
        play.ClickVerb(0x02);
        play.ClickRoom(56, 60);
        play.ClickRoom(56, 60);
        Assert.True(play.RunUntil(() => session.Snapshot.ObjectStates.GetValueOrDefault(830) == 0 && session.UserInputOn && !session.InCutscene), "the door closes");
        play.Log("door closed");
        play.ClickVerb(0x06);
        play.ClickRoom(24, 92);
        play.ClickRoom(24, 92);
        var reached = play.RunUntil(() => session.InCutscene && session.CutsceneSkippable, 400);
        if (!reached) foreach (var line in session.SessionTrace.Where(l => !l.Contains("delay") && !l.Contains("waits")).TakeLast(60)) output.WriteLine("trace " + line);
        Assert.True(reached, "the switch's cutscene reaches its override point");
        play.Log("lights out");
        var skip = session.PressKey(0x1B);
        output.WriteLine("Esc: " + string.Join(" ", skip.Uncertainties));
        Assert.True(play.RunUntil(() => !session.InCutscene, 300), "the skipped cutscene ends");
        Assert.Contains(session.SessionTrace, line => line.Contains("abortCutscene", StringComparison.OrdinalIgnoreCase) || line.Contains("override", StringComparison.OrdinalIgnoreCase));
        play.Log("cutscene skipped");

        // (2) Leaving the room through the game's own code: "Open" (0x01) the door (object 830), then "Walk to" it (one click: Script 4 +0x0123),
        // whose code walks the ego out and runs loadRoomWithEgo 0x1C, 0x01 (Room 73 object 830 verb 0x0A +0x0025).
        Assert.True(play.RunUntil(() => session.UserInputOn && !session.InCutscene));
        play.ClickVerb(0x01);
        play.ClickRoom(56, 60);
        play.ClickRoom(56, 60);
        Assert.True(play.RunUntil(() => session.Snapshot.ObjectStates.GetValueOrDefault(830) == 1 && session.UserInputOn && !session.InCutscene), "the door opens");
        play.Log("door opened");
        play.ClickRoom(56, 60);
        Assert.True(play.RunUntil(() => session.CurrentRoomId != 73), "the door's Walk to leaves Room 73");
        Assert.Equal(1, session.CurrentRoomId);
        play.Log("left the gym");

        // (4) A dialogue: Room 1's entry puts Script 29 in var[0x032] while Marcus (actor 4) is there; Script 113 turns Talk (0x0D) on; Talk on Marcus
        // runs it through the sentence script (Script 2 +0x0041 startScript var[0x032]); Script 29 installs Script 18 as the input script
        // (+0x0039) and offers verbs 0x78 to 0x7A (+0x01B6). The user's click on one sets var[0x072] (Script 18 +0x0077).
        Assert.True(play.RunUntil(() => session.UserInputOn && !session.InCutscene && session.Verbs.Any(verb => verb.VerbId == 0x0D && verb.Mode == 1)),
            "Talk is on in Room 1");
        var marcus = session.Actors.Single(actor => actor.Number == 4 && actor.Room == 1);
        play.ClickVerb(0x0D);
        play.ClickRoom(marcus.X!.Value, marcus.Y!.Value - 20);
        play.ClickRoom(marcus.X!.Value, marcus.Y!.Value - 20);
        Assert.True(play.RunUntil(() => session.Verbs.Any(verb => verb.VerbId == 0x78 && verb.Mode == 1) && session.UserInputOn), "the dialogue offers its choices");
        Assert.Contains(session.SessionTrace, line => line == "sentence script 2 runs verb 0x0D with objects 0 and 4");
        Assert.Equal(18, play.Var(ScummVars.VerbScript));
        play.Log("dialogue choices");
        Assert.Equal("Don't be a twit, Marcus.  He had it coming to him.", play.Glyphs(0x78));
        play.ClickVerb(0x79);
        // Script 18 prints choices 120 to 122 as the ego's line (+0x0068), waits 120 jiffies and puts the choice in var[0x072] (+0x0077); Script 29 waits on it (+0x025E) and its branch for 0x79 answers with Marcus's line (actor 4).
        var said = play.RunUntil(() => session.Messages.OnScreen.Any(message => message.Actor == 1 && message.Text.Length > 0), 300);
        Assert.True(said, "the ego says the choice");
        play.Log("the ego's line");
        Assert.True(play.RunUntil(() => play.Var("var[0x072]") == 0x79, 200), "the choice reaches var[0x072]");
        play.Log("choice taken");
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(message => message.Actor == 4 && message.Text.Length > 0), 900), "Marcus answers");
        play.Log("Marcus answers");
        Assert.Equal(0, session.DeliveredInput.Count(input => input.Area == ScummSession.KeyClickArea));
    }

    [Fact]
    public void TheRightButtonStaysTheAppsAndNeverReachesTheGame()
    {
        // R-7.16 (Rule, 16 Oct): version 3 scripts never see a right click (ScummVM processInput turns it into Esc outside LOOM, checkExecVerbs gives
        // mode 0 up to version 3), so Indy3 does not need it: the right button is the app's, nothing is queued, and Esc keeps its override rule (R-7.11).
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new ScummGameSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        session.AdvanceFrame();
        session.AdvanceFrame();
        Assert.True(session.UserInputOn);
        Assert.False(ScummProfile.For(game).RightClickReachesGame);
        var delivered = session.DeliveredInput.Count;

        Assert.Null(session.ClickScreen(176, 112, true, out var refused));
        Assert.Contains("R-7.16", refused);
        session.AdvanceFrame();
        Assert.Equal(delivered, session.DeliveredInput.Count);
        Assert.False(session.CutsceneSkippable);
        // The left button is unchanged: one click, mode 0.
        Assert.Equal(0, session.ClickScreen(176, 112, out _)!.Mode);
    }
}
