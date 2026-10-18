using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S6 Play for Monkey Island 2: LeChuck's Revenge (SCUMM v5, fixture <c>mi2 talkie en</c>; 17 October 2026; REQUIREMENTS §7.1, R-7.7 to R-7.18): the acceptance
/// route of R-7.18 played headless through the game's own interface. The play boot ends at the campfire (Room 4) with the player in control; left alone, the game
/// goes on: Room 4's local script 204 has Guybrush walk off, arms jump hook 1 of the campfire song (sound 108, +0x016A) and waits for the song to end (+0x01A0..+0x01BA)
/// before it chains to Script 93, which brings him to the edge of Woodtick (Room 7). From there: look at the sign (46) and skip its cutscene, pick it up (the shovel,
/// 45, reaches the inventory), meet Largo on the bridge (Room 7 local scripts 203/204: two dialogue choices through input scripts 14 and 206), and leave through the
/// dinky door (41) to Room 8. Verbs: 2 Open, 3 Close, 4 Give, 5 Push, 6 Pull, 7 Use, 8 Look at, 9 Pick up, 0x0A Talk to, 0x0B Walk to (Script 4 is the input script, as on
/// Monkey Island CD). Record: <c>.verification/monkey2-play-20261017/README.md</c>. Skips without the data.
/// </summary>
public sealed class ScummMonkey2PlayTests(ITestOutputHelper output)
{
    private static string? Folder()
    {
        var path = Path.Combine(TestGames.Root, "mi2 talkie en");
        return File.Exists(Path.Combine(path, "MONKEY2.000")) ? path : null;
    }

    private const int LookAt = 0x08, PickUp = 0x09;

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
        public bool Quiet => Idle && Session.Messages.OnScreen.Count == 0 && !Session.EgoMoving;

        public string Sentence => Glyphs(0x64);

        /// <summary>A verb's text as drawn: the escape codes (two bytes after codes 9, 10, 12, 13, 14) draw nothing.</summary>
        public string Glyphs(int verbId)
        {
            if (Session.Verbs.FirstOrDefault(item => item.VerbId == verbId) is not { } verb) return "";
            var glyphs = Session.VerbGlyphs(verb);
            var shown = new List<byte>();
            for (var i = 0; i < glyphs.Count; i++)
            {
                if (glyphs[i] is 0xFF or 0xFE)
                {
                    var code = i + 1 < glyphs.Count ? glyphs[i + 1] : 0;
                    i += code is 9 or 10 or 12 or 13 or 14 ? 3 : 1;
                    continue;
                }
                if (glyphs[i] >= 0x20) shown.Add(glyphs[i]);
            }
            return ScummTextDecoder.DecodeGlyphs(shown).Trim();
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

        public ScummPlayInput ClickRoom(int roomX, int roomY, bool right = false)
        {
            var screenX = ScreenX(roomX);
            Session.PointAtScreen((screenX, roomY + Session.MainScreen.Top));
            Run(1);
            var input = Session.ClickScreen(screenX, roomY + Session.MainScreen.Top, right, out var refused);
            Assert.True(input is not null, refused);
            Assert.Equal((ScummSession.SceneClickArea, right ? 2 : 1), (input!.Area, input.Mode));
            output.WriteLine($"frame {Frames}: {input.Description}");
            Run(2);
            return input;
        }

        public void Log(string what) => output.WriteLine($"frame {Frames} [{what}] room {Session.CurrentRoomId}, view {Session.ViewLeft}, input script {Var(ScummVars.VerbScript)}, " +
                                                         $"sentence \"{Sentence}\", inventory [{string.Join(", ", Session.Snapshot.Inventory)}], cutscene {Session.InCutscene}, input {Session.UserInputOn}, " +
                                                         $"messages [{string.Join(" | ", Session.Messages.OnScreen.Select(message => message.Text))}]");
    }

    /// <summary>The play boot (the window's path), then the frames the game plays alone from the campfire to Woodtick (Room 7).</summary>
    private Player AtWoodtick(out GameLibrary game)
    {
        game = new GameFolderScanner().Scan(Folder()!);
        var play = new Player(ScummGameSession.BootForPlay(game, out var objects), output);
        Assert.NotEmpty(objects.States);
        var session = play.Session;
        Assert.Equal(4, session.CurrentRoomId);
        Assert.True(play.Idle);
        // Room 4 local script 204 arms jump hook 1 of sound 108 and waits for the song to end: the hook jumps to the song's track 1, which ends.
        Assert.True(play.RunUntil(() => session.CurrentRoomId == 7 && play.Quiet, 1500), "the game goes on to Woodtick");
        Assert.Contains(session.Snapshot.Uncertainties, item => item.Contains("jump hook 1 armed") && item.Contains("its track 1"));
        Assert.DoesNotContain(108, session.Snapshot.ActiveSounds);
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal((7, 696, 53), (ego.Room, ego.X, ego.Y));
        play.Log("Woodtick");
        return play;
    }

    [Fact]
    public void TheAcceptanceRouteFromWoodtickRunsThroughTheGamesOwnInterface()
    {
        if (Folder() is null) return;
        var play = AtWoodtick(out var game);
        var session = play.Session;
        var profile = ScummProfile.For(game);
        Assert.True(profile.PlaysThroughVerbs);
        Assert.True(profile.RightClickReachesGame);
        Assert.Equal(32, profile.UntouchableClass);

        // The game's screen: room rows 0 to 143, Script 4 as input script, the nine verbs drawn as text, the inventory slots 0xC8..0xCF and arrows 0xD0/0xD1 as
        // image verbs from Room 94, the sentence line (verb 0x64).
        Assert.Equal((0, 144), session.MainScreen);
        Assert.Equal(4, play.Var(ScummVars.VerbScript));
        var area = session.PlayVerbArea().Items;
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 }, area.Where(item => item.IsText && item.VerbId < 0x64).Select(item => item.VerbId).Order());
        Assert.Equal(Enumerable.Range(0xC8, 10), area.Where(item => item.VerbId is >= 0xC8 and <= 0xD1).Select(item => item.VerbId).Order());
        Assert.All(area.Where(item => item.VerbId is >= 0xC8 and <= 0xD1), item => Assert.Equal(94, item.ImageRoom));

        // 1. Compose a sentence and skip a cutscene: "Look at" then the sign (object 46): its verb 8 prints "`Welcome to Woodtick`" and opens a cutscene
        // with an override (+0x0028..+0x002F); Esc skips "`No Trezer huntin zone`" (+0x0034).
        play.ClickVerb(LookAt);
        Assert.Equal("Look at", play.Sentence);
        play.Hover(736, 28);
        Assert.Equal("Look at sign", play.Sentence);
        play.ClickRoom(736, 28);
        Assert.True(play.RunUntil(() => session.CutsceneSkippable, 400));
        Assert.Contains(session.Messages.OnScreen, message => message.Text.Contains("Welcome to Woodtick"));
        play.Log("sign's cutscene");
        session.PressKey(0x1B);
        Assert.True(play.RunUntil(() => play.Quiet, 600));
        Assert.DoesNotContain(session.SessionTrace, line => line.Contains("No Trezer"));
        Assert.Contains(session.SessionTrace, line => line.StartsWith("abortCutscene: Room 07 Object 46 Verb 0x08", StringComparison.Ordinal));

        // 2. Take an object: "Pick up" the sign. Its verb 9 (+0x000C) runs pickupObject 45 (the shovel): the inventory gets it and Script 9 draws its icon.
        play.ClickVerb(PickUp);
        play.Hover(736, 28);
        Assert.Equal("Pick up sign", play.Sentence);
        play.ClickRoom(736, 28);
        Assert.True(play.RunUntil(() => session.Snapshot.Inventory.Contains(45), 600));
        Assert.True(play.RunUntil(() => play.Quiet, 900));
        play.Log("shovel taken");
        Assert.Equal(new[] { 45, 49, 50, 51, 52, 53, 54, 55, 56 }, session.Snapshot.Inventory.Order());
        // Script 9 draws each held object's icon in a slot: the shovel's verb 0x5B names object 911 (0x038F) of Room 94.
        Assert.Contains(session.PlayVerbArea().Items, item => item.VerbId is >= 0xC8 and <= 0xCF && item.ImageObject == 911 && item.ImageRoom == 94);

        // 3. A dialogue: walking west, Largo stops Guybrush at the toll bridge (Room 7 local script 203, then 204). Script 14 becomes the input script and lists the
        // choices as verbs 0x78..0x7B; the route picks the first, "Is this some sort of bribe situation?" (204 +0x00BE). Largo shakes him upside down and local
        // script 206 takes the input for the second list; the route picks "Then who keeps up the law and maintains order?" (204 +0x0509). Largo takes the riches.
        play.ClickRoom(474, 66);
        Assert.True(play.RunUntil(() => play.Var(ScummVars.VerbScript) == 14 && play.Idle && session.Messages.OnScreen.Count == 0, 600));
        Assert.Equal("Is this some sort of bribe situation?", play.Glyphs(0x78));
        Assert.Equal(new[] { 0x78, 0x79, 0x7A, 0x7B }, session.PlayVerbArea().Items.Where(item => item.VerbId is >= 0x78 and <= 0x80).Select(item => item.VerbId));
        play.ClickVerb(0x78);
        Assert.True(play.RunUntil(() => play.Var(ScummVars.VerbScript) == 206 && play.Idle && session.Messages.OnScreen.Count == 0, 900));
        Assert.Equal("Then who keeps up the law and maintains order?", play.Glyphs(0x78));
        play.ClickVerb(0x78);
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(message => message.Text.Contains("Then who keeps up the law")), 120));
        Assert.True(play.RunUntil(() => play.Var(ScummVars.VerbScript) == 4 && play.Quiet, 1500));
        play.Log("robbed");
        Assert.Equal(new[] { 45 }, session.Snapshot.Inventory);

        // 4. Leave the room: the dinky door (41), whose verb 0x0B (Walk to, the default) loads Room 8 with the ego (+0x0023).
        for (var tries = 0; tries < 6 && 252 - session.ViewLeft is < 16 or > 303; tries++)
        {
            play.ClickRoom(session.ViewLeft + 10, 66);
            play.RunUntil(() => !session.EgoMoving && !session.CameraMoving, 600);
        }
        play.Hover(252, 56);
        Assert.Equal("Walk to door", play.Sentence);
        play.ClickRoom(252, 56);
        Assert.True(play.RunUntil(() => session.CurrentRoomId == 8 && play.Quiet, 900));
        play.Log("Room 8");
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal((8, 51, 112), (ego.Room, ego.X, ego.Y));

        // Every input was a click with mode 1 through Script 4, 14 or 206; no key but the Esc skip reached the game.
        Assert.All(session.DeliveredInput, input => Assert.Equal(1, input.Mode));
        foreach (var sound in session.SoundEvents) output.WriteLine($"sound {sound.Event.Kind} {sound.Event.TargetId} {sound.Event.SourceLabel} +0x{sound.Event.Offset:X4} {sound.Event.Certainty}");
    }

    /// <summary>R-7.16: a right click on the sign is <c>runInputScript(2, 0, 2)</c>; Script 4 +0x0005 takes the default verb in <c>var[0x09C]</c> (the sign's verb
    /// 0x5A sets 8) and the sentence script looks at it.</summary>
    [Fact]
    public void ARightClickOnTheSignRunsItsDefaultVerb()
    {
        if (Folder() is null) return;
        var play = AtWoodtick(out _);
        var session = play.Session;
        play.Hover(736, 28);
        var input = play.ClickRoom(736, 28, right: true);
        Assert.Equal((ScummSession.SceneClickArea, 0, 2), (input.Area, input.Value, input.Mode));
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(message => message.Text.Contains("Welcome to Woodtick")), 400));
    }

    /// <summary>The play screen composes Woodtick with the verbs, the inventory icons and the cross-hair at the pointer.</summary>
    [Fact]
    public void ThePlayScreenDrawsWoodtickWithTheVerbsAndTheCursor()
    {
        if (Folder() is null) return;
        var play = AtWoodtick(out var game);
        var session = play.Session;
        var screen = new ScummPlayScreen(game, session, game.V4Disks!.Disks.OfType<ScummV5Archive>().First().ReadIndexedObjects().States);
        play.Hover(736, 28);
        var frame = screen.Compose(0);
        Assert.Equal(7, frame.RoomId);
        Assert.True(frame.CursorShown);
        Assert.True(frame.Pixels.Take(ScummPlayFrame.Width * 144).Distinct().Count() > 16);
        Assert.True(frame.Pixels.Skip(ScummPlayFrame.Width * 152).Distinct().Count() > 3);
    }
}
