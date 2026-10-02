using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomWriterContinuityTests
{
    [Fact]
    public void TwoObservedResumptionsRetainTheirOwnResourceParentAndEstimates()
    {
        var matches = new List<LoomCompletionRead>();
        var tracker = new LoomRuntimeRelationTracker(new LoomCompletionObserver(matches.Add).Observe);
        tracker.Observe(Write(1));
        tracker.Observe(Boundary(2, "instruction", 12));
        tracker.Observe(Boundary(3, "end", 12, resume: 8));
        tracker.Observe(Boundary(4, "begin", 13, offset: 8));
        tracker.Observe(Boundary(5, "end", 13, resume: 9));
        tracker.Observe(Boundary(6, "begin", 14, offset: 9));
        tracker.Observe(Boundary(7, "end", 14, stop: true));
        tracker.Observe(Read(8));
        var match = Assert.Single(matches);
        Assert.True(match.MatchesOwnLiteralChild);
        Assert.True(match.WriterTerminated);
        Assert.Equal(14, match.WriterEnd!.Invocation);
        Assert.Equal(new long[] { 3, 4, 5, 6, 7 }, match.ResumedTerminalContinuity!.Boundaries.Select(b => b.Sequence));
        Assert.All(match.ResumedTerminalContinuity.Boundaries, b => Assert.True(b.PathEstimated));
        Assert.False(match.Read.Observation.PathEstimated);
        Assert.True(match.Writer!.ValueEstimated);
        Assert.Throws<NotSupportedException>(() => ((IList<LoomScriptObservation>)match.ResumedTerminalContinuity.Boundaries).Clear());
        Assert.Contains(match.Limits, l => l.Contains("scheduler/callee"));
    }

    [Theory]
    [InlineData("missing-yield")]
    [InlineData("missing-begin")]
    [InlineData("offset")]
    [InlineData("parent")]
    [InlineData("resource")]
    [InlineData("invocation")]
    [InlineData("partial")]
    [InlineData("unresolved")]
    [InlineData("bound")]
    public void IncompleteOrForeignContinuityCannotDonateTerminalStatus(string gap)
    {
        var matches = new List<LoomCompletionRead>();
        var tracker = new LoomRuntimeRelationTracker(new LoomCompletionObserver(matches.Add, boundaryLimit: gap == "bound" ? 2 : 128).Observe);
        tracker.Observe(Write(1));
        tracker.Observe(Boundary(2, "instruction", 12));
        if (gap != "missing-yield") tracker.Observe(Boundary(3, "end", 12, resume: 8));
        var begin = Boundary(4, "begin", 13, offset: 8);
        begin = gap switch {
            "offset" => begin with { Site = begin.Site! with { Offset = 9 } },
            "parent" => begin with { ParentSlot = 9 },
            "resource" => begin with { Site = begin.Site! with { ResourceOffset = 1 } },
            "invocation" => begin with { Invocation = 12 },
            _ => begin
        };
        if (gap != "missing-begin") tracker.Observe(begin);
        var end = Boundary(5, "end", 13, stop: true);
        end = gap switch {
            "partial" => end with { ReachedStop = false },
            "unresolved" => end with { UnresolvedReason = "unsupported execution" },
            _ => end
        };
        tracker.Observe(end);
        tracker.Observe(Read(6));
        var match = Assert.Single(matches);
        Assert.False(match.WriterTerminated);
        Assert.Null(match.ResumedTerminalContinuity);
        Assert.NotNull(match.TerminalRejection);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("external")]
    [InlineData("filtered")]
    [InlineData("replacement")]
    public void CancellationOmissionsAndReplacementBreakTheObservedChain(string gap)
    {
        var matches = new List<LoomCompletionRead>();
        var tracker = new LoomRuntimeRelationTracker(new LoomCompletionObserver(matches.Add).Observe);
        tracker.Observe(Write(1));
        tracker.Observe(Boundary(2, "instruction", 12));
        tracker.Observe(Boundary(3, "end", 12, resume: 8));
        if (gap == "cancel") tracker.Observe(new(4, 8, null, 0, null, new Dictionary<string, ushort>(), [], false, CancelledSlots: [12]));
        if (gap == "external") tracker.NotifyExternal(["var[0x060]"]);
        if (gap == "filtered") tracker.NotifyExternal(reason: "filtered source site");
        tracker.Observe(Boundary(5, "begin", 13, offset: 8) with { Slot = gap == "replacement" ? 19 : 12 });
        tracker.Observe(Boundary(6, "end", 13, stop: true) with { Slot = gap == "replacement" ? 19 : 12 });
        tracker.Observe(Read(7));
        Assert.False(Assert.Single(matches).WriterTerminated);
        Assert.Null(matches[0].ResumedTerminalContinuity);
    }

    [Fact]
    public void ActualExecutorResumeIsObservedSeparatelyFromTheEarlierZeroRead()
    {
        var matches = new List<LoomCompletionRead>();
        var records = new List<LoomScriptObservation>();
        var observer = new LoomScriptObserver(records.Add,
            runtimeRelations: new(new LoomCompletionObserver(matches.Add).Observe));
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        var writer = Code(31, ("move", "var[0x060] = 0x0000"), ("breakHere", ""), ("stopObjectCode", ""));
        var reader = Code(20, ("equalZero", "var[0x060], branch +0 -> 0x0001"), ("stopObjectCode", ""));
        var executor = new LoomBoundedScriptExecutor();
        var yielded = executor.Execute(writer, runtime.ObjectStates, [], runtime: runtime, pathEstimated: true, observation: new(observer, 12, 8));
        executor.Execute(reader, runtime.ObjectStates, [], runtime: runtime, observation: new(observer, 8));
        executor.Execute(writer, runtime.ObjectStates, [], runtime: runtime, startOffset: yielded.ResumeOffset!.Value,
            pathEstimated: yielded.PathEstimated, observation: new(observer, 12, 8));
        executor.Execute(reader, runtime.ObjectStates, [], runtime: runtime, observation: new(observer, 8));
        Assert.Equal(2, matches.Count);
        Assert.True(matches[0].MatchesOwnLiteralChild);
        Assert.False(matches[0].WriterTerminated);
        Assert.True(matches[1].WriterTerminated);
        Assert.NotNull(matches[1].ResumedTerminalContinuity);
        Assert.True(matches[1].Writer!.ValueEstimated);
    }

    private static LoomScriptObservation Write(long sequence) => Boundary(sequence, "instruction", 12) with {
        Site = Site("instruction", 0) with { Operation = "move" },
        OperandBindings = [new("destination-before", "var[0x060]", "var[0x060]", 0, true), new("source", "0x0000", null, 0, false)]
    };
    private static LoomScriptObservation Boundary(long sequence, string stage, long invocation, int offset = 1, int? resume = null, bool stop = false) =>
        new(sequence, 12, 8, invocation, Site(stage, offset), new Dictionary<string, ushort> { ["var[0x060]"] = 0 }, ["var[0x060]"], true,
            ResumeOffset: resume, ReachedStop: stop, OperandBindings: stage == "instruction" ? [] : null);
    private static LoomScriptObservation Read(long sequence) => Boundary(sequence, "instruction", 99) with {
        Slot = 8, ParentSlot = null, PathEstimated = false, Site = Site("instruction", 0) with { Script = 20, Operation = "equalZero" },
        OperandBindings = [new("condition", "var[0x060]", "var[0x060]", 0, true)]
    };
    private static LoomScriptObservationSite Site(string stage, int offset) => new(31, null, 1, 0, stage, offset, "stopObjectCode", "");
    private static LoomScriptDisassembly Code(int script, params (string Op, string Args)[] operations) => new(script, 1, 0, operations.Length, operations.Length,
        operations.Select((p, i) => new LoomScriptInstruction(i, 0, p.Op, p.Args, "00")).ToArray(), null, null, [], []);
}
