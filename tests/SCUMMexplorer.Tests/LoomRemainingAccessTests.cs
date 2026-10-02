using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomRemainingAccessTests
{
    [Fact]
    public void DecodedRoomWithEgoCopiesImplicitSelectorAndSignedLiteralWalkCoordinates()
    {
        var access = Capture("loadRoomWithEgo", "var[0x060], 0x02, walkTo=(-1, 32767)").SourceAccess!;
        Assert.Equal(new[] { "implicit-ego", "target", "room", "walk-x", "walk-y" }, access.Bindings.Select(b => b.Role));
        Assert.Equal("var[0x001]", access.Bindings[0].Key);
        Assert.Equal((ushort)65535, access.Bindings[3].Value);
        Assert.Equal("-1", access.Bindings[3].Operand);
        Assert.True(access.Bindings[1].ValueEstimated);
        Assert.Equal(3, access.NativeDependencies!.Count);
        Assert.All(access.NativeDependencies, d => { Assert.Null(d.Value); Assert.NotNull(d.UnresolvedReason); });
        Assert.NotNull(access.Barrier);
    }

    [Theory]
    [InlineData("0x01, 0x02, walkTo=(32768, 0)")]
    [InlineData("0x01, 0x02, walkTo=(var[0x060], 0)")]
    [InlineData("0x01, 0x02, walkTo=(-1, 0) trailing")]
    public void MalformedRoomArrivalNeverInventsCoordinates(string operands)
    {
        var access = Capture("loadRoomWithEgo", operands).SourceAccess!;
        Assert.Empty(access.Bindings);
        Assert.NotNull(access.Barrier);
    }

    [Fact]
    public void FilteredEgoAndObjectRemainUnresolvedNativeDependencies()
    {
        var access = Capture("loadRoomWithEgo", "var[0x060], 0x02, walkTo=(-1, 0)", k => k is not "var[0x001]" and not "var[0x060]").SourceAccess!;
        Assert.All(access.Bindings.Take(2), b => { Assert.Null(b.Value); Assert.NotNull(b.UnresolvedReason); });
        Assert.All(access.NativeDependencies!, d => Assert.Null(d.Value));
    }

    [Theory]
    [InlineData("copyString, var[0x060], 0x02", 2)]
    [InlineData("getStringChar, local[0x000], var[0x060], 0x02", 2)]
    [InlineData("loadString, var[0x060], \"var[0x061], 0x02; 0x03\"", 1)]
    public void StringStorageIsExplicitlyOpaqueAndInlineTextNeverBecomesAnAccess(string operands, int count)
    {
        var access = Capture("stringOps", operands).SourceAccess!;
        Assert.Equal(count, access.NativeDependencies!.Count);
        Assert.All(access.NativeDependencies, d => { Assert.Equal("string-storage", d.Domain); Assert.Null(d.Value); Assert.NotNull(d.UnresolvedReason); });
        Assert.DoesNotContain(access.Bindings, b => b.Key == "var[0x061]");
        Assert.NotNull(access.Barrier);
    }

    [Theory]
    [InlineData("message", "message-wait", null)]
    [InlineData("camera", "camera-wait", null)]
    [InlineData("actor, 0x01", "getActorMoving", 0L)]
    public void NativeWaitFieldIsIndependentOfItsNumericSelectorAndTimingBarrier(string operands, string domain, long? value)
    {
        var access = Capture("waitFor", operands).SourceAccess!;
        var native = access.NativeDependencies![0];
        Assert.Equal(operands == "message" ? 4 : 1, access.NativeDependencies.Count);
        Assert.Equal(domain, native.Domain);
        Assert.Equal(value, native.Value);
        Assert.NotNull(access.Barrier);
    }

    [Fact]
    public void OwnerQueryCopiesTheFieldButDoesNotInventEstimateCoverageOrALifetime()
    {
        var access = Capture("getObjectOwner", "local[0x000], var[0x060]").SourceAccess!;
        var native = Assert.Single(access.NativeDependencies!);
        Assert.Equal(15, native.Value);
        Assert.Null(native.ValueEstimated);
        Assert.NotNull(native.UnresolvedReason);
        Assert.True(native.Selector.ValueEstimated);
        Assert.NotNull(access.Barrier);
    }

    private static LoomScriptObservation Capture(string op, string operands, Func<string, bool>? filter = null)
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>(), new Dictionary<int, byte> { [204] = 15 });
        runtime.SetGlobalVariable("var[0x060]", 204);
        runtime.EstimatedVariables.Add("var[0x060]");
        var records = new List<LoomScriptObservation>();
        new LoomBoundedScriptExecutor().Execute(LoomCallObservationTests.Code((op, operands), ("stopObjectCode", "")),
            runtime.ObjectStates, [], runtime: runtime, observation: new(new(records.Add, includeKey: filter, captureSourceAccess: true), 1));
        return records.First(r => r.Site?.Stage == "instruction");
    }
}
