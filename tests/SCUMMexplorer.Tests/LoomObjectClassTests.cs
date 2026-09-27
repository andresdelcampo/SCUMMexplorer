using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Object classes after ScummVM's small-header rules: <c>putClass</c>,
/// <c>o5_ifClassOfIs</c>, and class 24 as kObjectClassUntouchable, which
/// <c>findObject</c> skips.
/// </summary>
public sealed class LoomObjectClassTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    [Theory]
    [InlineData("[0x0098]", 0u, true)]           // 0x80|24 sets class 24
    [InlineData("[0x0018]", 1u << 23, false)]    // 24 clears it
    [InlineData("[0x0000, 0x0098]", 0xFFu, true)] // 0 clears every class first
    public void SetClassFollowsPutClass(string classes, uint initial, bool untouchable)
    {
        var script = Script(
            new(0, 0, "setClass", $"0x0190, {classes}", "5D 90 01 98 FF"),
            new(5, 0, "stopObjectCode", "", "00"));
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>(), null,
            new Dictionary<int, uint> { [0x190] = initial });

        var run = new LoomBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime);

        Assert.True(run.ReachedStop);
        Assert.Equal(untouchable, LoomObjectClasses.Has(runtime.ObjectClasses[0x190], LoomObjectClasses.Untouchable));
        if (classes.StartsWith("[0x0000", StringComparison.Ordinal))
            Assert.Equal(1u << 23, runtime.ObjectClasses[0x190]);
    }

    [Theory]
    [InlineData("[0x0098]", true)]
    [InlineData("[0x0018]", false)]
    [InlineData("[0x0098, 0x0003]", true)]
    [InlineData("[0x0098, 0x0083]", false)]
    public void IfClassOfIsIsTrueOnlyWhenEveryListedClassMatches(string classes, bool fallsThrough)
    {
        var script = Script(
            new(0, 0, "ifClassOfIs", $"0x0190, {classes}, branch +0x0005 -> 0x000C", "00 00 00 00 00 00 00"),
            new(7, 0, "setState", "0x000A, 0x0001", "00 00 00 00 00"),
            new(12, 0, "stopObjectCode", "", "00"));
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte> { [10] = 0 }, null,
            new Dictionary<int, uint> { [0x190] = 1u << 23 });

        var run = new LoomBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime);

        Assert.Null(run.UnresolvedReason);
        Assert.Equal(fallsThrough ? (byte)1 : (byte)0, run.ObjectStates[10]);
    }

    [Fact]
    public void ClassTestWithoutKnownClassesStaysUnresolved()
    {
        var script = Script(
            new(0, 0, "ifClassOfIs", "0x0190, [0x0098], branch +0x0005 -> 0x000C", "00 00 00 00 00 00 00"),
            new(7, 0, "setState", "0x000A, 0x0001", "00 00 00 00 00"),
            new(12, 0, "stopObjectCode", "", "00"));

        var run = new LoomBoundedScriptExecutor().Execute(script, new Dictionary<int, byte> { [10] = 0 }, []);

        Assert.Contains("classes", run.UnresolvedReason, StringComparison.Ordinal);
    }

    [Fact]
    public void IndexClassBytesAreRead()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var snapshot = new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL"));

        Assert.NotNull(snapshot.Classes);
        Assert.Equal(snapshot.States.Count, snapshot.Classes!.Count);
        Assert.Contains(snapshot.Classes.Values, bits => bits != 0);
    }

    [Fact]
    public void Room22CatalogVisitMakesBothForestEdgeExitsUntouchable()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));

        var snapshot = session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 22));

        // Entry +0x00F9 starts local 212 when var[0x071] (the object clicked
        // to arrive) matches no known exit; 212 +0x002F/+0x003D set class 24
        // on 400 (to Room 17) and 402 (to Room 18), and +0x0021 clears it on 411.
        Assert.Contains(session.LastVisitEvents, item => item.Kind == LoomScriptEventKind.ScriptStarted && item.TargetId == 0xD4);
        Assert.True(snapshot.IsUntouchable(400));
        Assert.True(snapshot.IsUntouchable(402));
        Assert.False(snapshot.IsUntouchable(411));
    }

    private static LoomScriptDisassembly Script(params LoomScriptInstruction[] instructions) =>
        new(1, 1, 0, 32, 32, instructions, null, null, [], []);
}
