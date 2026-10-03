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
var gc = new LflIndexReader().ReadDeclaredScriptCount(Path.Combine(args[0], "00.LFL"));
var res = game.Rooms.Single(r => r.ResourceId == 43);
var recs = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(res.FilePath));
var o639 = recs.Single(o => o.Id == 639);
Console.WriteLine($"obj 639 x{o639.X} y{o639.Y} w{o639.Width} h{o639.Height} walk {o639.WalkX},{o639.WalkY}");
foreach (var v in new LoomObjectVerbReader(dialect).ReadAll(File.ReadAllBytes(res.FilePath), recs)[639]) { Console.WriteLine("verb " + v.Label); foreach (var i in new LoomObjectVerbReader(dialect).Decode(res, o639, v.VerbId).Instructions) Console.WriteLine($"  {i.Offset:X4} {i.Operation} {i.Operands}"); }
var r96 = game.Resources.Single(r => r.Kind == SCUMMexplorer.Core.Domain.ResourceKind.Script && r.ResourceId == 96);
foreach (var i in new LoomScriptReader(dialect).Decode(r96).Instructions.Take(40)) Console.WriteLine($"96 {i.Offset:X4} {i.Operation} {i.Operands}");
var r17 = game.Rooms.Single(r => r.ResourceId == 17);
foreach (var i in new LoomScriptReader(dialect).DecodeRoomLocalScript(r17, 200, gc).Instructions) Console.WriteLine($"17/200 {i.Offset:X4} {i.Operation} {i.Operands}");
var r66 = game.Resources.Single(r => r.Kind == SCUMMexplorer.Core.Domain.ResourceKind.Script && r.ResourceId == 66);
foreach (var i in new LoomScriptReader(dialect).Decode(r66).Instructions.Take(25)) Console.WriteLine($"66 {i.Offset:X4} {i.Operation} {i.Operands}");
return;
void Scan(string source, Func<LoomScriptDisassembly?> decode)
{
    LoomScriptDisassembly? script;
    try { script = decode(); } catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { return; }
    if (script is null) return;
    foreach (var i in script.Instructions)
        if (i.Operands.Contains("bit[0xD70]") || i.Operands.Contains("bit[0xD6F]") || i.Operands.Contains("var[0x07A]") && i.Operation=="move") Console.WriteLine($"{source} +0x{i.Offset:X4} {i.Operation} {i.Operands}");
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
