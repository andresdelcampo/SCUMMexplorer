
namespace SCUMMexplorer.Tests;

public sealed class LoomImplicitAccessTests
{
    [Theory]
    [InlineData("stringOps", "copyString, var[0x060], 0x02", 2)]
    [InlineData("stringOps", "setStringChar, 0x01, var[0x060], 0x03", 3)]
    [InlineData("stringOps", "createString, 0x01, var[0x060]", 2)]
    [InlineData("cursorCommand", "cursorHotspot, var[0x060], 0x02, 0x03", 3)]
    [InlineData("cursorCommand", "v3CharsetInit, var[0x060], 0x02", 2)]
    [InlineData("resourceRoutines", "loadObject, var[0x060], 0x00CC", 2)]
    [InlineData("roomOps", "roomScroll, var[0x060], 0x0140", 2)]
    [InlineData("roomOps", "setRoomScale, var[0x060], 0x02, 0x03, 0x04, 0x05, 0x06, 0x07", 7)]
    [InlineData("roomOps", "setRgbRoomIntensity, var[0x060], 0x02, 0x03, 0x04, 0x05", 5)]
    [InlineData("lights", "var[0x060], 0x02, 0x03", 3)]
    [InlineData("drawBox", "var[0x060], 0x02, 0x03, 0x04, 0x05", 5)]
    [InlineData("saveVerbs", "var[0x060], 0x02, 0x03", 3)]
    public void DecodedSubcommandsCopySelectedOperandsAndKeepEffectBarriers(string op, string text, int count)
    {
        var access = Capture(op, text).SourceAccess!;
        Assert.Equal(count, access.Bindings.Count);
        Assert.Contains(access.Bindings, b => b.Key == "var[0x060]" && b.Value == 7 && b.ValueEstimated == true);
        Assert.Equal("unmodelled instruction access", access.Barrier);
    }

    [Theory]
    [InlineData("message", "var[0x003]", "implicit-message")]
    [InlineData("camera", "var[0x002]", "implicit-camera")]
    [InlineData("actor, var[0x060]", "var[0x060]", "target")]
    public void ImplicitWaitReadsRemainSeparateFromNativeTimingEffects(string args, string key, string role)
    {
        var record = Capture("waitFor", args);
        var binding = Assert.Single(record.SourceAccess!.Bindings);
        Assert.Equal(key, binding.Key);
        Assert.Equal(role, binding.Role);
        Assert.NotNull(record.SourceAccess.Barrier);
        Assert.Empty(record.OperandBindings!);
    }

    [Fact]
    public void StringQueryDestinationDoesNotInventAWriteOrigin()
    {
        var record = Capture("stringOps", "getStringChar, var[0x060], 0x01, 0x02");
        Assert.Equal("destination-before", record.SourceAccess!.Bindings[0].Role);
        Assert.NotNull(record.SourceAccess.Barrier);
    }

    [Theory]
    [InlineData("stringOps", "loadString, var[0x060], \"var[0x061], 0x02; var[0x062]\"")]
    [InlineData("verbOps", "0x01, [color, var[0x060]; name, \"var[0x061]; at, var[0x062], 0x01\"]")]
    public void InlineStringsStopNumericParsing(string op, string args)
    {
        var bindings = Capture(op, args).SourceAccess!.Bindings;
        Assert.Contains(bindings, b => b.Key == "var[0x060]");
        Assert.DoesNotContain(bindings, b => b.Key is "var[0x061]" or "var[0x062]");
    }

    [Fact]
    public void VerbPrefixCopiesOrderedIndexedFieldsWithinTheExistingSubcommandBound()
    {
        var record = Capture("verbOps", "0x01, [at, var[0x032+var[0x060]], 0x0020; assignObject, 0x00CC, 0x02; on]");
        Assert.Equal(new[] { "target", "at-operand", "at-operand", "assignObject-operand", "assignObject-operand" },
            record.SourceAccess!.Bindings.Select(b => b.Role));
        Assert.Equal("var[0x060]", record.SourceAccess.Bindings[1].Index!.Key);
        Assert.True(record.SourceAccess.Bindings[1].Index!.ValueEstimated);
        record = Capture("verbOps", "0x01, [" + string.Join("; ", Enumerable.Repeat("on", 129)) + "]");
        Assert.Empty(record.SourceAccess!.Bindings);
        Assert.NotNull(record.SourceAccess.Barrier);
    }

    [Theory]
    [InlineData("stringOps", "unknown, var[0x060]")]
    [InlineData("roomOps", "roomScroll, var[0x060]")]
    [InlineData("resourceRoutines", "unknown, var[0x060]")]
    public void UnknownOrMalformedFormsStayUnresolved(string op, string args)
    {
        var access = Capture(op, args).SourceAccess!;
        Assert.Empty(access.Bindings);
        Assert.NotNull(access.Barrier);
    }

    private static ScummScriptObservation Capture(string op, string args)
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        runtime.SetGlobalVariable("var[0x060]", 7);
        runtime.EstimatedVariables.Add("var[0x060]");
        var records = new List<ScummScriptObservation>();
        var code = new ScummScriptDisassembly(2, 1, 0, 2, 2,
            [new(0, 0, op, args, "00"), new(1, 0, "stopObjectCode", "", "00")], null, null, [], []);
        new ScummBoundedScriptExecutor().Execute(code, runtime.ObjectStates, [], runtime: runtime,
            observation: new(new(records.Add, captureOperandBindings: true, captureSourceAccess: true), 1));
        return records.First(r => r.Site?.Stage == "instruction");
    }
}
