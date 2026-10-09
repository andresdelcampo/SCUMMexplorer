
namespace SCUMMexplorer.Tests;

public sealed class LoomExtendedSourceAccessTests
{
    [Fact]
    public void DecodedExpressionGrammarCapturesRealReadsWithoutChangingExecution()
    {
        var runtime = Runtime();
        runtime.SetGlobalVariable("var[0x060]", 8);
        runtime.EstimatedVariables.Add("var[0x060]");
        var (result, records) = Run(runtime, ("expression", "local[0x000], [var[0x060] 0x0003 -]"));
        Assert.True(result.ReachedStop);
        Assert.Equal((ushort)5, result.KnownVariables["local[0x000]"]);
        Assert.Contains("local[0x000]", result.EstimatedLocals);
        var access = Instruction(records).SourceAccess!;
        Assert.Null(access.Barrier);
        Assert.Equal(new[] { "destination-before", "source", "source" }, access.Bindings.Select(b => b.Role));
        Assert.True(access.Bindings[1].ValueEstimated);
        Assert.Empty(Instruction(records).OperandBindings!);
    }

    [Fact]
    public void NestedRandomCopiesPreinstructionOperandsAndRetainsTheEffectBarrier()
    {
        var runtime = Runtime();
        runtime.SetGlobalVariable("var[0x000]", 77);
        runtime.SetGlobalVariable("var[0x060]", 3);
        var (result, records) = Run(runtime,
            ("expression", "local[0x000], [getRandomNr(var[0x000], var[0x060]) 0x0001 +]"));
        Assert.True(result.ReachedStop);
        var access = Instruction(records).SourceAccess!;
        Assert.Equal("unmodelled instruction access", access.Barrier);
        Assert.Equal((ushort)77, access.Bindings.Single(b => b.Role == "nested-destination-before").Value);
        Assert.Equal((ushort)3, access.Bindings.Single(b => b.Role == "nested-bound-before").Value);
        Assert.DoesNotContain(access.Bindings, b => b.Role.Contains("scratch"));
        Assert.Single(result.RandomPicks);
        Assert.Contains("var[0x000]", runtime.EstimatedVariables);
    }

    [Theory]
    [InlineData("local[0x000], [unknown(var[0x060])]" )]
    [InlineData("local[0x000], [var[0x032+local[0x000]]]" )]
    public void UnsupportedNestedFormsRemainBarriers(string expression)
    {
        var (_, records) = Run(Runtime(), ("expression", expression));
        Assert.Equal("unmodelled instruction access", Instruction(records).SourceAccess!.Barrier);
    }

    [Fact]
    public void ObjectCallRecordsOrderedCallerArgumentsWithoutCalleeBinding()
    {
        var runtime = Runtime();
        runtime.SetGlobalVariable("var[0x060]", 204);
        runtime.EstimatedVariables.Add("var[0x060]");
        var (_, records) = Run(runtime, ("startObject", "var[0x060], 0x38, [var[0x032+local[0x000]], 0x0000]"));
        var access = Instruction(records).SourceAccess!;
        Assert.Equal("unmodelled instruction access", access.Barrier);
        Assert.Equal(new[] { "target", "verb", "argument", "argument" }, access.Bindings.Select(b => b.Role));
        Assert.True(access.Bindings[0].ValueEstimated);
        Assert.Equal(new int?[] { 0, 1 }, access.Bindings.Where(b => b.Role == "argument").Select(b => b.ArgumentIndex));
        Assert.Equal("local[0x000]", access.Bindings[2].Index!.Key);
        Assert.Empty(Instruction(records).OperandBindings!);
    }

    [Theory]
    [InlineData("putActor", "0x01, var[0x060], 0x0020", 3)]
    [InlineData("startSound", "var[0x060]", 1)]
    [InlineData("setState", "var[0x060], 0x01", 2)]
    public void EventOperandsDoNotRemoveIndependentEffectBarriers(string op, string text, int count)
    {
        var (_, records) = Run(Runtime(), (op, text));
        var access = Instruction(records).SourceAccess!;
        Assert.Equal(count, access.Bindings.Count);
        Assert.Equal("unmodelled instruction access", access.Barrier);
    }

    [Fact]
    public void ActorSubcommandsCopyPaletteOperandsWithoutCreatingPlacementEvidence()
    {
        var (_, records) = Run(Runtime(), ("actorOps", "0x01, [palette, var[0x060], 0x02; elevation, 0x0000]"));
        var access = Instruction(records).SourceAccess!;
        Assert.Equal(new[] { "target", "palette-operand", "palette-operand", "elevation-operand" }, access.Bindings.Select(b => b.Role));
        Assert.Equal("unmodelled instruction access", access.Barrier);
        Assert.Null(access.NativeDependencies);
    }

    [Theory]
    [InlineData("print", "0x01, [position, var[0x060], 0x0000; text, \"var[0x061]; color, var[0x062]\"]")]
    [InlineData("actorOps", "0x01, [elevation, var[0x060]; actorName, \"var[0x061]; palette, var[0x062], 0x01\"]")]
    public void InlineTextIsNeverMistakenForStorageAccess(string op, string text)
    {
        var (_, records) = Run(Runtime(), (op, text));
        var access = Instruction(records).SourceAccess!;
        Assert.Contains(access.Bindings, b => b.Key == "var[0x060]");
        Assert.DoesNotContain(access.Bindings, b => b.Key is "var[0x061]" or "var[0x062]");
        Assert.NotNull(access.Barrier);
    }

    [Fact]
    public void OversizedSubcommandAccessFailsClosed()
    {
        var (_, records) = Run(Runtime(), ("actorOps", "0x01, [" + string.Join("; ", Enumerable.Repeat("palette, 0x01, 0x02", 129)) + "]"));
        var access = Instruction(records).SourceAccess!;
        Assert.Empty(access.Bindings);
        Assert.NotNull(access.Barrier);
    }

    [Fact]
    public void ObjectStateAndClassDependenciesRetainNativeEstimatesSeparately()
    {
        var runtime = Runtime();
        runtime.ObjectStates[204] = 1;
        runtime.ObjectClasses[204] = 0x00800000;
        runtime.EstimatedStates.Add(204);
        var (_, records) = Run(runtime, ("ifState", "0x00CC, 0x01, branch +0 -> 0x0001"),
            ("ifClassOfIs", "0x00CC, [0x98], branch +0 -> 0x0002"));
        var dependencies = records.Where(r => r.Site?.Stage == "instruction").SelectMany(r => r.SourceAccess?.NativeDependencies ?? []).ToArray();
        Assert.Equal(new[] { "object-state", "object-classes" }, dependencies.Select(d => d.Domain));
        Assert.Equal(new long?[] { 1, 0x00800000 }, dependencies.Select(d => d.Value));
        Assert.All(dependencies, d => Assert.True(d.ValueEstimated));
        Assert.All(dependencies, d => Assert.False(d.Selector.ValueEstimated));
        Assert.False(records[0].PathEstimated);
        Assert.Throws<NotSupportedException>(() => ((IList<ScummScriptNativeDependency>)Instruction(records).SourceAccess!.NativeDependencies!).Clear());
    }

    [Fact]
    public void NativeActorDependencyKeepsPlacementEstimateIndependentOfPalette()
    {
        var runtime = Runtime();
        runtime.Actors.Put(1, 24, 32, 2, new(2, null, null, runtime.ObjectClasses), null);
        runtime.Actors.MarkPaletteEstimate(1, "possible palette");
        var (_, records) = Run(runtime, ("getActorX", "local[0x000], 0x01"));
        var dependency = Assert.Single(Instruction(records).SourceAccess!.NativeDependencies!);
        Assert.Equal(24, dependency.Value);
        Assert.False(dependency.ValueEstimated);
        runtime.Actors.MarkEstimate(1, "estimated placement");
        (_, records) = Run(runtime, ("getActorX", "local[0x000], 0x01"));
        dependency = Assert.Single(Instruction(records).SourceAccess!.NativeDependencies!);
        Assert.True(dependency.ValueEstimated);
        Assert.Equal("estimated placement", dependency.Estimate);
        Assert.False(dependency.Selector.ValueEstimated);
    }

    [Fact]
    public void FilteredNativeSelectorsCannotRevealTheirSelectedField()
    {
        var runtime = Runtime();
        runtime.SetGlobalVariable("var[0x060]", 1);
        var (_, records) = Run(runtime, [("getActorRoom", "local[0x000], var[0x060]")], include: key => key != "var[0x060]");
        var access = Instruction(records).SourceAccess!;
        Assert.Equal("unresolved or filtered source access", access.Barrier);
        var dependency = Assert.Single(access.NativeDependencies!);
        Assert.Null(dependency.Value);
        Assert.Null(dependency.Selector.Value);
        Assert.NotNull(dependency.UnresolvedReason);
    }

    [Fact]
    public void CallbackDependenciesAreUnresolvedAndNeverInvokeTheCallbackAgain()
    {
        var runtime = Runtime();
        var calls = 0;
        runtime.SoundRunning = _ => { calls++; return (false, "selected clock estimate"); };
        var (_, records) = Run(runtime, ("isSoundRunning", "local[0x000], 0x01"));
        Assert.Equal(1, calls);
        var dependency = Assert.Single(Instruction(records).SourceAccess!.NativeDependencies!);
        Assert.Null(dependency.Value);
        Assert.Contains("does not invoke callbacks", dependency.UnresolvedReason);
    }

    [Fact]
    public void RawSameValueNotificationExpiresAliasesWithoutChangingStorageOrEstimates()
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var tracker = new ScummRuntimeRelationTracker(updates.Add);
        var observer = new ScummScriptObserver(_ => { }, runtimeRelations: tracker);
        var runtime = Runtime();
        runtime.EstimatedVariables.Add("var[0x0D6]");
        using (runtime.ObserveGlobalMutations(observer))
        {
            new ScummBoundedScriptExecutor().Execute(Code(("equalZero", "bit[0xD67], branch +0 -> 0x0001")),
                runtime.ObjectStates, [], runtime: runtime, observation: new(observer, 1));
            Assert.Single(tracker.ActiveRelations);
            runtime.GlobalVariables["var[0x0D6]"] = 0;
            Assert.Single(tracker.ActiveRelations); // Raw writes still require notification.
            runtime.NotifyGlobalMutation("var[0x0D6]");
            Assert.Empty(tracker.ActiveRelations);
            Assert.Equal((ushort)0, runtime.GlobalVariables["var[0x0D6]"]);
            Assert.Contains("var[0x0D6]", runtime.EstimatedVariables);
        }
        var count = updates.Count;
        runtime.NotifyGlobalMutation("var[0x0D6]");
        Assert.Equal(count, updates.Count);
    }

    private static ScummScriptRuntimeState Runtime() => new(new Dictionary<int, byte>());
    private static ScummScriptObservation Instruction(List<ScummScriptObservation> records) =>
        records.First(r => r.Site?.Stage == "instruction");
    private static (ScummBoundedScriptResult, List<ScummScriptObservation>) Run(ScummScriptRuntimeState runtime,
        params (string Op, string Args)[] operations) => Run(runtime, operations, _ => true);
    private static (ScummBoundedScriptResult, List<ScummScriptObservation>) Run(ScummScriptRuntimeState runtime,
        (string Op, string Args)[] operations, Func<string, bool> include)
    {
        var records = new List<ScummScriptObservation>();
        var result = new ScummBoundedScriptExecutor().Execute(Code(operations), runtime.ObjectStates, [], runtime: runtime,
            observation: new(new(records.Add, includeKey: include, captureOperandBindings: true, captureSourceAccess: true), 1));
        return (result, records);
    }
    private static ScummScriptDisassembly Code(params (string Op, string Args)[] operations)
    {
        var code = operations.Append((Op: "stopObjectCode", Args: "")).ToArray();
        return new(2, 1, 0, code.Length, code.Length,
            code.Select((o, i) => new ScummScriptInstruction(i, 0, o.Op, o.Args, "00")).ToArray(), null, null, [], []);
    }
}
