using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomCompletionObserverTests
{
    [Fact]
    public void OwnLiteralChildUsesInstructionOrderAndPreservesIndependentEstimates()
    {
        var results = Capture(tracker => {
            Write(tracker, 1, 12, 8, path: true);
            Confirm(tracker, 2, 12, estimated: true);
            tracker.Observe(End(3, 12));
            Read(tracker, 4, 8, estimated: true, path: false);
        });
        var match = Assert.Single(results);
        Assert.True(match.MatchesOwnLiteralChild);
        Assert.True(match.Writer!.Observation.PathEstimated);
        Assert.True(match.Writer.ValueEstimated);
        Assert.True(match.Read.Binding.ValueEstimated);
        Assert.False(match.Read.Observation.PathEstimated);
        Assert.True(match.WriterEnd!.ReachedStop);
        Assert.True(match.WriterTerminated);
        Assert.Null(match.TerminalRejection);
        Assert.Equal(1, match.Writer.Observation.Sequence);
        Assert.Equal(2, match.Writer.ConfirmationSequence);
        Assert.Equal(4, match.Read.Observation.Sequence);
        Assert.Contains(match.Limits, l => l.Contains("scheduler/callee"));
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("cancel")]
    [InlineData("alias")]
    [InlineData("external")]
    [InlineData("omitted")]
    [InlineData("early")]
    public void ForeignCancelledAliasedExternalOmittedAndEarlyWritersCannotMatch(string kind)
    {
        var results = Capture(tracker => {
            Write(tracker, 1, 12, kind == "foreign" ? 9 : 8, value: kind == "early" ? (ushort)1 : (ushort)0);
            Confirm(tracker, 2, 12, value: kind == "early" ? (ushort)1 : (ushort)0, parent: kind == "foreign" ? 9 : 8);
            if (kind == "cancel") tracker.Observe(new(3, 8, null, 0, null, new Dictionary<string, ushort>(), [], false,
                CancelledNumber: 31, CancelledSlots: [12]));
            if (kind == "alias") tracker.Observe(LoomRuntimeRelationTests.Record(3, 9, 9, "move",
                [new("destination-before", "var[0x0D6]", "var[0x0D6]", 0, false), new("source", "0x0000", null, 0, false)],
                ("var[0x0D6]", 0), ("bit[0xD67]", 0)));
            if (kind == "external") tracker.NotifyExternal(["bit[0xD67]"]);
            if (kind == "omitted") tracker.NotifyExternal(reason: "filtered source site");
            Read(tracker, 4, 8);
        });
        Assert.False(Assert.Single(results).MatchesOwnLiteralChild);
        Assert.NotNull(results[0].Rejection);
    }

    [Fact]
    public void CancelledOldSlotCannotDonateWriterIdentityToItsReplacement()
    {
        var results = Capture(tracker => {
            Write(tracker, 1, 12, 8);
            Confirm(tracker, 2, 12);
            tracker.Observe(new(3, 8, null, 0, null, new Dictionary<string, ushort>(), [], false,
                CancelledNumber: 31, CancelledSlots: [12]));
            Read(tracker, 4, 8);
            Write(tracker, 5, 13, 8);
            Confirm(tracker, 6, 13);
            Read(tracker, 7, 8);
        });
        Assert.False(results[0].MatchesOwnLiteralChild);
        Assert.True(results[1].MatchesOwnLiteralChild);
        Assert.Equal(13, results[1].Writer!.Observation.Slot);
    }

    [Fact]
    public void EqualNumericValuesCannotConfirmAMissingOrNonliteralWrite()
    {
        var results = Capture(tracker => {
            Write(tracker, 1, 12, 8, literal: false);
            Confirm(tracker, 2, 12);
            Read(tracker, 3, 8);
            Write(tracker, 4, 13, 8);
            tracker.NotifyExternal(); // Retire the pending move before its next snapshot.
            Confirm(tracker, 5, 13);
            Read(tracker, 6, 8);
        });
        Assert.False(results[0].MatchesOwnLiteralChild);
        Assert.Equal("writer is not an observed literal zero move", results[0].Rejection);
        Assert.False(results[1].MatchesOwnLiteralChild);
        Assert.Null(results[1].Writer);
    }

    [Fact]
    public void TerminalObservationDoesNotSubstituteForTheLiteralWriter()
    {
        var results = Capture(tracker => {
            tracker.Observe(End(1, 12));
            Read(tracker, 2, 8);
            Write(tracker, 3, 12, 8);
            Confirm(tracker, 4, 12);
            Read(tracker, 5, 8);
        });
        Assert.False(results[0].MatchesOwnLiteralChild);
        Assert.Null(results[0].Writer);
        Assert.True(results[1].MatchesOwnLiteralChild);
        Assert.Null(results[1].WriterEnd); // Earlier termination cannot describe the later write.
        Assert.Throws<NotSupportedException>(() => ((IList<string>)results[0].Limits).Clear());
    }

    [Theory]
    [InlineData("yield")]
    [InlineData("partial")]
    [InlineData("unresolved")]
    [InlineData("other-resource")]
    public void WriterMatchDoesNotTurnAnUnrelatedOrNonterminalEndIntoTermination(string kind)
    {
        var results = Capture(tracker => {
            Write(tracker, 1, 12, 8);
            Confirm(tracker, 2, 12, estimated: true);
            var end = End(3, 12);
            end = kind switch {
                "yield" => end with { ReachedStop = false, ResumeOffset = 1 },
                "partial" => end with { ReachedStop = false },
                "unresolved" => end with { UnresolvedReason = "omitted callee effects" },
                _ => end with { Site = end.Site! with { ResourceOffset = 7 } }
            };
            tracker.Observe(end);
            Read(tracker, 4, 8, estimated: true);
        });
        var match = Assert.Single(results);
        Assert.False(match.WriterTerminated);
        Assert.NotNull(match.TerminalRejection);
        Assert.Equal(kind != "unresolved", match.MatchesOwnLiteralChild);
        if (kind == "other-resource") Assert.Null(match.WriterEnd);
        if (kind is "yield" or "partial") Assert.NotNull(match.WriterEnd);
    }

    [Fact]
    public void ArithmeticZeroIsObservedButCannotBecomeALiteralCompletionWriter()
    {
        var results = Capture(tracker => {
            tracker.Observe(LoomRuntimeRelationTests.Record(1, 12, 12, "and",
                [new("destination-before", "var[0x060]", "var[0x060]", 1, false),
                 new("source", "0x0000", null, 0, false)], ("var[0x060]", 1)) with { ParentSlot = 8 });
            tracker.Observe(LoomRuntimeRelationTests.Record(2, 12, 12, "stopObjectCode", [], ("var[0x060]", 0)) with { ParentSlot = 8 });
            tracker.Observe(LoomRuntimeRelationTests.Record(3, 8, 8, "equalZero",
                [new("condition", "var[0x060]", "var[0x060]", 0, false)], ("var[0x060]", 0)));
        });
        var match = Assert.Single(results);
        Assert.NotNull(match.Writer);
        Assert.False(match.MatchesOwnLiteralChild);
        Assert.Equal("writer is not an observed literal zero move", match.Rejection);
    }

    private static List<LoomCompletionRead> Capture(Action<LoomRuntimeRelationTracker> action)
    {
        var results = new List<LoomCompletionRead>();
        var completion = new LoomCompletionObserver(results.Add);
        action(new(completion.Observe));
        return results;
    }
    private static void Write(LoomRuntimeRelationTracker tracker, long sequence, long slot, long parent,
        ushort value = 0, bool path = false, bool literal = true) => tracker.Observe(
            LoomRuntimeRelationTests.Record(sequence, slot, slot, "move",
                [new("destination-before", "bit[0xD67]", "bit[0xD67]", 0, false, BackingWord: "var[0x0D6]"),
                 new("source", literal ? $"0x{value:X4}" : "local[0x000]", literal ? null : "local[0x000]", value, false)],
                ("var[0x0D6]", 0), ("bit[0xD67]", 0)) with { ParentSlot = parent, PathEstimated = path,
                    Site = new(31, null, 1, 0, "instruction", 0, "move", "") });
    private static void Confirm(LoomRuntimeRelationTracker tracker, long sequence, long slot, ushort value = 0, bool estimated = false, long parent = 8) =>
        tracker.Observe(LoomRuntimeRelationTests.Record(sequence, slot, slot, "stopObjectCode", [],
            ("var[0x0D6]", (ushort)(value << 7)), ("bit[0xD67]", value)) with {
                ParentSlot = parent, Site = new(31, null, 1, 0, "instruction", 0, "stopObjectCode", ""),
                Estimated = estimated ? ["bit[0xD67]"] : [] });
    private static LoomScriptObservation End(long sequence, long slot) => new(sequence, slot, 8, slot,
        new(31, null, 1, 0, "end", 0x322), new Dictionary<string, ushort> { ["var[0x0D6]"] = 0, ["bit[0xD67]"] = 0 },
        ["bit[0xD67]"], true, ReachedStop: true);
    private static void Read(LoomRuntimeRelationTracker tracker, long sequence, long slot, bool estimated = false, bool path = false) =>
        tracker.Observe(LoomRuntimeRelationTests.Record(sequence, slot, slot, "equalZero",
            [new("condition", "bit[0xD67]", "bit[0xD67]", 0, estimated, BackingWord: "var[0x0D6]")],
            ("var[0x0D6]", 0), ("bit[0xD67]", 0)) with { PathEstimated = path, Estimated = estimated ? ["bit[0xD67]"] : [] });
}
