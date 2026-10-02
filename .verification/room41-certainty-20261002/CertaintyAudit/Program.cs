using System.Text.Json;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

// Isolated execution from captured globals/estimates. No reference to the live
// session, actor placement, note replacement, estimate removal or scene dispatch.
var game = new GameFolderScanner().Scan(args[0]);
var reader = new LoomScriptReader();
var executor = new LoomBoundedScriptExecutor();
LoomScriptDisassembly Script(int id) => reader.Decode(game.Resources.Single(r =>
    r.Kind == ResourceKind.Script && r.ResourceId == id));
LoomScriptRuntimeState Copy(JsonElement snapshot)
{
    var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
    foreach (var p in snapshot.GetProperty("snapshot").GetProperty("KnownGlobalVariables").EnumerateObject())
        runtime.GlobalVariables[p.Name] = p.Value.GetUInt16();
    runtime.EstimatedVariables.UnionWith(snapshot.GetProperty("estimatedVariables").EnumerateArray().Select(p => p.GetString()!));
    return runtime;
}
var results = new List<object>();
foreach (var file in Directory.GetFiles(args[1], "room41-*.json").Order())
{
    var packet = JsonDocument.Parse(File.ReadAllText(file)).RootElement;
    var snapshots = packet.GetProperty("snapshots").EnumerateArray().ToArray();
    var before = snapshots.Single(s => s.GetProperty("stage").GetString() == "draft-4-before");
    var entered = snapshots.Single(s => Regex.IsMatch(s.GetProperty("stage").GetString()!, @"^draft-4-(click|key)-note-4$"));
    var gate = executor.Execute(Script(10), new Dictionary<int, byte>(), [], instructionLimit: 1,
        runtime: Copy(before), initialLocals: new Dictionary<string, ushort> { ["local[0x000]"] = 1 });
    var scanRuntime = Copy(entered);
    var children = new List<object>();
    var scan = executor.Execute(Script(22), new Dictionary<int, byte>(), [], runtime: scanRuntime,
        onEvent: (e, r) =>
        {
            if (e.Kind == LoomScriptEventKind.ScriptChained && e.TargetId == 26)
                return LoomBoundedScriptExecutor.StopAfterEvent;
            if (e.Kind != LoomScriptEventKind.ScriptStarted || e.TargetId != 23)
                throw new InvalidDataException($"Unexpected isolated scan event: {e.Format()}");
            int slot = Convert.ToInt32(Regex.Match(e.Operands, @"\[0x([0-9A-Fa-f]+)\]").Groups[1].Value, 16);
            var child = executor.Execute(Script(23), new Dictionary<int, byte>(), [], runtime: r,
                initialLocals: new Dictionary<string, ushort> { ["local[0x000]"] = (ushort)slot },
                estimatedLocals: e.EstimatedArgumentIndexes?.Contains(0) == true ? ["local[0x000]"] : [],
                pathEstimated: e.Certainty != LoomScriptEventCertainty.Reached);
            if (!child.ReachedStop || child.UnresolvedReason is not null)
                throw new InvalidDataException("Isolated script023 did not complete.");
            children.Add(new { slot, e.Certainty, e.EstimatedArgumentIndexes,
                response = r.GlobalVariables["var[0x065]"], responseEstimated = r.EstimatedVariables.Contains("var[0x065]") });
            return "";
        });
    if (!scan.ReachedStop || scan.UnresolvedReason is not null)
        throw new InvalidDataException("Isolated script022 did not reach chain026.");
    object? dye = null;
    var firstDyeNote = snapshots.SingleOrDefault(s => s.GetProperty("stage").GetString() == "draft-2-key-note-1");
    if (firstDyeNote.ValueKind != JsonValueKind.Undefined)
    {
        var lesson = executor.Execute(Script(31), new Dictionary<int, byte>(), [], runtime: Copy(firstDyeNote),
            initialLocals: new Dictionary<string, ushort> { ["local[0x000]"] = 1 },
            onEvent: (_, _) => LoomBoundedScriptExecutor.StopAfterEvent);
        dye = new { lesson.Trace, lesson.Events, lesson.PathEstimated, lesson.UnresolvedReason,
            scope = "Observed first dye-note globals; stop at the first source event, without sound/actor effects." };
    }
    results.Add(new { packet = file, gate = new { gate.Trace, gate.PathEstimated, gate.UnresolvedReason,
        scope = "Only script010's first condition; expected instruction-limit stop at0007, no actor or note-buffer mutation." },
        scan = new { scan.Trace, scan.Events, scan.PathEstimated, scan.UnresolvedReason, children,
        scope = "Observed four-note globals; native022 and synchronous023, stop at chain026; no scene dispatch." }, dye });
}
Directory.CreateDirectory(Path.GetDirectoryName(args[2])!);
File.WriteAllText(args[2], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Isolated certainty traces: {results.Count} captured source scans; no live-session writes.");
