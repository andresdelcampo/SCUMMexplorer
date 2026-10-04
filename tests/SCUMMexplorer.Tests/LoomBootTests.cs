using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The boot: script 001 loads the intro screen (Room 69), waits for bit 0xD6B,
/// sets the hooks, plays the opening cutscene (script 044) and chains to the
/// input loop (script 006).
/// </summary>
public sealed class LoomBootTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));

    [Fact]
    public void BootRunsPastTheIntroScreenAndTheOpeningCutscene()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));
        var snapshot = session.Snapshot;
        var variables = snapshot.KnownGlobalVariables;

        // The opening cutscene ends in Room 02 (script 044 +0x0536), and script 001 chains on (+0x0249).
        Assert.Equal(2, snapshot.RoomId);
        Assert.InRange(session.BootFrames, 1, LoomExplorationSession.BootFrameLimit - 1);
        Assert.Contains("Script 01 chains to script 6", session.SessionTrace);
        Assert.Contains("Script 01 continues at +0x0162 in Room 69 after loadRoom", session.SessionTrace);
        Assert.Contains("Script 44 continues at +0x03BC in Room 53 after loadRoom", session.SessionTrace);
        Assert.False(session.InCutscene);
        Assert.Null(session.LastAction);

        // Values script 001 sets after the intro wait (+0x016B to +0x01B1) and at its end (+0x0240).
        Assert.Equal((ushort)1, variables["bit[0xD6B]"]);
        Assert.Equal((ushort)5, variables["var[0x020]"]);
        Assert.Equal((ushort)14, variables["var[0x01D]"]);
        Assert.Equal((ushort)15, variables["var[0x01E]"]);
        Assert.Equal((ushort)2, variables["var[0x023]"]);
        Assert.Equal((ushort)3, variables["var[0x024]"]);
        Assert.Equal((ushort)1, variables["var[0x001]"]);
        Assert.Equal((ushort)1, variables["bit[0xD83]"]);
        Assert.Equal((ushort)0, variables["bit[0xD6E]"]);

        // The intro screen's answer is an assumption, and it says which code it stands for.
        Assert.Contains(snapshot.Uncertainties, line => line.StartsWith("Player input assumed: script 001 +0x0166 waits for bit[0xD6B], which only Room 69 local script 205 sets (+0x0048)", StringComparison.Ordinal) &&
                                                        line.Contains("local 203 +0x01D8, local 203 +0x0241", StringComparison.Ordinal));
    }

    [Fact]
    public void OpeningCutsceneStartsRoom01LocalsThroughALocalTarget()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));
        var snapshot = session.Snapshot;

        // Script 044 +0x01C8 and +0x0215: startScript local[0x001] with local[0x001] = 0xC8..0xD0
        // (getVarOrDirectByte). The second loop restarts each copy (runScript, not recursive).
        Assert.DoesNotContain(snapshot.Uncertainties, line => line.Contains("dynamic target", StringComparison.Ordinal));
        // Local 208's only limitation was its animateActor, which the actor table applies since M2b.
        Assert.DoesNotContain(snapshot.Uncertainties, line => line.StartsWith("Room 01 local script 208 +0x0000", StringComparison.Ordinal));
        Assert.Contains("script 208 started again: runScript ended its running copy first", session.SessionTrace);
        Assert.Contains("stopScript 200 at Script 44 +0x041B: ended its pending run", session.SessionTrace);
    }

    [Fact]
    public void OpeningPlayedFrameByFrameEndsAsTheBootDoes()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL"));
        var booted = new LoomExplorationSession(game, objects);
        var played = new LoomExplorationSession(game, objects, stopAtOpening: true);

        // Script 001 +0x0222 has just started script 044; the intro screen's answer is already assumed.
        Assert.True(played.BootRunning);
        // Script 044 opens no cutscene (no beginCutscene): script 001 +0x021D sets the in-a-cutscene bit itself.
        Assert.False(played.InCutscene);
        Assert.Equal((ushort)1, played.Snapshot.KnownGlobalVariables["bit[0xD6E]"]);
        Assert.InRange(played.BootFramesBeforeOpening, 1, booted.BootFrames - 1);
        Assert.Contains(played.Snapshot.Uncertainties, line => line.StartsWith("Player input assumed: script 001 +0x0166", StringComparison.Ordinal));

        var rooms = new List<int>();
        var lightsInRoom01 = new HashSet<int>();
        var frames = 0;
        while (played.BootRunning && frames++ < LoomExplorationSession.BootFrameLimit)
        {
            played.AdvanceFrame();
            if (played.Snapshot.RoomId is int room && (rooms.Count == 0 || rooms[^1] != room)) rooms.Add(room);
            if (played.Snapshot.RoomId == 1)
                foreach (var actor in played.Actors.Where(item => item.Room == 1 && item.Visible == true && item.Number is >= 1 and <= 9))
                    lightsInRoom01.Add(actor.Number);
        }

        Assert.False(played.BootRunning);
        Assert.Equal(booted.BootFrames, played.BootFrames);
        // Script 044's rooms (decoded): 01, 02, 01, 02, 53, 02. Rooms 69's screen came before the stop.
        Assert.Equal([1, 2, 1, 2, 53, 2], rooms);
        // Room 01 locals 200–208 each put one of actors 1–9 in the sky (their points of light).
        Assert.True(lightsInRoom01.Count >= 5, $"points of light seen: {string.Join(", ", lightsInRoom01)}");

        var egoBooted = booted.Actors.Single(item => item.Number == 1);
        var egoPlayed = played.Actors.Single(item => item.Number == 1);
        Assert.Equal((egoBooted.Room, egoBooted.X, egoBooted.Y, egoBooted.Look.Facing),
            (egoPlayed.Room, egoPlayed.X, egoPlayed.Y, egoPlayed.Look.Facing));
        Assert.Equal<(int?, int?, int?, int?)>((2, 828, 60, 270), (egoPlayed.Room, egoPlayed.X, egoPlayed.Y, egoPlayed.Look.Facing));
        Assert.Equal(booted.Snapshot.KnownGlobalVariables["var[0x071]"], played.Snapshot.KnownGlobalVariables["var[0x071]"]);
        Assert.Equal(booted.CameraX, played.CameraX);
        Assert.Equal(booted.Snapshot.Uncertainties, played.Snapshot.Uncertainties);
    }

    [Fact]
    public void EscapeSkipsTheOpeningAndBobbinFacesTheScreen()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")),
            stopAtOpening: true);

        var frames = 0;
        while (session.BootRunning && !session.CutsceneSkippable && frames++ < LoomExplorationSession.BootFrameLimit)
            session.AdvanceFrame();
        Assert.True(session.CutsceneSkippable);
        session.PressKey(0x1B);
        while (session.BootRunning && frames++ < LoomExplorationSession.BootFrameLimit)
            session.AdvanceFrame();

        Assert.False(session.BootRunning);
        Assert.Equal(2, session.Snapshot.RoomId);
        // Script 044 +0x063E: after an abort (var[0x005]) +0x0678 turns Bobbin to the screen (animateActor 1, 0xFA).
        var ego = session.Actors.Single(item => item.Number == 1);
        Assert.Equal<(int?, int?, int?, int?)>((2, 828, 60, 180), (ego.Room, ego.X, ego.Y, ego.Look.Facing));
    }

    [Fact]
    public void BootCanStopAtTheIntroScreenLoad()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")),
            completeBoot: false);

        Assert.Null(session.Snapshot.RoomId);
        Assert.Equal((ushort)0, session.Snapshot.KnownGlobalVariables["var[0x023]"]);
        Assert.Contains(session.Snapshot.Uncertainties, line => line.Contains("room transition awaits", StringComparison.Ordinal));
    }

    [Fact]
    public void NativeIntroAwaitsInputAndExecutesChallengeExpressions()
    {
        string folder = Environment.GetEnvironmentVariable("SCUMM_SOUND_TEST_ROOT") ?? Folder;
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")),
            stopAtBootInput: true);
        Assert.Equal(69, session.Snapshot.RoomId);
        Assert.Equal(200, session.Snapshot.KnownGlobalVariables["var[0x020]"]);
        Assert.Equal(0, session.Snapshot.KnownGlobalVariables["bit[0xD6B]"]);
        Assert.DoesNotContain(session.Snapshot.Uncertainties, line => line.Contains("as if the player", StringComparison.Ordinal));
        var target = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 69).FilePath)).Single(o => o.Id == 876);
        session.Click(target.X + target.Width / 2, target.Y + target.Height / 2);
        for (int i = 0; i < 32; i++) session.AdvanceFrame();
        Assert.All(Enumerable.Range(0, 4), i => Assert.InRange(session.Snapshot.KnownGlobalVariables[$"var[0x{0x52+i:X3}]"], (ushort)0, (ushort)11));
        Assert.Contains(session.Snapshot.RandomPicks, pick => pick.Offset == 0x0049 && pick.Max == 5);
        Assert.Equal(69, session.Snapshot.RoomId);
        Assert.Equal(0, session.Snapshot.KnownGlobalVariables["bit[0xD6B]"]);
    }
}
