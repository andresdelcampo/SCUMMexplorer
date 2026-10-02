using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomCallBoundaryOrderTests
{
    [Theory]
    [InlineData("none")]
    [InlineData("terminal-begin")]
    [InlineData("yield-begin")]
    public void ChildEndsRequireTheirOwnActualEndPhase(string defect)
    {
        var chronology = new List<object>();
        LoomCallObservationTests.WithSession(new() { [2] = [0x80, 0] }, (session, _, _) => {
            session.RunTopLevelScript(LoomCallObservationTests.Code(("startScript", "0x02, []"), ("stopObjectCode", "")));
            session.AdvanceFrame();
        }, chronology: chronology);
        var updates = new List<LoomCallCorrespondence>();
        var tracker = new LoomCallCorrespondenceTracker(updates.Add);
        foreach (var item in chronology)
        {
            bool Alter(LoomScriptObservationSite? site, bool? stop) => site is { Script: 2, Stage: "end" } &&
                (defect == "terminal-begin" && stop == true || defect == "yield-begin" && stop == false);
            if (item is LoomScriptObservation script)
                tracker.ObserveScript(Alter(script.Site, script.ReachedStop) ? script with { Site = script.Site! with { Stage = "begin" } } : script);
            else if (item is LoomCallObservation call)
                tracker.ObserveCall(Alter(call.Site, call.ReachedStop) ? call with { Site = call.Site with { Stage = "begin" } } : call);
        }
        if (defect == "none") Assert.Contains(updates, u => u.ChildTerminated);
        else Assert.False(updates.Any(u => u.ChildTerminated), defect);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("host-offset")]
    [InlineData("host-sequence")]
    [InlineData("host-path")]
    [InlineData("initial-offset")]
    [InlineData("initial-instruction")]
    [InlineData("late-initialization")]
    [InlineData("continuation-begin")]
    [InlineData("native")]
    [InlineData("cancelled")]
    public void ImmediateCorrespondenceRequiresActualInitializationAndDispatchPhases(string defect)
    {
        var chronology = Capture(chain: false);
        var dispatch = chronology.OfType<LoomCallObservation>().Single(c => c.Stage == "dispatch").Call!;
        var updates = new List<LoomCallCorrespondence>();
        var tracker = new LoomCallCorrespondenceTracker(updates.Add);
        long sequence = 0;
        foreach (var item in chronology)
        {
            if (item is LoomScriptObservation script)
            {
                if (script.Site is { Script: 2, Stage: "begin" })
                {
                    if (defect == "late-initialization")
                    {
                        var host = chronology.OfType<LoomCallObservation>().Single(c => c.Stage == "dispatch-ended");
                        tracker.ObserveCall(host with { Sequence = ++sequence });
                    }
                    if (defect == "native") tracker.NotifyExternal("omitted native scheduler boundary", dispatch.Slot);
                    if (defect == "cancelled") script = script with { Site = null, CancelledSlots = [dispatch.Slot] };
                }
                tracker.ObserveScript(ChangeSite(script, defect));
            }
            else if (item is LoomCallObservation call)
            {
                if (call.Stage == "dispatch-ended")
                {
                    if (defect == "late-initialization") continue;
                    call = defect switch {
                        "host-offset" => call with { Site = call.Site with { Offset = call.Site.Offset + 1 } },
                        "host-sequence" => call with { InstructionSequence = call.InstructionSequence + 1 },
                        "host-path" => call with { PathEstimated = !call.PathEstimated },
                        _ => call
                    };
                }
                if (call.Stage == "initialized" && call.Site.Script == 2)
                    call = call with { Site = InitialSite(call.Site, defect) };
                if (call.Stage == "caller-continued" && defect == "continuation-begin")
                    call = call with { Site = call.Site with { Stage = "begin" } };
                tracker.ObserveCall(call with { Sequence = ++sequence });
            }
        }
        if (defect == "none") Assert.Contains(updates, u => u.ImmediateStopCorrespondence is not null);
        else Assert.False(updates.Any(u => u.ImmediateStopCorrespondence is not null), defect);
        if (defect is "initial-offset" or "initial-instruction" or "late-initialization")
            Assert.DoesNotContain(updates, u => u.Binding is not null);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("missing-caller-end")]
    [InlineData("caller-end-begin")]
    [InlineData("caller-end-yield")]
    [InlineData("omitted")]
    public void DeferredChainNeedsItsObservedHostAndTerminatedChainingCaller(string defect)
    {
        var chronology = Capture(chain: true);
        var dispatch = chronology.OfType<LoomCallObservation>().Single(c => c.Stage == "dispatch" && c.Site.Operation == "chainScript").Call!;
        var updates = new List<LoomCallCorrespondence>();
        var tracker = new LoomCallCorrespondenceTracker(updates.Add);
        long sequence = 0;
        foreach (var item in chronology)
        {
            if (item is LoomScriptObservation script)
            {
                if (script.Site is { Script: 2, Stage: "end" })
                    script = defect switch {
                        "caller-end-begin" => script with { Site = script.Site with { Stage = "begin" } },
                        "caller-end-yield" => script with { ReachedStop = false, ResumeOffset = 0 },
                        _ => script
                    };
                tracker.ObserveScript(script);
            }
            else if (item is LoomCallObservation call)
            {
                if (call.Stage == "caller-ended" && call.Call?.Id == dispatch.Id && defect == "missing-caller-end") continue;
                if (call.Site is { Script: 2, Stage: "end" })
                    call = defect switch {
                        "caller-end-begin" => call with { Site = call.Site with { Stage = "begin" } },
                        "caller-end-yield" => call with { ReachedStop = false, ResumeOffset = 0 },
                        _ => call
                    };
                if (call.Stage == "initialized" && call.Call?.Id == dispatch.Id && defect == "omitted")
                    tracker.NotifyExternal("omitted deferred scheduler boundary", call.ParentSlot);
                tracker.ObserveCall(call with { Sequence = ++sequence });
            }
        }
        var chain = updates.Where(u => u.Dispatch?.Id == dispatch.Id).ToArray();
        if (defect == "none") Assert.Contains(chain, u => u.Binding is not null && u.ChildTerminated);
        else Assert.DoesNotContain(chain, u => u.Binding is not null);
        Assert.All(chain, u => Assert.Null(u.ImmediateStopCorrespondence));
    }

    private static LoomScriptObservationSite InitialSite(LoomScriptObservationSite site, string defect) => defect switch {
        "initial-offset" => site with { Offset = 1 },
        "initial-instruction" => site with { Stage = "instruction" },
        _ => site
    };

    private static LoomScriptObservation ChangeSite(LoomScriptObservation script, string defect)
    {
        if (script.Site is { Script: 2, Stage: "begin" } site) return script with { Site = InitialSite(site, defect) };
        if (script.Site is { Script: 0, Stage: "instruction", Operation: "stopObjectCode" } continuation && defect == "continuation-begin")
            return script with { Site = continuation with { Stage = "begin" } };
        return script;
    }

    private static List<object> Capture(bool chain)
    {
        var chronology = new List<object>();
        var programs = chain ? new Dictionary<int, byte[]> { [2] = [0x42, 3, 0xFF], [3] = [0] }
            : new Dictionary<int, byte[]> { [2] = [0] };
        LoomCallObservationTests.WithSession(programs, (session, _, _) =>
            session.RunTopLevelScript(LoomCallObservationTests.Code(("startScript", "0x02, []"), ("stopObjectCode", ""))),
            chronology: chronology);
        return chronology;
    }
}
