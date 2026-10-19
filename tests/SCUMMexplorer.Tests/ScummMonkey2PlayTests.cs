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
    private static string? Folder(string copy = "mi2 talkie en")
    {
        var path = Path.Combine(TestGames.Root, copy);
        return File.Exists(Path.Combine(path, "MONKEY2.000")) ? path : null;
    }

    /// <summary>
    /// S9 Monkey2 (18 October 2026): the texts of the route as each copy's own scripts write them (kept per copy, never chosen by language in code). The floppy
    /// releases (<c>mi2 mt32 en</c>, <c>mi2 sb16 en</c>, <c>mi2 classic es</c>) print the sign's two lines in one <c>printEgo</c> without a cutscene; the fan talkie
    /// patch wraps the second line in a cutscene with an override (read from the copy: <see cref="SignOpensCutscene"/>). Record:
    /// <c>.verification/monkey2-editions-20261018/README.md</c>.
    /// </summary>
    public sealed record RouteTexts(string LookAt, string LookAtSign, string PickUpSign, string Welcome, string SecondLine, string FirstChoice, string SecondChoice,
        string SecondChoiceSaid, string WalkToDoor);

    private static readonly RouteTexts EnglishTexts = new("Look at", "Look at sign", "Pick up sign", "Welcome to Woodtick", "No Trezer", "Is this some sort of bribe situation?",
        "Then who keeps up the law and maintains order?", "Then who keeps up the law", "Walk to door");
    // The two Spanish copies are different translations of the interface (the floppy's verb is "Mirar a", the talkie's "Mirar").
    private static readonly RouteTexts SpanishFloppyTexts = new("Mirar a", "Mirar a rótulo", "Coger rótulo", "Vienbenido a Woodtick", "Prohivido Vuskar", "¿Es esto un soborno?",
        "¿Entonces quién cuida de la ley y del orden público?", "cuida de la ley", "Ir a puerta");
    private static readonly RouteTexts SpanishTalkieTexts = SpanishFloppyTexts with { LookAt = "Mirar", LookAtSign = "Mirar rótulo" };

    private static readonly Dictionary<string, RouteTexts> RouteTextsByCopy = new()
    {
        ["mi2 talkie en"] = EnglishTexts, ["mi2 mt32 en"] = EnglishTexts, ["mi2 sb16 en"] = EnglishTexts,
        ["mi2 classic es"] = SpanishFloppyTexts, ["mi2 talkie es"] = SpanishTalkieTexts
    };

    public static TheoryData<string> Copies => new() { "mi2 talkie en", "mi2 mt32 en", "mi2 sb16 en", "mi2 classic es", "mi2 talkie es" };

    /// <summary>The sign's verb 8 code (Room 7 object 46) opens a cutscene in this copy's own data.</summary>
    private static bool SignOpensCutscene(GameLibrary game)
    {
        var data = ScummDataSources.For(game);
        var sign = data.ReadRoom(7).Objects.Single(item => item.Id == 46);
        return data.ReadObjectVerb(7, sign, LookAt, out _).Instructions.Any(item => item.Operation == "cutscene");
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
                // '@' is a filler the charset draws as nothing (the Spanish talkie pads its dialogue lines with it).
                if (glyphs[i] >= 0x20 && glyphs[i] != (byte)'@') shown.Add(glyphs[i]);
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
    private Player AtWoodtick(out GameLibrary game, string copy = "mi2 talkie en")
    {
        game = new GameFolderScanner().Scan(Folder(copy)!);
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

    [Theory]
    [MemberData(nameof(Copies))]
    public void TheAcceptanceRouteFromWoodtickRunsThroughTheGamesOwnInterface(string copy)
    {
        if (Folder(copy) is null) return;
        var texts = RouteTextsByCopy[copy];
        var play = AtWoodtick(out var game, copy);
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
        // with an override (+0x0028..+0x002F); Esc skips "`No Trezer huntin zone`" (+0x0034). The floppy releases print both lines in one printEgo (no cutscene):
        // the route then waits for both lines to be said.
        play.ClickVerb(LookAt);
        Assert.Equal(texts.LookAt, play.Sentence);
        play.Hover(736, 28);
        Assert.Equal(texts.LookAtSign, play.Sentence);
        play.ClickRoom(736, 28);
        if (SignOpensCutscene(game))
        {
            Assert.True(play.RunUntil(() => session.CutsceneSkippable, 400));
            Assert.Contains(session.Messages.OnScreen, message => message.Text.Contains(texts.Welcome));
            play.Log("sign's cutscene");
            session.PressKey(0x1B);
            Assert.True(play.RunUntil(() => play.Quiet, 600));
            Assert.DoesNotContain(session.SessionTrace, line => line.Contains(texts.SecondLine));
            Assert.Contains(session.SessionTrace, line => line.StartsWith("abortCutscene: Room 07 Object 46 Verb 0x08", StringComparison.Ordinal));
        }
        else
        {
            Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(message => message.Text.Contains(texts.Welcome)), 400));
            Assert.False(session.InCutscene);
            play.Log("sign's lines");
            Assert.True(play.RunUntil(() => play.Quiet, 900));
        }

        // 2. Take an object: "Pick up" the sign. Its verb 9 (+0x000C) runs pickupObject 45 (the shovel): the inventory gets it and Script 9 draws its icon.
        play.ClickVerb(PickUp);
        play.Hover(736, 28);
        Assert.Equal(texts.PickUpSign, play.Sentence);
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
        Assert.Equal(texts.FirstChoice, play.Glyphs(0x78));
        Assert.Equal(new[] { 0x78, 0x79, 0x7A, 0x7B }, session.PlayVerbArea().Items.Where(item => item.VerbId is >= 0x78 and <= 0x80).Select(item => item.VerbId));
        play.ClickVerb(0x78);
        Assert.True(play.RunUntil(() => play.Var(ScummVars.VerbScript) == 206 && play.Idle && session.Messages.OnScreen.Count == 0, 900));
        Assert.Equal(texts.SecondChoice, play.Glyphs(0x78));
        play.ClickVerb(0x78);
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(message => message.Text.Contains(texts.SecondChoiceSaid)), 120));
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
        Assert.Equal(texts.WalkToDoor, play.Sentence);
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
    [Theory]
    [MemberData(nameof(Copies))]
    public void ARightClickOnTheSignRunsItsDefaultVerb(string copy)
    {
        if (Folder(copy) is null) return;
        var play = AtWoodtick(out _, copy);
        var session = play.Session;
        play.Hover(736, 28);
        var input = play.ClickRoom(736, 28, right: true);
        Assert.Equal((ScummSession.SceneClickArea, 0, 2), (input.Area, input.Value, input.Mode));
        Assert.True(play.RunUntil(() => session.Messages.OnScreen.Any(message => message.Text.Contains(RouteTextsByCopy[copy].Welcome)), 400));
    }

    /// <summary>
    /// S9 accents (18 October 2026): every byte from 0x80 up that a copy's texts hold (string literals of every global, entry, exit, local and object-verb
    /// script, and the object names; text controls led by 0xFF or 0xFE and their operands skipped) has a glyph in each of the copy's own charsets, read as the
    /// play screen reads them. The Spanish copies write CP437 codes (0xA0 á, 0x82 é, 0xA1 í, 0xA2 ó, 0xA3 ú, 0xA4 ñ, 0xA8 ¿, 0xAD ¡); the play screen draws them
    /// as the data holds them, so no table.
    /// </summary>
    [Theory]
    [MemberData(nameof(Copies))]
    public void EveryTextByteAboveAsciiHasAGlyphInTheCopysCharsets(string copy)
    {
        if (Folder(copy) is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        var data = ScummDataSources.For(game);
        var found = new Dictionary<byte, int>();
        void Text(IReadOnlyList<byte> bytes)
        {
            for (var i = 0; i < bytes.Count; i++)
            {
                if (bytes[i] == 0) break;
                if (bytes[i] is 0xFF or 0xFE) { i += i + 1 < bytes.Count && bytes[i + 1] is 1 or 2 or 3 or 8 ? 1 : 3; continue; }
                if (bytes[i] >= 0x80) found[bytes[i]] = found.GetValueOrDefault(bytes[i]) + 1;
            }
        }
        var literal = new System.Text.RegularExpressions.Regex("\"((?:\\\\.|[^\"\\\\])*)\"");
        void Script(ScummScriptDisassembly? script)
        {
            if (script is null) return;
            foreach (var instruction in script.Instructions)
                foreach (System.Text.RegularExpressions.Match match in literal.Matches(instruction.Operands))
                    Text(ScummTextDecoder.ParseEscaped(match.Groups[1].Value));
        }
        foreach (var id in data.GlobalScriptIds()) Script(data.GlobalScript(id));
        foreach (var room in game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct())
        {
            ScummRoomData roomData;
            try { roomData = data.ReadRoom(room); } catch (Exception ex) when (ex is IOException or InvalidDataException) { continue; }
            Script(data.RoomEntryScript(room));
            Script(data.RoomExitScript(room));
            foreach (var id in data.RoomLocalScriptIds(room)) Script(data.RoomLocalScript(room, id));
            foreach (var name in roomData.ObjectNames.Values) Text(name);
            foreach (var (_, entries) in data.ReadObjectVerbs(room, roomData.Objects))
                foreach (var entry in entries.Where(e => !e.IsFallback))
                    Script(data.ReadObjectVerb(room, roomData.Objects.First(o => o.Id == entry.ObjectId), entry.VerbId, out _));
        }
        output.WriteLine($"{copy}: text bytes from 0x80: {string.Join(" ", found.OrderBy(p => p.Key).Select(p => $"{p.Key:X2}x{p.Value} '{ScummTextDecoder.DecodeGlyphs([p.Key])}'"))}");
        var charsets = Enumerable.Range(0, 8).Select(n => (n, charset: session.PlayCharset(n))).Where(p => p.charset is not null).ToArray();
        Assert.NotEmpty(charsets);
        var lacking = new List<string>();
        foreach (var (n, charset) in charsets)
        {
            var missing = found.Keys.Where(code => charset!.CharWidth(code) == 0).Order().Select(code => code.ToString("X2")).ToArray();
            output.WriteLine($"charset {n}: no glyph for [{string.Join(" ", missing)}]");
            lacking.AddRange(missing.Select(code => $"{n}:{code}"));
        }
        // Charsets 1, 2, 4 and 5 draw every accent of every copy; the rare 0x83 â (1 to 5 uses), 0x89 ë (2 to 4, Spanish) and 0x92 Æ (2, talkie patch only) lack a
        // glyph in some of them, and charsets 3, 6 and 7 lack most accents in every copy, English and Spanish alike: facts of the game's fonts (drawn as nothing,
        // width 0, as the original reads the width table), not of an edition.
        Assert.DoesNotContain(lacking, item => item[0] is '1' or '2' or '4' or '5' && item[2..] is not ("83" or "89" or "92"));
        if (copy.EndsWith(" es", StringComparison.Ordinal)) Assert.True(found.ContainsKey(0xA4) && found.ContainsKey(0xA8) && found.ContainsKey(0xAD), "the Spanish texts write ñ, ¿ and ¡");
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
