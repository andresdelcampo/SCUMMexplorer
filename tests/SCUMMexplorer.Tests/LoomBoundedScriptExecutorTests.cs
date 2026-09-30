using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

public sealed class LoomBoundedScriptExecutorTests
{
    [Fact]
    public void ExecutedEstimatedSoundKeepsConditionalCertainty()
    {
        var script = Script(new(0, 0, "startSound", "0x13", "1C 13"), new(2, 0, "stopObjectCode", "", "00"));
        var run = new LoomBoundedScriptExecutor().Execute(script, new Dictionary<int, byte>(), [], pathEstimated: true);
        var sound = Assert.Single(run.Events, e => e.Kind == LoomScriptEventKind.SoundStarted);
        Assert.True(sound.Executed);
        Assert.Equal(LoomScriptEventCertainty.Conditional, sound.Certainty);
        Assert.Equal(19, sound.TargetId);
    }

    [Fact]
    public void SoundCollectedFromUnknownBranchIsNotExecuted()
    {
        var script = Script(
            new(0, 0, "isEqual", "var[0x123], 0x0001, branch +0x0003 -> 0x0008", "00 00 00 00 00"),
            new(5, 0, "startSound", "0x13", "1C 13 00"),
            new(8, 0, "stopObjectCode", "", "00"));
        var run = new LoomBoundedScriptExecutor().Execute(script, new Dictionary<int, byte>(), []);
        var sound = Assert.Single(run.Events, e => e.Kind == LoomScriptEventKind.SoundStarted);
        Assert.False(sound.Executed);
        Assert.Equal(LoomScriptEventCertainty.Conditional, sound.Certainty);
    }

    [Theory]
    [InlineData("isEqual", "local[0x000], 0x0000", true)]
    [InlineData("isEqual", "local[0x000], 0x0001", false)]
    [InlineData("isLess", "local[0x000], 0x0001", false)]
    [InlineData("isGreater", "local[0x000], 0x0001", true)]
    public void ConditionalFallsThroughWhenTrueAndBranchesWhenFalse(
        string operation, string operands, bool appliesState)
    {
        var script = Script(
            new(0, 0, operation, operands + ", branch +0x0005 -> 0x000A", "00 00 00 00 00"),
            new(5, 0, "setState", "0x000A, 0x0001", "00 00 00 00 00"),
            new(10, 0, "stopObjectCode", "", "00"));

        var run = new LoomBoundedScriptExecutor().Execute(script,
            new Dictionary<int, byte> { [10] = 0 }, Array.Empty<LoomRoomObjectRecord>());

        Assert.Equal(appliesState ? (byte)1 : (byte)0, run.ObjectStates[10]);
        Assert.True(run.ReachedStop);
        Assert.Null(run.UnresolvedReason);
    }

    [Fact]
    public void SetVarRangeWritesConsecutiveVariables()
    {
        // o5_setVarRange: values go to the result variable and the ones after it.
        var script = Script(
            new(0, 0, "setVarRange", "local[0x004], count=3, [0x0A, 0x0B, 0xFFF0]", "00 00 00 00 00"),
            new(5, 0, "move", "local[0x001] = 0x0001", "00 00 00 00 00"),
            new(10, 0, "isEqual", "local[0x004+local[0x001]], 0x000B, branch +0x0005 -> 0x0014", "00 00 00 00 00"),
            new(15, 0, "setState", "0x000A, 0x0001", "00 00 00 00 00"),
            new(20, 0, "isGreater", "local[0x006], 0x0000, branch +0x0005 -> 0x001E", "00 00 00 00 00"),
            new(25, 0, "setState", "0x000B, 0x0001", "00 00 00 00 00"),
            new(30, 0, "stopObjectCode", "", "00"));

        var run = new LoomBoundedScriptExecutor().Execute(script,
            new Dictionary<int, byte> { [10] = 0, [11] = 0 }, Array.Empty<LoomRoomObjectRecord>());

        Assert.Null(run.UnresolvedReason);
        Assert.Equal((byte)1, run.ObjectStates[10]);
        // 0xFFF0 is the signed word -16, so o5_isGreater (0 > -16) falls through.
        Assert.Equal((byte)1, run.ObjectStates[11]);
    }

    [Fact]
    public void RelationalComparisonUsesSignedWords()
    {
        var script = Script(
            new(0, 0, "move", "local[0x000] = 0xFFFF", "00 00 00 00 00"),
            new(5, 0, "isGreater", "local[0x000], 0x0000, branch +0x0005 -> 0x000F", "00 00 00 00 00"),
            new(10, 0, "setState", "0x000A, 0x0001", "00 00 00 00 00"),
            new(15, 0, "stopObjectCode", "", "00"));

        var run = new LoomBoundedScriptExecutor().Execute(script,
            new Dictionary<int, byte> { [10] = 0 }, Array.Empty<LoomRoomObjectRecord>());

        Assert.Equal((byte)1, run.ObjectStates[10]);
        Assert.True(run.ReachedStop);
        Assert.Null(run.UnresolvedReason);
    }

    [Theory]
    [InlineData("ifState", 1, true)]
    [InlineData("ifState", 0, false)]
    [InlineData("ifNotState", 1, false)]
    [InlineData("ifNotState", 0, true)]
    public void StateConditionUsesKnownObjectState(string operation, byte initialState, bool appliesState)
    {
        var script = Script(
            new(0, 0, operation, "0x000A, 0x01, branch +0x0005 -> 0x000A", "00 00 00 00 00"),
            new(5, 0, "setState", "0x000A, 0x02", "00 00 00 00 00"),
            new(10, 0, "stopObjectCode", "", "00"));

        var run = new LoomBoundedScriptExecutor().Execute(script,
            new Dictionary<int, byte> { [10] = initialState }, Array.Empty<LoomRoomObjectRecord>());

        Assert.Equal(appliesState ? (byte)2 : initialState, run.ObjectStates[10]);
        Assert.Null(run.UnresolvedReason);
    }

    [Fact]
    public void JumpUsesEncodedOffsetWithSparseDisassembly()
    {
        var script = Script(
            new(0, 0, "jumpRelative", "+2 -> 0x0005", "18 02 00"),
            new(5, 0, "stopObjectCode", "", "00"));

        var run = new LoomBoundedScriptExecutor().Execute(script,
            new Dictionary<int, byte>(), Array.Empty<LoomRoomObjectRecord>());

        Assert.True(run.ReachedStop);
        Assert.Null(run.UnresolvedReason);
    }

    [Fact]
    public void RuntimeCarriesGlobalsAndObjectStateAcrossFrameBoundary()
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte> { [10] = 0 });
        var script = Script(
            new(0, 0, "move", "var[0x002] = 0x0002", "00 00 00 00 00"),
            new(5, 0, "breakHere", "", "80"),
            new(6, 0, "setState", "0x000A, var[0x002]", "00 00 00 00 00"),
            new(11, 0, "stopObjectCode", "", "00"));
        var executor = new LoomBoundedScriptExecutor();

        var first = executor.Execute(script, runtime.ObjectStates, [], runtime: runtime);
        var second = executor.Execute(script, runtime.ObjectStates, [], runtime: runtime,
            startOffset: first.ResumeOffset!.Value);

        Assert.Equal(6, first.ResumeOffset);
        Assert.Equal((ushort)2, runtime.GlobalVariables["var[0x002]"]);
        Assert.Equal((byte)2, runtime.ObjectStates[10]);
        Assert.True(second.ReachedStop);
        Assert.Null(second.UnresolvedReason);
    }

    [Fact]
    public void DrawObjectScalesSmallHeaderCoordinatesAndClearsPeer()
    {
        var records = new[]
        {
            new LoomRoomObjectRecord(10, 0, 0, 0, 8, 8, 0, 0, 0, 0, 0, 0, null),
            new LoomRoomObjectRecord(11, 1, 16, 24, 8, 8, 0, 0, 0, 0, 0, 0, null)
        };
        var script = Script(
            new(0, 0, "drawObject", "0x000A, at, 0x0002, 0x0003", "25 0A 00 02 00 03 00"),
            new(7, 0, "stopObjectCode", "", "00"));

        var run = new LoomBoundedScriptExecutor().Execute(script,
            new Dictionary<int, byte> { [10] = 0, [11] = 1 }, records);

        Assert.Null(run.UnresolvedReason);
        Assert.Equal(new RoomObjectPosition(16, 24), run.ObjectPositions[10]);
        Assert.Equal((byte)1, run.ObjectStates[10]);
        Assert.Equal((byte)0, run.ObjectStates[11]);
    }

    [Fact]
    public void GlobalWordIncrementRefreshesBitAliases()
    {
        var script = Script(
            new(0, 0, "move", "var[0x002] = 0x0001", "00 00 00 00 00"),
            new(5, 0, "increment", "var[0x002]", "00 00 00"),
            new(8, 0, "isEqual", "bit[0x021], 0x0001, branch +0x0005 -> 0x0012", "00 00 00 00 00"),
            new(13, 0, "setState", "0x000A, 0x0001", "00 00 00 00 00"),
            new(18, 0, "stopObjectCode", "", "00"));

        var run = new LoomBoundedScriptExecutor().Execute(script,
            new Dictionary<int, byte> { [10] = 0 }, []);

        Assert.Null(run.UnresolvedReason);
        Assert.Equal((byte)1, run.ObjectStates[10]);
        Assert.Equal((ushort)0, run.KnownVariables["bit[0x020]"]);
        Assert.Equal((ushort)1, run.KnownVariables["bit[0x021]"]);
    }

    [Fact]
    public void ZeroInitializedBitSelectsTheReachedRoomLoad()
    {
        var script = Script(
            new(0, 0, "notEqualZero", "bit[0xD7B], branch +0x000B -> 0x0010", "A8 7B 8D 0B 00"),
            new(5, 0, "loadRoomWithEgo", "0x02D1, 0x32, walkTo=(300, 134)", "24 D1 02 32 2C 01 86 00"),
            new(13, 0, "jumpRelative", "+8 -> 0x0018", "18 08 00"),
            new(16, 0, "loadRoomWithEgo", "0x00C9, 0x05, walkTo=(160, 120)", "24 C9 00 05 A0 00 78 00"),
            new(24, 0, "stopObjectCode", "", "00"));
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        var executor = new LoomBoundedScriptExecutor();

        var first = executor.Execute(script, runtime.ObjectStates, [], runtime: runtime);
        Assert.Equal(5, Assert.Single(first.Events, item => item.Kind == LoomScriptEventKind.RoomLoaded).TargetId);

        runtime.GlobalVariables["var[0x0D7]"] = 1 << 11;
        var second = executor.Execute(script, runtime.ObjectStates, [], runtime: runtime);
        Assert.Equal(50, Assert.Single(second.Events, item => item.Kind == LoomScriptEventKind.RoomLoaded).TargetId);
    }

    [Fact]
    public void StopScriptZeroEndsTheCurrentScript()
    {
        var script = Script(
            new(0, 0, "stopScript", "0x00", "62 00"),
            new(2, 0, "setState", "0x000A, 0x0001", "00 00 00 00"),
            new(6, 0, "stopObjectCode", "", "00"));

        var run = new LoomBoundedScriptExecutor().Execute(script,
            new Dictionary<int, byte> { [10] = 0 }, Array.Empty<LoomRoomObjectRecord>());

        Assert.True(run.ReachedStop);
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(0, run.ObjectStates[10]);
    }

    [Fact]
    public void StopScriptOfAnotherScriptNeedsASessionToApplyIt()
    {
        var script = Script(
            new(0, 0, "stopScript", "0x2A", "62 2A"),
            new(2, 0, "stopObjectCode", "", "00"));
        var executor = new LoomBoundedScriptExecutor();

        var alone = executor.Execute(script, new Dictionary<int, byte>(), Array.Empty<LoomRoomObjectRecord>());
        Assert.Contains("needs a session", alone.UnresolvedReason, StringComparison.Ordinal);

        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        LoomScriptEvent? stopped = null;
        var handled = executor.Execute(script, runtime.ObjectStates, [], runtime: runtime,
            onEvent: (scriptEvent, _) => { stopped = scriptEvent; return ""; });

        Assert.True(handled.ReachedStop);
        Assert.Equal(LoomScriptEventKind.ScriptStopped, stopped?.Kind);
        Assert.Equal(0x2A, stopped?.TargetId);
    }

    private static LoomScriptDisassembly Script(params LoomScriptInstruction[] instructions) =>
        new(1, 1, 0, 32, 16, instructions, null, null, [], []);
}
