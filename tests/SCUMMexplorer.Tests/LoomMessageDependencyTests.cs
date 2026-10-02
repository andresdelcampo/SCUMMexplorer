using System.Text.Json;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomMessageDependencyTests
{
    [Fact]
    public void StoredMessageWaitFieldsRemainIndependentOfTheGlobalAndUnknownProvenance()
    {
        var runtime = Talking();
        runtime.SetGlobalVariable(LoomMessageTable.HaveMsgVariable, 255); // Independent native/global storage.
        runtime.EstimatedVariables.Add(LoomMessageTable.HaveMsgVariable);
        var observation = Capture(runtime);
        var access = observation.SourceAccess!;
        Assert.NotNull(access.Barrier);
        Assert.Equal("message-wait", access.NativeDependencies![0].Domain);
        Assert.Null(access.NativeDependencies[0].Value);
        var fields = access.NativeDependencies.Skip(1).ToArray();
        Assert.Equal(new long?[] { runtime.Messages.HaveMsg, runtime.Messages.TalkDelay, runtime.Messages.TalkingActor }, fields.Select(d => d.Value));
        Assert.Equal(1, fields[0].Value);
        Assert.Equal((ushort)255, fields[0].Selector.Value);
        Assert.All(fields, d => {
            Assert.True(d.Selector.ValueEstimated);
            Assert.Null(d.ValueEstimated);
            Assert.Contains("message source estimate", d.Estimate);
            Assert.NotNull(d.UnresolvedReason);
        });
    }

    [Fact]
    public void FilteredImplicitMessageOperandDoesNotDiscloseNativeFields()
    {
        var observation = Capture(Talking(), k => k != LoomMessageTable.HaveMsgVariable);
        var dependency = Assert.Single(observation.SourceAccess!.NativeDependencies!);
        Assert.Equal("message-wait", dependency.Domain);
        Assert.Null(dependency.Value);
        Assert.Null(dependency.Selector.Value);
    }

    [Fact]
    public void MissingNativeFieldProvenanceCannotBecomeALiveLifetimeOrNumericOrigin()
    {
        var runtime = Talking();
        var native = new LoomNativeDependencyTracker(_ => { });
        var numeric = new LoomRuntimeRelationTracker(_ => { });
        var records = new List<LoomScriptObservation>();
        new LoomBoundedScriptExecutor().Execute(LoomCallObservationTests.Code(("waitFor", "message")), runtime.ObjectStates, [],
            runtime: runtime, observation: new(new(records.Add, nativeDependencies: native, runtimeRelations: numeric), 1));
        Assert.Empty(native.ActiveRelations);
        Assert.DoesNotContain(numeric.ActiveRelations, r => r.ObservedOrigin is not null);
        Assert.Equal(4, records.First(r => r.Site?.Stage == "instruction").SourceAccess!.NativeDependencies!.Count);
    }

    [Theory]
    [InlineData("frame")]
    [InlineData("skip")]
    [InlineData("room")]
    [InlineData("print")]
    [InlineData("route")]
    [InlineData("box")]
    [InlineData("verb")]
    public void ExecutedMessageMutationsNotifyEvenForNoOpsAndPreserveOtherNativeFamilies(string operation)
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        var updates = new List<LoomNativeDependencyUpdate>();
        var tracker = new LoomNativeDependencyTracker(updates.Add);
        // Synthetic complete native reads isolate the notification API; production message
        // fields retain unknown provenance and establish no loaded lifetime claim.
        var site = new LoomScriptObservationSite(0, null, 0, 0, "instruction", 0, "debug", "0x0000");
        var selector = new LoomScriptOperandBinding("native-message", "_message", null, 0, false);
        tracker.Observe(new(1, 1, null, 1, site, new Dictionary<string, ushort>(), [], true,
            SourceAccess: new([], null, [], [new("message-talk-delay", selector, 0, true, "historical native estimate"),
                new("object-position-x", selector with { Value = 204 }, 16, false)])));
        Assert.Equal(2, tracker.ActiveRelations.Count);
        var history = tracker.ActiveRelations[0];
        var observer = new LoomScriptObserver(_ => { }, nativeDependencies: tracker);
        using (runtime.ObserveGlobalMutations(observer)) {
            switch (operation) {
                case "frame": runtime.Messages.StartFrame(0, runtime); break;
                case "skip": runtime.Messages.SkipTalkDelay(); break;
                case "room": runtime.Messages.RoomEntered(runtime); break;
                case "print": runtime.Messages.Print(new(255, [], null, "no text", 0, null, null), runtime); break;
                case "route": runtime.Messages.RouteSkipped("prior skipped route"); break;
                case "box": runtime.Messages.DrawBox(0, 0, 0, 0, 0); break;
                case "verb": runtime.Messages.VerbDrawn(new(0, 0, 0, 0, 0)); break;
            }
        }
        Assert.Equal("object-position-x", Assert.Single(tracker.ActiveRelations).Dependency.Domain);
        Assert.Contains(updates, u => u.Observation is null && u.Invalidated.Any(i => i.Read == history.Id));
        Assert.True(history.Dependency.ValueEstimated);
        Assert.Equal("historical native estimate", history.Dependency.Estimate);
    }

    [Fact]
    public void UnscopedAndRawExternalMessageNotificationsRequireAnAttachedSink()
    {
        var runtime = Talking();
        var tracker = new LoomNativeDependencyTracker(_ => { });
        var selector = new LoomScriptOperandBinding("native-message", "_message", null, 0, false);
        tracker.Observe(new(1, 1, null, 1, new(0, null, 0, 0, "instruction", 0, "debug"), new Dictionary<string, ushort>(), [], false,
            SourceAccess: new([], null, [], [new("message-have-msg", selector, 1, true)])));
        runtime.Messages.SkipTalkDelay();
        Assert.Single(tracker.ActiveRelations);
        using (runtime.ObserveGlobalMutations(new(_ => { }, nativeDependencies: tracker))) runtime.NotifyNativeMutation("message");
        Assert.Empty(tracker.ActiveRelations);
    }

    [Fact]
    public void CopiedWaitAndNotificationObservationLeaveEveryMessageFrameAndLegacyInstructionExact()
    {
        string Run(bool observed) {
            var runtime = Talking();
            var outer = new List<LoomScriptObservation>();
            var frames = new List<string>();
            var observer = new LoomScriptObserver(outer.Add, captureSourceAccess: true, nativeDependencies: observed ? new(_ => { }) : null);
            using (runtime.ObserveGlobalMutations(observer)) {
                for (var i = 0; i < 8; i++) {
                    new LoomBoundedScriptExecutor().Execute(LoomCallObservationTests.Code(("waitFor", "message"), ("stopObjectCode", "")),
                        runtime.ObjectStates, [], runtime: runtime, observation: new(observer, 1));
                    runtime.Messages.StartFrame(15, runtime);
                    frames.Add(JsonSerializer.Serialize(new { runtime.Messages.HaveMsg, runtime.Messages.TalkDelay, runtime.Messages.Talk,
                        runtime.GlobalVariables, runtime.EstimatedVariables }));
                }
            }
            return JsonSerializer.Serialize(new { frames, outer });
        }
        // Both runs capture access identically; only independent lifetime notifications differ.
        Assert.Equal(Run(false), Run(true));
    }

    private static LoomScriptRuntimeState Talking()
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        runtime.Messages.Print(new(255, [], [65, 0], "synthetic talk", 0, null, "message source estimate"), runtime);
        return runtime;
    }
    private static LoomScriptObservation Capture(LoomScriptRuntimeState runtime, Func<string, bool>? key = null)
    {
        var records = new List<LoomScriptObservation>();
        new LoomBoundedScriptExecutor().Execute(LoomCallObservationTests.Code(("waitFor", "message")), runtime.ObjectStates, [],
            runtime: runtime, observation: new(new(records.Add, includeKey: key, captureSourceAccess: true), 1));
        return records.First(r => r.Site?.Stage == "instruction");
    }
}
