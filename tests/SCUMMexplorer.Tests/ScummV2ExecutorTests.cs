using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class ScummV2ExecutorTests
{
    [Fact]
    public void PickupPreservesStateFlagsAndDoesNotLaunchAnInventoryScript()
    {
        var runtime = Runtime();
        runtime.GlobalVariables[ScummVars.V2.Ego] = 3;
        runtime.ObjectOwners[10] = 15;
        runtime.ObjectStates[10] = 5;
        var script = new ScummScriptDisassembly(1, 1, 0, 2, 2,
            [new(0, 0, "pickupObject", "0x000A", "00"), new(1, 0, "stopObjectCode", "", "00")], null, null, [], []);
        var run = new ScummBoundedScriptExecutor().Execute(script, runtime.ObjectStates,
            [new(10, 1, 0, 0, 8, 8, 0, 0, 0, 0, 0, 0, null)], runtime: runtime);
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(15, runtime.ObjectStates[10]);
        Assert.Equal(3, runtime.ObjectOwners[10]);
        Assert.DoesNotContain(run.Events, e => e.Kind == ScummScriptEventKind.ScriptStarted);
    }
    [Fact]
    public void VerbSetupUsesTheExplicitSlotAndDynamicDelete()
    {
        var runtime = Runtime();
        var created = Run(runtime, ("verbOps", "0x03, [position 0x01, 0x14; slot 0x02; preposition 0x01; name, \"Open\"]"));
        Assert.Null(created.UnresolvedReason);
        var verb = Assert.Single(runtime.Verbs.Snapshot(), v => v.VerbId == 3);
        Assert.Equal(3, verb.Slot);
        Assert.Equal(8, verb.X);
        Assert.Equal(160, verb.Y);
        var deleted = Run(runtime, ("move", "var[0x050], 0x02"), ("verbOps", "0x00, [delete var[0x050]]"));
        Assert.Null(deleted.UnresolvedReason);
        Assert.DoesNotContain(runtime.Verbs.Snapshot(), v => v.VerbId == 3);
    }
    [Fact]
    public void PrintNormalizesOldTextAndPreservesNativeCharacterCount()
    {
        var runtime = Runtime();
        var run = Run(runtime, ("print", "0xFF, [text \"hello\"]"));
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(5, run.KnownVariables[ScummVars.V2.CharCount]);
        Assert.Equal(5, runtime.GlobalVariables[ScummVars.V2.CharCount]);
    }
    [Theory]
    [InlineData("Unpickupable", 0, 0)]
    [InlineData("Unpickupable", 1, 9)]
    [InlineData("Pickupable", 1, 1)]
    [InlineData("Pickupable", 0, 8)]
    public void StateBranchJumpsWhenTheNamedFlagMatches(string condition, byte initial, byte expected)
    {
        var runtime = Runtime();
        runtime.ObjectStates[10] = initial;
        var run = Run(runtime, ("ifState", $"0x000A, {condition}, branch 1 -> 0x0002"), ("setState", "0x000A, IntrinsicOn"));
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(expected, run.ObjectStates[10]);
    }
    [Fact]
    public void RangeOperandsKeepTheirByteValues()
    {
        var run = Run(Runtime(), ("setVarRange", "var[0x0EA], [0x00, 0x01]"));
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(0, run.KnownVariables["var[0x0EA]"]);
        Assert.Equal(1, run.KnownVariables["var[0x0EB]"]);
    }

    [Theory]
    [InlineData(250, "v2StartObject", 253, 1, 1)]
    [InlineData(253, "v2StartObject", 253, 0, 1)]
    [InlineData(254, "v2StopObject", 254, 0, 0)]
    [InlineData(13, "v2StartObject", 13, 0, 0)]
    public void ImmediateSentencePreservesSpecialDispatchFlags(int verb, string operation, int effectiveVerb, int background, int special)
    {
        var runtime = Runtime();
        var run = Run(runtime, ("doSentence", $"0x{verb:X2}, 0x002A, 0x0000, sub=0x01"));
        var call = Assert.Single(run.Events, e => e.Kind == ScummScriptEventKind.ObjectScriptStarted);
        Assert.Equal(operation, call.Operation);
        Assert.Equal(new int?[] { 42, effectiveVerb, background, special }, call.ResolvedArguments);
    }

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
