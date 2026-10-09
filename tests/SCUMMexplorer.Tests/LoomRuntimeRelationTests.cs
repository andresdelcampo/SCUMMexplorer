
namespace SCUMMexplorer.Tests;

public sealed class LoomRuntimeRelationTests
{
    [Fact]
    public void IndexedReadExpiresOnSameValueIndexWriteWhileHistoryKeepsItsEstimates()
    {
        var updates = Run(("move", "local[0x001] = var[0x032+var[0x060]]"),
            ("move", "var[0x060] = 0x0002"), ("stopObjectCode", ""));
        var read = updates.SelectMany(u => u.Reads).First(r => r.Binding.Key == "var[0x034]");
        Assert.Equal((ushort)17, read.Binding.Value);
        Assert.True(read.Binding.Index!.ValueEstimated);
        Assert.False(read.Binding.ValueEstimated);
        Assert.True(read.Observation.PathEstimated);
        Assert.Contains(new ScummRuntimeDependency("var[0x060]", null), read.Dependencies);
        Assert.Contains(updates.Single(u => u.Observation?.Site?.Offset == 1).Invalidated, i => i.Read == read.Id);
        var copy = updates.SelectMany(u => u.Writes).First(w => w.Destination.Key == "local[0x001]");
        Assert.Contains(read.Id, copy.SourceReads);
        Assert.True(copy.ValueEstimated);
        Assert.Throws<NotSupportedException>(() => ((IList<long>)copy.SourceReads).Clear());
    }

    [Theory]
    [InlineData("var[0x0D6] = 0x0000", "bit[0xD67]")]
    [InlineData("bit[0xD67] = 0x0000", "var[0x0D6]")]
    [InlineData("bit[0xD69] = 0x0000", "bit[0xD67]")]
    public void WordBitAndSiblingAliasesConservativelyExpireRelations(string write, string readKey)
    {
        var updates = Run(("equalZero", readKey + ", branch +0 -> 0x0001"), ("move", write), ("stopObjectCode", ""));
        var read = updates.SelectMany(u => u.Reads).Single(r => r.Binding.Key == readKey);
        Assert.Contains(updates.SelectMany(u => u.Invalidated), i => i.Read == read.Id && i.Reason.Contains("aliases"));
    }

    [Fact]
    public void ArgumentRelationsRetainOrderWholeBindingsAndExpireOnSourceWrites()
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var tracker = new ScummRuntimeRelationTracker(updates.Add);
        var first = Record(1, 8, 1, "startScript", [new("target", "0x03", null, 3, false),
            new("argument", "var[0x034]", "var[0x034]", 17, true, ArgumentIndex: 0),
            new("argument", "0x0000", null, 0, false, ArgumentIndex: 1)]);
        tracker.Observe(first);
        tracker.NotifyExternal(["var[0x034]"]);
        var args = updates[0].Reads.Where(r => r.Binding.Role == "argument").ToArray();
        Assert.Equal(new int?[] { 0, 1 }, args.Select(r => r.Binding.ArgumentIndex));
        Assert.True(args[0].Binding.ValueEstimated);
        Assert.Contains(updates[1].Invalidated, i => i.Read == args[0].Id);
        Assert.Empty(tracker.ActiveRelations);
    }

    [Fact]
    public void FilteredSitesAndUnmodelledEffectsPreventStaleWriterReuse()
    {
        var filtered = Run(("move", "var[0x060] = 0x0002"), ("debug", ""),
            ("move", "local[0x001] = var[0x060]"), ("stopObjectCode", ""),
            include: s => s.Operation != "debug");
        Assert.Contains(filtered, u => u.Barrier == "filtered source site");
        Assert.Null(filtered.SelectMany(u => u.Reads).Single(r => r.Binding.Key == "var[0x060]").ObservedOrigin);
        var updates = new List<ScummRuntimeRelationUpdate>();
        var tracker = new ScummRuntimeRelationTracker(updates.Add);
        tracker.Observe(Record(1, 1, 1, "equalZero", [new("condition", "var[0x060]", "var[0x060]", 0, false)]));
        tracker.Observe(Record(2, 2, 2, "unknownEffect", []));
        Assert.Empty(tracker.ActiveRelations);
        Assert.Equal("unmodelled instruction access", updates[^1].Barrier);
    }

    [Fact]
    public void CancellationAndReplacementKeepLocalScopesDistinct()
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var tracker = new ScummRuntimeRelationTracker(updates.Add);
        tracker.Observe(Record(1, 8, 1, "equalZero", [new("condition", "local[0x000]", "local[0x000]", 0, false)]));
        tracker.Observe(Record(2, 9, 2, "equalZero", [new("condition", "local[0x000]", "local[0x000]", 0, false)]));
        tracker.Observe(new(3, 1, null, 0, null, new Dictionary<string, ushort>(), [], false, CancelledNumber: 3, CancelledSlots: [8]));
        Assert.Equal(9, Assert.Single(tracker.ActiveRelations).Observation.Slot);
        Assert.Equal(8, updates[0].Reads.Single().Dependencies.Single().LocalSlot);
        Assert.Single(updates[^1].Invalidated);
    }

    [Fact]
    public void ChangedSnapshotsAndExplicitSameValueExternalEffectsInvalidateReads()
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var tracker = new ScummRuntimeRelationTracker(updates.Add);
        tracker.Observe(Record(1, 8, 1, "equalZero", [new("condition", "var[0x060]", "var[0x060]", 0, false)], ("var[0x060]", 0)));
        tracker.Observe(Record(2, 9, 2, "debug", [], ("var[0x060]", 1)));
        Assert.Contains(updates[^1].Invalidated, i => i.Reason == "changed snapshot or external effect");
        tracker.Observe(Record(3, 8, 3, "equalZero", [new("condition", "var[0x060]", "var[0x060]", 1, false)]));
        tracker.NotifyExternal(["var[0x060]"]);
        Assert.Empty(tracker.ActiveRelations);
        Assert.Single(updates[^1].Invalidated);
    }

    [Fact]
    public void BoundsAndUnresolvedIndexesNeverEstablishOrigins()
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var tracker = new ScummRuntimeRelationTracker(updates.Add, relationLimit: 2);
        for (var i = 1; i <= 3; i++) tracker.Observe(Record(i, 1, 1, "equalZero",
            [new("condition", "var[0x060]", "var[0x060]", 0, false)]));
        Assert.Contains(updates, u => u.Barrier == "relation bound reached");
        tracker.Observe(Record(4, 1, 1, "move", [new("destination-before", "local[0x001]", "local[0x001]", 0, false),
            new("source", "var[0x032+var[0x060]]", null, null, null,
                new("index", "var[0x060]", "var[0x060]", null, null), UnresolvedReason: "unresolved index")]));
        Assert.All(updates[^1].Reads, r => Assert.Null(r.ObservedOrigin));
        Assert.Empty(updates[^1].Writes);
    }

    [Fact]
    public void MissingSnapshotsAndAliasEstimateChangesExpireCorrespondence()
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var tracker = new ScummRuntimeRelationTracker(updates.Add);
        var record = Record(1, 1, 1, "equalZero", [new("condition", "bit[0xD67]", "bit[0xD67]", 0, false)],
            ("var[0x0D6]", 0), ("bit[0xD67]", 0));
        tracker.Observe(record);
        tracker.Observe(record with { Sequence = 2, Estimated = ["bit[0xD67]"] });
        Assert.Contains(updates[^1].Invalidated, i => i.Read == updates[0].Reads.Single().Id);
        tracker.Observe(Record(3, 1, 1, "debug", []));
        Assert.Contains(updates[^1].Invalidated, i => i.Reason == "missing or filtered snapshot dependency");
        Assert.Empty(tracker.ActiveRelations);
    }

    [Fact]
    public void LocalRelationSurvivesAResumptionButOutOfOrderRecordsAreRejected()
    {
        var tracker = new ScummRuntimeRelationTracker(_ => { });
        var record = Record(1, 8, 1, "equalZero", [new("condition", "local[0x000]", "local[0x000]", 0, false)], ("local[0x000]", 0));
        tracker.Observe(record);
        tracker.Observe(record with { Sequence = 2, Site = record.Site! with { Stage = "end" }, OperandBindings = null,
            ResumeOffset = 1, ReachedStop = false });
        tracker.Observe(record with { Sequence = 3, Invocation = 2, Site = record.Site! with { Stage = "begin" }, OperandBindings = null });
        Assert.Single(tracker.ActiveRelations);
        Assert.Throws<ArgumentException>(() => tracker.Observe(record));
    }

    [Fact]
    public void RelationCapturePreservesRuntimeResultsAndObservationChronology()
    {
        var (untracked, baseline) = Execute(false);
        var (tracked, observed) = Execute(true);
        Assert.Equal(untracked.KnownVariables, tracked.KnownVariables);
        Assert.Equal(untracked.Trace, tracked.Trace);
        Assert.Equal(untracked.EstimatedLocals, tracked.EstimatedLocals);
        Assert.Equal(baseline.Select(r => (r.Sequence, r.Slot, r.Invocation, r.PathEstimated)),
            observed.Select(r => (r.Sequence, r.Slot, r.Invocation, r.PathEstimated)));

        static (ScummBoundedScriptResult, List<ScummScriptObservation>) Execute(bool track)
        {
            var records = new List<ScummScriptObservation>();
            var runtime = Runtime();
            var result = new ScummBoundedScriptExecutor().Execute(Code(("move", "local[0x001] = var[0x032+var[0x060]]"),
                ("stopObjectCode", "")), runtime.ObjectStates, [], runtime: runtime,
                observation: new(new(records.Add, captureOperandBindings: true,
                    runtimeRelations: track ? new(_ => { }) : null), 1));
            return (result, records);
        }
    }

    private static ScummScriptRuntimeState Runtime()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        runtime.GlobalVariables["var[0x060]"] = 2;
        runtime.GlobalVariables["var[0x034]"] = 17;
        runtime.EstimatedVariables.Add("var[0x060]");
        return runtime;
    }
    private static ScummScriptDisassembly Code(params (string Op, string Args)[] operations) =>
        new(2, 1, 0, operations.Length, operations.Length,
            operations.Select((o, i) => new ScummScriptInstruction(i, 0, o.Op, o.Args, "00")).ToArray(), null, null, [], []);
    private static List<ScummRuntimeRelationUpdate> Run((string, string) first, (string, string) second, (string, string) third,
        (string, string)? fourth = null, Func<ScummScriptObservationSite, bool>? include = null)
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var runtime = Runtime();
        new ScummBoundedScriptExecutor().Execute(Code(fourth is { } last ? [first, second, third, last] : [first, second, third]),
            runtime.ObjectStates, [], runtime: runtime, pathEstimated: true,
            observation: new(new(_ => { }, includeSite: include, runtimeRelations: new(updates.Add)), 1));
        return updates;
    }
    internal static ScummScriptObservation Record(long sequence, long slot, long run, string op,
        IReadOnlyList<ScummScriptOperandBinding> bindings, params (string Key, ushort Value)[] values) =>
        new(sequence, slot, null, run, new(2, null, 1, 0, "instruction", 0, op, ""),
            values.ToDictionary(p => p.Key, p => p.Value), [], false, OperandBindings: bindings);
}
