using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class ScummV2ClosestTests
{
    private static ScummScriptRuntimeState Runtime() => new(new Dictionary<int, byte>())
        { EngineVersion = ScummVersion.V2, CurrentRoom = 1 };

    private static ScummBoundedScriptResult Run(ScummScriptRuntimeState runtime, params (string Op, string Args)[] ops)
    {
        var instructions = ops.Select((op, i) => new ScummScriptInstruction(i, 0, op.Op,
            string.Join(", ", op.Args.Split(',', StringSplitOptions.TrimEntries).Select(token =>
                ushort.TryParse(token, out var number) ? $"0x{number:X4}" : token)), "00")).ToList();
        instructions.Add(new(ops.Length, 0, "stopObjectCode", "", "00"));
        return new ScummBoundedScriptExecutor().Execute(new(1, 1, 0, instructions.Count, instructions.Count,
            instructions, null, null, [], []), runtime.ObjectStates, [], runtime: runtime);
    }

    private static void Range(ScummScriptRuntimeState runtime, ushort min, ushort max)
    { runtime.GlobalVariables["var[0x013]"] = min; runtime.GlobalVariables["var[0x014]"] = max; }

    [Fact]
    public void DescendingTieChoosesHighestActorUsingOldCoordinates()
    {
        var runtime = Runtime(); Range(runtime, 2, 3);
        var run = Run(runtime, ("putActorInRoom", "1, 1"), ("putActorInRoom", "2, 1"), ("putActorInRoom", "3, 1"),
            ("putActor", "1, 10, 20"), ("putActor", "2, 13, 25"), ("putActor", "3, 7, 15"),
            ("getClosestObjActor", "var[0x050], 1"));
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(3, run.KnownVariables["var[0x050]"]);
    }

    [Theory]
    [InlineData(254, 2)]
    [InlineData(255, 255)]
    [InlineData(256, 255)]
    public void NativeSentinelRejectsDistancesAtAndBeyond255(int distance, int expected)
    {
        var runtime = Runtime(); Range(runtime, 2, 2);
        var run = Run(runtime, ("putActorInRoom", "1, 1"), ("putActorInRoom", "2, 1"),
            ("putActor", "1, 0, 0"), ("putActor", $"2, {distance}, 0"),
            ("getClosestObjActor", "var[0x050], 1"));
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(expected, run.KnownVariables["var[0x050]"]);
    }

    [Fact]
    public void RangeIncludesSelfAndNativeDoWhileExaminesMaximumEvenForInvertedRange()
    {
        var runtime = Runtime(); Range(runtime, 3, 2);
        var run = Run(runtime, ("putActorInRoom", "2, 1"), ("putActor", "2, 1, 1"),
            ("getClosestObjActor", "var[0x050], 2"));
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(2, run.KnownVariables["var[0x050]"]);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 255)]
    [InlineData(0, 255)]
    public void SameNonCurrentNonzeroRoomHasZeroDistanceOtherwiseNotFound(int candidateRoom, int expected)
    {
        var runtime = Runtime(); Range(runtime, 2, 2);
        var run = Run(runtime, ("putActorInRoom", "1, 2"), ("putActorInRoom", $"2, {candidateRoom}"),
            ("getClosestObjActor", "var[0x050], 1"));
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(expected, run.KnownVariables["var[0x050]"]);
    }

    [Fact]
    public void UnknownCandidateFailsWithoutOverwritingDestination()
    {
        var runtime = Runtime(); Range(runtime, 2, 2);
        runtime.GlobalVariables["var[0x050]"] = 99;
        runtime.Actors.Forget(2, "test: native position unavailable");
        var run = Run(runtime, ("putActorInRoom", "1, 1"), ("putActor", "1, 1, 1"),
            ("getClosestObjActor", "var[0x050], 1"));
        Assert.Contains("getClosestObjActor", run.UnresolvedReason);
        Assert.Equal(99, run.KnownVariables["var[0x050]"]);
    }

    [Fact]
    public void EstimatedNativeRangeMarksResultAndSubsequentConditionalPath()
    {
        var runtime = Runtime(); Range(runtime, 1, 1);
        runtime.EstimatedVariables.Add("var[0x013]");
        var run = Run(runtime, ("putActorInRoom", "1, 1"), ("putActor", "1, 1, 1"),
            ("getClosestObjActor", "var[0x050], 1"), ("move", "var[0x051], var[0x050]"));
        Assert.Null(run.UnresolvedReason);
        Assert.Contains("var[0x050]", runtime.EstimatedVariables);
        Assert.Contains("var[0x051]", runtime.EstimatedVariables);
    }

    [Theory]
    [InlineData(0, 10, 4, 48)]
    [InlineData(20, 10, 4, 112)]
    [InlineData(0, 300, 255, 360)]
    public void WalkToActorScalesNativeDistanceAndArrivesWithoutRunningPastTarget(int start, int target, int distance, int expectedX)
    {
        var runtime = Runtime();
        runtime.Actors.InitializeV2Game(false);
        runtime.Actors.SetIgnoreBoxes(1, true);
        runtime.Actors.SetIgnoreBoxes(2, true);
        var run = Run(runtime, ("putActorInRoom", "1, 1"), ("putActorInRoom", "2, 1"),
            ("putActor", $"1, {start}, 20"), ("putActor", $"2, {target}, 20"),
            ("walkActorToActor", $"1, 2, {distance}"));
        Assert.Null(run.UnresolvedReason);
        var place = new ScummActorTable.Placement(1, null, null, null);
        for (var frame = 0; frame < 400 && runtime.Actors[1].Moving != 0; frame++) runtime.Actors.StepWalks(place);
        Assert.Equal(0, runtime.Actors[1].Moving);
        Assert.Equal(expectedX, runtime.Actors[1].X);
        Assert.Equal(40, runtime.Actors[1].Y);
    }
}
