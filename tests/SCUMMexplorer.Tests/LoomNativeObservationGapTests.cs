
namespace SCUMMexplorer.Tests;

public sealed class LoomNativeObservationGapTests
{
    [Theory]
    [InlineData("instruction", false)]
    [InlineData("begin", false)]
    [InlineData("end", false)]
    [InlineData("cancel-other", false)]
    [InlineData("instruction", true)]
    [InlineData("begin", true)]
    [InlineData("end", true)]
    [InlineData("cancel-other", true)]
    public void OmittedObservationCannotPreserveNativeHistoryThroughEqualValues(string phase, bool gap)
    {
        var updates = new List<ScummNativeDependencyUpdate>();
        var tracker = new ScummNativeDependencyTracker(updates.Add);
        var read = Read(41); // A selected stream can start at an arbitrary sequence.
        tracker.Observe(read);
        var history = Assert.Single(tracker.ActiveRelations);
        var next = read with { Sequence = gap ? 43 : 42, SourceAccess = new([], null, []),
            Site = read.Site! with { Stage = phase == "cancel-other" ? "instruction" : phase } };
        if (phase == "cancel-other") next = next with { Site = null, CancelledSlots = [999] };
        tracker.Observe(next);
        if (gap) {
            Assert.Empty(tracker.ActiveRelations);
            Assert.Contains(updates[^1].Invalidated, i => i.Read == history.Id && i.Reason == "missing native observation sequence boundary");
            Assert.Equal("missing native observation sequence boundary", updates[^1].Barrier);
        }
        else Assert.Equal(history, Assert.Single(tracker.ActiveRelations));
        Assert.True(history.Dependency.ValueEstimated);
        Assert.Equal("copied native estimate", history.Dependency.Estimate);
        Assert.True(history.Observation.PathEstimated);
    }

    [Fact]
    public void FreshCopiedReadAfterGapHasItsOwnIdentityAndCannotRevivePreviousHistory()
    {
        var updates = new List<ScummNativeDependencyUpdate>();
        var tracker = new ScummNativeDependencyTracker(updates.Add);
        tracker.Observe(Read(1));
        var history = Assert.Single(tracker.ActiveRelations);
        tracker.Observe(Read(3));
        var current = Assert.Single(tracker.ActiveRelations);
        Assert.NotEqual(history.Id, current.Id);
        Assert.Equal(history.Dependency, current.Dependency);
        Assert.Contains(updates[^1].Invalidated, i => i.Read == history.Id);
        Assert.Equal(current, Assert.Single(updates[^1].Reads));
        tracker.NotifyNativeMutation("actor", 1);
        Assert.Empty(tracker.ActiveRelations);
        Assert.True(history.Dependency.ValueEstimated);
    }

    [Theory]
    [InlineData("string-storage")]
    [InlineData("object-owner")]
    public void ExplicitOpaqueMutationStillRetiresAllFamiliesWithoutAChangedValueClaim(string domain)
    {
        var tracker = new ScummNativeDependencyTracker(_ => { });
        tracker.Observe(Read(1));
        var history = Assert.Single(tracker.ActiveRelations);
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        using (runtime.ObserveGlobalMutations(new(_ => { }, nativeDependencies: tracker)))
            runtime.NotifyNativeMutation(domain, 204);
        Assert.Empty(tracker.ActiveRelations);
        Assert.Equal(2, history.Dependency.Value);
        Assert.Equal("copied native estimate", history.Dependency.Estimate);
        tracker.Observe(Read(2));
        runtime.NotifyNativeMutation(domain, 204); // Disposed scope observes nothing.
        Assert.Single(tracker.ActiveRelations);
    }

    [Fact]
    public void RepeatedObservationIsRejectedWithoutMutatingPublishedHistory()
    {
        var tracker = new ScummNativeDependencyTracker(_ => { });
        tracker.Observe(Read(1));
        var history = Assert.Single(tracker.ActiveRelations);
        Assert.Throws<ArgumentException>(() => tracker.Observe(Read(1)));
        Assert.Equal(history, Assert.Single(tracker.ActiveRelations));
    }

    private static ScummScriptObservation Read(long sequence)
    {
        var selector = new ScummScriptOperandBinding("source", "0x01", null, 1, false);
        return new(sequence, 8, 7, 1, new(2, null, 1, 0, "instruction", 0, "getActorRoom", ""),
            new Dictionary<string, ushort>(), [], true, SourceAccess: new([selector], null, [],
                [new("getActorRoom", selector, 2, true, "copied native estimate")]));
    }
}
