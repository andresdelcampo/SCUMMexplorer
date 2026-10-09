
namespace SCUMMexplorer.Tests;

public sealed class LoomCorrelatedNumericTests
{
    private static ScummScriptDisassembly Code(params (string Op, string Args)[] operations) =>
        new(2, 3, 0x100, operations.Length, operations.Length,
            operations.Select((o, i) => new ScummScriptInstruction(i, 0, o.Op, o.Args, "00")).ToArray(), null, null, [], []);

    private static ScummNumericWorld World(string id, params (string Key, ushort Number)[] values) =>
        new(id, "Hypothetical complete tuple; no selected runtime values.",
            values.ToDictionary(p => p.Key, p => ScummNumericValue.Hypothesis(p.Number, "source tuple " + id)));

    [Fact]
    public void MaskedComponentsRemainInTheSameWorldAcrossIndexedReadsAndArguments()
    {
        var decoder = Code(("move", "local[0x001] = var[0x032+local[0x000]]"),
            ("move", "var[0x052] = local[0x001]"), ("and", "var[0x052] &= 0x0007"),
            ("divide", "local[0x001] /= 0x0008"), ("and", "local[0x001] &= 0x0007"),
            ("move", "var[0x053] = local[0x001]"), ("stopObjectCode", ""));
        var caller = Code(("startScript", "0x03, [var[0x060]]"),
            ("chainScript", "0x04, [var[0x052], var[0x053]]"));
        var worlds = new[] { World("first", ("var[0x060]", 2), ("var[0x034]", 17)),
            World("second", ("var[0x060]", 4), ("var[0x036]", 10)) };
        var results = new ScummCorrelatedNumericAnalyzer().Analyze(caller, worlds, _ => decoder);
        Assert.Equal(new ushort?[] { 1, 2 }, results.Select(r => r.Boundary!.Arguments[0].Number));
        Assert.Equal(new ushort?[] { 2, 1 }, results.Select(r => r.Boundary!.Arguments[1].Number));
        Assert.All(results, r =>
        {
            Assert.Null(r.UnresolvedReason);
            Assert.False(r.ReachedStop); // Only a chain boundary, never dispatch/effect completion.
            Assert.All(r.Boundary!.Arguments, a => Assert.Contains("source tuple " + r.Id, a.Sources));
            Assert.Equal((ushort)0, r.Locals["local[0x001]"].Number); // Child locals do not alias caller locals.
        });
        Assert.DoesNotContain("var[0x052]", worlds[0].Globals.Keys);
    }

    [Fact]
    public void UnknownIndexAndOpaqueResponseCannotBecomeSelectedNumbers()
    {
        var unknown = new ScummCorrelatedNumericAnalyzer().Analyze(Code(("move", "var[0x065] = var[0x032+var[0x060]]")),
            [World("missing", ("var[0x032]", 9))]).Single();
        Assert.Contains("unknown external", unknown.UnresolvedReason);
        Assert.Empty(unknown.Writes);
        var world = new ScummNumericWorld("opaque", "Response deliberately opaque.",
            new Dictionary<string, ScummNumericValue> { ["var[0x033]"] = ScummNumericValue.Opaque("response0", "slot0 source") });
        var result = new ScummCorrelatedNumericAnalyzer().Analyze(Code(("move", "var[0x065] = var[0x033]"),
            ("isEqual", "var[0x065], 0x4241, branch +0 -> 0x0002"), ("stopObjectCode", "")), [world]).Single();
        Assert.Null(result.Globals["var[0x065]"].Number);
        Assert.Equal("response0", result.Globals["var[0x065]"].Symbol);
        Assert.Contains("opaque numeric", result.UnresolvedReason);
    }

    [Fact]
    public void WordWritesReplaceBitsAndBitWritesPreserveKnownSiblingBits()
    {
        var result = new ScummCorrelatedNumericAnalyzer().Analyze(Code(("move", "bit[0xD67] = 0x0000"),
            ("move", "var[0x0D6] = 0x0080"), ("move", "var[0x052] = bit[0xD67]"),
            ("move", "bit[0xD6C] = 0x0001"), ("move", "var[0x053] = bit[0xD67]"), ("stopObjectCode", "")),
            [World("alias", ("var[0x0D6]", 0xFFFF))]).Single();
        Assert.True(result.ReachedStop);
        Assert.Equal((ushort)1, result.Globals["var[0x052]"].Number);
        Assert.Equal((ushort)1, result.Globals["var[0x053]"].Number);
        Assert.Equal((ushort)0x1080, result.Globals["var[0x0D6]"].Number);
    }

    [Theory]
    [InlineData("breakHere")]
    [InlineData("putActor")]
    [InlineData("getRandomNr")]
    public void EffectAndSchedulerBoundariesDoNotAssumeACalleeReturned(string operation)
    {
        var child = Code((operation, ""), ("move", "var[0x052] = 0x0000"), ("stopObjectCode", ""));
        var result = new ScummCorrelatedNumericAnalyzer().Analyze(Code(("startScript", "0x03, []"),
            ("move", "var[0x053] = 0x0000"), ("stopObjectCode", "")), [World("bounded")], _ => child).Single();
        Assert.False(result.ReachedStop);
        Assert.Equal(operation, result.Boundary!.Operation);
        Assert.Empty(result.Writes);
    }

    [Theory]
    [InlineData(0xFFFF, 0, false)]
    [InlineData(0, 0xFFFF, true)]
    public void RelationalOperandsUseOldV3SignedReversedOrdering(ushort first, ushort second, bool expected)
    {
        var result = new ScummCorrelatedNumericAnalyzer().Analyze(Code(("isLess", "var[0x052], var[0x053], branch +0 -> 0x0001"),
            ("stopObjectCode", "")), [World("signed", ("var[0x052]", first), ("var[0x053]", second))]).Single();
        Assert.Equal(expected, result.Branches.Single().Condition);
    }

    [Fact]
    public void LimitsAndMissingCalleesLeaveExplicitUnresolvedResults()
    {
        var cycle = new ScummCorrelatedNumericAnalyzer().Analyze(Code(("jumpRelative", "0 -> 0x0000")),
            [World("cycle")], instructionLimit: 3).Single();
        Assert.Contains("instruction limit", cycle.UnresolvedReason);
        var missing = new ScummCorrelatedNumericAnalyzer().Analyze(Code(("startScript", "0x03, []")), [World("missing")]).Single();
        Assert.Contains("unresolved numeric callee", missing.UnresolvedReason);
        var recursive = Code(("startScript", "0x03, []"));
        var bounded = new ScummCorrelatedNumericAnalyzer().Analyze(recursive, [World("recursive")], _ => recursive, callLimit: 2).Single();
        Assert.Contains("call limit", bounded.UnresolvedReason);
    }

    [Fact]
    public void SelfStopRetainsThePendingFlagAndDoesNotClaimACompletionSummary()
    {
        var result = new ScummCorrelatedNumericAnalyzer().Analyze(Code(("move", "bit[0xD67] = 0x0001"),
            ("stopScript", "0x00"), ("move", "bit[0xD67] = 0x0000")), [World("early-stop")]).Single();
        Assert.True(result.ReachedStop);
        Assert.Equal("stopScript", result.Boundary!.Operation);
        Assert.Equal((ushort)1, result.Globals["bit[0xD67]"].Number);
        Assert.Single(result.Writes);
    }

    [Fact]
    public void RangeAndWrappingArithmeticHaveSourceDependencies()
    {
        var result = new ScummCorrelatedNumericAnalyzer().Analyze(Code(("setVarRange", "var[0x052], count=2, [0xFFFF, 0x0003]"),
            ("increment", "var[0x052]"), ("multiply", "var[0x053] *= 0xFFFF"), ("stopObjectCode", "")), [World("range")]).Single();
        Assert.Null(result.UnresolvedReason);
        Assert.Equal((ushort)0, result.Globals["var[0x052]"].Number);
        Assert.Equal((ushort)0xFFFD, result.Globals["var[0x053]"].Number);
        Assert.Contains(result.Globals["var[0x053]"].Sources, s => s.EndsWith("+0x0002"));
    }
}
