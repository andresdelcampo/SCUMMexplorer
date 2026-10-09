using System.Text.Json;

namespace SCUMMexplorer.Tests;

public sealed class LoomCallCorrespondenceTests
{
    [Fact]
    public void ActualAssignmentsAndThreeImmediateBoundariesSupplyOnlyStructuralCorrespondence()
    {
        var updates = new List<ScummCallCorrespondence>();
        var tracker = new ScummCallCorrespondenceTracker(updates.Add);
        LoomCallObservationTests.WithSession(new() { [2] = [0] }, (session, _, _) => {
            session.RunTopLevelScript(LoomCallObservationTests.Code(("startScript", "0x02, [0x0007]"), ("stopObjectCode", "")));
        }, correspondence: tracker);
        var result = updates.Last(u => u.ImmediateStopCorrespondence is not null);
        var assignment = Assert.Single(result.Binding!.SuppliedAssignments);
        Assert.Equal("argument", assignment.Argument.Role);
        Assert.Equal("supplied-local", assignment.Local.Role);
        Assert.Equal((ushort)7, assignment.Local.Value);
        Assert.Equal(result.Dispatch!.Slot, result.Binding.Initialization.ParentSlot);
        Assert.True(result.ChildTerminated);
        Assert.Equal("end", result.ImmediateStopCorrespondence!.ChildEnd.Stage);
        Assert.Equal("dispatch-ended", result.HostReturn!.Stage);
        Assert.Equal("caller-continued", result.CallerContinuation!.Stage);
        Assert.Contains(result.Limits, l => l.Contains("no callee-local numeric origins"));
        Assert.Throws<NotSupportedException>(() => ((IList<ScummCalleeAssignment>)result.Binding.SuppliedAssignments).Clear());
        Assert.Equal(0, tracker.PendingCalls);
    }

    [Fact]
    public void YieldAndLaterRestorationKeepHostReturnAndImmediateCallerContinuationSeparate()
    {
        var updates = new List<ScummCallCorrespondence>();
        var tracker = new ScummCallCorrespondenceTracker(updates.Add);
        LoomCallObservationTests.WithSession(new() { [2] = [0x80, 0] }, (session, _, _) => {
            session.RunTopLevelScript(LoomCallObservationTests.Code(("startScript", "0x02, [0x0007]"), ("stopObjectCode", "")));
            var yielded = updates.Last(u => u.CallerContinuation is not null);
            Assert.NotNull(yielded.HostReturn);
            Assert.False(yielded.ChildTerminated);
            Assert.Null(yielded.ImmediateStopCorrespondence);
            session.AdvanceFrame();
        }, correspondence: tracker);
        var terminal = updates.Last(u => u.ChildTerminated);
        Assert.Equal(new[] { "initialized", "end", "resumed-locals", "end" }, terminal.ChildBoundaries.Select(b => b.Stage));
        Assert.NotEqual(terminal.ChildBoundaries[0].Invocation, terminal.ChildBoundaries[^1].Invocation);
        Assert.Null(terminal.ImmediateStopCorrespondence);
        Assert.Equal(0, tracker.PendingCalls);
    }

    [Fact]
    public void FrozenCallerRequiresActualRestoredLocalsAndItsFirstResumedInstruction()
    {
        var updates = new List<ScummCallCorrespondence>();
        LoomCallObservationTests.WithSession(new() { [2] = [0x0A, 3, 0xFF, 0], [3] = [0x60, 1, 0x80, 0x60, 0, 0] }, (session, _, _) => {
            session.RunTopLevelScript(LoomCallObservationTests.Code(("startScript", "0x02, [0x0007]"), ("stopObjectCode", "")));
            Assert.DoesNotContain(updates, u => u.CallerResumption is not null);
            session.AdvanceFrame(); session.AdvanceFrame();
        }, correspondence: new(updates.Add));
        var resumed = updates.Last(u => u.Dispatch?.Site.Script == 2 && u.CallerResumption is not null);
        Assert.Null(resumed.CallerContinuation);
        Assert.Null(resumed.ImmediateStopCorrespondence);
        var proof = resumed.CallerResumption!;
        Assert.Equal(proof.Yield.ResumeOffset, proof.RestoredLocals.Site.Offset);
        Assert.Equal(proof.RestoredLocals.Site.Offset, proof.FirstInstruction.Site!.Offset);
        Assert.NotEqual(proof.Yield.Invocation, proof.FirstInstruction.Invocation);
        Assert.Equal((ushort)7, proof.RestoredLocals.Locals[0].Value);
    }

    [Fact]
    public void NonreturningChainKeepsItsDeferredBindingAndRejectsTheChainingParentAsATerminal()
    {
        var updates = new List<ScummCallCorrespondence>();
        LoomCallObservationTests.WithSession(new() { [2] = [0x42, 3, 1, 9, 0, 0xFF], [3] = [0] }, (session, _, _) => {
            session.RunTopLevelScript(LoomCallObservationTests.Code(("startScript", "0x02, []"), ("stopObjectCode", "")));
        }, correspondence: new(updates.Add));
        Assert.Contains(updates, u => u.Dispatch?.Site.Operation == "chainScript" && u.Binding is not null && u.ChildTerminated);
        Assert.DoesNotContain(updates, u => u.Dispatch?.Site.Operation == "chainScript" && u.ImmediateStopCorrespondence is not null);
        Assert.Contains(updates, u => u.Dispatch?.Site.Script == 0 && u.ChildRejection == "child end is partial/foreign/omitted/chained");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrPartialChildCannotBorrowHostReturnAsTermination(bool missing)
    {
        var updates = new List<ScummCallCorrespondence>();
        LoomCallObservationTests.WithSession(new() { [2] = [0xFF] }, (session, _, _) => {
            session.RunTopLevelScript(LoomCallObservationTests.Code(("startScript", missing ? "0x63, []" : "0x02, []"), ("stopObjectCode", "")));
        }, correspondence: new(updates.Add));
        Assert.Contains(updates, u => u.HostReturn is not null && u.CallerContinuation is not null);
        Assert.DoesNotContain(updates, u => u.ChildTerminated || u.ImmediateStopCorrespondence is not null);
        if (missing) Assert.All(updates, u => Assert.Null(u.Binding));
    }

    [Fact]
    public void CancellationAndReplacementRetireCorrespondenceAndKeepHistoricalAssignments()
    {
        var updates = new List<ScummCallCorrespondence>();
        LoomCallObservationTests.WithSession(new() { [2] = [0x0A, 3, 0xFF, 0x80, 0], [3] = [0x62, 2, 0] }, (session, _, _) => {
            session.RunTopLevelScript(LoomCallObservationTests.Code(("startScript", "0x02, [0x0007]"), ("stopObjectCode", "")));
        }, correspondence: new(updates.Add));
        var retired = updates.First(u => u.Barrier == "call slot cancelled or replaced" && u.Binding is not null);
        Assert.Equal((ushort)7, retired.Binding!.SuppliedAssignments[0].Local.Value);
        Assert.False(retired.ChildTerminated);
        Assert.Null(retired.ImmediateStopCorrespondence);
    }

    [Theory]
    [InlineData("local")]
    [InlineData("dispatch")]
    [InlineData("end")]
    public void FilteredAssignmentsDispatchOrEndsCannotSupplyCorrespondence(string filter)
    {
        var updates = new List<ScummCallCorrespondence>();
        LoomCallObservationTests.WithSession(new() { [2] = [0] }, (session, _, _) => {
            session.RunTopLevelScript(LoomCallObservationTests.Code(("startScript", "0x02, [0x0007]"), ("stopObjectCode", "")));
        }, site: s => (filter != "dispatch" || s.Operation != "startScript") && (filter != "end" || s.Stage != "end"),
            key: k => filter != "local" || k != "local[0x000]", correspondence: new(updates.Add));
        Assert.DoesNotContain(updates, u => u.ImmediateStopCorrespondence is not null);
        if (filter is "local" or "dispatch") Assert.All(updates, u => Assert.Null(u.Binding));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("missing-dispatch")]
    [InlineData("foreign-parent")]
    [InlineData("foreign-resource")]
    [InlineData("missing-end")]
    [InlineData("reused-run")]
    [InlineData("wrong-resume")]
    [InlineData("external")]
    [InlineData("bound")]
    public void IncompleteChildChainsFailClosedDespiteEqualNumbers(string defect)
    {
        var calls = new List<ScummCallObservation>();
        var scripts = new List<ScummScriptObservation>();
        var chronology = new List<object>();
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>());
        var observer = new ScummScriptObserver(s => { scripts.Add(s); chronology.Add(s); }, receiveCall: c => { calls.Add(c); chronology.Add(c); });
        var caller = new ScummObservedCall(1, 2, 1, null, 1, new(0, null, 0, 0, "instruction", 0, "startScript", "0x02, [0x0007]"),
            [new("target", "0x02", null, 2, false), new("argument", "0x0007", null, 7, false, ArgumentIndex: 0)], false);
        // Capture genuine executor boundaries, then perturb only the copied evidence.
        new ScummBoundedScriptExecutor().Execute(LoomCallObservationTests.Code(("startScript", "0x02, [0x0007]"), ("stopObjectCode", "")),
            runtime.ObjectStates, [], runtime: runtime, observation: new(observer, 1), onEvent: (_, _) => {
                new ScummBoundedScriptExecutor().Execute(new(2, 0, 0, 2, 2, [new(0, 0x80, "breakHere", "", "80"), new(1, 0, "stopObjectCode", "", "00")], null, null, [], []),
                    runtime.ObjectStates, [], runtime: runtime, initialLocals: new Dictionary<string, ushort> { ["local[0x000]"] = 7 }, observation: new(observer, 2, 1, caller));
                return "";
            });
        new ScummBoundedScriptExecutor().Execute(new(2, 0, 0, 2, 2, [new(1, 0, "stopObjectCode", "", "00")], null, null, [], []),
            runtime.ObjectStates, [], runtime: runtime, startOffset: 1, initialLocals: new Dictionary<string, ushort> { ["local[0x000]"] = 7 },
            observation: new(observer, 2, 1, caller, Resuming: true));
        var updates = new List<ScummCallCorrespondence>();
        var tracker = new ScummCallCorrespondenceTracker(updates.Add, boundaryLimit: defect == "bound" ? 2 : 128);
        foreach (var item in chronology)
        {
            if (item is ScummScriptObservation s) tracker.ObserveScript(s);
            else if (item is ScummCallObservation c)
            {
                if (defect == "missing-dispatch" && c.Stage == "dispatch" || defect == "missing-end" && c.Stage == "end" && c.Slot == 2 && c.ResumeOffset is not null) continue;
                if (c.Stage == "resumed-locals")
                {
                    if (defect == "foreign-parent") c = c with { ParentSlot = 99 };
                    if (defect == "foreign-resource") c = c with { Site = c.Site with { ResourceOffset = 99 } };
                    if (defect == "reused-run") c = c with { Invocation = calls.First(b => b.Stage == "initialized" && b.Slot == 2).Invocation };
                    if (defect == "wrong-resume") c = c with { Site = c.Site with { Offset = 0 } };
                    if (defect == "external") tracker.NotifyExternal();
                }
                tracker.ObserveCall(c);
            }
        }
        if (defect == "none") Assert.Contains(updates, u => u.ChildTerminated);
        else Assert.DoesNotContain(updates, u => u.ChildTerminated || u.ImmediateStopCorrespondence is not null);
    }

    [Fact]
    public void TrackerOnlyCaptureLeavesLegacyPacketAndSequencesExact()
    {
        string Run(bool capture) {
            string packet = "";
            LoomCallObservationTests.WithSession(new() { [2] = [0x80, 0] }, (session, _, outer) => {
                session.RunTopLevelScript(LoomCallObservationTests.Code(("startScript", "0x02, [0x0007]"), ("stopObjectCode", "")));
                session.AdvanceFrame();
                packet = JsonSerializer.Serialize(new { session.Snapshot, session.SessionTrace, outer });
            }, capture: false, correspondence: capture ? new(_ => { }) : null);
            return packet;
        }
        Assert.Equal(Run(false), Run(true));
    }
}
