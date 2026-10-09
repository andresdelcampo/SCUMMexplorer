
namespace SCUMMexplorer.Tests;

public sealed class LoomExpressionObservationTests
{
    [Fact]
    public void NestedWritesScratchAndLaterReadsUseActualInInstructionValues()
    {
        var (result, outer, steps) = Run("local[0x000], [getRandomNr(var[0x000], 0x0000) var[0x000] +]");
        Assert.True(result.ReachedStop);
        Assert.Equal(new[] { "destination-selected", "nested-before", "nested-write", "scratch-read", "read", "final-write" }, steps.Select(s => s.Stage));
        Assert.Equal((ushort)77, steps[1].Bindings[0].Value);
        Assert.All(steps.Skip(2), s => Assert.Equal((ushort)0, Assert.Single(s.Bindings).Value));
        Assert.True(steps[3].Bindings[0].ValueEstimated);
        Assert.NotNull(outer.Single(r => r.Site?.Operation == "expression").SourceAccess!.Barrier);
        Assert.All(steps, s => { Assert.Equal(2, s.InstructionSequence); Assert.Equal(4, s.Slot); Assert.Equal(3, s.ParentSlot); });
        Assert.Equal(Enumerable.Range(1, steps.Count), steps.Select(s => s.Step));
        Assert.Single(result.RandomPicks);
        Assert.Throws<NotSupportedException>(() => ((IList<ScummScriptOperandBinding>)steps[0].Bindings).Clear());
    }

    [Fact]
    public void ScratchIsReadFromVarZeroEvenWhenNestedDestinationIsAnotherCell()
    {
        var (result, _, steps) = Run("local[0x000], [getRandomNr(var[0x060], 0x0000)]");
        Assert.Equal((ushort)77, result.KnownVariables["local[0x000]"]);
        Assert.Equal((ushort)0, steps.Single(s => s.Stage == "nested-write").Bindings[0].Value);
        var scratch = steps.Single(s => s.Stage == "scratch-read").Bindings[0];
        Assert.Equal("var[0x000]", scratch.Key);
        Assert.Equal((ushort)77, scratch.Value);
        Assert.False(scratch.ValueEstimated);
    }

    [Fact]
    public void FinalWriteUsesTheDestinationSelectedBeforeItsIndexChanges()
    {
        var (result, _, steps) = Run("var[0x032+var[0x000]], [getRandomNr(var[0x000], 0x0000)]");
        Assert.Equal("var[0x07F]", steps[0].Bindings[0].Key);
        Assert.Equal("var[0x07F]", steps[^1].Bindings[0].Key);
        Assert.Equal((ushort)0, result.KnownVariables["var[0x07F]"]);
    }

    [Fact]
    public void PartialExecutionKeepsTheObservedWriteAndExplicitFailure()
    {
        var (result, _, steps) = Run("local[0x000], [getRandomNr(var[0x000], 0x0000) 0x0000 /]");
        Assert.False(result.ReachedStop);
        Assert.Contains(steps, s => s.Stage == "nested-write");
        Assert.DoesNotContain(steps, s => s.Stage == "final-write");
        Assert.Equal("expression divides by zero", steps[^1].Barrier);
    }

    [Fact]
    public void FilteredKeysRemainUnresolvedInTheSeparateChronology()
    {
        var (_, _, steps) = Run("local[0x000], [getRandomNr(var[0x000], 0x0000)]", key => key != "var[0x000]");
        Assert.All(steps.SelectMany(s => s.Bindings).Where(b => b.Key == "var[0x000]"), b => {
            Assert.Null(b.Value); Assert.Null(b.ValueEstimated); Assert.NotNull(b.UnresolvedReason);
        });
        var (_, _, filtered) = Run("local[0x000], [getRandomNr(var[0x000], 0x0000)]", site: s => s.Operation != "expression");
        Assert.Empty(filtered);
    }

    [Fact]
    public void DefaultAndObservedExecutionHaveIdenticalResultsPicksAndOuterSequences()
    {
        var text = "local[0x000], [getRandomNr(var[0x000], 0x0003) 0x0001 +]";
        var (observed, records, _) = Run(text);
        var (baseline, old, steps) = Run(text, capture: false);
        Assert.Empty(steps);
        Assert.Equal(baseline.KnownVariables, observed.KnownVariables);
        Assert.Equal(baseline.Trace, observed.Trace);
        Assert.Equal(baseline.RandomPicks, observed.RandomPicks);
        Assert.Equal(baseline.EstimatedLocals, observed.EstimatedLocals);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(old), System.Text.Json.JsonSerializer.Serialize(records));
    }

    [Fact]
    public void SeparateObservationBoundDoesNotStopExpressionExecution()
    {
        var text = "local[0x000], [" + string.Join(" ", Enumerable.Repeat("getRandomNr(var[0x000], 0x0000)", 1366)) +
            " " + string.Join(" ", Enumerable.Repeat("+", 1365)) + "]";
        var (result, _, steps) = Run(text);
        Assert.True(result.ReachedStop);
        Assert.Equal(1366, result.RandomPicks.Count);
        Assert.Equal(4097, steps.Count);
        Assert.Equal("expression observation step bound reached", steps[^1].Barrier);
    }

    private static (ScummBoundedScriptResult, List<ScummScriptObservation>, List<ScummExpressionObservation>) Run(
        string text, Func<string, bool>? key = null, Func<ScummScriptObservationSite, bool>? site = null, bool capture = true)
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        runtime.SetGlobalVariable("var[0x000]", 77);
        var records = new List<ScummScriptObservation>();
        var steps = new List<ScummExpressionObservation>();
        var code = new ScummScriptDisassembly(2, 1, 0, 2, 2,
            [new(0, 0, "expression", text, "00"), new(1, 0, "stopObjectCode", "", "00")], null, null, [], []);
        var result = new ScummBoundedScriptExecutor().Execute(code, runtime.ObjectStates, [], runtime: runtime,
            observation: new(new(records.Add, site, key, captureSourceAccess: true,
                receiveExpression: capture ? steps.Add : null), 4, 3));
        return (result, records, steps);
    }
}
