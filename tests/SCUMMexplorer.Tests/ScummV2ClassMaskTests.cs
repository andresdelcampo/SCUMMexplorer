namespace SCUMMexplorer.Tests;

public sealed class ScummV2ClassMaskTests
{
    [Theory]
    [InlineData(ScummVersion.V1, 0x83, 0x81, true)]
    [InlineData(ScummVersion.V2, 0x83, 0x81, true)]
    [InlineData(ScummVersion.V1, 0x01, 0x03, false)]
    [InlineData(ScummVersion.V2, 0x01, 0x03, false)]
    [InlineData(ScummVersion.V1, 0x00, 0x00, true)]
    [InlineData(ScummVersion.V2, 0x00, 0x00, true)]
    [InlineData(ScummVersion.V1, -1, 0x00, false)]
    [InlineData(ScummVersion.V2, -1, 0x00, false)]
    public void NativeHeaderMaskIsNotALaterNumberedClass(ScummVersion version, int mask, int wanted, bool matches)
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>())
            { EngineVersion = version, ObjectClassMask = id => id == 10 ? mask : -1 };
        // An unrelated later-engine class table must have no effect on this opcode.
        runtime.ObjectClasses[10] = uint.MaxValue;
        runtime.GlobalVariables["var[0x050]"] = 10;
        runtime.GlobalVariables["var[0x051]"] = (ushort)wanted;
        var result = Run(runtime);
        Assert.Null(result.UnresolvedReason);
        Assert.Equal(matches ? 1 : 0, result.KnownVariables.GetValueOrDefault("var[0x052]"));
    }

    [Fact]
    public void UnreadableHeaderFailsClosedInsteadOfGuessingMissingOrMatching()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>())
            { EngineVersion = ScummVersion.V2, ObjectClassMask = _ => null };
        runtime.GlobalVariables["var[0x050]"] = 10;
        runtime.GlobalVariables["var[0x051]"] = 1;
        Assert.Contains("ifClassOfIs", Run(runtime).UnresolvedReason!);
    }

    private static ScummBoundedScriptResult Run(ScummScriptRuntimeState runtime)
    {
        ScummScriptInstruction[] instructions =
        [new(0, 0xDD, "ifClassOfIs", "var[0x050], var[0x051], +1 -> 0x0002", "DD"),
            new(1, 0x1A, "move", "var[0x052], 0x0001", "1A"),
            new(2, 0, "stopObjectCode", "", "00")];
        var script = new ScummScriptDisassembly(1, 1, 0, 3, 3, instructions, null, null, [], []);
        return new ScummBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime);
    }
}
