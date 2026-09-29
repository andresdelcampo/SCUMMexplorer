using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// ScummVM <c>scummLoop(delta)</c> for v3: the engine timers go up by the frame's jiffies (<c>VAR_TIMER_NEXT</c>),
/// and <c>decreaseScriptDelay</c> counts a <c>delay</c>/<c>delayVariable</c> down before the scripts run; the slot
/// runs again on the frame its delay falls below 0.
/// </summary>
public sealed class LoomFrameTimingTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    private static LoomExplorationSession? Start()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return null;
        var game = new GameFolderScanner().Scan(Folder);
        return new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));
    }

    private static ushort? Var(LoomExplorationSession session, string name) =>
        session.Snapshot.KnownGlobalVariables.TryGetValue(name, out var value) ? value : null;

    // A synthetic script: the delay, then var[0x0F0] = 1 (no game code uses var[0x0F0]).
    private static LoomScriptDisassembly Delayed(LoomScriptInstruction delay) =>
        new(250, 1, 0, 32, 16,
            [delay, new(delay.Offset + 4, 0x1A, "move", "var[0x0F0] = 0x0001", "1A F0 00 01 00"), new(delay.Offset + 9, 0, "stopObjectCode", "", "00")],
            null, null, [], [], "Synthetic test script");

    private static int FramesUntilSet(LoomExplorationSession session)
    {
        for (var frame = 1; frame <= 100; frame++)
        {
            session.AdvanceFrame();
            if (Var(session, "var[0x0F0]") == 1) return frame;
        }
        return -1;
    }

    [Fact]
    public void ADelayOf60JiffiesRunsAgainOnTheEleventhFrame()
    {
        if (Start() is not { } session) return;
        Assert.Equal(6, session.FrameJiffies);
        session.RunTopLevelScript(Delayed(new(0, 0x2E, "delay", "0x00003C", "2E 3C 00 00")));
        Assert.NotEqual((ushort?)1, Var(session, "var[0x0F0]"));
        // 60 − 6k < 0 first at k = 11: the script goes on at the start of the 11th frame, 66 jiffies later.
        Assert.Equal(11, FramesUntilSet(session));
    }

    [Fact]
    public void ADelayVariableReadsItsVariable()
    {
        if (Start() is not { } session) return;
        session.RunTopLevelScript(new LoomScriptDisassembly(250, 1, 0, 32, 16,
            [new(0, 0x1A, "move", "var[0x0F1] = 0x000D", "1A F1 00 0D 00"), new(5, 0, "stopObjectCode", "", "00")],
            null, null, [], [], "Synthetic test script"));
        session.RunTopLevelScript(Delayed(new(0, 0x2B, "delayVariable", "var[0x0F1]", "2B F1 00 00")));
        // 13 jiffies: 7, 1, then −5 on the third frame.
        Assert.Equal(3, FramesUntilSet(session));
    }

    [Fact]
    public void TheEngineTimersCountTheFramesJiffies()
    {
        if (Start() is not { } session) return;
        var before = (Var(session, "var[0x00B]"), Var(session, "var[0x00D]"), Var(session, "var[0x02F]"));
        Assert.NotNull(before.Item1);
        Assert.NotNull(before.Item2);
        Assert.NotNull(before.Item3);
        for (var frame = 0; frame < 5; frame++) session.AdvanceFrame();
        // VAR_TIMER is the last frame's jiffies; VAR_TMR_3 and VAR_TIMER_TOTAL go up by 6 a frame.
        Assert.Equal((ushort?)6, Var(session, "var[0x02E]"));
        Assert.Equal((ushort?)(before.Item2 + 30), Var(session, "var[0x00D]"));
        Assert.Equal((ushort?)(before.Item3 + 30), Var(session, "var[0x02F]"));
    }
}
