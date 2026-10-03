using SCUMMexplorer.Core.Rendering;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

// Lists every decoded reference to a script number, to check whether the
// undecodable index entries (0, 51, 55, 116) are ever started.
var folder = args[0];
var game = new GameFolderScanner().Scan(folder);
var dialect = ScummScriptDialects.For(game.Id);
var reader = new LoomScriptReader(dialect);
var verbs = new LoomObjectVerbReader(dialect);
var globalCount = new LflIndexReader().ReadDeclaredScriptCount(Path.Combine(folder, "00.LFL"));
var hits = new List<string>();
void Scan(string source, Func<LoomScriptDisassembly?> decode)
{
    LoomScriptDisassembly? script;
    try { script = decode(); } catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { return; }
    if (script is null) return;
    foreach (var i in script.Instructions) if (i.Operands.Contains("var[0x07D]")) Console.WriteLine($"{source} +0x{i.Offset:X4} {i.Operation} {i.Operands}");
}
foreach (var r in game.Resources.Where(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue).OrderBy(r => r.ResourceId))
    Scan($"script {r.ResourceId:000}", () => reader.Decode(r));
foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue).OrderBy(r => r.ResourceId))
{
    var id = room.ResourceId!.Value; var bytes = File.ReadAllBytes(room.FilePath);
    Scan($"Room {id:00} entry", () => reader.DecodeRoomEntry(room));
    Scan($"Room {id:00} exit", () => reader.DecodeRoomExit(room));
    var data = LflEncoding.DecodeXorFF(bytes); var end = data[0] | data[1] << 8; var c = 29 + data[20] * 4 + data[23] + data[24];
    var locals = new HashSet<int>(); while (c < end - 2 && data[c] != 0) { if (data[c] >= globalCount) locals.Add(data[c]); c += 3; }
    foreach (var l in locals) Scan($"Room {id:00} local {l}", () => reader.DecodeRoomLocalScript(room, l, globalCount));
    var records = new LoomRoomObjectDirectory().Decode(bytes);
    var tables = verbs.ReadAll(bytes, records);
    foreach (var rec in records) if (tables.TryGetValue(rec.Id, out var vs))
        foreach (var v in vs.DistinctBy(v => v.ScriptOffset)) Scan($"Room {id:00} object {rec.Id} {v.Label}", () => verbs.Decode(room, rec, v.VerbId));
}
