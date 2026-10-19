namespace SCUMMexplorer.Tests;

public sealed class ScummDynamicEgoRoomTests
{
    [Theory]
    [InlineData(ScummVersion.V1, false)]
    [InlineData(ScummVersion.V2, false)]
    [InlineData(ScummVersion.V3, false)]
    [InlineData(ScummVersion.V4, false)]
    [InlineData(ScummVersion.V2, true)]
    [InlineData(ScummVersion.V3, true)]
    public void RoomVariableBindsTheNativeTargetWithoutLosingItsEstimate(ScummVersion version, bool estimated)
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>()) { EngineVersion = version };
        runtime.GlobalVariables["var[0x050]"] = 145;
        runtime.GlobalVariables["var[0x051]"] = 190;
        if (estimated) runtime.EstimatedVariables.Add("var[0x050]");
        var result = Execute(runtime, "var[0x051], var[0x050], walkTo=(8, 60)");
        var load = Assert.Single(result.Events, e => e.Operation == "loadRoomWithEgo");
        Assert.Equal(145, load.TargetId);
        Assert.Equal(new int?[] { 190, 145, 8, 60 }, load.ResolvedArguments);
        Assert.Equal(estimated ? ScummScriptEventCertainty.Conditional : ScummScriptEventCertainty.Reached, load.Certainty);
    }

    [Fact]
    public void UnknownRoomRemainsUnresolvedAndDoesNotBecomeZero()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>()) { EngineVersion = ScummVersion.V2 };
        runtime.GlobalVariables.Remove("var[0x050]"); // explicit unknown; native globals normally start zero
        var result = Execute(runtime, "0x00BE, var[0x050], walkTo=(-1, -1)");
        var load = Assert.Single(result.Events, e => e.Operation == "loadRoomWithEgo");
        Assert.Null(load.TargetId);
        Assert.Equal(new int?[] { 190, null, -1, -1 }, load.ResolvedArguments);
    }

    [Fact]
    public void LiteralRoomAndSignedWalkCoordinatesKeepTheirExistingValues()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>()) { EngineVersion = ScummVersion.V2 };
        runtime.GlobalVariables["var[0x050]"] = 9;
        runtime.EstimatedVariables.Add("var[0x050]");
        var result = Execute(runtime, "0x00BE, 0x91, walkTo=(-1, -1)");
        var load = Assert.Single(result.Events, e => e.Operation == "loadRoomWithEgo");
        Assert.Equal(145, load.TargetId);
        Assert.Equal(new int?[] { 190, 145, -1, -1 }, load.ResolvedArguments);
        Assert.Equal(ScummScriptEventCertainty.Reached, load.Certainty);
    }

    private static ScummBoundedScriptResult Execute(ScummScriptRuntimeState runtime, string operands)
    {
        var script = new ScummScriptDisassembly(1, 1, 0, 2, 2,
            [new(0, 0, "loadRoomWithEgo", operands, "00"), new(1, 0, "stopObjectCode", "", "00")], null, null, [], []);
        return new ScummBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime, onEvent: (_, _) => "");
    }
}
