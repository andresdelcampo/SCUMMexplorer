
namespace SCUMMexplorer.Tests;

public sealed class LoomCallPhasePayloadTests
{
    [Theory]
    [InlineData("child", "none")]
    [InlineData("child", "stop")]
    [InlineData("child", "yield")]
    [InlineData("child", "resume")]
    [InlineData("caller", "none")]
    [InlineData("caller", "stop")]
    [InlineData("caller", "yield")]
    [InlineData("caller", "resume")]
    [InlineData("first", "none")]
    [InlineData("first", "stop")]
    [InlineData("first", "yield")]
    [InlineData("first", "resume")]
    public void RestorationAndItsFirstInstructionCannotCarryEndResults(string phase, string defect)
    {
        var chronology = new List<object>();
        LoomCallObservationTests.WithSession(new() { [2] = [0x0A, 3, 0xFF, 0], [3] = [0x60, 1, 0x80, 0x60, 0, 0] },
            (session, _, _) => {
                session.RunTopLevelScript(LoomCallObservationTests.Code(("startScript", "0x02, [0x0007]"), ("stopObjectCode", "")));
                session.AdvanceFrame(); session.AdvanceFrame();
            }, chronology: chronology);
        var outgoing = chronology.OfType<ScummCallObservation>().Single(b => b.Stage == "dispatch" && b.Site.Script == 2).Call!;
        var selected = chronology.OfType<ScummCallObservation>().First(b => b.Stage == "resumed-locals" && b.Site.Script == (phase == "child" ? 3 : 2));
        var first = chronology.OfType<ScummScriptObservation>().First(s => s.Invocation == selected.Invocation && s.Site?.Stage == "instruction");
        var updates = new List<ScummCallCorrespondence>();
        var tracker = new ScummCallCorrespondenceTracker(updates.Add);
        bool? stop = defect == "stop" ? true : defect == "yield" ? false : null;
        int? resume = defect == "resume" ? selected.Site.Offset : null;
        foreach (var item in chronology)
        {
            if (item is ScummScriptObservation script)
            {
                if (defect != "none" && script.Sequence == (phase == "first" ? first.Sequence : selected.InstructionSequence))
                    script = script with { ReachedStop = stop, ResumeOffset = resume };
                tracker.ObserveScript(script);
            }
            else if (item is ScummCallObservation boundary)
            {
                if (defect != "none" && phase != "first" && boundary.Sequence == selected.Sequence)
                    boundary = boundary with { ReachedStop = stop, ResumeOffset = resume };
                tracker.ObserveCall(boundary);
            }
        }
        var actual = updates.Where(u => u.Dispatch?.Id == outgoing.Id).ToArray();
        Assert.Contains(actual, u => u.Binding is not null && u.HostReturn is not null);
        if (defect == "none") {
            Assert.Contains(actual, u => u.ChildTerminated);
            Assert.Contains(actual, u => u.CallerResumption is not null);
        }
        else if (phase == "child") Assert.DoesNotContain(actual, u => u.ChildTerminated);
        else Assert.DoesNotContain(actual, u => u.CallerResumption is not null);
        Assert.All(actual, u => Assert.Null(u.ImmediateStopCorrespondence));
    }

    [Theory]
    [InlineData("dispatch", "none")]
    [InlineData("dispatch", "stop")]
    [InlineData("dispatch", "yield")]
    [InlineData("dispatch", "resume")]
    [InlineData("continued", "none")]
    [InlineData("continued", "stop")]
    [InlineData("continued", "yield")]
    [InlineData("continued", "resume")]
    public void DispatchAndImmediateContinuationCannotCarryEndResults(string phase, string defect)
    {
        var chronology = new List<object>();
        LoomCallObservationTests.WithSession(new() { [2] = [0] }, (session, _, _) =>
            session.RunTopLevelScript(LoomCallObservationTests.Code(("startScript", "0x02, []"), ("stopObjectCode", ""))),
            chronology: chronology);
        var selected = chronology.OfType<ScummCallObservation>().Single(b => b.Stage == (phase == "dispatch" ? "dispatch" : "caller-continued"));
        var updates = new List<ScummCallCorrespondence>();
        var tracker = new ScummCallCorrespondenceTracker(updates.Add);
        bool? stop = defect == "stop" ? true : defect == "yield" ? false : null;
        int? resume = defect == "resume" ? selected.Site.Offset : null;
        foreach (var item in chronology)
        {
            if (item is ScummScriptObservation script)
                tracker.ObserveScript(defect != "none" && script.Sequence == selected.InstructionSequence
                    ? script with { ReachedStop = stop, ResumeOffset = resume } : script);
            else if (item is ScummCallObservation boundary)
                tracker.ObserveCall(defect != "none" && boundary.Sequence == selected.Sequence
                    ? boundary with { ReachedStop = stop, ResumeOffset = resume } : boundary);
        }
        if (defect == "none") Assert.Contains(updates, u => u.ImmediateStopCorrespondence is not null);
        else Assert.DoesNotContain(updates, u => u.ImmediateStopCorrespondence is not null);
        if (phase == "dispatch" && defect != "none") Assert.DoesNotContain(updates, u => u.Binding is not null);
    }
}
