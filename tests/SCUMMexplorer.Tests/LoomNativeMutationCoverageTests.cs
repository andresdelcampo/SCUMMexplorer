using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomNativeMutationCoverageTests
{
    [Theory]
    [InlineData("state")]
    [InlineData("classes")]
    [InlineData("owner")]
    [InlineData("remove-state")]
    [InlineData("remove-classes")]
    [InlineData("remove-owner")]
    public void NativeSettersAndRemovalsRetireHistoricalReadsWithoutChangingEstimates(string operation)
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte> { [204] = 1 },
            new Dictionary<int, byte> { [204] = 15 }, new Dictionary<int, uint> { [204] = 0 });
        runtime.EstimatedStates.Add(204);
        var native = new LoomNativeDependencyTracker(_ => { });
        var observer = new LoomScriptObserver(_ => { }, nativeDependencies: native);
        using (runtime.ObserveGlobalMutations(observer)) {
            Query(runtime, observer, "ifState", "0x00CC, 0x01, branch +0 -> 0x0001");
            var history = Assert.Single(native.ActiveRelations);
            switch (operation) {
                case "state": runtime.SetObjectState(204, 1); break;
                case "classes": runtime.SetObjectClasses(204, 0); break;
                case "owner": runtime.SetObjectOwner(204, 15); break;
                case "remove-state": runtime.RemoveObjectState(204); break;
                case "remove-classes": runtime.RemoveObjectClasses(204); break;
                case "remove-owner": runtime.RemoveObjectOwner(204); break;
            }
            Assert.Empty(native.ActiveRelations);
            Assert.True(history.Dependency.ValueEstimated);
            Assert.Contains(204, runtime.EstimatedStates);
        }
    }

    [Theory]
    [InlineData("elevation")]
    [InlineData("direction")]
    [InlineData("place")]
    [InlineData("room")]
    [InlineData("forget")]
    [InlineData("estimate")]
    [InlineData("hide")]
    public void ActualActorMutatorsRetireSameValueAndChangedFieldCorrespondence(string operation)
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        var place = new LoomActorTable.Placement(2, null, null, runtime.ObjectClasses);
        runtime.Actors.Put(1, 24, 32, 2, place, null);
        var native = new LoomNativeDependencyTracker(_ => { });
        var observer = new LoomScriptObserver(_ => { }, nativeDependencies: native);
        using (runtime.ObserveGlobalMutations(observer)) {
            Query(runtime, observer, "getActorRoom", "local[0x000], 0x01");
            var history = Assert.Single(native.ActiveRelations);
            switch (operation) {
                case "elevation": runtime.Actors.SetElevation(1, 0); break;
                case "direction": runtime.Actors.SetDirection(1, 180, place); break;
                case "place": runtime.Actors.Put(1, 24, 32, 2, place, null); break;
                case "room": runtime.Actors.PutInRoom(1, 2, place, null); break;
                case "forget": runtime.Actors.Forget(1, "test unknown"); break;
                case "estimate": runtime.Actors.MarkEstimate(1, "test estimate"); break;
                case "hide": runtime.Actors.HideAll(); break;
            }
            Assert.Empty(native.ActiveRelations);
            Assert.Equal(2, history.Dependency.Value);
        }
    }

    [Fact]
    public void ActualPaletteWritesAndPaletteEstimatesKeepPlacementIndependent()
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        runtime.Actors.Put(1, 24, 32, 2, new(2, null, null, runtime.ObjectClasses), null);
        var native = new LoomNativeDependencyTracker(_ => { });
        var observer = new LoomScriptObserver(_ => { }, nativeDependencies: native);
        using (runtime.ObserveGlobalMutations(observer)) {
            Query(runtime, observer, "getActorRoom", "local[0x000], 0x01");
            runtime.Actors.SetPalette(1, 0, 0);
            runtime.Actors.MarkPaletteEstimate(1, "test palette estimate");
            Assert.Single(native.ActiveRelations);
            Assert.Null(runtime.Actors[1].Estimate);
            Assert.NotNull(runtime.Actors[1].Look.PaletteEstimate);
        }
    }

    [Fact]
    public void NotificationScopeDisposalAndRawSameValueLimitsRemainExplicit()
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte> { [204] = 1 });
        var native = new LoomNativeDependencyTracker(_ => { });
        var observer = new LoomScriptObserver(_ => { }, nativeDependencies: native);
        Query(runtime, observer, "ifState", "0x00CC, 0x01, branch +0 -> 0x0001");
        runtime.SetObjectState(204, 1); // Executor notification scope is already disposed.
        Assert.Single(native.ActiveRelations);
        using (runtime.ObserveGlobalMutations(observer)) {
            runtime.ObjectStates[204] = 1; // Compatible raw dictionary still requires notification.
            Assert.Single(native.ActiveRelations);
            runtime.NotifyNativeMutation("object-state", 204);
            Assert.Empty(native.ActiveRelations);
        }
    }

    [Fact]
    public void SameValueSourceStateWriteIsNotifiedEvenAfterConservativeAccessBarrier()
    {
        var updates = new List<LoomNativeDependencyUpdate>();
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte> { [204] = 1 });
        Query(runtime, new(_ => { }, nativeDependencies: new(updates.Add)), "setState", "0x00CC, 0x01");
        Assert.Contains(updates, u => u.Observation is null && u.Barrier == "notified native mutation (including same value)");
        Assert.Equal(1, runtime.ObjectStates[204]);
    }

    private static void Query(LoomScriptRuntimeState runtime, LoomScriptObserver observer, string op, string args) =>
        new LoomBoundedScriptExecutor().Execute(LoomCallObservationTests.Code((op, args), ("stopObjectCode", "")),
            runtime.ObjectStates, [], runtime: runtime, observation: new(observer, 1));
}
