using System.Buffers.Binary;
using System.Text.Json;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomCallObservationTests
{
    [Fact]
    public void ActualInitializationRetainsSuppliedDefaultAndEstimatedLocalsWithoutOrigins()
    {
        var records = new List<LoomCallObservation>();
        var relations = new LoomRuntimeRelationTracker(_ => { });
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        new LoomBoundedScriptExecutor().Execute(Code(("stopObjectCode", "")), runtime.ObjectStates, [], runtime: runtime,
            initialLocals: new Dictionary<string, ushort> { ["local[0x000]"] = 7 }, estimatedLocals: ["local[0x000]"],
            observation: new(new(_ => { }, runtimeRelations: relations, receiveCall: records.Add), 1), pathEstimated: true);
        var init = records[0];
        Assert.Equal("initialized", init.Stage);
        Assert.Equal(26, init.Locals.Count);
        Assert.Equal("supplied-local", init.Locals[0].Role);
        Assert.Equal((ushort)7, init.Locals[0].Value);
        Assert.True(init.Locals[0].ValueEstimated);
        Assert.True(init.PathEstimated);
        Assert.All(init.Locals.Skip(1), b => { Assert.Equal("default-zero", b.Role); Assert.Equal((ushort)0, b.Value); });
        Assert.Empty(relations.ActiveRelations);
        Assert.Null(init.Call);
        Assert.NotNull(init.Barrier);
        Assert.Throws<NotSupportedException>(() => ((IList<LoomScriptOperandBinding>)init.Locals).Clear());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualChildStopOrYieldIsIndependentOfHostDispatchAndCallerContinuation(bool yield)
    {
        WithSession(new() { [2] = yield ? [0x80, 0] : [0] }, (session, records, _) => {
            session.RunTopLevelScript(Code(("startScript", "0x02, [0x0007]"), ("debug", "0x0001"), ("stopObjectCode", "")));
            var dispatch = records.Single(r => r.Stage == "dispatch");
            var init = records.Single(r => r.Stage == "initialized" && r.Call?.Id == dispatch.Call!.Id);
            Assert.Equal(dispatch.Slot, init.ParentSlot);
            Assert.Equal((ushort)7, init.Locals[0].Value);
            Assert.Equal("supplied-local", init.Locals[0].Role);
            var end = records.Single(r => r.Stage == "end" && r.Slot == init.Slot);
            Assert.Equal(!yield, end.ReachedStop);
            Assert.Equal(yield ? 1 : null, end.ResumeOffset);
            var returned = records.Single(r => r.Stage == "dispatch-ended");
            var continued = records.Single(r => r.Stage == "caller-continued");
            Assert.True(init.Sequence < end.Sequence && end.Sequence < returned.Sequence && returned.Sequence < continued.Sequence);
            Assert.Equal(dispatch.Invocation, continued.Invocation);
            Assert.Contains("callee outcome remains separate", returned.Barrier);
            if (yield) {
                session.AdvanceFrame();
                var resumed = Assert.Single(records, r => r.Stage == "resumed-locals");
                Assert.Equal(init.Slot, resumed.Slot);
                Assert.Equal(init.Call, resumed.Call);
                Assert.NotEqual(init.Invocation, resumed.Invocation);
                Assert.All(resumed.Locals, b => Assert.Equal("restored-local", b.Role));
            }
        });
    }

    [Fact]
    public void ChainRecordsDeferredInitializationAndNoCallerContinuation()
    {
        WithSession(new() { [2] = [0x42, 3, 1, 9, 0, 0xFF], [3] = [0] }, (session, records, _) => {
            session.RunTopLevelScript(Code(("startScript", "0x02, []"), ("stopObjectCode", "")));
            var chain = records.Single(r => r.Stage == "dispatch" && r.Site.Operation == "chainScript");
            var init = records.Single(r => r.Stage == "initialized" && r.Call?.Id == chain.Call!.Id);
            Assert.Equal((ushort)9, init.Locals[0].Value);
            Assert.Equal(chain.Slot, init.ParentSlot);
            Assert.DoesNotContain(records, r => r.Stage == "caller-continued" && r.Call?.Id == chain.Call!.Id);
            Assert.Contains(records, r => r.Stage == "caller-ended" && r.Call?.Id == chain.Call!.Id);
        });
    }

    [Fact]
    public void CancelledCallerDoesNotDonateAContinuationFromItsReachedStop()
    {
        WithSession(new() { [2] = [0x0A, 3, 0xFF, 0x80, 0], [3] = [0x62, 2, 0] }, (session, records, _) => {
            session.RunTopLevelScript(Code(("startScript", "0x02, []"), ("stopObjectCode", "")));
            var cancelled = records.Single(r => r.Stage == "dispatch" && r.Site.Script == 2);
            Assert.DoesNotContain(records, r => r.Stage == "caller-continued" && r.Call?.Id == cancelled.Call!.Id);
            Assert.Contains(records, r => r.Stage == "caller-ended" && r.Call?.Id == cancelled.Call!.Id && r.ReachedStop == true);
        });
    }

    [Fact]
    public void MissingCalleeIsAHostReturnWithNoInitialization()
    {
        WithSession(new() { [2] = [0] }, (session, records, _) => {
            session.RunTopLevelScript(Code(("startScript", "0x63, []"), ("stopObjectCode", "")));
            var call = records.Single(r => r.Stage == "dispatch").Call!;
            Assert.DoesNotContain(records, r => r.Stage == "initialized" && r.Call?.Id == call.Id);
            Assert.Contains(records, r => r.Stage == "dispatch-ended" && r.Call?.Id == call.Id);
            Assert.Contains(records, r => r.Stage == "caller-continued" && r.Call?.Id == call.Id);
        });
    }

    [Fact]
    public void FilteredDispatchOrLocalsCannotSupplyTransferEvidence()
    {
        WithSession(new() { [2] = [0] }, (session, records, _) => {
            session.RunTopLevelScript(Code(("startScript", "0x02, [0x0007]"), ("stopObjectCode", "")));
            Assert.DoesNotContain(records, r => r.Stage == "dispatch");
            Assert.All(records.Where(r => r.Stage == "initialized"), r => {
                Assert.Null(r.Call); Assert.Null(r.Locals[0].Value); Assert.NotNull(r.Locals[0].UnresolvedReason);
            });
        }, site: s => s.Operation != "startScript", key: k => k != "local[0x000]");
    }

    [Fact]
    public void NestedCallsKeepActualImmediateDispatchAndResourceIdentities()
    {
        WithSession(new() { [2] = [0x0A, 3, 0xFF, 0], [3] = [0] }, (session, records, _) => {
            session.RunTopLevelScript(Code(("startScript", "0x02, []"), ("stopObjectCode", "")));
            var init = records.Single(r => r.Stage == "initialized" && r.Site.Script == 3);
            Assert.Equal(2, init.Call!.Site.Script);
            Assert.Equal(init.ParentSlot, init.Call.Slot);
            Assert.Equal(3, init.Site.Container);
            Assert.NotEqual(init.Call.Invocation, init.Invocation);
        });
    }

    [Fact]
    public void PartialChildEndCannotBecomeATerminalOrHideTheActualCallerContinuation()
    {
        WithSession(new() { [2] = [0xFF] }, (session, records, _) => {
            session.RunTopLevelScript(Code(("startScript", "0x02, []"), ("stopObjectCode", "")));
            var child = records.Single(r => r.Stage == "end" && r.Site.Script == 2);
            Assert.False(child.ReachedStop);
            Assert.NotNull(child.Barrier);
            Assert.Null(child.ResumeOffset);
            Assert.Contains(records, r => r.Stage == "caller-continued" && r.Call?.Id == child.Call!.Id);
        });
    }

    [Fact]
    public void FrozenCallerHasAYieldBoundaryAndRestoresLocalsInItsOwnLaterInvocation()
    {
        WithSession(new() { [2] = [0x0A, 3, 0xFF, 0], [3] = [0x60, 1, 0x80, 0x60, 0, 0] }, (session, records, _) => {
            session.RunTopLevelScript(Code(("startScript", "0x02, [0x0007]"), ("stopObjectCode", "")));
            var call = records.Single(r => r.Stage == "dispatch" && r.Site.Script == 2).Call!;
            Assert.DoesNotContain(records, r => r.Stage == "caller-continued" && r.Call?.Id == call.Id);
            var ended = records.Single(r => r.Stage == "caller-ended" && r.Call?.Id == call.Id);
            Assert.False(ended.ReachedStop);
            Assert.NotNull(ended.ResumeOffset);
            session.AdvanceFrame(); session.AdvanceFrame();
            var resumed = records.Single(r => r.Stage == "resumed-locals" && r.Site.Script == 2);
            Assert.Equal(ended.Slot, resumed.Slot);
            Assert.NotEqual(ended.Invocation, resumed.Invocation);
            Assert.Equal((ushort)7, resumed.Locals[0].Value);
        });
    }

    [Fact]
    public void ReplacementInitializesANewSlotInsteadOfRestoringTheCancelledCallersLocals()
    {
        WithSession(new() { [2] = [0x28, 0, 0x40, 7, 0, 0x0A, 2, 1, 1, 0, 0xFF, 0, 0x80, 0] }, (session, records, _) => {
            session.RunTopLevelScript(Code(("startScript", "0x02, []"), ("stopObjectCode", "")));
            var copies = records.Where(r => r.Stage == "initialized" && r.Site.Script == 2).ToArray();
            Assert.Equal(2, copies.Length);
            Assert.NotEqual(copies[0].Slot, copies[1].Slot);
            Assert.Equal(copies[0].Slot, copies[1].Call!.Slot);
            Assert.Equal((ushort)1, copies[1].Locals[0].Value);
            session.AdvanceFrame();
            Assert.DoesNotContain(records, r => r.Stage == "resumed-locals" && r.Slot == copies[0].Slot);
            Assert.Contains(records, r => r.Stage == "resumed-locals" && r.Slot == copies[1].Slot);
        });
    }

    [Fact]
    public void SeparateCallBoundaryBoundStopsRecordingAndLeavesExecutionAndOuterSequenceExact()
    {
        var code = Code(Enumerable.Repeat(("startScript", "0x02, []"), 1400).Append(("stopObjectCode", "")).ToArray());
        var records = new List<LoomCallObservation>();
        var outer = new List<LoomScriptObservation>();
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        var result = new LoomBoundedScriptExecutor().Execute(code, runtime.ObjectStates, [], runtime: runtime,
            onEvent: (_, _) => "", observation: new(new(outer.Add, receiveCall: records.Add), 1));
        Assert.True(result.ReachedStop);
        Assert.Equal(1401, result.InstructionsVisited);
        Assert.Equal(1403, outer.Count);
        Assert.Equal(4097, records.Count);
        Assert.Equal("call observation boundary bound reached", records[^1].Barrier);
    }

    [Fact]
    public void OptionalCallSinkPreservesLegacySequencePacketAndExecution()
    {
        string Run(bool capture) {
            string packet = "";
            WithSession(new() { [2] = [0x80, 0] }, (session, _, outer) => {
                session.RunTopLevelScript(Code(("startScript", "0x02, [0x0007]"), ("stopObjectCode", "")));
                session.AdvanceFrame();
                packet = JsonSerializer.Serialize(new { session.Snapshot, session.SessionTrace, outer });
            }, capture: capture);
            return packet;
        }
        Assert.Equal(Run(false), Run(true));
    }

    internal static LoomScriptDisassembly Code(params (string Op, string Args)[] operations) => new(0, 0, 0, operations.Length, operations.Length,
        operations.Select((p, i) => new LoomScriptInstruction(i, 0, p.Op, p.Args, "00")).ToArray(), null, null, [], []);

    private static void WithSession(Dictionary<int, byte[]> programs,
        Action<LoomExplorationSession, List<LoomCallObservation>, List<LoomScriptObservation>> check,
        Func<LoomScriptObservationSite, bool>? site = null, Func<string, bool>? key = null, bool capture = true)
    {
        var directory = Path.Combine(Path.GetTempPath(), "loom-calls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            programs.Add(1, [0]);
            var resources = new List<GameResource>();
            foreach (var (id, code) in programs) {
                var data = new byte[code.Length + 4];
                BinaryPrimitives.WriteUInt16LittleEndian(data, (ushort)data.Length);
                code.CopyTo(data, 4);
                var path = Path.Combine(directory, $"{id:00}.LFL");
                File.WriteAllBytes(path, data.Select(b => (byte)(b ^ 0xFF)).ToArray());
                resources.Add(new($"Script {id}", ResourceKind.Script, path, id, ContainerFileNumber: id, Offset: 0));
            }
            var game = new GameLibrary("LOOM", directory, ScummVersion.V3, "DOS", resources, "synthetic call fixtures", GameId.Loom);
            var records = new List<LoomCallObservation>();
            var outer = new List<LoomScriptObservation>();
            check(new(game, new(new Dictionary<int, byte>(), new Dictionary<int, byte>()), completeBoot: false,
                observer: new(outer.Add, site, key, receiveCall: capture ? records.Add : null)), records, outer);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
