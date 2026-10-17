using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S4b Monkey2 (17 October 2026): the data-driven <see cref="ScummGameSession"/> boots Monkey Island 2: LeChuck's Revenge (SCUMM v5, <c>MONKEY2.000</c>/<c>.001</c>,
/// the conformance fixture <c>mi2 talkie en</c>) with the game's own scripts: Script 1 reads its settings (the missing <c>monkey2.cfg</c> leaves string 0x1B as
/// the script wrote it), sets the iMUSE volume through <c>soundKludge</c>, Script 128 skips the copy protection for boot parameter 0, Script 130 waits in Room 108
/// for the choice "Monkey Island 2" / "Monkey 2 Lite" (the explorer clicks the full game, labelled assumed input), then the opening (Rooms 87, 3, 103, 109, 5)
/// and the campfire on Scabb Island (Room 4) where the player has control. Record: <c>.verification/monkey2-session-20261017/README.md</c>. Skips without the data.
/// </summary>
public sealed class ScummMonkey2SessionTests
{
    private static readonly string[] Unsupported = ["unsupported", "not modeled", "could not", "disassembly stops", "undecoded", "cannot", "needs a session", "unknown", "depends on"];

    private static string? Folder()
    {
        var path = Path.Combine(TestGames.Root, "mi2 talkie en");
        return File.Exists(Path.Combine(path, "monkey2.000")) || File.Exists(Path.Combine(path, "MONKEY2.000")) ? path : null;
    }

    private static ScummGameSession Boot(string folder, out GameLibrary game)
    {
        game = new GameFolderScanner().Scan(folder);
        var data = (ScummV5DataSource)ScummDataSources.For(game);
        return new ScummGameSession(game, data.ReadIndexedObjects(), 0, null, data);
    }

    private static void AssertNothingUnsupported(ScummGameSession session) =>
        Assert.DoesNotContain(session.Snapshot.Uncertainties, item => Unsupported.Any(item.Contains) && !item.Contains("seeded pick"));

    [Fact]
    public void TheProfileGivesTheV5DialectTheChoiceClickAndTheV5Source()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameFamily.Monkey2, GameSupport.FamilyOf(game));
        var profile = ScummProfile.For(game);

        Assert.Equal(ScummScriptDialect.V5, profile.Dialect);
        Assert.Equal(ScummVersion.V5, profile.EngineVersion);
        Assert.True(profile.BootEndsWhenPlayerHasControl);
        // Script 131 +0x0067: object 0x484 is "Monkey Island 2" (Regular Mode), 0x485 "Monkey 2 Lite".
        Assert.Equal(new ScummBootInputPlan(null, ClickObject: 0x484), profile.BootInput);
        // GID_MONKEY's var[74] = 1225 is the CD release's only.
        Assert.DoesNotContain(profile.EngineVariablesAtStart, item => item.Variable == "var[0x04A]");

        var data = Assert.IsType<ScummV5DataSource>(ScummDataSources.For(game));
        Assert.NotNull(data.GlobalScript(1));
        Assert.NotNull(data.GlobalScript(130));
        var choice = data.ReadRoom(108);
        Assert.Contains(choice.Objects, item => item.Id == 0x484);
        // A v5 object stores an image per state (IM01..IM0F); the views take one per object, the one its state shows.
        Assert.Single(data.ReadObjectImages(108), image => image.Id == 1154);
        var states = new Dictionary<int, byte> { [1154] = 2 };
        Assert.NotEqual(data.ReadObjectImages(108).Single(i => i.Id == 1154).Pixels, data.ReadObjectImages(108, states).Single(i => i.Id == 1154).Pixels);
    }

    [Fact]
    public void TheBootTakesTheFullGameAndReachesTheCampfireWithThePlayerInControl()
    {
        if (Folder() is not { } folder) return;

        var session = Boot(folder, out _);
        var report = session.BootReport!;

        Assert.False(report.HitFrameLimit);
        Assert.InRange(report.Frames, 2500, 4500);
        Assert.Equal([108, 0, 87, 0, 3, 103, 109, 0, 5, 4], report.RoomsEntered);
        Assert.Equal(4, report.FinalRoom);
        Assert.True(session.PlayerHasControl);
        Assert.False(session.InCutscene);
        AssertNothingUnsupported(session);
        // The one assumed input: a click on "Monkey Island 2" through the game's input script (Script 131); no key is pressed.
        Assert.Single(session.SessionTrace, item => item.StartsWith("boot: Player input assumed", StringComparison.Ordinal));
        Assert.Contains(session.SessionTrace, item => item.StartsWith("boot: Player input assumed: a click on object 1156 in Room 108", StringComparison.Ordinal));
        Assert.Contains(session.SessionTrace, item => item.StartsWith("input: click in the room", StringComparison.Ordinal) && item.Contains("runs script 131 with [2, 0, 1]"));
        Assert.Equal(0, session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x062]"));
        // soundKludge answers are written to VAR_SOUNDRESULT and labelled where the song is steered.
        Assert.Contains(session.Snapshot.Uncertainties, item => item.Contains("soundKludge"));
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal(4, ego.Room);
        Assert.Equal(1, ego.Look.Costume);
    }

    [Fact]
    public void TheBootIsTheSameEveryRun()
    {
        if (Folder() is not { } folder) return;

        var first = Boot(folder, out _).BootReport!;
        var second = Boot(folder, out _).BootReport!;

        Assert.Equal(first.Frames, second.Frames);
        Assert.Equal(first.RoomsEntered, second.RoomsEntered);
    }

    [Fact]
    public void VersionFiveReadsTheRoomPaletteColourWithItsIndex()
    {
        // script_v5.cpp o5_roomOps SO_ROOM_PALETTE without GF_SMALL_HEADER: red, green, blue (words), a sub byte, the index (byte); then stopObjectCode.
        byte[] code = [0x33, 0x04, 0x10, 0x00, 0x20, 0x00, 0x30, 0x00, 0x00, 0x05, 0xA0];

        var v5 = new ScummScriptReader(ScummScriptDialect.V5).DecodeCode(code, 1, 1, 0);

        Assert.Equal("roomOps", v5.Instructions[0].Operation);
        Assert.StartsWith("setPalColor, 0x05", v5.Instructions[0].Operands);
        Assert.Equal(0x000B - 1, v5.Instructions[1].Offset);
        Assert.Equal("stopObjectCode", v5.Instructions[1].Operation);
    }

    [Fact]
    public void SoundKludgeAnswersFromTheSoundClockAndLabelsSteeredSongs()
    {
        // [0x10D, 5, 1, 0]: player command 13 (volume fade) for sound 5, which is not playing: -1 (findActivePlayer finds none), exact.
        // [0x0D, 5]: status of sound 5: 0. Then [0x10C, 5, 0, 1]: a hook for a song that is not playing: -1.
        var words = ScummBoundedScriptExecutor.SoundKludgeWords("[0x010D, 0x0005, var[0x010], 0x0000]",
            new Dictionary<string, ushort> { ["var[0x010]"] = 7 });
        Assert.Equal([0x010D, 5, 7, 0], words);
        Assert.Null(ScummBoundedScriptExecutor.SoundKludgeWords("[0x010D, var[0x011]]", new Dictionary<string, ushort>()));
        Assert.Equal([-1], ScummBoundedScriptExecutor.SoundKludgeWords("[0xFFFF]", new Dictionary<string, ushort>()));
    }
}
