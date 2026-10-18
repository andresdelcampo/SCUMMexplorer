using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;
var folder = args.FirstOrDefault() ?? "zak ega en";
var game = new GameFolderScanner().Scan(Path.Combine(@"D:\Juegos\Lucas", folder));
var data = ScummDataSources.For(game);
var census = new List<ScummScriptDisassembly>();
if (args.Contains("catalog"))
{
    var timer = System.Diagnostics.Stopwatch.StartNew();
    var session = ScummGameSession.BootForPlay(game,out _);
    Console.Error.WriteLine("Boot: " + timer.Elapsed);
    var indexed=((ScummV2DataSource)data).ReadIndexedObjects();
    foreach(var item in session.CaptureInventoryStatus().Catalog) Console.WriteLine($"ITEM {item.ObjectId} state {indexed.States.GetValueOrDefault(item.ObjectId)} room {item.SourceRoomId} {item.Name}: {item.Evidence}");
    Console.Error.WriteLine("Catalog: " + timer.Elapsed);
    foreach(var note in session.InventoryCatalogNotes) Console.WriteLine(note);
    return;
}
void Dump(string label, ScummScriptDisassembly? script)
{
    if (script is null) return;
    census.Add(script);
    if(args.Contains("census")) return;
    Console.WriteLine(label + " entry " + script.EntryOffset);
    foreach(var i in script.Instructions) Console.WriteLine($"{i.Offset:X4} {i.Operation} {i.Operands}");
}
foreach(var id in data.GlobalScriptIds()) Dump("GLOBAL " + id, data.GlobalScript(id));
foreach(var room in game.Rooms.Where(x=>x.ResourceId.HasValue).Select(x=>x.ResourceId!.Value).Distinct().Order())
{
    var info=data.ReadRoom(room);
    Dump("ENTRY " + room,data.RoomEntryScript(room)); Dump("EXIT " + room,data.RoomExitScript(room));
    var verbs=data.ReadObjectVerbs(room, info.Objects);
    foreach(var obj in info.Objects.GroupBy(x=>x.Id).Select(x=>x.Last()))
    {
        var name=info.ObjectNames.TryGetValue(obj.Id,out var bytes)?ScummTextDecoder.DecodeGlyphs(bytes):"";
        foreach(var verb in verbs.GetValueOrDefault(obj.Id)??[])
            Dump($"OBJECT {obj.Id} room {room} verb {verb.VerbId} {name}",data.ReadObjectVerb(room,obj,verb.VerbId,out _));
    }
}
if(args.Contains("census"))
{
    Console.WriteLine($"SCRIPTS {census.Count}; complete {census.Count(s=>s.HasCompleteControlFlow && s.DecodeNote is null)}; pickup occurrences {census.Sum(s=>s.Instructions.Count(i=>i.Operation=="pickupObject"))}");
    foreach(var s in census.Where(s=>!s.HasCompleteControlFlow || s.DecodeNote is not null)) Console.WriteLine($"ISSUE {s.Context??$"Script {s.ScriptId}"}: {s.DecodeNote}; {string.Join(';',s.Issues??[])}");
    foreach(var script in census)
        foreach(var i in script.Instructions.Where(i=>i.Operation=="setState" && (i.Operands.EndsWith(", Pickupable") || i.Operands.EndsWith(", Unpickupable")) || i.Operation is "startScript" or "chainScript" && !i.Operands.StartsWith("0x")))
            Console.WriteLine($"{script.Context??$"Script {script.ScriptId}"} +{i.Offset:X4}: {i.Operation} {i.Operands}");
}
