using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomScriptObserverTests
{
    [Fact]
    public void IndexedBindingsSeparateSelectedValueIndexAndInheritedPath()
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        runtime.GlobalVariables["var[0x060]"] = 0x1002; // Resolver masks the index to 12 bits.
        runtime.GlobalVariables["var[0x034]"] = 17;
        runtime.EstimatedVariables.Add("var[0x060]");
        var records = ObserveOperands(runtime, "move", "local[0x001] = var[0x032+var[0x060]]", path: true);
        var read = records[1].OperandBindings!.Single(b => b.Role == "source");
        Assert.Equal("var[0x034]", read.Key);
        Assert.Equal((ushort)17, read.Value);
        Assert.False(read.ValueEstimated);
        Assert.Equal((ushort)0x1002, read.Index!.Value);
        Assert.True(read.Index.ValueEstimated);
        Assert.True(records[1].PathEstimated);
        Assert.Contains("local[0x001]", records[^1].Estimated);
        Assert.Throws<NotSupportedException>(() => ((IList<LoomScriptOperandBinding>)records[1].OperandBindings!).Clear());
        runtime.GlobalVariables["var[0x060]"] = 4;
        Assert.Equal((ushort)0x1002, read.Index.Value);
    }

    [Fact]
    public void UnknownAndFilteredIndexesNeverBindACellOrFabricateZero()
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        runtime.GlobalVariables.Remove("var[0x060]"); // Runtime initializes ordinary globals to zero.
        runtime.GlobalVariables["var[0x032]"] = 9;
        var unknown = ObserveOperands(runtime, "move", "local[0x001] = var[0x032+var[0x060]]")[1]
            .OperandBindings!.Single(b => b.Role == "source");
        Assert.Null(unknown.Key);
        Assert.Null(unknown.Value);
        Assert.Equal("unresolved index", unknown.UnresolvedReason);
        runtime.GlobalVariables["var[0x060]"] = 0;
        var filtered = ObserveOperands(runtime, "move", "local[0x001] = var[0x032+var[0x060]]",
            include: key => key != "var[0x060]")[1].OperandBindings!.Single(b => b.Role == "source");
        Assert.Null(filtered.Key);
        Assert.Null(filtered.Value);
        Assert.Equal("filtered operand", filtered.Index!.UnresolvedReason);
    }

    [Fact]
    public void BitBindingsKeepBackingWordAndItsEstimateWithoutClaimingAWriter()
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        runtime.GlobalVariables["var[0x0D6]"] = 0x80;
        runtime.EstimatedVariables.Add("var[0x0D6]");
        var binding = ObserveOperands(runtime, "move", "local[0x001] = bit[0xD67]")[1]
            .OperandBindings!.Single(b => b.Role == "source");
        Assert.Equal("var[0x0D6]", binding.BackingWord);
        Assert.Equal((ushort)1, binding.Value);
        Assert.True(binding.ValueEstimated);
        var filtered = ObserveOperands(runtime, "move", "local[0x001] = bit[0xD67]",
            include: key => key != "var[0x0D6]")[1].OperandBindings!.Single(b => b.Role == "source");
        Assert.Null(filtered.Value);
        Assert.Null(filtered.ValueEstimated);
    }

    [Fact]
    public void CallBindingsPreserveWholeArgumentOrderAndNestedIndexSelection()
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        runtime.GlobalVariables["var[0x060]"] = 2;
        runtime.GlobalVariables["var[0x034]"] = 3;
        runtime.GlobalVariables["var[0x055]"] = 8;
        runtime.EstimatedVariables.Add("var[0x055]");
        var bindings = ObserveOperands(runtime, "startScript",
            "0x03, [var[0x052+var[0x032+var[0x060]]], 0x0000, local[0x000]]")[1].OperandBindings!;
        Assert.Equal(new int?[] { 0, 1, 2 }, bindings.Where(b => b.Role == "argument").Select(b => b.ArgumentIndex));
        var first = bindings[1];
        Assert.Equal("var[0x055]", first.Key);
        Assert.Equal((ushort)8, first.Value);
        Assert.True(first.ValueEstimated);
        Assert.Equal("var[0x034]", first.Index!.Key);
        Assert.Equal("var[0x060]", first.Index.Index!.Key);
        Assert.Null(bindings[2].Key); // Literal zero is distinct from missing input.
        Assert.Equal((ushort)0, bindings[2].Value);
        Assert.Equal("local[0x000]", bindings[3].Key);
    }

    [Fact]
    public void BindingCapturePreservesWritesResultsAndDefaultObservationSequence()
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        runtime.GlobalVariables["var[0x060]"] = 0;
        var defaults = ObserveOperands(runtime, "move", "var[0x060] = 0x0002", capture: false);
        runtime.GlobalVariables["var[0x060]"] = 0;
        var bound = ObserveOperands(runtime, "move", "var[0x060] = 0x0002");
        Assert.Equal(defaults.Select(r => r.Sequence), bound.Select(r => r.Sequence));
        Assert.Equal(defaults.Select(r => r.Values["var[0x060]"]), bound.Select(r => r.Values["var[0x060]"]));
        Assert.All(defaults, r => Assert.Null(r.OperandBindings));
        Assert.Equal((ushort)0, bound[1].OperandBindings![0].Value); // Pre-write destination.
        Assert.Equal((ushort)2, bound[^1].Values["var[0x060]"]);
        Assert.Null(bound[0].OperandBindings); // Begin/end do not imply an instruction access.
        Assert.Null(bound[^1].OperandBindings);
    }

    private static List<LoomScriptObservation> ObserveOperands(LoomScriptRuntimeState runtime, string op,
        string operands, bool path = false, Func<string, bool>? include = null, bool capture = true)
    {
        var records = new List<LoomScriptObservation>();
        var code = new LoomScriptDisassembly(2, 1, 0, 2, 2,
            [new(0, 0, op, operands, "00"), new(1, 0, "stopObjectCode", "", "00")], null, null, [], []);
        new LoomBoundedScriptExecutor().Execute(code, runtime.ObjectStates, [], runtime: runtime,
            pathEstimated: path, observation: new(new(records.Add, includeKey: include, captureOperandBindings: capture), 1));
        return records;
    }

    [Fact]
    public void SameTurnWriterAndReaderUseSequenceAndCausalSlotIdentity()
    {
        var observations = new List<LoomScriptObservation>();
        WithSession(new() {
            [2] = [0x0A, 3, 0xFF, 0x28, 0x67, 0x8D, 0, 0, 0], // Child, then equalZero D67.
            [3] = [0x1A, 0x67, 0x8D, 0, 0, 0] // Literal D670, then stop.
        }, new(observations.Add), session => session.RunTopLevelScript(Driver(2)));
        var writer = observations.Single(r => r.Site is { Script: 3, Stage: "instruction", Offset: 0 });
        var after = observations.First(r => r.Invocation == writer.Invocation && r.Sequence > writer.Sequence);
        var reader = observations.Single(r => r.Site is { Script: 2, Operation: "equalZero" });
        Assert.Equal((ushort)0, after.Values["bit[0xD67]"]);
        Assert.True(after.Sequence < reader.Sequence);
        Assert.Equal(reader.Slot, writer.ParentSlot);
        Assert.NotEqual(reader.Slot, writer.Slot);
        Assert.True(observations.Zip(observations.Skip(1)).All(pair => pair.First.Sequence < pair.Second.Sequence));
    }

    [Fact]
    public void ResumptionKeepsTheSlotWhileCancellationAndReplacementStayDistinct()
    {
        var observations = new List<LoomScriptObservation>();
        WithSession(new() {
            [2] = [0x80, 0x62, 3, 0x0A, 3, 0xFF, 0],
            [3] = [0x80, 0x46, 0xF1, 0, 0]
        }, new(observations.Add), session =>
        {
            session.RunTopLevelScript(Driver(2, 3));
            session.AdvanceFrame();
            session.AdvanceFrame();
            Assert.Equal((ushort)1, session.Snapshot.KnownGlobalVariables["var[0x0F1]"]);
        });
        var starts = observations.Where(r => r.Site is { Script: 3, Stage: "begin", Offset: 0 }).ToArray();
        Assert.Equal(2, starts.Length);
        Assert.NotEqual(starts[0].Slot, starts[1].Slot);
        var cancel = observations.Single(r => r.CancelledNumber == 3);
        Assert.Equal(new[] { starts[0].Slot }, cancel.CancelledSlots);
        var resumed = observations.Single(r => r.Site is { Script: 3, Stage: "begin", Offset: 1 });
        Assert.Equal(starts[1].Slot, resumed.Slot);
        Assert.NotEqual(starts[1].Invocation, resumed.Invocation);
        Assert.DoesNotContain(observations, r => r.Slot == starts[0].Slot && r.Sequence > cancel.Sequence && r.Site is { Stage: "instruction" });
    }

    [Fact]
    public void ACancelledExecutingCallerHasNoFollowingSourceInstruction()
    {
        var observations = new List<LoomScriptObservation>();
        WithSession(new() {
            [2] = [0x0A, 3, 0xFF, 0x1A, 0xF1, 0, 1, 0, 0],
            [3] = [0x62, 2, 0]
        }, new(observations.Add), session => session.RunTopLevelScript(Driver(2)));
        var cancel = observations.Single(r => r.CancelledNumber == 2);
        Assert.DoesNotContain(observations, r => r.Slot == cancel.CancelledSlots!.Single() && r.Site is { Stage: "instruction", Offset: 3 });
        Assert.Contains(observations, r => r.Slot == cancel.CancelledSlots!.Single() && r.Site is { Stage: "end" } && r.ReachedStop == true);
    }

    [Fact]
    public void CopiesCannotMutateOrFollowLaterRuntimeWrites()
    {
        var observations = new List<LoomScriptObservation>();
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        runtime.GlobalVariables["var[0x0F1]"] = 9;
        var code = new LoomScriptDisassembly(2, 1, 0, 2, 2,
            [new(0, 0, "move", "var[0x0F1] = 0x0000", "00"), new(1, 0, "stopObjectCode", "", "00")], null, null, [], []);
        var result = new LoomBoundedScriptExecutor().Execute(code, runtime.ObjectStates, [], runtime: runtime,
            pathEstimated: true, observation: new(new(observations.Add), 1));
        Assert.True(result.PathEstimated);
        Assert.Contains("var[0x0F1]", runtime.EstimatedVariables);
        var before = observations.Single(r => r.Site is { Stage: "instruction", Offset: 0 });
        var end = observations.Single(r => r.Site is { Stage: "end" });
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, ushort>)before.Values)["var[0x0F1]"] = 22);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)end.Estimated).Clear());
        runtime.GlobalVariables["var[0x0F1]"] = 99;
        runtime.EstimatedVariables.Clear();
        Assert.Equal((ushort)9, before.Values["var[0x0F1]"]);
        Assert.Equal((ushort)0, end.Values["var[0x0F1]"]);
        Assert.Contains("var[0x0F1]", end.Estimated);
    }

    [Fact]
    public void FilteredObservationsPreserveTheUnobservedAndObservedResults()
    {
        var code = new LoomScriptDisassembly(2, 1, 0, 2, 2,
            [new(0, 0, "move", "var[0x0F1] = 0x0001", "00"), new(1, 0, "stopObjectCode", "", "00")], null, null, [], []);
        var records = new List<LoomScriptObservation>();
        var first = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        var second = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        var executor = new LoomBoundedScriptExecutor();
        var unobserved = executor.Execute(code, first.ObjectStates, [], runtime: first);
        var observed = executor.Execute(code, second.ObjectStates, [], runtime: second, observation: new(new(records.Add,
            site => site.Stage == "instruction", key => key == "var[0x0F1]"), 1));
        Assert.Equal(unobserved.KnownVariables, observed.KnownVariables);
        Assert.Equal(unobserved.Trace, observed.Trace);
        Assert.Equal(unobserved.Events, observed.Events);
        Assert.All(records, r => Assert.Single(r.Values));
        Assert.Equal(first.EstimatedVariables, second.EstimatedVariables);
    }

    private static LoomScriptDisassembly Driver(params int[] numbers) => new(0, 1, 0, numbers.Length + 1, numbers.Length + 1,
        numbers.Select((id, offset) => new LoomScriptInstruction(offset, 0x0A, "startScript", $"0x{id:X2}, []", "00"))
            .Append(new(numbers.Length, 0, "stopObjectCode", "", "00")).ToArray(), null, null, [], []);

    private static void WithSession(Dictionary<int, byte[]> programs, LoomScriptObserver observer, Action<LoomExplorationSession> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "loom-observer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            programs.Add(1, [0]);
            var resources = new List<GameResource>();
            foreach (var (id, code) in programs)
            {
                var data = new byte[code.Length + 4];
                BinaryPrimitives.WriteUInt16LittleEndian(data, (ushort)data.Length);
                code.CopyTo(data, 4);
                var path = Path.Combine(directory, $"{id:00}.LFL");
                File.WriteAllBytes(path, data.Select(b => (byte)(b ^ 0xFF)).ToArray());
                resources.Add(new($"Script {id}", ResourceKind.Script, path, id, ContainerFileNumber: id, Offset: 0));
            }
            var game = new GameLibrary("LOOM", directory, ScummVersion.V3, "DOS", resources, "synthetic observer fixtures", GameId.Loom);
            check(new LoomExplorationSession(game, new(new Dictionary<int, byte>(), new Dictionary<int, byte>()), completeBoot: false, observer: observer));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
