using System.Diagnostics;

namespace SCUMMexplorer.Tests;

/// <summary>
/// R-6.48 (24 Oct 2026): hovering must stay responsive. The per-move work of the native game screen (the game's pointer, the scene, the hotspot list and the
/// frame) must not grow with the number of moves: thousands of moves in and out of an object keep the cost and the session's state bounded. The window half of
/// this is `.verification/hover-freeze-live-20261024/LiveHover` (a real MainWindow); this is its headless core.
/// </summary>
public sealed class HoverCostGuardTests
{
    [Fact]
    public void ManiacEnhancedHoveringInAndOutOfTheSignAfterTheIntroKeepsPerMoveWorkBounded()
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out var indexed);
        void Frames(int count) { for (var i = 0; i < count; i++) session.AdvanceFrame(); }
        void Until(Func<bool> ready, int limit = 2500) { for (var i = 0; i < limit && !ready(); i++) Frames(1); Assert.True(ready()); }
        // The user's steps: Play Game Intro, the two kids, start, skip the cutscene.
        foreach (var (x, y) in new[] { (76, 104), (196, 104), (292, 60) }) { Assert.NotNull(session.ClickScreen(x, y + session.MainScreen.Top, out _)); Frames(101); }
        Until(() => session.CurrentRoomId == 44 && session.CutsceneSkippable);
        session.PressKey(27);
        Until(() => session.PlayerHasControl);
        Frames(60);
        var screen = new ScummPlayScreen(game, session, indexed.States);
        // The sign; the room's exit to Room 1 (object 258, a strip at 0, 0) is a hotspot too since 2 November 2026.
        var sign = ScummRoomOverlays.Objects(screen.Scene()!, new Dictionary<int, int[]>()).Single(item => !item.IsDoorway && (item.X, item.Y) == (8, 80));
        Assert.Equal((8, 80), (sign.X, sign.Y));
        Assert.True(HoverCost(session, screen, sign.Y + 12 + session.MainScreen.Top, rounds: 8) < 4.0);
    }

    [Fact]
    public void ZakEnhancedBootHoveringAcrossTheRoomKeepsPerMoveWorkBounded()
    {
        var folder = Path.Combine(TestGames.Root, "zakE ega en");
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out var indexed);
        for (var i = 0; i < 300; i++) session.AdvanceFrame();
        Assert.True(HoverCost(session, new ScummPlayScreen(game, session, indexed.States), 60 + session.MainScreen.Top, rounds: 8) < 4.0);
    }

    [Fact]
    public void OnHoverRedrawsOnlyWhenWhatIsPointedAtChangesAndASceneCostsOneCapture()
    {
        // The table of Maniac enhanced's Room 44 (the sign, 8,80 32x24) and a doorway: thousands of moves change the key only on entering and leaving.
        var sign = new RoomHotspot(278, 1, "cartel", 8, 80, 32, 24, []);
        var door = new RoomHotspot(300, 2, null, 100, 40, 20, 40, [1]);
        IReadOnlyList<RoomHotspot> table = [sign, door];
        string? last = null; var changes = 0;
        for (var i = 0; i < 20000; i++)
        {
            var x = 4 + (int)(150 * (0.5 + 0.5 * Math.Sin(i * 0.002 * (1 + i / 4000))));
            var key = RoomHotspotRules.HoverKey(table, x, 90, objects: true, doorways: true);
            if (key != last) { changes++; last = key; }
        }
        Assert.InRange(changes, 4, 400);
        Assert.Equal("278|", RoomHotspotRules.HoverKey(table, 20, 90, true, true));
        Assert.Equal("|300", RoomHotspotRules.HoverKey(table, 110, 50, true, true));
        Assert.Equal("|", RoomHotspotRules.HoverKey(table, 20, 90, false, false));

        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out var indexed);
        for (var i = 0; i < 300; i++) session.AdvanceFrame();
        var screen = new ScummPlayScreen(game, session, indexed.States);
        screen.Scene();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 200; i++) screen.Scene();
        var perScene = (GC.GetAllocatedBytesForCurrentThread() - before) / 200;
        Assert.True(perScene < 400_000, $"a scene allocated {perScene} bytes");
    }

    // Sweeps the game pointer in and out across the room at several speeds; returns the last round's mean cost over the first round's (a bound of 4 allows noise).
    private static double HoverCost(ScummGameSession session, ScummPlayScreen screen, int rowY, int rounds)
    {
        var means = new List<double>();
        var traceBefore = session.SessionTrace.Count;
        for (var round = 0; round < rounds; round++)
        {
            var start = Stopwatch.GetTimestamp();
            for (var i = 0; i < 400; i++)
            {
                var speed = 0.03 * (1 + i / 70 % 6);
                var x = 4 + (int)(110 * (0.5 + 0.5 * Math.Sin(i * speed)));
                var y = rowY - 12 + (int)(24 * (0.5 + 0.5 * Math.Sin(i * speed * 0.37)));
                session.PointAtScreen((x, y));
                session.AdvanceFrame();
                var scene = screen.Scene();
                if (scene is not null) ScummRoomOverlays.Objects(scene, new Dictionary<int, int[]>());
                screen.Compose(i);
            }
            means.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / 400);
        }
        Assert.True(session.SessionTrace.Count - traceBefore < rounds * 400, "the session's trace must not grow by more than a line per move");
        return means[^1] / Math.Max(means[0], 0.05);
    }
}
