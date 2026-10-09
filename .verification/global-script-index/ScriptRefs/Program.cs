using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

// Lists every decoded reference to a script number, to check whether the
// undecodable index entries (0, 51, 55, 116) are ever started.
var folder = args[0];
var game = new GameFolderScanner().Scan(folder);
var dialect = ScummProfile.For(game).Dialect;
var reader = new ScummScriptReader(dialect);
var verbs = new ScummObjectVerbReader(dialect);
var globalCount = new LflIndexReader().ReadDeclaredScriptCount(Path.Combine(folder, "00.LFL"));
var ops = new HashSet<string> { "startScript", "chainScript", "stopScript", "isScriptRunning", "freezeScripts" };
var watched = new HashSet<int> { 0, 51, 55, 116 };
var refs = new List<string>(); var dynamic = new List<string>(); var failures = new List<string>();
var sources = 0;
void Scan(string source, Func<ScummScriptDisassembly?> decode)
{
    ScummScriptDisassembly? script;
    try { script = decode(); }
    catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { failures.Add($"{source}: {ex.Message}"); return; }
    if (script is null) return;
    sources++;
    if (source == "Room 02 local 203") foreach (var x in script.Instructions) Console.WriteLine($"DUMP +0x{x.Offset:X4} {x.Operation} {x.Operands}");
    foreach (var i in script.Instructions)
    {
        if (i.Operation is "startScript" or "chainScript" && (i.Operands.StartsWith("0xCB") || i.Operands.StartsWith("0xCD"))) Console.WriteLine($"CALL {source} +0x{i.Offset:X4} {i.Operation} {i.Operands}");
        if (!ops.Contains(i.Operation)) continue;
        var parts = i.Operands.Split(','); var first = (i.Operation == "isScriptRunning" ? parts.ElementAtOrDefault(1) ?? "" : parts[0]).Trim();
        if (first.StartsWith("0x") && int.TryParse(first[2..], System.Globalization.NumberStyles.HexNumber, null, out var id))
        { if (watched.Contains(id)) refs.Add($"{source} +0x{i.Offset:X4} {i.Operation} {i.Operands}"); }
        else dynamic.Add($"{source} +0x{i.Offset:X4} {i.Operation} {i.Operands}");
    }
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
    var records = new ScummRoomObjectDirectory().Decode(bytes);
    var tables = verbs.ReadAll(bytes, records);
    foreach (var rec in records) if (tables.TryGetValue(rec.Id, out var vs))
        foreach (var v in vs.DistinctBy(v => v.ScriptOffset)) Scan($"Room {id:00} object {rec.Id} {v.Label}", () => verbs.Decode(room, rec, v.VerbId));
}
Console.WriteLine($"Decoded sources: {sources}; global script count declared in 00.LFL: {globalCount}");
Console.WriteLine($"Literal references to scripts 0, 51, 55, 116: {refs.Count}"); refs.ForEach(s => Console.WriteLine("  " + s));
Console.WriteLine($"Script references through a variable (target unknown): {dynamic.Count}"); dynamic.ForEach(s => Console.WriteLine("  " + s));
Console.WriteLine($"Decode failures: {failures.Count}"); failures.ForEach(s => Console.WriteLine("  " + s));
