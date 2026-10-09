
namespace SCUMMexplorer.Tests;

public sealed class LoomExpressionTests
{
    private static ScummScriptDisassembly Script(string operands) => new(1, 1, 0, 32, 16,
        [new(0, 0, "expression", operands, "AC 00 40 FF"), new(4, 0, "stopObjectCode", "", "00")], null, null, [], []);

    [Theory]
    [InlineData("[0x0008 0x0003 -]", 5)]
    [InlineData("[0xFFF8 0x0003 /]", -2)]
    [InlineData("[0x7FFF 0x0002 * 0x0002 /]", 32767)]
    [InlineData("[0xFFFF 0x0002 +]", 1)]
    public void ExpressionUsesOrderedIntegerStackAndSignedWordInputs(string body, int expected)
    {
        var run = new ScummBoundedScriptExecutor().Execute(Script("local[0x000], " + body), new Dictionary<int, byte>(), []);
        Assert.True(run.ReachedStop);
        Assert.Null(run.UnresolvedReason);
        Assert.Equal(unchecked((ushort)expected), run.KnownVariables["local[0x000]"]);
    }

    [Fact]
    public void TimerAndPointerExpressionPreservesOperandEstimates()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        runtime.GlobalVariables["var[0x00B]"] = 24;
        runtime.GlobalVariables["var[0x014]"] = 120;
        runtime.GlobalVariables["var[0x015]"] = 40;
        runtime.EstimatedVariables.Add("var[0x014]");
        var run = new ScummBoundedScriptExecutor().Execute(
            Script("local[0x001], [var[0x00B] var[0x014] + var[0x015] +]"), new Dictionary<int, byte>(), [], runtime: runtime);
        Assert.Equal(184, run.KnownVariables["local[0x001]"]);
        Assert.Contains("local[0x001]", run.EstimatedLocals);
        Assert.False(run.PathEstimated);
    }

    [Fact]
    public void NestedRandomOpcodeWritesScratchAndKeepsSeededResultEstimated()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        var run = new ScummBoundedScriptExecutor().Execute(Script("local[0x007], [getRandomNr(var[0x000], 0x05) 0x0001 +]"),
            new Dictionary<int, byte>(), [], runtime: runtime);
        Assert.True(run.ReachedStop);
        var pick = Assert.Single(run.RandomPicks);
        Assert.InRange(pick.Value, 0, 5);
        Assert.Equal(pick.Value, runtime.GlobalVariables["var[0x000]"]);
        Assert.Equal(pick.Value + 1, run.KnownVariables["local[0x007]"]);
        Assert.Contains("var[0x000]", runtime.EstimatedVariables);
        Assert.Contains("local[0x007]", run.EstimatedLocals);
    }

    [Theory]
    [InlineData("[0x0001 +]")]
    [InlineData("[0x0001 0x0000 /]")]
    [InlineData("[0x0001 0x0002]")]
    [InlineData("[getActorX(var[0x000], 0x01)]")]
    [InlineData("[var[0x123]]")]
    public void UnsupportedOrInvalidExpressionDoesNotWriteDestination(string body)
    {
        var run = new ScummBoundedScriptExecutor().Execute(Script("local[0x000], " + body),
            new Dictionary<int, byte>(), [], initialLocals: new Dictionary<string, ushort> { ["local[0x000]"] = 77 });
        Assert.False(run.ReachedStop);
        Assert.NotNull(run.UnresolvedReason);
        Assert.Equal(77, run.KnownVariables["local[0x000]"]);
    }
}
