
namespace SCUMMexplorer.Tests;

public sealed class LoomSourceAccessTests
{
    [Fact]
    public void SameValueQueryWritesExpireTheDestinationWithoutInventingQueryLineage()
    {
        var (updates, records, runtime) = Execute(("move", "local[0x000] = var[0x060]"),
            ("isSoundRunning", "var[0x060], 0x0000"), ("stopObjectCode", ""));
        var read = updates.SelectMany(u => u.Reads).Single(r => r.Binding.Role == "source" && r.Binding.Key == "var[0x060]");
        Assert.Contains(updates.Single(u => u.Observation?.Site?.Offset == 1).Invalidated, i => i.Read == read.Id);
        var query = records.Single(r => r.Site?.Operation == "isSoundRunning");
        Assert.Empty(query.OperandBindings!); // Preserve the operand API's previous coverage.
        Assert.Equal("var[0x060]", query.SourceAccess!.Bindings[0].Key);
        Assert.Null(query.SourceAccess.Barrier);
        Assert.Equal((ushort)0, runtime.GlobalVariables["var[0x060]"]);
        Assert.DoesNotContain(updates.SelectMany(u => u.Writes), w => w.Observation.Site?.Operation == "isSoundRunning");
    }

    [Fact]
    public void ParsedRangeInvalidatesEveryDestinationAndKeepsUnrelatedReads()
    {
        var (updates, _, _) = Execute(("debug", "var[0x060]"), ("debug", "var[0x061]"),
            ("debug", "var[0x064]"), ("setVarRange", "var[0x060], count=2, [0x0000, 0x0000]"), ("stopObjectCode", ""));
        var range = updates.Single(u => u.Observation?.Site?.Operation == "setVarRange");
        Assert.Equal(2, range.Invalidated.Count);
        Assert.Null(range.Barrier);
        Assert.DoesNotContain(range.Invalidated, i => i.Read == updates.SelectMany(u => u.Reads)
            .Single(r => r.Binding.Key == "var[0x064]").Id);
    }

    [Fact]
    public void FilteredRangeIndexClearsCorrespondenceRatherThanNamingTheRuntimeCell()
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var records = new List<ScummScriptObservation>();
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        ExecuteCode(runtime, new(records.Add, includeKey: k => k != "var[0x062]", runtimeRelations: new(updates.Add)),
            ("debug", "var[0x060]"), ("setVarRange", "var[0x060+var[0x062]], count=2, [0x0000, 0x0000]"), ("stopObjectCode", ""));
        var range = updates.Single(u => u.Observation?.Site?.Operation == "setVarRange");
        Assert.NotNull(range.Barrier);
        Assert.Single(range.Invalidated);
        Assert.All(range.Observation!.SourceAccess!.Bindings, b => Assert.Null(b.Index!.Value));
        Assert.All(range.Observation.SourceAccess.Bindings, b => Assert.Null(b.Key));
    }

    [Fact]
    public void ExpressionOperandsUseCopiedReadsWhileNestedRandomAccessRemainsABarrier()
    {
        var (_, records, _) = Execute(("expression", "local[0x000] = [var[0x060] 0x0001 +]"), ("stopObjectCode", ""));
        var access = records.Single(r => r.Site?.Operation == "expression").SourceAccess!;
        Assert.Null(access.Barrier);
        Assert.Equal(new[] { "destination-before", "source", "source" }, access.Bindings.Select(b => b.Role));
        Assert.Throws<NotSupportedException>(() => ((IList<ScummScriptOperandBinding>)access.Bindings).Clear());
        var (updates, _, _) = Execute(("debug", "var[0x060]"),
            ("expression", "local[0x000] = [getRandomNr(var[0x000], 0x0001)]"), ("stopObjectCode", ""));
        Assert.Contains(updates, u => u.Barrier == "unmodelled instruction access" && u.Invalidated.Count == 1);
    }

    [Fact]
    public void DebugAndDelayReadRecursiveIndexesWithoutChangingLegacyBindings()
    {
        var (_, records, _) = Execute(("debug", "var[0x060+var[0x062]]"), ("delayVariable", "var[0x064]"));
        var debug = records.Single(r => r.Site?.Operation == "debug");
        Assert.Empty(debug.OperandBindings!);
        Assert.Equal("var[0x060]", debug.SourceAccess!.Bindings.Single().Key);
        Assert.Equal("var[0x062]", debug.SourceAccess.Bindings.Single().Index!.Key);
        Assert.Equal("var[0x064]", records.Single(r => r.Site?.Operation == "delayVariable").SourceAccess!.Bindings.Single().Key);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScopedExternalSameValueWritesAndRemovalsRetireAliasesImmediately(bool remove)
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var tracker = new ScummRuntimeRelationTracker(updates.Add);
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        var observer = new ScummScriptObserver(_ => { }, runtimeRelations: tracker);
        using (runtime.ObserveGlobalMutations(observer))
        {
            ExecuteCode(runtime, observer, ("equalZero", "bit[0xD67], branch +0 -> 0x0001"), ("stopObjectCode", ""));
            Assert.Single(tracker.ActiveRelations);
            if (remove) runtime.RemoveGlobalVariable("var[0x0D6]");
            else runtime.SetGlobalVariable("var[0x0D6]", 0);
            Assert.Empty(tracker.ActiveRelations);
            Assert.Equal("runtime global mutation (including same value)", updates[^1].Barrier);
        }
        var count = updates.Count;
        runtime.SetGlobalVariable("var[0x0D6]", 0);
        Assert.Equal(count, updates.Count); // The scoped sink was restored, not retained by a run.
    }

    [Fact]
    public void MutationsInAnEventCallbackRetireRelationsWithoutAddingInstructionSequenceNumbers()
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var records = new List<ScummScriptObservation>();
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        new ScummBoundedScriptExecutor().Execute(Code(("debug", "var[0x060]"),
            ("startScript", "0x03, []"), ("stopObjectCode", "")), runtime.ObjectStates, [], runtime: runtime,
            onEvent: (_, state) => { state.SetGlobalVariable("var[0x060]", 0); return ""; },
            observation: new(new(records.Add, runtimeRelations: new(updates.Add)), 1));
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, records.Select(r => r.Sequence));
        Assert.Contains(updates, u => u.Observation is null && u.Invalidated.Count == 1);
        Assert.Null(runtime.MutationObserver);
    }

    [Fact]
    public void SourceAccessCaptureIsOptionalAndNeverChangesResultsOrEstimates()
    {
        var first = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        var second = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        first.EstimatedVariables.Add("var[0x060]"); second.EstimatedVariables.Add("var[0x060]");
        var a = new List<ScummScriptObservation>(); var b = new List<ScummScriptObservation>();
        var unbound = ExecuteCode(first, new(a.Add), ("expression", "var[0x060] = [0x0000]"), ("stopObjectCode", ""));
        var bound = ExecuteCode(second, new(b.Add, captureSourceAccess: true),
            ("expression", "var[0x060] = [0x0000]"), ("stopObjectCode", ""));
        Assert.Equal(unbound.KnownVariables, bound.KnownVariables);
        Assert.Equal(unbound.Trace, bound.Trace);
        Assert.Equal(first.EstimatedVariables, second.EstimatedVariables);
        Assert.Equal(a.Select(r => (r.Sequence, r.Slot, r.Invocation, r.PathEstimated)),
            b.Select(r => (r.Sequence, r.Slot, r.Invocation, r.PathEstimated)));
        Assert.All(a, r => Assert.Null(r.SourceAccess));
        Assert.Null(b[0].SourceAccess); Assert.Null(b[^1].SourceAccess);
    }

    private static (List<ScummRuntimeRelationUpdate>, List<ScummScriptObservation>, ScummScriptRuntimeState)
        Execute(params (string Op, string Args)[] operations)
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var records = new List<ScummScriptObservation>();
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        ExecuteCode(runtime, new(records.Add, runtimeRelations: new(updates.Add)), operations);
        return (updates, records, runtime);
    }
    private static ScummBoundedScriptResult ExecuteCode(ScummScriptRuntimeState runtime, ScummScriptObserver observer,
        params (string Op, string Args)[] operations) => new ScummBoundedScriptExecutor().Execute(Code(operations),
            runtime.ObjectStates, [], runtime: runtime, observation: new(observer, 1));
    private static ScummScriptDisassembly Code(params (string Op, string Args)[] operations) =>
        new(2, 1, 0, operations.Length, operations.Length,
            operations.Select((o, i) => new ScummScriptInstruction(i, 0, o.Op, o.Args, "00")).ToArray(), null, null, [], []);
}
