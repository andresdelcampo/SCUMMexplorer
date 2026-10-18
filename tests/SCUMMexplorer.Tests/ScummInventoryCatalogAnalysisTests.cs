namespace SCUMMexplorer.Tests;

public sealed class ScummInventoryCatalogAnalysisTests
{
    private static ScummScriptDisassembly Script(int id, params (string Op, string Operands)[] operations) =>
        new(id, 0, 0, operations.Length, operations.Length,
            operations.Select((op, index) => new ScummScriptInstruction(index, 0, op.Op, op.Operands, "00")).ToArray(),
            null, null, [], [], Issues: []);

    [Theory]
    [InlineData(35, false)]
    [InlineData(34, true)]
    public void KnownCallerArgumentPrunesNativeNumericFalseJump(int argument, bool expected)
    {
        var helper = Script(91, ("isNotEqual", "var[0x07B], 0x0023, +2 -> 0x0003"),
            ("pickupObject", ScummVars.V2.ActiveObject1), ("stopObjectCode", ""), ("stopObjectCode", ""));
        var caller = Script(92, ("move", $"var[0x07B], 0x{argument:X4}"), ("startScript", "0x005B"), ("stopObjectCode", ""));
        var found = new List<int?>();
        var analysis = new ScummInventoryCatalogAnalysis(new Dictionary<int, ScummScriptDisassembly?> { [91] = helper },
            (_, _, _) => null, (_, _, id, _, _) => found.Add(id), _ => { });
        analysis.Scan(caller, 173, 9);
        Assert.Equal(expected, found.Contains(173));
    }

    [Fact]
    public void SequentialSentenceWriteOverridesOriginalObjectBeforeHelper()
    {
        var helper = Script(91, ("pickupObject", ScummVars.V2.ActiveObject1), ("stopObjectCode", ""));
        var caller = Script(92, ("move", "var[0x009], 0x0315"), ("startScript", "0x005B"), ("stopObjectCode", ""));
        var found = new List<int?>();
        var analysis = new ScummInventoryCatalogAnalysis(new Dictionary<int, ScummScriptDisassembly?> { [91] = helper },
            (_, _, _) => null, (_, _, id, _, _) => found.Add(id), _ => { });
        analysis.Scan(caller, 173, 9);
        Assert.Equal(new int?[] { 789 }, found);
    }

    [Fact]
    public void ResidentHandlerRelayPreservesOriginalSentenceObject()
    {
        var caller = Script(92, ("doSentence", "0xFD, 0x2329, 0x0000, sub=0x01"), ("stopObjectCode", ""));
        var middle = Script(93, ("doSentence", "0xFD, 0x232A, 0x0000, sub=0x01"), ("stopObjectCode", ""));
        var leaf = Script(94, ("pickupObject", ScummVars.V2.ActiveObject1), ("stopObjectCode", ""));
        var found = new List<int?>();
        var analysis = new ScummInventoryCatalogAnalysis(new Dictionary<int, ScummScriptDisassembly?>(),
            (_, obj, _) => obj switch { 9001 => middle, 9002 => leaf, _ => null },
            (_, _, id, _, _) => found.Add(id), _ => { });
        analysis.Scan(caller, 173, 9);
        Assert.Equal(new int?[] { 173 }, found);
    }

    [Theory]
    [InlineData("Pickupable", false, true)]
    [InlineData("Pickupable", true, false)]
    [InlineData("Unpickupable", false, false)]
    [InlineData("Unpickupable", true, true)]
    [InlineData("Unpickupable", null, true)]
    public void NativeStateNameIsJumpCaseAndMutableFlagKeepsBothEdges(string flag, bool? state, bool expected)
    {
        var script = Script(92, ("ifState", $"var[0x009], {flag}, +2 -> 0x0003"),
            ("pickupObject", ScummVars.V2.ActiveObject1), ("stopObjectCode", ""), ("stopObjectCode", ""));
        var found = new List<int?>();
        var analysis = new ScummInventoryCatalogAnalysis(new Dictionary<int, ScummScriptDisassembly?>(),
            (_, _, _) => null, (_, _, id, _, _) => found.Add(id), _ => { },
            new Dictionary<int, bool?> { [173] = state });
        analysis.Scan(script, 173, 9);
        Assert.Equal(expected, found.Contains(173));
    }
}
