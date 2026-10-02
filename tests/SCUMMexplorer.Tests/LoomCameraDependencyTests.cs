using System.Text.Json;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomCameraDependencyTests
{
    [Fact]
    public void CameraWaitCopiesSignedNativeStorageWithUnknownIndependentProvenance()
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        runtime.SetGlobalVariable("var[0x002]", 240);
        runtime.EstimatedVariables.Add("var[0x002]");
        runtime.Camera.DestX = -8;
        runtime.Camera.Mode = LoomCamera.FollowActor;
        runtime.Camera.Follows = 3;
        runtime.Camera.MovingToActor = true;
        var records = Capture(runtime);
        var access = records.Single(r => r.Site?.Operation == "waitFor").SourceAccess!;
        var opaque = access.NativeDependencies![0];
        Assert.Equal("camera-wait", opaque.Domain);
        Assert.Null(opaque.Value);
        Assert.NotNull(opaque.UnresolvedReason);
        var fields = access.NativeDependencies.Skip(1).ToArray();
        Assert.Equal(new[] { "camera-destination-x", "camera-mode", "camera-follows", "camera-moving-to-actor" }, fields.Select(d => d.Domain));
        Assert.Equal(new long?[] { -8, 2, 3, 1 }, fields.Select(d => d.Value));
        Assert.All(fields, d => {
            Assert.Equal((ushort)240, d.Selector.Value);
            Assert.True(d.Selector.ValueEstimated);
            Assert.Null(d.ValueEstimated);
            Assert.NotNull(d.UnresolvedReason);
        });
        Assert.NotNull(access.Barrier);
    }

    [Fact]
    public void FilteredCameraPositionCannotDiscloseNativeFields()
    {
        var access = Capture(new(new Dictionary<int, byte>()), key: k => k != "var[0x002]")
            .Single(r => r.Site?.Operation == "waitFor").SourceAccess!;
        Assert.Equal("camera-wait", Assert.Single(access.NativeDependencies!).Domain);
        Assert.Null(access.Bindings[0].Value);
    }

    [Fact]
    public void CopiedNativeCameraFieldsSupplyNeitherLiveLifetimeNorNumericOrigin()
    {
        var native = new LoomNativeDependencyTracker(_ => { });
        var numeric = new LoomRuntimeRelationTracker(_ => { });
        var access = Capture(new(new Dictionary<int, byte>()), native, numeric)
            .Single(r => r.Site?.Operation == "waitFor").SourceAccess!;
        Assert.Equal(5, access.NativeDependencies!.Count);
        Assert.Empty(native.ActiveRelations);
        Assert.DoesNotContain(numeric.ActiveRelations, r => r.ObservedOrigin is not null);
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("follows")]
    [InlineData("destination")]
    [InlineData("moving")]
    [InlineData("reset")]
    [InlineData("step")]
    [InlineData("early-clamp")]
    public void ExecutedIdenticalAndNoOpCameraMutationsRetireOnlyTheCameraFamily(string operation)
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        var updates = new List<LoomNativeDependencyUpdate>();
        var tracker = SyntheticReads(updates.Add);
        var history = tracker.ActiveRelations.First(r => r.Storage!.Family == "camera");
        using (runtime.ObserveGlobalMutations(new(_ => { }, nativeDependencies: tracker))) {
            switch (operation) {
                case "mode": runtime.Camera.Mode = runtime.Camera.Mode; break;
                case "follows": runtime.Camera.Follows = runtime.Camera.Follows; break;
                case "destination": runtime.Camera.DestX = runtime.Camera.DestX; break;
                case "moving": runtime.Camera.MovingToActor = runtime.Camera.MovingToActor; break;
                case "reset": runtime.Camera.Reset(160); break;
                case "step": Assert.Equal(160, runtime.Camera.Move(160, 160, 160, (160, 160), null)); break;
                case "early-clamp": Assert.Equal(160, runtime.Camera.Move(152, 160, 160, (160, 160), null)); break;
            }
        }
        Assert.Equal("object", Assert.Single(tracker.ActiveRelations).Storage!.Family);
        Assert.Contains(updates, u => u.Observation is null && u.Invalidated.Any(i => i.Read == history.Id));
        Assert.True(history.Dependency.ValueEstimated);
        Assert.Equal("historical synthetic camera estimate", history.Dependency.Estimate);
        Assert.Empty(runtime.EstimatedVariables);
    }

    [Fact]
    public void UnattachedAndDisposedCameraNotificationsDetectNothing()
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        var previous = SyntheticReads();
        var temporary = SyntheticReads();
        runtime.Camera.DestX = 160;
        Assert.Equal(2, previous.ActiveRelations.Count);
        using (runtime.ObserveGlobalMutations(new(_ => { }, nativeDependencies: previous))) {
            using (runtime.ObserveGlobalMutations(new(_ => { }, nativeDependencies: temporary))) runtime.Camera.Reset(160);
            Assert.Equal(2, previous.ActiveRelations.Count);
            Assert.Single(temporary.ActiveRelations);
            runtime.Camera.DestX = 160;
            Assert.Single(previous.ActiveRelations);
        }
    }

    [Fact]
    public void CameraObservationLeavesEveryFrameAndLegacyInstructionExact()
    {
        string Run(bool tracked) {
            var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
            runtime.Camera.Reset(192);
            var outer = new List<LoomScriptObservation>();
            var frames = new List<string>();
            var observer = new LoomScriptObserver(outer.Add, captureSourceAccess: true,
                nativeDependencies: tracked ? new(_ => { }) : null);
            using (runtime.ObserveGlobalMutations(observer)) {
                for (var i = 0; i < 6; i++) {
                    new LoomBoundedScriptExecutor().Execute(LoomCallObservationTests.Code(("waitFor", "camera"), ("stopObjectCode", "")),
                        runtime.ObjectStates, [], runtime: runtime, observation: new(observer, 1));
                    runtime.SetGlobalVariable("var[0x002]", (ushort)runtime.Camera.Move(runtime.GlobalVariables["var[0x002]"],
                        160, 320, (160, 320), null));
                    frames.Add(JsonSerializer.Serialize(new { runtime.Camera.Mode, runtime.Camera.Follows, runtime.Camera.DestX,
                        runtime.Camera.MovingToActor, runtime.GlobalVariables, runtime.EstimatedVariables }));
                }
            }
            return JsonSerializer.Serialize(new { frames, outer });
        }
        Assert.Equal(Run(false), Run(true));
    }

    private static List<LoomScriptObservation> Capture(LoomScriptRuntimeState runtime,
        LoomNativeDependencyTracker? native = null, LoomRuntimeRelationTracker? numeric = null, Func<string, bool>? key = null)
    {
        var records = new List<LoomScriptObservation>();
        new LoomBoundedScriptExecutor().Execute(LoomCallObservationTests.Code(("waitFor", "camera"), ("stopObjectCode", "")),
            runtime.ObjectStates, [], runtime: runtime,
            observation: new(new(records.Add, includeKey: key, captureSourceAccess: true, nativeDependencies: native, runtimeRelations: numeric), 1));
        return records;
    }

    private static LoomNativeDependencyTracker SyntheticReads(Action<LoomNativeDependencyUpdate>? receive = null)
    {
        var tracker = new LoomNativeDependencyTracker(receive ?? (_ => { }));
        var selector = new LoomScriptOperandBinding("implicit-camera", "var[0x002]", "var[0x002]", 240, false);
        // Complete synthetic reads isolate notification; loaded camera provenance stays unknown.
        tracker.Observe(new(1, 1, null, 1, new(0, null, 0, 0, "instruction", 0, "debug"),
            new Dictionary<string, ushort> { ["var[0x002]"] = 240 }, [], true,
            SourceAccess: new([], null, [], [new("camera-destination-x", selector, 160, true, "historical synthetic camera estimate"),
                new("object-position-x", selector with { Key = null, Value = 204 }, 16, false)])));
        Assert.Equal(2, tracker.ActiveRelations.Count);
        Assert.Equal(0, tracker.ActiveRelations.First(r => r.Storage!.Family == "camera").Storage!.Subject);
        return tracker;
    }
}
