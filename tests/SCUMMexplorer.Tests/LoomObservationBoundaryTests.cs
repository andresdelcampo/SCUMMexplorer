
namespace SCUMMexplorer.Tests;

public sealed class LoomObservationBoundaryTests
{
    [Theory]
    [InlineData("script")]
    [InlineData("context")]
    [InlineData("container")]
    [InlineData("resource")]
    [InlineData("parent")]
    [InlineData("stage")]
    public void AMatchingNumberCannotConfirmAForeignSourceOrCausalSnapshot(string mismatch)
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var tracker = new ScummRuntimeRelationTracker(updates.Add);
        var writer = Write(1, 12, 8);
        tracker.Observe(writer);
        var confirmation = Confirm(2, 12, 8);
        confirmation = mismatch switch {
            "script" => confirmation with { Site = confirmation.Site! with { Script = 99 } },
            "context" => confirmation with { Site = confirmation.Site! with { Context = "other object" } },
            "container" => confirmation with { Site = confirmation.Site! with { Container = 2 } },
            "resource" => confirmation with { Site = confirmation.Site! with { ResourceOffset = 7 } },
            "parent" => confirmation with { ParentSlot = 9 },
            _ => confirmation with { Site = confirmation.Site! with { Stage = "begin" } }
        };
        tracker.Observe(confirmation);
        Assert.Empty(updates.SelectMany(u => u.Writes));
    }

    [Theory]
    [InlineData("invocation")]
    [InlineData("parent")]
    public void AForeignTerminalBoundaryCannotEstablishWriterTermination(string mismatch)
    {
        var matches = new List<ScummCompletionRead>();
        var completion = new ScummCompletionObserver(matches.Add);
        var tracker = new ScummRuntimeRelationTracker(completion.Observe);
        tracker.Observe(Write(1, 12, 8));
        tracker.Observe(Confirm(2, 12, 8));
        var end = Confirm(3, 12, 8) with { Site = Site("end"), ReachedStop = true };
        end = mismatch == "invocation" ? end with { Invocation = 13 } : end with { ParentSlot = 9 };
        tracker.Observe(end);
        tracker.Observe(Read(4, 8));
        var match = Assert.Single(matches);
        Assert.True(match.MatchesOwnLiteralChild);
        Assert.False(match.WriterTerminated);
        Assert.NotNull(match.TerminalRejection);
        Assert.True(match.Writer!.ValueEstimated);
        Assert.True(match.Read.Binding.ValueEstimated);
    }

    [Fact]
    public void SameInvocationTerminalBoundaryPreservesIndependentKnownAndEstimatedPaths()
    {
        var matches = new List<ScummCompletionRead>();
        var tracker = new ScummRuntimeRelationTracker(new ScummCompletionObserver(matches.Add).Observe);
        tracker.Observe(Write(1, 12, 8));
        tracker.Observe(Confirm(2, 12, 8));
        tracker.Observe(Confirm(3, 12, 8) with { Site = Site("end"), ReachedStop = true });
        tracker.Observe(Read(4, 8));
        var match = Assert.Single(matches);
        Assert.True(match.MatchesOwnLiteralChild);
        Assert.True(match.WriterTerminated);
        Assert.True(match.Writer!.Observation.PathEstimated);
        Assert.False(match.Read.Observation.PathEstimated);
    }

    [Fact]
    public void ObservedArgumentNumbersDoNotEstablishCalleeLocalOriginsOrReturns()
    {
        var updates = new List<ScummRuntimeRelationUpdate>();
        var tracker = new ScummRuntimeRelationTracker(updates.Add);
        tracker.Observe(Write(1, 12, 8));
        tracker.Observe(Confirm(2, 12, 8) with {
            Site = Site("instruction") with { Operation = "startScript" },
            OperandBindings = [new("target", "0x02", null, 2, false),
                new("argument", "var[0x060]", "var[0x060]", 0, true, ArgumentIndex: 0)]
        });
        var argument = updates[^1].Reads.Single(r => r.Binding.Role == "argument");
        Assert.NotNull(argument.ObservedOrigin);
        var child = Read(3, 13) with { ParentSlot = 12,
            Values = new Dictionary<string, ushort> { ["var[0x060]"] = 0, ["local[0x000]"] = 0 },
            OperandBindings = [new("condition", "local[0x000]", "local[0x000]", 0, true)] };
        tracker.Observe(child);
        var local = Assert.Single(updates[^1].Reads);
        Assert.Null(local.ObservedOrigin);
        Assert.Equal(13, local.Dependencies.Single().LocalSlot);
        Assert.Empty(updates[^1].Writes);
    }

    private static ScummScriptObservationSite Site(string stage) => new(31, null, 1, 0, stage, 0, "stopObjectCode", "");
    private static ScummScriptObservation Write(long sequence, long slot, long parent) =>
        new(sequence, slot, parent, slot, Site("instruction") with { Operation = "move" },
            new Dictionary<string, ushort> { ["var[0x060]"] = 0 }, ["var[0x060]"], true,
            OperandBindings: [new("destination-before", "var[0x060]", "var[0x060]", 0, true),
                new("source", "0x0000", null, 0, false)]);
    private static ScummScriptObservation Confirm(long sequence, long slot, long parent) =>
        Write(sequence, slot, parent) with { Site = Site("instruction"), OperandBindings = [] };
    private static ScummScriptObservation Read(long sequence, long slot) =>
        Write(sequence, slot, 0) with { ParentSlot = null, PathEstimated = false,
            Site = Site("instruction") with { Script = 20, Operation = "equalZero" },
            OperandBindings = [new("condition", "var[0x060]", "var[0x060]", 0, true)] };
}
