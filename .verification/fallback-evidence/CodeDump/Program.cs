using SCUMMexplorer.Core.Rendering;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

// Dumps every decoded instruction of every source (global scripts, room
// entry/exit/local scripts, object verbs) as TSV, plus the object directory of
// every room, so object references can be searched with grep.
var folder = args[0];
var output = args[1];
var game = new GameFolderScanner().Scan(folder);
var dialect = ScummScriptDialects.For(game.Id);
var reader = new LoomScriptReader(dialect);
var verbs = new LoomObjectVerbReader(dialect);
var globalCount = new LflIndexReader().ReadDeclaredScriptCount(Path.Combine(folder, "00.LFL"));
using var code = new StreamWriter(Path.Combine(output, "code.tsv"));
using var objects = new StreamWriter(Path.Combine(output, "objects.tsv"));
objects.WriteLine("room\tid\tlocalIndex\tparentLocal\tparentState\thasImage\tx\ty\tw\th");
void Scan(string source, Func<LoomScriptDisassembly?> decode)
{
    LoomScriptDisassembly? script;
    try { script = decode(); }
    catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { code.WriteLine($"{source}\t-\tDECODE-FAILURE\t{ex.Message}"); return; }
    if (script is null) return;
    foreach (var i in script.Instructions) code.WriteLine($"{source}\t+0x{i.Offset:X4}\t{i.Operation}\t{i.Operands}");
}
foreach (var r in game.Resources.Where(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue).OrderBy(r => r.ResourceId))
    Scan($"script {r.ResourceId:000}", () => reader.Decode(r));
foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue).OrderBy(r => r.ResourceId))
{
    var id = room.ResourceId!.Value; var bytes = File.ReadAllBytes(room.FilePath);
    Scan($"Room {id:00} entry", () => reader.DecodeRoomEntry(room));
    Scan($"Room {id:00} exit", () => reader.DecodeRoomExit(room));
    foreach (var l in LoomSceneDrawBatchesProbe.Locals(bytes, globalCount)) Scan($"Room {id:00} local {l}", () => reader.DecodeRoomLocalScript(room, l, globalCount));
    var records = new LoomRoomObjectDirectory().Decode(bytes);
    var images = new LoomRoomObjects().Decode(bytes).ToDictionary(i => i.Id);
    foreach (var rec in records)
    {
        images.TryGetValue(rec.Id, out var img);
        objects.WriteLine($"{id:00}\t{rec.Id}\t{rec.LocalIndex}\t{rec.ParentLocalIndex}\t{rec.ParentRequiredState}\t{img is not null}\t{img?.X}\t{img?.Y}\t{img?.Width}\t{img?.Height}");
    }
    var tables = verbs.ReadAll(bytes, records);
    foreach (var rec in records) if (tables.TryGetValue(rec.Id, out var vs))
        foreach (var v in vs.DistinctBy(v => v.ScriptOffset)) Scan($"Room {id:00} object {rec.Id} {v.Label}", () => verbs.Decode(room, rec, v.VerbId));
}
static class LoomSceneDrawBatchesProbe
{
    public static IEnumerable<int> Locals(byte[] bytes, int globalCount)
    {
        var data = LflEncoding.DecodeXorFF(bytes); var end = data[0] | data[1] << 8; var c = 29 + data[20] * 4 + data[23] + data[24];
        var locals = new List<int>(); while (c < end - 2 && data[c] != 0) { if (data[c] >= globalCount && !locals.Contains(data[c])) locals.Add(data[c]); c += 3; }
        return locals;
    }
}
