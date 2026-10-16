using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S6 Play for the 256-colour Indiana Jones and the Last Crusade (16 October 2026; REQUIREMENTS §7.1, Proposed R-7.7 to R-7.18): the R-7.18 route of
/// <see cref="ScummIndy3PlayTests"/> played headless from Room 73 of this release, and the game screen composed with its 256-colour room palette.
/// The play profile comes from the format and dialect (version 3 engine, Indy3 dialect), not from a family test. Record:
/// <c>.verification/indy3vga-play-20261016/README.md</c>. Skips when the game folder is absent (<c>SCUMM_INDY3_VGA_TEST_ROOT</c>).
/// </summary>
public sealed class ScummIndy3VgaPlayTests(ITestOutputHelper output)
{
    [Fact]
    public void TheProfilePlaysThroughTheVerbsWithTheVersionThreeClickAndTheSameKeys()
    {
        if (ScummIndy3VgaSessionTests.Folder() is not { } folder) return;
        var game = new SCUMMexplorer.Core.Discovery.GameFolderScanner().Scan(folder);
        var profile = ScummProfile.For(game);

        Assert.True(profile.PlaysThroughVerbs);
        // checkExecVerbs: mode is 0 while _game.version <= 3; ScummVM runs this release as version 3 although its blocks are v4-style.
        Assert.Equal(0, profile.ClickInputMode);
        // R-7.16 (16 Oct): a version 3 game's scripts never see a right click, so the right button stays the app's.
        Assert.False(profile.RightClickReachesGame);
        // Script 24 is the same instructions as EGA: F5 (319) chains to the save/load Script 9.
        Assert.Equal([0x13F], profile.UndeliveredKeys);
    }

    [Fact]
    public void TheAcceptanceRouteFromRoom73RunsThroughTheGamesOwnInterface()
    {
        if (ScummIndy3VgaSessionTests.Folder() is not { } folder) return;
        var session = ScummIndy3VgaSessionTests.Boot(folder, out var game);
        Assert.NotNull(session.PlayCharset(0));

        ScummIndy3PlayTests.RunAcceptanceRoute(game, session, output);

        Assert.NotEmpty(session.DeliveredInput);
        Assert.All(session.DeliveredInput.Where(input => input.Area != ScummSession.KeyClickArea), input => Assert.Equal(0, input.Mode));
    }

    [Fact]
    public void TheGameScreenUsesTheRoomPaletteForVerbsMessagesAndTheCrosshair()
    {
        if (ScummIndy3VgaSessionTests.Folder() is not { } folder) return;
        var session = ScummIndy3VgaSessionTests.Boot(folder, out var game);
        var data = (ScummV4RoomFileDataSource)ScummDataSources.For(game);
        var screen = new ScummPlayScreen(game, session, data.ReadIndexedObjects().States);
        session.AdvanceFrame();

        var scene = screen.Scene();
        Assert.NotNull(scene);
        Assert.Equal(73, scene.RoomId);
        Assert.Equal(256, scene.Room.Palette.Count);
        var palette = scene.Room.Palette;

        // drawVerb: the verb's colour is a raw index into the 256-colour palette (no remapping; verbs.cpp, charset.cpp CharsetRendererV3::setColor with
        // GF_OLD256 keeps colour & 0x7F). Script 96 gives the twelve verbs colour 2 and the sentence line colour 3.
        var frame = screen.Compose(0);
        var verbs = frame.Verbs;
        var pickUp = verbs.Items.Single(item => item.VerbId == 0x0B);
        var verbPixels = Enumerable.Range(0, verbs.Height).SelectMany(y => Enumerable.Range(0, verbs.Width).Select(x => (x, y)))
            .Where(p => p.x >= pickUp.X && p.x < pickUp.X + 40 && p.y >= pickUp.Y - ScummVerbArea.Top && p.y < pickUp.Y - ScummVerbArea.Top + 8)
            .Select(p => frame.Pixels[(ScummVerbArea.Top + p.y) * ScummPlayFrame.Width + p.x]).Distinct().ToArray();
        Assert.Contains(palette[2] | 0xFF000000u, verbPixels);

        // setBuiltinCursor: colours 15, 15, 7, 8 as raw palette indices (default_cursor_colors), one step every two frames.
        session.PointAtScreen((160, 80));
        var crosshair = new[] { 0, 2, 4, 6 }.Select(step => screen.Compose(step).Pixels[80 * ScummPlayFrame.Width + 160 + 6]).ToArray();
        Assert.Equal([palette[15] | 0xFF000000u, palette[15] | 0xFF000000u, palette[7] | 0xFF000000u, palette[8] | 0xFF000000u], crosshair);
        output.WriteLine($"verb colours {string.Join(" ", verbPixels.Select(c => c.ToString("X8")))}; crosshair {string.Join(" ", crosshair.Select(c => c.ToString("X8")))}");
    }

    [Fact]
    public void TheLightSwitchDarkensTheWholeScreenThroughTheShadowPalette()
    {
        if (ScummIndy3VgaSessionTests.Folder() is not { } folder) return;
        var session = ScummIndy3VgaSessionTests.Boot(folder, out var game);
        var data = (ScummV4RoomFileDataSource)ScummDataSources.For(game);
        var screen = new ScummPlayScreen(game, session, data.ReadIndexedObjects().States);
        void Frames(int count) { for (var i = 0; i < count; i++) session.AdvanceFrame(); }
        bool Until(Func<bool> condition, int limit)
        {
            for (var i = 0; i < limit && !condition(); i++) session.AdvanceFrame();
            return condition();
        }
        void Verb(int id)
        {
            var item = session.PlayVerbArea().Items.Single(entry => entry.VerbId == id);
            Assert.NotNull(session.ClickScreen(item.X + 1, item.Y + 1, out _));
            Frames(2);
        }
        void Room(int x, int y)
        {
            Assert.NotNull(session.ClickScreen(x - session.ViewLeft, y + session.MainScreen.Top, out _));
            Frames(2);
        }

        // "Close" the door (object 830), then "Push" the switch (object 837, a child of the closed door). In this release the switch's code (Room 73
        // object 837 verb 0x06 +0x0004 to +0x0244) darkens the gym with roomOps roomPalette loops, not with lights: slots 0x00-0x0E show 0xB0-0xBE, ...
        Assert.True(Until(() => session.UserInputOn && !session.InCutscene, 300));
        Verb(0x02);
        Room(56, 60);
        Room(56, 60);
        Assert.True(Until(() => session.Snapshot.ObjectStates.GetValueOrDefault(830) == 0 && session.UserInputOn && !session.InCutscene, 600), "the door closes");
        Verb(0x06);
        Room(24, 92);
        Room(24, 92);
        Assert.True(Until(() => session.InCutscene && session.CutsceneSkippable, 600), "the switch's cutscene reaches its override point");

        // updatePalette with _shadowPalRemap: the verb screen's colour 2 is shown as colour 0xB2 like the room's.
        var scene = screen.Scene()!;
        var palette = scene.Room.Palette;
        var frame = screen.Compose(0);
        var verbs = frame.Verbs;
        var push = verbs.Items.Single(item => item.VerbId == 0x06);
        var shown = Enumerable.Range(push.Y, 8).SelectMany(y => Enumerable.Range(push.X, 24).Select(x => frame.Pixels[y * ScummPlayFrame.Width + x])).Distinct().ToArray();
        Assert.Contains(palette[0xB2] | 0xFF000000u, shown);
        Assert.DoesNotContain(palette[2] | 0xFF000000u, shown);
    }
}
