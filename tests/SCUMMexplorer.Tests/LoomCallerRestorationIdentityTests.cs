
namespace SCUMMexplorer.Tests;

public sealed class LoomCallerRestorationIdentityTests
{
    [Theory]
    [InlineData("none")]
    [InlineData("foreign-id")]
    [InlineData("missing-call")]
    [InlineData("foreign-arguments")]
    [InlineData("foreign-path")]
    [InlineData("omitted-initialization")]
    [InlineData("native-boundary")]
    [InlineData("cancelled")]
    public void EqualRestoredNumbersNeedTheActualObservedIncomingDispatch(string defect)
    {
        var chronology = new List<object>();
        LoomCallObservationTests.WithSession(new() { [2] = [0x0A, 3, 0xFF, 0], [3] = [0x60, 1, 0x80, 0x60, 0, 0] },
            (session, _, _) => {
                session.RunTopLevelScript(LoomCallObservationTests.Code(("startScript", "0x02, [0x0007]"), ("stopObjectCode", "")));
                session.AdvanceFrame(); session.AdvanceFrame();
            }, site: s => defect != "omitted-initialization" || s.Script != 2 || s.Stage != "begin" || s.Offset != 0,
            chronology: chronology);
        var updates = new List<ScummCallCorrespondence>();
        var tracker = new ScummCallCorrespondenceTracker(updates.Add);
        var outgoing = chronology.OfType<ScummCallObservation>().Single(b => b.Stage == "dispatch" && b.Site.Script == 2).Call!;
        foreach (var item in chronology)
        {
            if (item is ScummScriptObservation script) {
                if (defect == "cancelled" && script.Site is { Stage: "begin", Script: 2, Offset: 3 })
                    script = script with { Site = null, CancelledSlots = [outgoing.Slot] };
                tracker.ObserveScript(script);
            }
            else if (item is ScummCallObservation boundary)
            {
                if (boundary.Stage == "resumed-locals" && boundary.Slot == outgoing.Slot)
                {
                    var incoming = boundary.Call!;
                    boundary = defect switch {
                        "foreign-id" => boundary with { Call = incoming with { Id = incoming.Id + 1000 } },
                        "missing-call" => boundary with { Call = null },
                        "foreign-arguments" => boundary with { Call = incoming with { Operands = incoming.Operands.Select(b =>
                            b.Role == "argument" ? b with { Value = 99 } : b).ToArray() } },
                        "foreign-path" => boundary with { Call = incoming with { PathEstimated = !incoming.PathEstimated } },
                        _ => boundary
                    };
                    if (defect == "native-boundary") tracker.NotifyExternal("omitted native scheduler boundary", outgoing.Slot);
                }
                tracker.ObserveCall(boundary);
            }
        }
        var actual = updates.Where(u => u.Dispatch?.Id == outgoing.Id).ToArray();
        Assert.Contains(actual, u => u.Binding is not null && u.HostReturn is not null && u.CallerEnd is not null);
        if (defect == "none") {
            var resumed = actual.Last(u => u.CallerResumption is not null);
            Assert.Equal((ushort)7, resumed.CallerResumption!.RestoredLocals.Locals[0].Value);
            Assert.True(resumed.ChildTerminated);
            Assert.Null(resumed.ImmediateStopCorrespondence);
        }
        else Assert.False(actual.Any(u => u.CallerResumption is not null || u.ImmediateStopCorrespondence is not null), defect);
    }
}
