using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;
using System.Text.Json;

var game = new GameFolderScanner().Scan(@"D:\Juegos\Lucas\zakE ega en");
var data = ScummDataSources.For(game);
var session = ScummGameSession.BootForPlay(game, out _);
for (int f = 0; f < 2500 && !session.CaptureInventoryStatus().CanEdit; f++) session.AdvanceFrame();
var view = session.CaptureInventoryStatus();
Console.WriteLine($"CATALOG {view.Catalog.Count}");
foreach (var item in view.Catalog) Console.WriteLine($"ITEM {item.ObjectId} room {item.SourceRoomId}: {item.Name}: {item.Evidence}");
foreach (var note in session.InventoryCatalogNotes) Console.WriteLine("AUDIT " + note);

bool Wanted(ScummScriptDisassembly script) => script.Instructions.Any(i => i.Operation == "pickupObject" || i.Operation == "setState" && i.Operands.Contains("Pickupable"));
var scripts = new List<object>();
void Dump(string context, ScummScriptDisassembly? script, bool force = false)
{
    if (script is not null) scripts.Add(new {context, script.EntryOffset, script.CodeLength, script.DecodeNote, script.UndecodedOffset, script.Issues, Instructions = script.Instructions.Select(i => new {i.Offset, i.Operation, i.Operands})});
    if (script is null || !force && !Wanted(script)) return;
    Console.WriteLine("SCRIPT " + context);
    foreach (var i in script.Instructions) Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");
}
foreach (var id in data.GlobalScriptIds()) Dump($"Global {id}", data.GlobalScript(id), new[]{24,55,119}.Contains(id));
foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).Distinct().Order())
{
    Dump($"Room {room} entry", data.RoomEntryScript(room));
    Dump($"Room {room} exit", data.RoomExitScript(room));
    foreach (var id in data.RoomLocalScriptIds(room)) Dump($"Room {room} local {id}", data.RoomLocalScript(room,id));
    var rd = data.ReadRoom(room);
    foreach (var obj in rd.Objects.Where(o => new[] {49,52,59,68,70,44,53}.Contains(o.Id)))
        Console.WriteLine($"HEADER {obj.Id} room {room} class {obj.V2ClassMask}; name {ScummTextDecoder.DecodeGlyphs(rd.ObjectNames[obj.Id])}");
    var verbs = data.ReadObjectVerbs(room,rd.Objects);
    foreach (var obj in rd.Objects.GroupBy(o => o.Id).Select(g => g.Last()))
        foreach (var verb in verbs.GetValueOrDefault(obj.Id) ?? [])
            Dump($"Room {room} object {obj.Id} verb {verb.VerbId}", data.ReadObjectVerb(room,obj,verb.VerbId,out _), obj.Id == 139);
}
File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"scripts.json"),JsonSerializer.Serialize(scripts,new JsonSerializerOptions{WriteIndented=true}));
