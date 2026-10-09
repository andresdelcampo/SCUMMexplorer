using System.Buffers.Binary;
using System.Text.Json;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class LoomObjectCallCorrespondenceTests
{
    [Theory]
    [InlineData(0x38)]
    [InlineData(0xFF)]
    public void ActualObjectEntryBindsRequestedVerbAndIndependentAssignments(int entryVerb)
    {
        var updates = new List<ScummCallCorrespondence>();
        WithRoom([0], entryVerb, (session, _, _) => {
            session.RunTopLevelScript(Caller());
        }, tracker: new(updates.Add));
        var result = updates.Last(u => u.ImmediateStopCorrespondence is not null);
        var binding = result.Binding!;
        Assert.Equal(204, binding.ObjectSelection!.ObjectId);
        Assert.Equal(0x38, binding.ObjectSelection.RequestedVerb);
        Assert.Equal(entryVerb, binding.ObjectSelection.Entry.VerbId);
        Assert.Equal(binding.Initialization.Site, binding.ObjectSelection.Site);
        Assert.Equal(62, binding.Initialization.Site.ResourceOffset);
        Assert.Equal((ushort)7, Assert.Single(binding.SuppliedAssignments).Local.Value);
        Assert.All(binding.Initialization.Locals.Skip(1), b => Assert.Equal("default-zero", b.Role));
        Assert.Contains(result.Limits, l => l.Contains("no completion promotion"));
    }

    [Theory]
    [InlineData(0x38)]
    [InlineData(0xFF)]
    public void YieldedObjectRequiresEveryRestorationAndCannotDonateAnImmediateTriple(int entryVerb)
    {
        var updates = new List<ScummCallCorrespondence>();
        var tracker = new ScummCallCorrespondenceTracker(updates.Add);
        WithRoom([0x80, 0x80, 0], entryVerb, (session, _, _) => {
            session.RunTopLevelScript(Caller());
            Assert.DoesNotContain(updates, u => u.ChildTerminated);
            session.AdvanceFrame();
            Assert.DoesNotContain(updates, u => u.ChildTerminated);
            session.AdvanceFrame();
        }, tracker: tracker);
        var result = updates.Last(u => u.ChildTerminated);
        Assert.Equal(new[] { "initialized", "end", "resumed-locals", "end", "resumed-locals", "end" }, result.ChildBoundaries.Select(b => b.Stage));
        Assert.Equal(3, result.ChildBoundaries.Where(b => b.Stage != "end").Select(b => b.Invocation).Distinct().Count());
        Assert.NotNull(result.HostReturn);
        Assert.NotNull(result.CallerContinuation);
        Assert.Null(result.ImmediateStopCorrespondence);
        Assert.Null(result.CallerResumption);
        Assert.Equal(0, tracker.PendingCalls);
    }

    [Theory]
    [InlineData("0x0000, 0x38, [0x0007]")]
    [InlineData("0x00CD, 0x38, [0x0007]")]
    [InlineData("0x00CC, 0x39, [0x0007]")]
    public void AbsentObjectOrVerbKeepsHostReturnWithoutInventingACallee(string operands)
    {
        var updates = new List<ScummCallCorrespondence>();
        WithRoom([0], 0x38, (session, _, _) => session.RunTopLevelScript(Caller(operands)), tracker: new(updates.Add));
        Assert.Contains(updates, u => u.HostReturn is not null && u.CallerContinuation is not null);
        Assert.DoesNotContain(updates, u => u.Binding is not null || u.ChildTerminated || u.ImmediateStopCorrespondence is not null);
    }

    [Fact]
    public void FrozenRootCallerRestorationRetainsUnobservedIncomingDispatchBarrier()
    {
        var updates = new List<ScummCallCorrespondence>();
        WithRoom([0x60, 1, 0x80, 0x60, 0, 0], 0x38, (session, _, _) => {
            session.RunTopLevelScript(Caller());
            session.AdvanceFrame(); session.AdvanceFrame();
        }, tracker: new(updates.Add));
        Assert.Contains(updates, u => u.ChildTerminated);
        Assert.DoesNotContain(updates, u => u.CallerResumption is not null);
        Assert.Contains(updates, u => u.CallerRejection == "missing/foreign/reused caller restoration");
    }

    [Theory]
    [InlineData("none")]
    [InlineData("missing-selection")]
    [InlineData("foreign-object")]
    [InlineData("foreign-request")]
    [InlineData("foreign-entry")]
    [InlineData("foreign-resource")]
    [InlineData("repeated-selection")]
    [InlineData("external")]
    [InlineData("bound")]
    [InlineData("missing-restoration")]
    public void SelectedEntryAndResumedObjectFailClosedAcrossIncompleteEvidence(string defect)
    {
        var chronology = new List<object>();
        WithRoom([0x80, 0], 0xFF, (session, _, _) => {
            session.RunTopLevelScript(Caller()); session.AdvanceFrame();
        }, chronology: chronology);
        var updates = new List<ScummCallCorrespondence>();
        var tracker = new ScummCallCorrespondenceTracker(updates.Add, boundaryLimit: defect == "bound" ? 2 : 128);
        foreach (var item in chronology)
        {
            switch (item)
            {
                case ScummScriptObservation script: tracker.ObserveScript(script); break;
                case ScummCallObservation call:
                    if (defect != "missing-restoration" || call.Stage != "resumed-locals") tracker.ObserveCall(call);
                    break;
                case ScummObjectCalleeSelection selection:
                    if (defect == "missing-selection") break;
                    if (defect == "foreign-object") selection = selection with { ObjectId = 205 };
                    if (defect == "foreign-request") selection = selection with { RequestedVerb = 0x39 };
                    if (defect == "foreign-entry") selection = selection with { Entry = selection.Entry with { ObjectId = 205 } };
                    if (defect == "foreign-resource") selection = selection with { Site = selection.Site with { ResourceOffset = 63 } };
                    if (defect == "external") tracker.NotifyExternal();
                    tracker.ObserveObjectSelection(selection);
                    if (defect == "repeated-selection") tracker.ObserveObjectSelection(selection);
                    break;
            }
        }
        if (defect == "none") Assert.Contains(updates, u => u.ChildTerminated && u.Binding?.ObjectSelection is not null);
        else Assert.DoesNotContain(updates, u => u.ChildTerminated || u.ImmediateStopCorrespondence is not null);
    }

    [Fact]
    public void FilteredObjectTargetDoesNotExposeItsSelectedEntryOrSupplyABinding()
    {
        var chronology = new List<object>();
        var updates = new List<ScummCallCorrespondence>();
        WithRoom([0], 0x38, (session, _, _) => session.RunTopLevelScript(LoomCallObservationTests.Code(
            ("move", "var[0x060] = 0x00CC"), ("startObject", "var[0x060], 0x38, [0x0007]"), ("stopObjectCode", ""))),
            tracker: new(updates.Add), chronology: chronology, key: k => k != "var[0x060]");
        Assert.DoesNotContain(chronology, item => item is ScummObjectCalleeSelection);
        Assert.DoesNotContain(updates, u => u.Binding is not null || u.ImmediateStopCorrespondence is not null);
    }

    [Fact]
    public void FilteredCalleeSiteDoesNotExposeItsSelectedEntryOrSupplyABinding()
    {
        var chronology = new List<object>();
        var updates = new List<ScummCallCorrespondence>();
        WithRoom([0], 0x38, (session, _, _) => session.RunTopLevelScript(Caller()), tracker: new(updates.Add),
            chronology: chronology, site: s => s.Stage != "begin" || s.Context is null);
        Assert.DoesNotContain(chronology, item => item is ScummObjectCalleeSelection);
        Assert.DoesNotContain(updates, u => u.Binding is not null || u.ChildTerminated || u.ImmediateStopCorrespondence is not null);
    }

    [Fact]
    public void ObjectSelectionAndTrackerLeaveLegacyCallsInstructionsAndPacketsExact()
    {
        string Run(bool tracked) {
            string packet = "";
            WithRoom([0x80, 0], 0xFF, (session, calls, scripts) => {
                session.RunTopLevelScript(Caller()); session.AdvanceFrame();
                packet = JsonSerializer.Serialize(new { session.Snapshot, session.SessionTrace, calls, scripts });
            }, tracker: tracked ? new(_ => { }) : null);
            return packet;
        }
        Assert.Equal(Run(false), Run(true));
    }

    private static ScummScriptDisassembly Caller(string args = "0x00CC, 0x38, [0x0007]") =>
        LoomCallObservationTests.Code(("startObject", args), ("stopObjectCode", ""));

    private static void WithRoom(byte[] code, int entryVerb,
        Action<LoomExplorationSession, List<ScummCallObservation>, List<ScummScriptObservation>> check,
        ScummCallCorrespondenceTracker? tracker = null, List<object>? chronology = null, Func<string, bool>? key = null,
        Func<ScummScriptObservationSite, bool>? site = null)
    {
        var folder = Path.Combine(Path.GetTempPath(), "loom-object-calls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            var data = new byte[62 + code.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(data, (ushort)data.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), 320);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), 144);
            data[20] = 1;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(31), 40);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(40), (ushort)(22 + code.Length));
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(44), 204);
            data[47] = 17; data[48] = 1; data[49] = 6; data[55] = 80;
            data[57] = (byte)entryVerb;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(58), 22);
            code.CopyTo(data, 62);
            var path = Path.Combine(folder, "06.LFL");
            File.WriteAllBytes(path, data.Select(b => (byte)(b ^ 0xFF)).ToArray());
            var room = new GameResource("Room 06", ResourceKind.Room, path, 6, ContainerFileNumber: 6);
            var game = new GameLibrary("LOOM", folder, ScummVersion.V3, "DOS", [room], "synthetic object calls", GameId.Loom);
            var calls = new List<ScummCallObservation>();
            var scripts = new List<ScummScriptObservation>();
            var observer = new ScummScriptObserver(s => { scripts.Add(s); chronology?.Add(s); }, includeSite: site, includeKey: key,
                receiveCall: c => { calls.Add(c); chronology?.Add(c); }, callCorrespondence: tracker,
                receiveObjectSelection: s => chronology?.Add(s));
            var session = new LoomExplorationSession(game, new(new Dictionary<int, byte> { [204] = 0 },
                new Dictionary<int, byte> { [204] = 15 }), completeBoot: false, observer: observer);
            session.VisitRoom(room);
            check(session, calls, scripts);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}
