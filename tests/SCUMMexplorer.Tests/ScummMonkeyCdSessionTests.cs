using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S4b MonkeyCd (17 October 2026): the data-driven <see cref="ScummGameSession"/> boots The Secret of Monkey Island CD (SCUMM v5, <c>MONKEY.000</c>/<c>MONKEY.001</c>
/// on the disc image, music on the disc's audio tracks) with the game's own scripts: no copy protection, the title credits (Room 10, waiting on the CD music
/// timer), the opening (Room 38) and the lookout (Room 38) where the player has control. Left alone, the lookout's music ends and the game goes on to the title
/// (Room 96) and the dock (Room 33). Record: <c>.verification/monkeycd-session-20261017/README.md</c>. Skips when the disc image is absent.
/// </summary>
public sealed class ScummMonkeyCdSessionTests
{
    private static readonly string[] Unsupported = ["unsupported", "not modeled", "could not", "disassembly stops", "undecoded", "cannot", "needs a session", "unknown", "depends on"];

    private static string? Folder()
    {
        var path = Path.Combine(TestGames.Root, "monkey1 vga en");
        return File.Exists(Path.Combine(path, "The_Secret_of_Monkey_Island.cue")) ? path : null;
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
    public void TheProfileGivesTheV5DialectEngineVariablesAndTheV5Source()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var profile = ScummProfile.For(game);

        Assert.Equal(ScummScriptDialect.V5, profile.Dialect);
        Assert.Equal(ScummVersion.V5, profile.EngineVersion);
        Assert.Null(profile.BootInput);
        // vars.cpp resetScummVars (v5): VAR_INPUTMODE 3, VAR_FADE_DELAY 3, VAR_V5_TALK_STRING_Y -0x50; GID_MONKEY: _scummVars[74] = 1225.
        Assert.Contains((ScummVars.InputMode, (ushort)3), profile.EngineVariablesAtStart);
        Assert.Contains((ScummVars.FadeDelay, (ushort)3), profile.EngineVariablesAtStart);
        Assert.Contains((ScummVars.TalkStringY, unchecked((ushort)-0x50)), profile.EngineVariablesAtStart);
        Assert.Contains(("var[0x04A]", (ushort)1225), profile.EngineVariablesAtStart);

        var data = Assert.IsType<ScummV5DataSource>(ScummDataSources.For(game));
        Assert.Equal(200, data.GlobalScriptCount);
        Assert.NotNull(data.GlobalScript(1));
        Assert.NotNull(data.RoomEntryScript(38));
        Assert.Contains(200, data.RoomLocalScriptIds(96));
        var dock = data.ReadRoom(33);
        Assert.NotNull(dock.WalkBoxes);
        Assert.Equal(5, dock.Objects.Count);
        Assert.NotEmpty(data.ReadObjectImages(33));
        Assert.NotEmpty(data.ReadIndexedObjects().States);
        Assert.NotNull(data.ReadCostume(1));
        Assert.Throws<InvalidDataException>(() => data.ReadRoom(250));
    }

    [Fact]
    public void TheBootReachesTheLookoutWithThePlayerInControlAndNothingUnsupported()
    {
        if (Folder() is not { } folder) return;

        var session = Boot(folder, out _);
        var report = session.BootReport!;

        Assert.False(report.HitFrameLimit);
        Assert.InRange(report.Frames, 1000, 3000);
        // No protection room: the cleared screen (0), the title credits (10), no room, the opening and lookout (38).
        Assert.Equal([0, 10, 0, 0, 38], report.RoomsEntered);
        Assert.Equal(38, report.FinalRoom);
        Assert.True(session.PlayerHasControl);
        Assert.False(session.InCutscene);
        Assert.DoesNotContain(session.SessionTrace, item => item.StartsWith("boot: Player input assumed", StringComparison.Ordinal));
        AssertNothingUnsupported(session);
        // The credits wait on VAR_MUSIC_TIMER, which counts jiffies from the start of the CD track (SoundCD::playCDTrack).
        Assert.Contains(session.SessionTrace, item => item.StartsWith("CD: sound 110 plays CD track 17", StringComparison.Ordinal));
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal(38, ego.Room);
        Assert.Equal(1, ego.Look.Costume);
        Assert.Equal(4, session.Snapshot.KnownGlobalVariables[ScummVars.VerbScript]);
        // The lookout's object 486 waits until the opening's track (sound 108) has ended before it loads the title room.
        Assert.Contains(session.PendingScriptSummary, item => item.StartsWith("Room 38 Object 486", StringComparison.Ordinal));
    }

    [Fact]
    public void LeftAloneTheLookoutsMusicEndsAndTheGameGoesOnToTheDock()
    {
        if (Folder() is not { } folder) return;
        var session = Boot(folder, out _);

        for (var frame = 0; frame < 5000 && session.CurrentRoomId != 33; frame++) session.AdvanceFrame();
        for (var frame = 0; frame < 900; frame++) session.AdvanceFrame();

        Assert.Equal(33, session.CurrentRoomId);
        Assert.Contains(session.SessionTrace, item => item.Contains("the session enters Room 96"));
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal(33, ego.Room);
        Assert.Equal((346, 133), (ego.X, ego.Y));
        AssertNothingUnsupported(session);
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
    public void VersionFiveReadsDrawObjectWithASubOpcodeAndPickupObjectWithARoom()
    {
        // script_v5.cpp: 0x05 o5_drawObject (object, sub-opcode 1 at x,y / 2 state / 0x1F nothing), 0x25 o5_pickupObject (object, room byte).
        // Bytes: drawObject 0x01E8 state 2; drawObject 0x01E8 neither; pickupObject 0x01E8 room 0; stopObjectCode.
        byte[] code = [0x05, 0xE8, 0x01, 0x02, 0x02, 0x00, 0x05, 0xE8, 0x01, 0x1F, 0x25, 0xE8, 0x01, 0x00, 0x00];

        var v5 = new ScummScriptReader(ScummScriptDialect.V5).DecodeCode(code, 1, 1, 0);
        var v4 = new ScummScriptReader(ScummScriptDialect.V4).DecodeCode(code, 1, 1, 0);

        Assert.Equal("0x01E8, state, 0x0002", v5.Instructions[0].Operands);
        Assert.Equal("0x01E8, default state and position", v5.Instructions[1].Operands);
        Assert.Equal(("pickupObject", "0x01E8, 0x00"), (v5.Instructions[2].Operation, v5.Instructions[2].Operands));
        Assert.Equal("drawObject", v4.Instructions[0].Operation);
        Assert.StartsWith("0x01E8, at", v4.Instructions[0].Operands);
    }
}
