
namespace SCUMMexplorer.Tests;

public sealed class LoomObservedLineageTests
{
    [Theory]
    [InlineData("add", "+=", 65535, 1, 0)]
    [InlineData("subtract", "-=", 0, 1, 65535)]
    [InlineData("multiply", "*=", 32768, 2, 0)]
    [InlineData("divide", "/=", 65535, 2, 32767)]
    [InlineData("and", "&=", 0x3018, 0xFFF, 0x18)]
    [InlineData("or", "|=", 0x1018, 0x2000, 0x3018)]
    [InlineData("increment", "", 65535, 0, 0)]
    [InlineData("decrement", "", 0, 0, 65535)]
    public void ArithmeticNeedsASnapshotAndRetainsBeforeValueAndIndependentEstimates(
        string operation, string symbol, int left, int right, int expected)
    {
        var runtime = Runtime();
        runtime.GlobalVariables["var[0x060]"] = (ushort)left;
        var updates = Run(runtime, (operation, symbol.Length == 0 ? "var[0x060]" : $"var[0x060] {symbol} 0x{right:X4}"),
            ("move", "local[0x000] = var[0x060]"), ("stopObjectCode", ""));
        Assert.Empty(updates.Single(u => u.Observation?.Site is { Stage: "instruction", Offset: 0 }).Writes);
        var write = updates.SelectMany(u => u.Writes).Single(w => w.Destination.Key == "var[0x060]");
        Assert.Equal((ushort)expected, write.Value);
        Assert.True(write.ValueEstimated);
        Assert.True(write.Observation.PathEstimated);
        Assert.True(write.ConfirmationSequence > write.Observation.Sequence);
        var before = updates.SelectMany(u => u.Reads).Single(r => r.Binding.Role == "destination-value");
        Assert.Equal((ushort)left, before.Binding.Value);
        Assert.False(before.Binding.ValueEstimated);
        Assert.Contains(before.Id, write.SourceReads);
        Assert.Same(write, updates.SelectMany(u => u.Reads).Single(r => r.Binding.Role == "source" && r.Binding.Key == "var[0x060]").ObservedOrigin);
    }

    [Fact]
    public void MasksIndexesAndOrderedArgumentsRetainTheActualWholeLineage()
    {
        var runtime = Runtime();
        runtime.GlobalVariables["var[0x034]"] = 0x3018;
        var updates = Run(runtime, ("move", "local[0x001] = var[0x060]"),
            ("multiply", "local[0x001] *= 0x0002"),
            ("move", "local[0x002] = var[0x032+local[0x001]]"),
            ("and", "local[0x002] &= 0x0007"), ("increment", "local[0x002]"),
            ("startScript", "0x03, [local[0x002], 0x0000]"), ("stopObjectCode", ""));
        var read = updates.SelectMany(u => u.Reads).Single(r => r.Binding.Role == "source" && r.Binding.Key == "var[0x034]");
        Assert.Equal("local[0x001]", read.IndexRead!.Binding.Key);
        Assert.Equal((ushort)2, read.IndexRead.Binding.Value);
        Assert.Equal("multiply", read.IndexRead.ObservedOrigin!.Observation.Site!.Operation);
        var args = updates.SelectMany(u => u.Reads).Where(r => r.Binding.Role == "argument").ToArray();
        Assert.Equal(new int?[] { 0, 1 }, args.Select(r => r.Binding.ArgumentIndex));
        Assert.Equal((ushort)1, args[0].Binding.Value);
        Assert.True(args[0].Binding.ValueEstimated);
        Assert.Equal("increment", args[0].ObservedOrigin!.Observation.Site!.Operation);
        Assert.Null(args[1].ObservedOrigin);
        var copies = updates.SelectMany(u => u.Writes).ToArray();
        var selectedCopy = copies.Single(w => w.Observation.Site!.Offset == 2);
        Assert.Contains(read.Id, selectedCopy.SourceReads);
        Assert.Contains(read.IndexRead.Id, selectedCopy.SourceReads);
        Assert.DoesNotContain(copies, w => w.Observation.Site!.Operation == "startScript");
    }

    [Fact]
    public void DestinationIndexIsHistoricalLineageAndSameValueIndexMutationRejectsAPendingCopy()
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var tracker = new ScummRuntimeRelationTracker(updates.Add);
        var destination = new ScummScriptOperandBinding("destination-before", "var[0x032+var[0x060]]", "var[0x034]", 0, false,
            new("index", "var[0x060]", "var[0x060]", 2, true));
        tracker.Observe(LoomRuntimeRelationTests.Record(1, 1, 1, "move",
            [destination, new("source", "0x0009", null, 9, false)], ("var[0x034]", 0), ("var[0x060]", 2)));
        Assert.True(updates[0].Reads.Single(r => r.Binding.Role == "index").Binding.ValueEstimated);
        tracker.NotifyExternal(["var[0x060]"]);
        tracker.Observe(LoomRuntimeRelationTests.Record(2, 1, 1, "debug", [], ("var[0x034]", 9), ("var[0x060]", 2)));
        Assert.Empty(updates.SelectMany(u => u.Writes));
    }

    [Fact]
    public void FailedDivisionAndMismatchedSnapshotsNeverManufactureAnOrigin()
    {
        var updates = Run(Runtime(), ("divide", "var[0x060] /= 0x0000"), ("stopObjectCode", ""));
        Assert.Empty(updates.SelectMany(u => u.Writes));
        Assert.Contains(updates, u => u.Barrier == "unresolved execution or omitted effects");
        var tracker = new ScummRuntimeRelationTracker(updates.Add);
        tracker.Observe(LoomRuntimeRelationTests.Record(1, 1, 1, "add",
            [new("destination-before", "var[0x060]", "var[0x060]", 1, false), new("source", "0x0001", null, 1, false)],
            ("var[0x060]", 1)));
        tracker.Observe(LoomRuntimeRelationTests.Record(2, 1, 1, "debug", [], ("var[0x060]", 3)));
        Assert.Empty(updates.SelectMany(u => u.Writes));
    }

    [Fact]
    public void OversizedArgumentBatchAndWriterStorageStayBounded()
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var tracker = new ScummRuntimeRelationTracker(updates.Add, relationLimit: 2);
        tracker.Observe(LoomRuntimeRelationTests.Record(1, 1, 1, "startScript",
            Enumerable.Range(0, 3).Select(i => new ScummScriptOperandBinding("argument", "var[0x060]", "var[0x060]", 1, false,
                ArgumentIndex: i)).ToArray()));
        Assert.Empty(tracker.ActiveRelations);
        Assert.Empty(updates[^1].Reads);
        Assert.Equal("relation bound reached", updates[^1].Barrier);
        var values = new List<(string, ushort)>();
        for (var i = 0; i < 4; i++)
        {
            var key = $"var[0x{0x60 + i:X3}]";
            values.Add((key, 0));
            tracker.Observe(LoomRuntimeRelationTests.Record(2 + i * 2, 1, 1, "move",
                [new("destination-before", key, key, 0, false), new("source", "0x0000", null, 0, false)], values.ToArray()));
            tracker.Observe(LoomRuntimeRelationTests.Record(3 + i * 2, 1, 1, "stopObjectCode", [], values.ToArray()));
        }
        Assert.Contains(updates.Skip(1), u => u.Barrier == "relation bound reached");
    }

    private static ScummScriptRuntimeState Runtime()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        runtime.GlobalVariables["var[0x060]"] = 1;
        return runtime;
    }
    private static List<ScummRuntimeRelationUpdate> Run(ScummScriptRuntimeState runtime,
        params (string Op, string Args)[] operations)
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var code = new ScummScriptDisassembly(2, 1, 0, operations.Length, operations.Length,
            operations.Select((o, i) => new ScummScriptInstruction(i, 0, o.Op, o.Args, "00")).ToArray(), null, null, [], []);
        new ScummBoundedScriptExecutor().Execute(code, runtime.ObjectStates, [], runtime: runtime, pathEstimated: true,
            onEvent: (_, _) => "", observation: new(new(_ => { }, runtimeRelations: new(updates.Add)), 1));
        return updates;
    }
}
