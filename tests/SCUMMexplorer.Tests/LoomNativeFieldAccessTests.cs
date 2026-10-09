
namespace SCUMMexplorer.Tests;

public sealed class LoomNativeFieldAccessTests
{
    [Theory]
    [InlineData("userInputOn", 1)]
    [InlineData("userInputOff", 0)]
    [InlineData("userInputSoftOn", -1)]
    [InlineData("userInputSoftOff", -3)]
    public void ImplicitNativeInputCopiesSignedBeforeValueAndRetiresOnExecutedWrite(string command, int after)
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>()) { UserPut = -2, UserPutEstimate = "prior input estimate" };
        var updates = new List<ScummNativeDependencyUpdate>();
        var capture = Capture(runtime, "cursorCommand", command, native: new(updates.Add));
        var dependency = Assert.Single(capture.SourceAccess!.NativeDependencies!);
        Assert.Equal("user-input", dependency.Domain);
        Assert.Equal("_userPut", dependency.Selector.Operand);
        Assert.Null(dependency.Selector.Key);
        Assert.Equal(-2, dependency.Value);
        Assert.True(dependency.ValueEstimated);
        Assert.Equal("prior input estimate", dependency.Estimate);
        Assert.NotNull(capture.SourceAccess.Barrier);
        Assert.Equal(after, runtime.UserPut);
        var read = updates.SelectMany(u => u.Reads).Single();
        Assert.Contains(updates.SelectMany(u => u.Invalidated), i => i.Read == read.Id);
        Assert.Equal(-2, read.Dependency.Value);
    }

    [Theory]
    [InlineData("getActorX")]
    [InlineData("getActorY")]
    public void ObjectFallbackCopiesStoredDrawingPositionWithoutClaimingWalkPointOrQueryOrigin(string op)
    {
        var runtime = Positioned();
        runtime.EstimatedStates.Add(204);
        var capture = Capture(runtime, op, "local[0x000], 0x00CC");
        var dependencies = capture.SourceAccess!.NativeDependencies!;
        Assert.Equal(3, dependencies.Count);
        Assert.Equal(op, dependencies[0].Domain);
        Assert.NotNull(dependencies[0].UnresolvedReason); // Original incomplete query survives.
        Assert.Equal(new long?[] { 16, 24 }, dependencies.Skip(1).Select(d => d.Value));
        Assert.All(dependencies.Skip(1), d => Assert.True(d.ValueEstimated));
        Assert.Equal("destination-before", capture.SourceAccess.Bindings[0].Role);
        Assert.Equal((ushort)0, capture.SourceAccess.Bindings[0].Value);
    }

    [Theory]
    [InlineData("set")]
    [InlineData("try-add")]
    [InlineData("remove")]
    [InlineData("absent-remove")]
    public void ExplicitPositionMutationsRetireSameValueAndAbsentNotificationsWithEstimatesRetained(string operation)
    {
        var runtime = Positioned();
        runtime.EstimatedStates.Add(204);
        var tracker = new ScummNativeDependencyTracker(_ => { });
        var observer = new ScummScriptObserver(_ => { }, nativeDependencies: tracker);
        using (runtime.ObserveGlobalMutations(observer)) {
            Query(runtime, observer, "getActorX", "local[0x000], 0x00CC");
            var historical = tracker.ActiveRelations.ToArray();
            Assert.Equal(2, historical.Length);
            switch (operation) {
                case "set": runtime.SetObjectPosition(204, new(16, 24)); break;
                case "try-add": Assert.False(runtime.TryAddObjectPosition(204, new(200, 200))); break;
                case "remove": Assert.True(runtime.RemoveObjectPosition(204)); break;
                case "absent-remove": runtime.ObjectPositions.Remove(204); Assert.False(runtime.RemoveObjectPosition(204)); break;
            }
            Assert.Empty(tracker.ActiveRelations);
            Assert.Contains(204, runtime.EstimatedStates);
            Assert.All(historical, r => Assert.True(r.Dependency.ValueEstimated));
        }
    }

    [Fact]
    public void RawPositionSameValueStillRequiresNotificationAndDoesNotClearHistory()
    {
        var runtime = Positioned();
        var tracker = new ScummNativeDependencyTracker(_ => { });
        var observer = new ScummScriptObserver(_ => { }, nativeDependencies: tracker);
        using (runtime.ObserveGlobalMutations(observer)) {
            Query(runtime, observer, "getActorY", "local[0x000], 0x00CC");
            var copied = tracker.ActiveRelations.ToArray();
            runtime.ObjectPositions[204] = new(16, 24);
            Assert.Equal(2, tracker.ActiveRelations.Count);
            runtime.NotifyNativeMutation("object-position", 204);
            Assert.Empty(tracker.ActiveRelations);
            Assert.Equal(24, copied[1].Dependency.Value);
        }
    }

    [Fact]
    public void FilteredObjectSelectorDisclosesNeitherStoredCoordinate()
    {
        var runtime = Positioned();
        runtime.SetGlobalVariable("var[0x060]", 204);
        var capture = Capture(runtime, "getActorX", "local[0x000], var[0x060]", filter: k => k != "var[0x060]");
        Assert.All(capture.SourceAccess!.NativeDependencies!, d => Assert.Null(d.Value));
        Assert.NotNull(capture.SourceAccess.Barrier);
    }

    [Fact]
    public void DrawCoordinatesRemainRawOperandsAndSentinelStillNotifiesStoredPosition()
    {
        var runtime = Positioned();
        var records = new[] { new ScummRoomObjectRecord(204, 0, 16, 24, 8, 8, 0, 0, 16, 24, 0, 0, null) };
        var updates = new List<ScummNativeDependencyUpdate>();
        var outer = new List<ScummScriptObservation>();
        var observer = new ScummScriptObserver(outer.Add, captureSourceAccess: true, nativeDependencies: new(updates.Add));
        new ScummBoundedScriptExecutor().Execute(LoomCallObservationTests.Code(("drawObject", "0x00CC, at, 0x00FF, 0x00FF"), ("stopObjectCode", "")),
            runtime.ObjectStates, records, runtime: runtime, observation: new(observer, 1));
        var access = outer.First(o => o.Site?.Stage == "instruction").SourceAccess!;
        Assert.Equal(new[] { "target", "draw-x", "draw-y" }, access.Bindings.Select(b => b.Role));
        Assert.Equal((ushort)255, access.Bindings[1].Value);
        Assert.NotNull(access.Barrier);
        Assert.Contains(access.NativeDependencies!, d => d.Domain == "draw-peer-effects" && d.UnresolvedReason is not null);
        Assert.Equal(new RoomObjectPosition(16, 24), runtime.ObjectPositions[204]);
        Assert.Contains(updates, u => u.Observation is null && u.Invalidated.Count == 2);
    }

    [Fact]
    public void NativeInputEstimateWriteAndSameValueAssignmentNotifyWithoutChangingOtherFamilies()
    {
        var runtime = Positioned();
        var tracker = new ScummNativeDependencyTracker(_ => { });
        var observer = new ScummScriptObserver(_ => { }, nativeDependencies: tracker);
        using (runtime.ObserveGlobalMutations(observer)) {
            Query(runtime, observer, "getActorX", "local[0x000], 0x00CC");
            runtime.UserPut = runtime.UserPut;
            runtime.UserPutEstimate = "explicit input estimate";
            Assert.Equal(2, tracker.ActiveRelations.Count);
            Assert.Equal("explicit input estimate", runtime.UserPutEstimate);
        }
    }

    private static ScummScriptRuntimeState Positioned()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte> { [204] = 1 });
        runtime.SetObjectPosition(204, new(16, 24));
        return runtime;
    }
    private static ScummScriptObservation Capture(ScummScriptRuntimeState runtime, string op, string args,
        ScummNativeDependencyTracker? native = null, Func<string, bool>? filter = null)
    {
        var records = new List<ScummScriptObservation>();
        Query(runtime, new(records.Add, includeKey: filter, captureSourceAccess: true, nativeDependencies: native), op, args);
        return records.First(r => r.Site?.Stage == "instruction");
    }
    private static void Query(ScummScriptRuntimeState runtime, ScummScriptObserver observer, string op, string args) =>
        new ScummBoundedScriptExecutor().Execute(LoomCallObservationTests.Code((op, args), ("stopObjectCode", "")),
            runtime.ObjectStates, [new ScummRoomObjectRecord(204, 0, 16, 24, 8, 8, 0, 0, 80, 96, 0, 0, null)],
            runtime: runtime, observation: new(observer, 1));
}
