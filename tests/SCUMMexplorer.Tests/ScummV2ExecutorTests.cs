using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class ScummV2ExecutorTests
{
    [Fact]
    public void UnsignedComparisonDoesNotTakeTheV3SignedPath()
    {
        var runtime = Runtime();
        var run = Run(runtime,
            ("move", "var[0x050], 0xFFFF"),
            ("isGreater", "var[0x050], 0x0000, branch 1 -> 0x0003"),
            ("setState", "0x000A, IntrinsicOn"));
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(0, run.ObjectStates[10]);
    }

    [Fact]
    public void BitWritesAndIndirectArithmeticShareTheGlobalWords()
    {
        var runtime = Runtime();
        var run = Run(runtime,
            ("move", "var[0x050], 0x0060"),
            ("assignVarWordIndirect", "var[0x050], 0x0004"),
            ("addIndirect", "var[0x050], 0x0002"),
            ("setBitVar", "0x0600, 0x0003, 0x0001"),
            ("getBitVar", "var[0x051], 0x0600, 0x0003"));
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(14, run.KnownVariables["var[0x060]"]);
        Assert.Equal(1, run.KnownVariables["var[0x051]"]);
    }

    [Fact]
    public void ActorCoordinatesRoundTripThroughV2Units()
    {
        var runtime = Runtime();
        var run = Run(runtime,
            ("putActor", "0x01, 0x09, 0x34"),
            ("getActorX", "var[0x050], 0x01"),
            ("getActorY", "var[0x051], 0x01"));
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(72, runtime.Actors[1].X);
        Assert.Equal(104, runtime.Actors[1].Y);
        Assert.Equal(9, run.KnownVariables["var[0x050]"]);
        Assert.Equal(52, run.KnownVariables["var[0x051]"]);
    }

    [Fact]
    public void StateMaskRetainsTheOtherThreeFlags()
    {
        var runtime = Runtime();
        runtime.ObjectStates[10] = 15;
        var run = Run(runtime, ("setState", "0x000A, IntrinsicOff"));
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(7, run.ObjectStates[10]);
    }

    private static ScummScriptRuntimeState Runtime() => new(new Dictionary<int, byte> { [10] = 0 })
        { EngineVersion = ScummVersion.V2 };

    private static ScummBoundedScriptResult Run(ScummScriptRuntimeState runtime, params (string Op, string Args)[] ops)
    {
        var instructions = ops.Select((op, i) => new ScummScriptInstruction(i, 0, op.Op, op.Args, "00")).ToList();
        instructions.Add(new(ops.Length, 0, "stopObjectCode", "", "00"));
        var script = new ScummScriptDisassembly(1, 1, 0, instructions.Count, instructions.Count, instructions, null, null, [], []);
        return new ScummBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime);
    }
}
