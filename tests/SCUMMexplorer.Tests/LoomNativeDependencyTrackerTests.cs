using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomNativeDependencyTrackerTests
{
    [Fact]
    public void NativeHistoryKeepsIndependentFieldSelectorIndexAndPathEstimates()
    {
        var updates = new List<LoomNativeDependencyUpdate>();
        var tracker = new LoomNativeDependencyTracker(updates.Add);
        var selector = new LoomScriptOperandBinding("source", "var[0x032+local[0x000]]", "var[0x034]", 1, false,
            new("index", "local[0x000]", "local[0x000]", 2, true));
        tracker.Observe(Record(1, selector, values: new() { ["var[0x034]"] = 1, ["local[0x000]"] = 2 }) with { Estimated = ["local[0x000]"] });
        var history = Assert.Single(tracker.ActiveRelations);
        Assert.True(history.Dependency.ValueEstimated);
        Assert.False(history.Dependency.Selector.ValueEstimated);
        Assert.True(history.Dependency.Selector.Index!.ValueEstimated);
        Assert.True(history.Observation.PathEstimated);
        tracker.NotifyGlobalMutation("var[0x034]");
        Assert.Empty(tracker.ActiveRelations);
        Assert.True(history.Dependency.ValueEstimated);
        Assert.Contains(updates[^1].Invalidated, i => i.Read == history.Id);
        Assert.Throws<NotSupportedException>(() => ((IList<LoomRuntimeDependency>)history.SelectorDependencies).Clear());
    }

    [Fact]
    public void RawSameValueNativeNotificationRetiresSharedObjectEstimatesWithoutNumericMutation()
    {
        var native = new LoomNativeDependencyTracker(_ => { });
        var numeric = new LoomRuntimeRelationTracker(_ => { });
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte> { [204] = 1 });
        runtime.EstimatedStates.Add(204);
        var observer = new LoomScriptObserver(_ => { }, runtimeRelations: numeric, nativeDependencies: native);
        using (runtime.ObserveGlobalMutations(observer))
        {
            Execute(runtime, observer, ("ifState", "0x00CC, 0x01, branch +0 -> 0x0001"), ("debug", "var[0x060]"));
            var history = Assert.Single(native.ActiveRelations);
            Assert.Single(numeric.ActiveRelations);
            runtime.ObjectStates[204] = 1;
            Assert.Single(native.ActiveRelations);
            runtime.NotifyNativeMutation("object-classes", 204); // Shared EstimatedStates alias.
            Assert.Empty(native.ActiveRelations);
            Assert.Single(numeric.ActiveRelations);
            Assert.True(history.Dependency.ValueEstimated);
            Assert.Contains(204, runtime.EstimatedStates);
            Assert.Equal(1, runtime.ObjectStates[204]);
        }
    }

    [Theory]
    [InlineData("var[0x0D6]")]
    [InlineData("bit[0xD69]")]
    public void RawSameValueGlobalAliasesExpireNativeSelectors(string key)
    {
        var tracker = new LoomNativeDependencyTracker(_ => { });
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        runtime.SetGlobalVariable("var[0x0D6]", 128);
        runtime.Actors.Put(1, 24, 32, 2, new(2, null, null, runtime.ObjectClasses), null);
        var observer = new LoomScriptObserver(_ => { }, nativeDependencies: tracker);
        using (runtime.ObserveGlobalMutations(observer))
        {
            Execute(runtime, observer, ("getActorRoom", "local[0x000], bit[0xD67]"));
            Assert.Single(tracker.ActiveRelations);
            runtime.NotifyGlobalMutation(key);
            Assert.Empty(tracker.ActiveRelations);
        }
    }

    [Fact]
    public void PaletteNotificationPreservesPlacementWhileActorMutationExpiresIt()
    {
        var tracker = new LoomNativeDependencyTracker(_ => { });
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        runtime.Actors.Put(1, 24, 32, 2, new(2, null, null, runtime.ObjectClasses), null);
        var observer = new LoomScriptObserver(_ => { }, nativeDependencies: tracker);
        using (runtime.ObserveGlobalMutations(observer))
        {
            Execute(runtime, observer, ("getActorRoom", "local[0x000], 0x01"));
            Assert.Single(tracker.ActiveRelations);
            runtime.NotifyNativeMutation("actor-palette", 1);
            Assert.Single(tracker.ActiveRelations);
            runtime.NotifyNativeMutation("actor", 1);
            Assert.Empty(tracker.ActiveRelations);
        }
    }

    [Fact]
    public void MissingCallbacksStayCopiedAndUnresolvedWithoutLiveNativeCorrespondence()
    {
        var updates = new List<LoomNativeDependencyUpdate>();
        var tracker = new LoomNativeDependencyTracker(updates.Add);
        var calls = 0;
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>()) { SoundRunning = _ => { calls++; return (false, null); } };
        Execute(runtime, new(_ => { }, nativeDependencies: tracker), ("isSoundRunning", "local[0x000], 0x01"));
        Assert.Equal(1, calls);
        var read = Assert.Single(updates.SelectMany(u => u.Reads));
        Assert.Null(read.Storage);
        Assert.Null(read.Dependency.Value);
        Assert.NotNull(read.Dependency.UnresolvedReason);
        Assert.Empty(tracker.ActiveRelations);
    }

    [Fact]
    public void UnknownEffectsAndFilteredSitesRetireNativeLifetime()
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        var tracker = new LoomNativeDependencyTracker(_ => { });
        runtime.Actors.Put(1, 24, 32, 2, new(2, null, null, runtime.ObjectClasses), null);
        var observer = new LoomScriptObserver(_ => { }, nativeDependencies: tracker, includeSite: s => s.Operation != "debug");
        Execute(runtime, observer, ("getActorRoom", "local[0x000], 0x01"), ("actorOps", "0x01, [palette, 0x01, 0x02]"));
        Assert.Empty(tracker.ActiveRelations); // Source effects remain conservative even for palette.
        Execute(runtime, observer,
            ("getActorRoom", "local[0x000], 0x01"), ("debug", ""));
        Assert.Empty(tracker.ActiveRelations);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("estimate")]
    [InlineData("backing")]
    public void SelectorSnapshotsAndBackingAliasesRetireCorrespondence(string change)
    {
        var tracker = new LoomNativeDependencyTracker(_ => { });
        var selector = new LoomScriptOperandBinding("source", "bit[0xD67]", "bit[0xD67]", 1, false, BackingWord: "var[0x0D6]");
        var record = Record(1, selector, values: new() { ["bit[0xD67]"] = 1, ["var[0x0D6]"] = 128 });
        tracker.Observe(record);
        var next = record with { Sequence = 2, SourceAccess = new([], null, []) };
        next = change switch {
            "missing" => next with { Values = new Dictionary<string, ushort>() },
            "estimate" => next with { Estimated = ["var[0x0D6]"] },
            _ => next with { Values = new Dictionary<string, ushort> { ["bit[0xD67]"] = 1, ["var[0x0D6]"] = 129 } }
        };
        tracker.Observe(next);
        Assert.Empty(tracker.ActiveRelations);
    }

    [Fact]
    public void LocalSelectorSurvivesResumeAndExpiresAtItsOwnTerminalBoundary()
    {
        var tracker = new LoomNativeDependencyTracker(_ => { });
        var selector = new LoomScriptOperandBinding("source", "local[0x000]", "local[0x000]", 1, false);
        var record = Record(1, selector, values: new() { ["local[0x000]"] = 1 });
        tracker.Observe(record);
        tracker.Observe(record with { Sequence = 2, Site = record.Site! with { Stage = "end" }, ReachedStop = false, ResumeOffset = 1 });
        tracker.Observe(record with { Sequence = 3, Invocation = 2, Site = record.Site! with { Stage = "begin" }, SourceAccess = null });
        Assert.Single(tracker.ActiveRelations);
        tracker.Observe(record with { Sequence = 4, Invocation = 2, Site = record.Site! with { Stage = "end" }, ReachedStop = true, SourceAccess = null });
        Assert.Empty(tracker.ActiveRelations);
        Assert.Throws<ArgumentException>(() => tracker.Observe(record));
    }

    [Fact]
    public void CancellationAndReplacementDoNotShareNativeLocalLifetimes()
    {
        var tracker = new LoomNativeDependencyTracker(_ => { });
        var selector = new LoomScriptOperandBinding("source", "local[0x000]", "local[0x000]", 1, false);
        var first = Record(1, selector, values: new() { ["local[0x000]"] = 1 });
        tracker.Observe(first);
        tracker.Observe(first with { Sequence = 2, Slot = 9 });
        tracker.Observe(new(3, 1, null, 0, null, new Dictionary<string, ushort>(), [], false, CancelledSlots: [8]));
        Assert.Equal(9, Assert.Single(tracker.ActiveRelations).Observation.Slot);
    }

    [Fact]
    public void BoundsAndUnresolvedSelectorsCannotCreateNativeLifetime()
    {
        var tracker = new LoomNativeDependencyTracker(_ => { }, relationLimit: 1);
        var record = Record(1, new("source", "0x01", null, 1, false));
        tracker.Observe(record with { SourceAccess = record.SourceAccess! with { NativeDependencies = [record.SourceAccess!.NativeDependencies![0], record.SourceAccess.NativeDependencies[0]] } });
        Assert.Empty(tracker.ActiveRelations);
        tracker.Observe(Record(2, new("source", "var[0x060]", "var[0x060]", null, null, UnresolvedReason: "filtered")));
        Assert.Empty(tracker.ActiveRelations);
        tracker.Observe(record with { Sequence = 3 });
        tracker.NotifyNativeMutation("unknown", 1);
        Assert.Empty(tracker.ActiveRelations);
    }

    [Fact]
    public void SameValueScopedIndexWriteRetiresNativeSelectionWithoutTouchingHistory()
    {
        var tracker = new LoomNativeDependencyTracker(_ => { });
        var selector = new LoomScriptOperandBinding("source", "var[0x032+local[0x000]]", "var[0x034]", 1, false,
            new("index", "local[0x000]", "local[0x000]", 2, false));
        var record = Record(1, selector, values: new() { ["var[0x034]"] = 1, ["local[0x000]"] = 2 });
        tracker.Observe(record);
        var history = Assert.Single(tracker.ActiveRelations);
        tracker.Observe(record with { Sequence = 2, SourceAccess = new([new("destination-before", "local[0x000]", "local[0x000]", 2, false)], null, []) });
        Assert.Empty(tracker.ActiveRelations);
        Assert.Equal((ushort)2, history.Dependency.Selector.Index!.Value);
    }

    [Fact]
    public void NativeRecorderIsOptionalAndMutationScopesRestoreTheirPreviousSink()
    {
        var first = new LoomNativeDependencyTracker(_ => { });
        var second = new LoomNativeDependencyTracker(_ => { });
        first.Observe(Record(1, new("source", "0x01", null, 1, false)));
        second.Observe(Record(1, new("source", "0x01", null, 1, false)));
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        using (runtime.ObserveGlobalMutations(new(_ => { }, nativeDependencies: first)))
        {
            using (runtime.ObserveGlobalMutations(new(_ => { }, nativeDependencies: second)))
                runtime.NotifyNativeMutation("actor", 1);
            Assert.Single(first.ActiveRelations);
            Assert.Empty(second.ActiveRelations);
            runtime.NotifyNativeMutation("actor", 1);
        }
        Assert.Empty(first.ActiveRelations);
        first.Observe(Record(2, new("source", "0x01", null, 1, false)));
        runtime.NotifyNativeMutation("actor", 1);
        Assert.Single(first.ActiveRelations);
    }

    private static LoomScriptObservation Record(long sequence, LoomScriptOperandBinding selector, Dictionary<string, ushort>? values = null) =>
        new(sequence, 8, 7, 1, new(2, null, 1, 0, "instruction", 0, "getActorRoom", ""), values ?? new(), [], true,
            SourceAccess: new([selector], null, [], [new("getActorRoom", selector, 2, true, "native estimate")]));

    private static void Execute(LoomScriptRuntimeState runtime, LoomScriptObserver observer, params (string Op, string Args)[] operations)
    {
        var code = operations.Append((Op: "stopObjectCode", Args: "")).ToArray();
        var script = new LoomScriptDisassembly(2, 1, 0, code.Length, code.Length,
            code.Select((p, i) => new LoomScriptInstruction(i, 0, p.Op, p.Args, "00")).ToArray(), null, null, [], []);
        new LoomBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime, observation: new(observer, 8, 7));
    }
}
