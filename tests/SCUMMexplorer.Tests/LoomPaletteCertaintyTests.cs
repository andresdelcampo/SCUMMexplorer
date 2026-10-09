
namespace SCUMMexplorer.Tests;

public sealed class LoomPaletteCertaintyTests
{
    private static ScummScriptRuntimeState Runtime()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>()) { CurrentRoom = 2 };
        var place = new ScummActorTable.Placement(2, null, null, null);
        foreach (var number in new[] { 1, 2 })
        {
            runtime.Actors.SetIgnoreBoxes(number, true);
            runtime.Actors.Put(number, 100 + number * 10, 50, 2, place, ego: 1);
        }
        return runtime;
    }

    private static ScummBoundedScriptResult Run(ScummScriptRuntimeState runtime, bool pathEstimated,
        params (string Operation, string Operands)[] operations)
    {
        var instructions = operations.Append(("stopObjectCode", ""))
            .Select((op, offset) => new ScummScriptInstruction(offset, 0, op.Item1, op.Item2, "00")).ToArray();
        var script = new ScummScriptDisassembly(1, 1, 0, instructions.Length, instructions.Length,
            instructions, null, null, [], []);
        var run = new ScummBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime,
            pathEstimated: pathEstimated);
        Assert.True(run.ReachedStop);
        return run;
    }

    [Fact]
    public void ConditionalPaletteRetainsItsProvenanceWithoutTaintingDistance()
    {
        var runtime = Runtime();
        var before = runtime.Actors[1];
        var palette = Run(runtime, true, ("actorOps", "0x01, [palette, 0x07, 0x0F; palette, 0x08, 0x02]"));
        Assert.Null(palette.UnresolvedReason);
        Assert.True(palette.PathEstimated);
        Assert.Equal((byte)15, runtime.Actors[1].Look.Palette[7]);
        Assert.NotNull(runtime.Actors[1].Look.PaletteEstimate);
        Assert.Null(runtime.Actors[1].Estimate);
        Assert.Equal((before.X, before.Y, before.Moving), (runtime.Actors[1].X, runtime.Actors[1].Y, runtime.Actors[1].Moving));
        var distance = Run(runtime, false, ("getDist", "var[0x010], 0x01, 0x02"));
        Assert.Null(distance.UnresolvedReason);
        Assert.Equal((ushort)10, runtime.GlobalVariables["var[0x010]"]);
        Assert.DoesNotContain("var[0x010]", runtime.EstimatedVariables);
    }

    [Fact]
    public void PaletteCannotEraseAnExistingMovementEstimate()
    {
        var runtime = Runtime();
        runtime.Actors.MarkEstimate(1, "earlier conditional movement");
        Run(runtime, true, ("actorOps", "0x01, [palette, 0x07, 0x0F]"));
        Run(runtime, false, ("getDist", "var[0x010], 0x01, 0x02"));
        Assert.Equal("earlier conditional movement", runtime.Actors[1].Estimate);
        Assert.Contains("var[0x010]", runtime.EstimatedVariables);
        Assert.NotNull(runtime.Actors[1].Look.PaletteEstimate);
    }

    [Theory]
    [InlineData("palette, 0x07, 0x0F; walkSpeed, 0x04, 0x02")]
    [InlineData("init; palette, 0x07, 0x0F")]
    [InlineData("costume, 0x01; palette, 0x07, 0x0F")]
    public void MixedActorCommandsKeepPlacementUncertainty(string commands)
    {
        var runtime = Runtime();
        Run(runtime, true, ("actorOps", $"0x01, [{commands}]"));
        Assert.NotNull(runtime.Actors[1].Estimate);
    }

    [Fact]
    public void EstimatedPaletteOperandKeepsItsProvenance()
    {
        var runtime = Runtime();
        runtime.GlobalVariables["var[0x010]"] = 15;
        runtime.EstimatedVariables.Add("var[0x010]");
        Run(runtime, false, ("actorOps", "0x01, [palette, 0x07, var[0x010]]"));
        Assert.NotNull(runtime.Actors[1].Look.PaletteEstimate);
        Assert.Null(runtime.Actors[1].Estimate);
        Assert.Contains("var[0x010]", runtime.EstimatedVariables);
    }

    [Fact]
    public void UnreadablePaletteActorPreservesEveryPosition()
    {
        var runtime = Runtime();
        Run(runtime, false, ("actorOps", "var[0x100], [palette, 0x07, 0x0F]"));
        Assert.Equal(110, runtime.Actors[1].X);
        Assert.Equal(120, runtime.Actors[2].X);
        Assert.All(runtime.Actors.Snapshot(), actor => Assert.NotNull(actor.Look.PaletteEstimate));
    }

    [Fact]
    public void UnreadablePaletteColorIsRecordedWithoutInventingAColor()
    {
        var runtime = Runtime();
        Run(runtime, false, ("actorOps", "0x01, [palette, 0x07, var[0x100]]"));
        Assert.Equal((byte)0, runtime.Actors[1].Look.Palette[7]);
        Assert.NotNull(runtime.Actors[1].Look.PaletteEstimate);
        Assert.Null(runtime.Actors[1].Estimate);
    }

    [Fact]
    public void UndecidablePaletteBranchKeepsPlacementAndRecordsPossibleColor()
    {
        var runtime = Runtime();
        Run(runtime, false, ("equalZero", "var[0x100], branch +0x0001 -> 0x0002"),
            ("actorOps", "0x01, [palette, 0x07, 0x0F]"));
        Assert.Equal(110, runtime.Actors[1].X);
        Assert.Null(runtime.Actors[1].Estimate);
        Assert.Equal((byte)0, runtime.Actors[1].Look.Palette[7]);
        Assert.NotNull(runtime.Actors[1].Look.PaletteEstimate);
    }
}
