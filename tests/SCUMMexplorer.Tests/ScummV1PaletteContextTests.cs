using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Tests;

public sealed class ScummV1PaletteContextTests
{
    private static ScummScriptInstruction Op(int offset, string operation, string operands) => new(offset, 0, operation, operands, "00 00 00 00");
    private static ScummScriptInstruction Actor(int offset, string actor, string value, bool costume) =>
        Op(offset, "actorOps", $"{actor}, {value}, sub=0x{(costume ? "04" : "02")}");
    private static IReadOnlyList<ScummV1CostumeColorContext> Pairs(params ScummScriptInstruction[] instructions) =>
        ScummV1PaletteContexts.SourcePairs(new(1, 1, 0, 100, 100, instructions, null, null, [], [], Issues: []), "test");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdjacentLiteralSourcePairWorksInEitherOrder(bool paletteFirst)
    {
        var context = Assert.Single(Pairs(Actor(0, "0x03", paletteFirst ? "0x09" : "0x07", !paletteFirst),
            Actor(4, "0x03", paletteFirst ? "0x07" : "0x09", paletteFirst)));
        Assert.Equal((7, 8, 9, 3), (context.Costume, context.Skin, context.Suit, context.Actor));
        Assert.Equal(ScummV1ColorContextBasis.SourcePath, context.Basis);
        Assert.Contains("not execution", context.Evidence);
    }

    [Fact]
    public void IdenticalVariableActorOperandCanEstablishAConditionalPair()
    {
        var context = Assert.Single(Pairs(Actor(0, "var[0x042]", "0x15", true), Actor(4, "var[0x042]", "0x09", false)));
        Assert.Null(context.Actor);
        Assert.Equal((21, 9), (context.Costume, context.Suit));
    }

    [Fact]
    public void DifferentAndPossiblyAliasingActorOperandsNeverBridgeSourcePairs()
    {
        Assert.Empty(Pairs(Actor(0, "0x05", "0x0B", false), Actor(4, "0x01", "0x09", true)));
        Assert.Empty(Pairs(Actor(0, "0x01", "0x0D", false), Actor(4, "var[0x042]", "0x07", false), Actor(8, "0x01", "0x05", true)));
    }

    [Theory]
    [InlineData("startScript", "0x22")]
    [InlineData("breakHere", "")]
    [InlineData("delay", "60")]
    [InlineData("move", "var[0x042], 0x05")]
    [InlineData("unknown", "")]
    public void CallsYieldsAndUnmodelledEffectsBreakConditionalPairs(string operation, string operands) =>
        Assert.Empty(Pairs(Actor(0, "0x01", "0x0D", false), Op(4, operation, operands), Actor(8, "0x01", "0x05", true)));

    [Fact]
    public void JumpIntoTheSecondSettingAndUndecodedGapsBreakPairs()
    {
        Assert.Empty(Pairs(Op(0, "isEqual", "var[0x001], 0x01, +4 -> 0x0008"),
            Actor(4, "0x01", "0x0D", false), Actor(8, "0x01", "0x05", true)));
        Assert.Empty(Pairs(Actor(0, "0x01", "0x0D", false), Actor(8, "0x01", "0x05", true)));
    }

    [Fact]
    public void AnUnresolvedPaletteDoesNotBorrowThePreviousLiteral()
    {
        Assert.Empty(Pairs(Actor(0, "0x04", "0x07", false), Actor(4, "0x04", "var[0x0C4]", false), Actor(8, "0x04", "0x07", true)));
    }

    private static ScummScriptObservation Observation(long sequence, string operands,
        IReadOnlyDictionary<string, ushort>? values = null, string[]? estimated = null, bool path = false) =>
        new(sequence, 1, null, 1, new(1, null, 1, 0, "instruction", (int)sequence, "actorOps", operands),
            values ?? new Dictionary<string, ushort>(), estimated ?? [], path);

    private static IReadOnlyList<ScummActorSnapshot> Actors(int actor, int costume, int suit)
    {
        var table = new ScummActorTable();
        table.InitializeV2Game(classic: true);
        table.SetPalette(actor, actor, suit);
        table.CostumeChanged(actor, costume, new(1, null, null, null));
        return table.Snapshot();
    }

    [Fact]
    public void NativeTimelineKeepsEarlierAndLaterColoursWithoutPromotingTheWholeCostume()
    {
        var timeline = new ScummV1PaletteContexts.Timeline();
        timeline.Observe(Observation(1, "0x0A, 0x20, sub=0x04"));
        timeline.Observe(Observation(2, "0x0A, 0x07, sub=0x02"));
        var contexts = timeline.Complete(Actors(10, 32, 7)).Where(c => c.Costume == 32).ToArray();
        Assert.Equal(new[] { 0, 7 }, contexts.Select(c => c.Suit));
        Assert.All(contexts, c => Assert.Equal(ScummV1ColorContextBasis.Opening, c.Basis));
        Assert.All(contexts, c => Assert.Contains("visibility", c.Evidence));
    }

    [Fact]
    public void ReachedIndirectCostumeUsesItsSelectedNativeValueAndEstimate()
    {
        var timeline = new ScummV1PaletteContexts.Timeline();
        timeline.Observe(Observation(1, "0x01, 0x0D, sub=0x02"));
        timeline.BeginPreview(27);
        timeline.Observe(Observation(2, "0x01, var[0x00E], sub=0x04", new Dictionary<string, ushort>
        { ["var[0x00E]"] = 176, ["var[0x0B0]"] = 5 }, ["var[0x0B0]"]));
        var context = Assert.Single(timeline.Complete(Actors(1, 5, 13)));
        Assert.Equal((5, 13, 27), (context.Costume, context.Suit, context.PreviewRoom));
        Assert.True(context.IsEstimate);
        Assert.Equal(ScummV1ColorContextBasis.RoomPreview, context.Basis);
    }

    [Fact]
    public void MissingActorBindingCannotCarryAStalePaletteIntoANewCostume()
    {
        var timeline = new ScummV1PaletteContexts.Timeline();
        timeline.Observe(Observation(1, "0x01, 0x0D, sub=0x02"));
        timeline.BeginPreview(1);
        timeline.Observe(Observation(2, "var[0x042], 0x07, sub=0x02"));
        timeline.Observe(Observation(3, "0x01, 0x05, sub=0x04"));
        Assert.Empty(timeline.Complete(Actors(1, 5, 13)));
    }

    [Fact]
    public void NativeMismatchRejectsUnappliedObservedEffects()
    {
        var timeline = new ScummV1PaletteContexts.Timeline();
        timeline.Observe(Observation(1, "0x01, 0x0D, sub=0x02"));
        Assert.Empty(timeline.Complete(Actors(1, 1, 0)));
    }

    [Theory]
    [InlineData("var[0x00E]", false, false)]
    [InlineData("var[0x00F]", false, false)]
    [InlineData("var[0x010]", false, false)]
    [InlineData("var[0x00E]", true, false)]
    [InlineData("var[0x00F]", true, false)]
    [InlineData("var[0x010]", true, false)]
    [InlineData("var[0x00E]", false, true)]
    [InlineData("var[0x00F]", false, true)]
    [InlineData("var[0x010]", false, true)]
    public void EveryNativeIndirectSlotRetainsTheSelectedColourAndBothEstimatesEvenAfterOverwrite(
        string pointer, bool indexEstimated, bool selectedEstimated)
    {
        var timeline = new ScummV1PaletteContexts.Timeline();
        var estimated = new List<string>();
        if (indexEstimated) estimated.Add(pointer);
        if (selectedEstimated) estimated.Add("var[0x002]");
        timeline.Observe(Observation(1, $"0x01, {pointer}, sub=0x02",
            new Dictionary<string, ushort> { [pointer] = 2, ["var[0x002]"] = 7 }, estimated.ToArray()));
        timeline.Observe(Observation(2, "0x01, 0x0D, sub=0x02"));
        var contexts = timeline.Complete(Actors(1, 1, 13));
        Assert.DoesNotContain(contexts, c => c.Suit == 2);
        var selected = Assert.Single(contexts, c => c.Suit == 7);
        Assert.Equal(indexEstimated || selectedEstimated, selected.IsEstimate);
        Assert.Contains(contexts, c => c is { Suit: 13, IsEstimate: false });
    }

    [Theory]
    [InlineData("var[0x00E]")]
    [InlineData("var[0x00F]")]
    [InlineData("var[0x010]")]
    public void MissingSelectedIndirectCellCannotCreateAPointerColouredHistoricalContext(string pointer)
    {
        var timeline = new ScummV1PaletteContexts.Timeline();
        timeline.Observe(Observation(1, $"0x01, {pointer}, sub=0x02", new Dictionary<string, ushort> { [pointer] = 2 }));
        timeline.Observe(Observation(2, "0x01, 0x0D, sub=0x02"));
        var context = Assert.Single(timeline.Complete(Actors(1, 1, 13)));
        Assert.Equal(13, context.Suit);
    }

    [Theory]
    [InlineData("var[0x00E]")]
    [InlineData("var[0x00F]")]
    [InlineData("var[0x010]")]
    public void NativeIndirectActorUsesTheSelectedIdentityInsteadOfThePointer(string pointer)
    {
        var timeline = new ScummV1PaletteContexts.Timeline();
        timeline.Observe(Observation(1, $"{pointer}, 0x07, sub=0x02",
            new Dictionary<string, ushort> { [pointer] = 2, ["var[0x002]"] = 1 }));
        timeline.Observe(Observation(2, "0x01, 0x0D, sub=0x02"));
        var contexts = timeline.Complete(Actors(1, 1, 13));
        Assert.Contains(contexts, c => c is { Actor: 1, Suit: 7 });
        Assert.DoesNotContain(contexts, c => c.Actor == 2);
    }

    [Fact]
    public void CancellationStopsBeforeAnyIndependentPreview()
    {
        var candidate = Path.Combine(TestGames.Root, "zak ega en");
        if (!Directory.Exists(candidate)) return;
        var colors = ScummV1ActorColors.Read(new GameFolderScanner().Scan(candidate));
        Assert.True(colors.SupportsContexts);
        Assert.Throws<OperationCanceledException>(() => colors.ContextsOf(5, new CancellationToken(true)));
    }

    [Fact]
    public void ManiacsAcceptedFixedTableHasNoNewContextExecution()
    {
        var candidate = Path.Combine(TestGames.Root, "mmansion ega en");
        if (!Directory.Exists(candidate)) return;
        var colors = ScummV1ActorColors.Read(new GameFolderScanner().Scan(candidate));
        Assert.False(colors.SupportsContexts);
        // Cancellation would fail immediately if any independent runtime were invoked.
        Assert.Empty(colors.ContextsOf(3, new CancellationToken(true)));
        Assert.Equal(ScummV1ColorBasis.InitialState, colors.ColorOf(3).Basis);
    }

    [Fact]
    public void OwnCopyContextsExposeTransformationsAndOpeningAlternativesButKeepAllEstimates()
    {
        var candidate = Path.Combine(TestGames.Root, "zak ega en");
        if (!Directory.Exists(candidate)) return;
        var colors = ScummV1ActorColors.Read(new GameFolderScanner().Scan(candidate));
        Assert.Contains(colors.ContextsOf(5), c => c is { Suit: 13, Actor: 1, PreviewRoom: 27, IsEstimate: true });
        Assert.Contains(colors.ContextsOf(6), c => c is { Suit: 6, Actor: 6, Basis: ScummV1ColorContextBasis.Opening });
        Assert.Contains(colors.ContextsOf(9), c => c is { Suit: 13, Actor: 1, PreviewRoom: 32, IsEstimate: true });
        Assert.Contains(colors.ContextsOf(13), c => c is { Suit: 0, Actor: 5, PreviewRoom: 31, IsEstimate: true });
        Assert.Contains(colors.ContextsOf(15), c => c is { Suit: 6, Actor: 6, PreviewRoom: 42, IsEstimate: true });
        var boss = colors.ContextsOf(32).Where(c => c.Basis == ScummV1ColorContextBasis.Opening).ToArray();
        Assert.Contains(boss, c => c.Suit == 0);
        // The palette 7 write occurs after the boss has changed to costume 31. The old
        // global estimate is retained but is not invented as a reached costume 32 context.
        Assert.DoesNotContain(boss, c => c.Suit == 7);
        Assert.Equal(new[] { 4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 32 },
            Enumerable.Range(1, 35).Where(i => colors.ColorOf(i).Basis == ScummV1ColorBasis.Estimate));
    }
}
