using System.Text.Json;

namespace SCUMMexplorer.Tests;

public sealed class LoomInventoryDependencyTests
{
    [Fact]
    public void OwnerMutationCopiesIndependentMembershipAndImplicitEgoWithoutOrigins()
    {
        var runtime = Runtime();
        runtime.Inventory.Add(204); // Deliberately differs from owner15 and ego1.
        runtime.EstimatedVariables.Add("var[0x001]");
        var native = new ScummNativeDependencyTracker(_ => { });
        var numeric = new ScummRuntimeRelationTracker(_ => { });
        var observation = Capture(runtime, native: native, numeric: numeric);
        var access = observation.SourceAccess!;
        Assert.Equal(new[] { "target", "source", "implicit-ego" }, access.Bindings.Select(b => b.Role));
        Assert.True(access.Bindings[0].ValueEstimated);
        Assert.True(access.Bindings[2].ValueEstimated);
        Assert.Equal(new long?[] { 15, 1 }, access.NativeDependencies!.Select(d => d.Value));
        Assert.All(access.NativeDependencies!, d => { Assert.Null(d.ValueEstimated); Assert.NotNull(d.UnresolvedReason); });
        Assert.NotNull(access.Barrier);
        Assert.Empty(native.ActiveRelations);
        Assert.DoesNotContain(numeric.ActiveRelations, r => r.ObservedOrigin is not null);
        Assert.Contains("var[0x001]", runtime.EstimatedVariables);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("index")]
    [InlineData("ego")]
    public void FilteredNumericInputsKeepTheirOwnDisclosureBarriers(string filter)
    {
        var runtime = Runtime();
        runtime.SetGlobalVariable("var[0x061]", 0);
        var access = Capture(runtime, target: "var[0x060+var[0x061]]", key: k => k != (filter switch {
            "target" => "var[0x060]", "index" => "var[0x061]", _ => "var[0x001]"
        })).SourceAccess!;
        if (filter == "ego") {
            Assert.Null(access.Bindings[2].Value);
            Assert.Equal(15, access.NativeDependencies![0].Value);
        }
        else Assert.All(access.NativeDependencies!, d => Assert.Null(d.Value));
        Assert.All(access.NativeDependencies!, d => { Assert.Null(d.ValueEstimated); Assert.NotNull(d.UnresolvedReason); });
        Assert.NotNull(access.Barrier);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameValueInventoryAttemptsNotifyOnlyTheirSelectedMembership(bool remove)
    {
        var runtime = Runtime();
        if (!remove) runtime.Inventory.Add(204);
        var tracker = SyntheticReads();
        var history = tracker.ActiveRelations.First(r => r.Storage == new ScummNativeStorage("inventory", 204));
        using (runtime.ObserveGlobalMutations(new(_ => { }, nativeDependencies: tracker))) {
            Assert.False(remove ? runtime.RemoveInventoryObject(204) : runtime.AddInventoryObject(204));
        }
        Assert.Equal(2, tracker.ActiveRelations.Count);
        Assert.DoesNotContain(tracker.ActiveRelations, r => r.Id == history.Id);
        Assert.True(history.Dependency.ValueEstimated);
        Assert.Equal("synthetic membership estimate", history.Dependency.Estimate);
    }

    [Theory]
    [InlineData("0x01", true)]
    [InlineData("0x0F", false)]
    public void ExecutedOwnerMutationNotifiesInventoryEvenForNoOpMembership(string owner, bool member)
    {
        var runtime = Runtime();
        if (member) runtime.Inventory.Add(204);
        var updates = new List<ScummNativeDependencyUpdate>();
        Capture(runtime, owner: owner, native: new(updates.Add));
        Assert.Equal(member, runtime.Inventory.Contains(204));
        // One owner notification and one membership notification. These assert neither a
        // changed value nor provenance; source access itself retains the opaque effect barrier.
        Assert.Equal(2, updates.Count(u => u.Observation is null && u.Barrier == "notified native mutation (including same value)"));
    }

    [Fact]
    public void RawUnattachedAndDisposedInventoryMutationsNeedExplicitNotification()
    {
        var runtime = Runtime();
        var tracker = SyntheticReads();
        runtime.AddInventoryObject(204);
        Assert.Equal(3, tracker.ActiveRelations.Count);
        using (runtime.ObserveGlobalMutations(new(_ => { }, nativeDependencies: tracker))) {
            runtime.Inventory.Add(204);
            Assert.Equal(3, tracker.ActiveRelations.Count);
            runtime.NotifyNativeMutation("inventory-membership", 204);
            Assert.Equal(2, tracker.ActiveRelations.Count);
        }
        runtime.RemoveInventoryObject(205);
        Assert.Equal(2, tracker.ActiveRelations.Count);
    }

    [Fact]
    public void OwnerObservationAndMutationTrackingLeaveExecutionAndLegacyPacketExact()
    {
        string Run(bool tracked) {
            var runtime = Runtime();
            var records = new List<ScummScriptObservation>();
            var result = new ScummBoundedScriptExecutor().Execute(LoomCallObservationTests.Code(
                ("setOwnerOf", "var[0x060], 0x01"), ("setOwnerOf", "var[0x060], 0x0F"), ("stopObjectCode", "")),
                runtime.ObjectStates, [], runtime: runtime, observation: new(new(records.Add,
                    nativeDependencies: tracked ? new(_ => { }) : null), 1));
            return JsonSerializer.Serialize(new { result, runtime.Inventory, runtime.ObjectOwners, runtime.EstimatedVariables,
                records = records.Select(r => r with { SourceAccess = null }) });
        }
        Assert.Equal(Run(false), Run(true));
    }

    [Theory]
    [InlineData("loadString, var[0x060], \"var[0x061], text\"", 1)]
    [InlineData("copyString, var[0x060], 0x02", 2)]
    [InlineData("createString, var[0x060], 0x04", 2)]
    [InlineData("setStringChar, var[0x060], 0x02, 0x41", 3)]
    [InlineData("getStringChar, local[0x000], var[0x060], 0x02", 2)]
    public void AllStringFormsRetainOpaqueStorageAndCannotBorrowInventoryProvenance(string operands, int count)
    {
        var records = new List<ScummScriptObservation>();
        var runtime = Runtime();
        var tracker = new ScummNativeDependencyTracker(_ => { });
        new ScummBoundedScriptExecutor().Execute(LoomCallObservationTests.Code(("stringOps", operands)),
            runtime.ObjectStates, [], runtime: runtime, observation: new(new(records.Add, nativeDependencies: tracker), 1));
        var access = records.Single(r => r.Site?.Stage == "instruction").SourceAccess!;
        Assert.Equal(count, access.NativeDependencies!.Count);
        Assert.All(access.NativeDependencies, d => { Assert.Equal("string-storage", d.Domain); Assert.Null(d.Value); Assert.NotNull(d.UnresolvedReason); });
        Assert.DoesNotContain(access.Bindings, b => b.Key == "var[0x061]");
        Assert.Empty(tracker.ActiveRelations);
    }

    private static ScummScriptRuntimeState Runtime()
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>(), new Dictionary<int, byte> { [204] = 15 });
        runtime.SetGlobalVariable("var[0x001]", 1);
        runtime.SetGlobalVariable("var[0x060]", 204);
        runtime.EstimatedVariables.Add("var[0x060]");
        return runtime;
    }

    private static ScummScriptObservation Capture(ScummScriptRuntimeState runtime, string target = "var[0x060]",
        string owner = "0x01", Func<string, bool>? key = null, ScummNativeDependencyTracker? native = null,
        ScummRuntimeRelationTracker? numeric = null)
    {
        var records = new List<ScummScriptObservation>();
        new ScummBoundedScriptExecutor().Execute(LoomCallObservationTests.Code(("setOwnerOf", $"{target}, {owner}"), ("stopObjectCode", "")),
            runtime.ObjectStates, [], runtime: runtime, observation: new(new(records.Add, includeKey: key,
                captureSourceAccess: true, nativeDependencies: native, runtimeRelations: numeric), 1));
        return records.First(r => r.Site?.Stage == "instruction");
    }

    private static ScummNativeDependencyTracker SyntheticReads()
    {
        var tracker = new ScummNativeDependencyTracker(_ => { });
        // Complete synthetic fields isolate mutation notification; actual membership provenance stays unknown.
        var selector = new ScummScriptOperandBinding("target", "0x00CC", null, 204, false);
        tracker.Observe(new(1, 1, null, 1, new(0, null, 0, 0, "instruction", 0, "debug"),
            new Dictionary<string, ushort>(), [], false, SourceAccess: new([], null, [], [
                new("inventory-membership", selector, 1, true, "synthetic membership estimate"),
                new("inventory-membership", selector with { Value = 205 }, 0, false),
                new("object-state", selector, 1, false)])));
        Assert.Equal(3, tracker.ActiveRelations.Count);
        return tracker;
    }
}
