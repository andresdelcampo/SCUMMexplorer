using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

// Captured pending-slot condition only. No live session or effect dispatch.
var game = new GameFolderScanner().Scan(args[0]);
var script = new LoomScriptReader().Decode(game.Resources.Single(r =>
    r.Kind == ResourceKind.Script && r.ResourceId == 20));
var packet = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
var results = new List<object>();
foreach (var snapshot in packet.GetProperty("snapshots").EnumerateArray()
    .Where(s => s.GetProperty("stage").GetString()!.StartsWith("draft-2-")))
{
    foreach (var pending in snapshot.GetProperty("pending").EnumerateArray()
        .Where(p => p.GetProperty("number").ValueKind == JsonValueKind.Number && p.GetProperty("number").GetInt32() == 20
            && p.GetProperty("offset").GetInt32() == 0x28))
    {
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        foreach (var p in snapshot.GetProperty("snapshot").GetProperty("KnownGlobalVariables").EnumerateObject())
            runtime.GlobalVariables[p.Name] = p.Value.GetUInt16();
        runtime.EstimatedVariables.UnionWith(snapshot.GetProperty("estimatedVariables").EnumerateArray().Select(p => p.GetString()!));
        var locals = pending.GetProperty("locals").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetUInt16());
        var run = new LoomBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], instructionLimit: 1,
            runtime: runtime, startOffset: 0x28, initialLocals: locals,
            estimatedLocals: pending.GetProperty("estimatedLocals").EnumerateArray().Select(p => p.GetString()!).ToArray(),
            pathEstimated: pending.GetProperty("pathEstimated").GetBoolean());
        results.Add(new { stage = snapshot.GetProperty("stage").GetString(), pendingPathEstimated = pending.GetProperty("pathEstimated").GetBoolean(),
            flag = runtime.GlobalVariables["bit[0xD67]"], flagEstimated = runtime.EstimatedVariables.Contains("bit[0xD67]"),
            run.PathEstimated, run.Trace, run.UnresolvedReason,
            scope = "Script020 +0028 only; expected instruction-limit stop; no event, actor, input-buffer or live-session effects." });
    }
}
if (results.Count == 0) throw new InvalidDataException("No captured script020 handshake slots.");
Directory.CreateDirectory(Path.GetDirectoryName(args[2])!);
File.WriteAllText(args[2], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{results.Count} captured Script020 handshake conditions; isolated copies only.");
