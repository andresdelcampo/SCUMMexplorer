using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

var game = new GameFolderScanner().Scan(@"D:\Juegos\Lucas\mmansion ega en");
var data = (ScummV2DataSource)ScummDataSources.For(game);
var session = ScummGameSession.BootForPlay(game, out _);
ManiacCutsceneSetup.PrepareIfNeeded(game, session);
for (int n = 0; n < 2500 && !session.CaptureInventoryStatus().CanEdit; n++) session.AdvanceFrame();
var view = session.CaptureInventoryStatus();
var blocks = new List<(string Context, ScummScriptDisassembly Script)>();
Console.WriteLine($"CATALOG {view.Catalog.Count}");
foreach (var item in view.Catalog) Console.WriteLine($"ITEM {item.ObjectId} room {item.SourceRoomId} {item.Name}: {item.Evidence}");
foreach (var note in session.InventoryCatalogNotes) Console.WriteLine("NOTE " + note);
void Show(ScummScriptDisassembly? script, string context, bool full = false)
{
    if (script is null) return;
    blocks.Add((context, script));
    var relevant = script.Instructions.Any(i => i.Operation == "pickupObject" || i.Operation == "setState" && i.Operands.Contains("Pickupable"));
    if (full || relevant || script.Instructions.Any(i => (i.Operation is "startScript" or "chainScript") && i.Operands.Split(',')[0].Trim() == "0x74"))
    {
        Console.WriteLine("SCRIPT " + context);
        foreach (var i in script.Instructions) Console.WriteLine($"  +{i.Offset:X4} {i.Operation} {i.Operands}");
    }
}
foreach (var id in data.GlobalScriptIds()) Show(data.GlobalScript(id), $"Global {id}", id is 2 or 116);
var rooms = game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).Distinct().Order().ToArray();
var locations = new Dictionary<int, List<(int Room, string Name)>>();
foreach (var room in rooms)
{
    var rd = data.ReadRoom(room);
    Show(data.RoomEntryScript(room), $"Room {room} entry");
    Show(data.RoomExitScript(room), $"Room {room} exit");
    var verbs = data.ReadObjectVerbs(room, rd.Objects);
    foreach (var obj in rd.Objects.GroupBy(o => o.Id).Select(g => g.Last()))
    {
        var name = rd.ObjectNames.TryGetValue(obj.Id, out var text) ? ScummTextDecoder.DecodeGlyphs(text) : "";
        if (!locations.TryGetValue(obj.Id, out var loc)) locations[obj.Id] = loc = [];
        loc.Add((room, name));
        foreach (var verb in verbs.GetValueOrDefault(obj.Id) ?? [])
            Show(data.ReadObjectVerb(room, obj, verb.VerbId, out _), $"Room {room} Object {obj.Id} ({name}) Verb {verb.VerbId}");
    }
}
foreach (var pair in data.ReadIndexedObjects().States.Where(p => (p.Value & 1) != 0))
    Console.WriteLine($"INDEX {pair.Key} {(locations.TryGetValue(pair.Key, out var loc) ? string.Join(';', loc.Select(x => $"room {x.Room} {x.Name}")) : "NO OBCD")}");
foreach (var block in blocks)
    foreach (var i in block.Script.Instructions)
        if (i.Operation is "startScript" or "chainScript" && !i.Operands.StartsWith("0x") ||
            i.Operation is "move" or "add" or "subtract" or "increment" or "decrement" && i.Operands.StartsWith("var[0x07B]"))
            Console.WriteLine($"FLOW {block.Context} +{i.Offset:X4} {i.Operation} {i.Operands}");
Console.WriteLine($"CENSUS blocks {blocks.Count}; pickup sites {blocks.Sum(b => b.Script.Instructions.Count(i => i.Operation == "pickupObject"))}; Pickupable writes {blocks.Sum(b => b.Script.Instructions.Count(i => i.Operation == "setState" && i.Operands.Contains("Pickupable")))}; code objects {locations.Count}; indexed code candidates {data.ReadIndexedObjects().States.Count(p => (p.Value & 1) != 0 && locations.ContainsKey(p.Key))}");
Console.WriteLine($"DECODE complete {blocks.Count(b => b.Script.HasCompleteControlFlow)}; undecoded {blocks.Count(b => b.Script.UndecodedOffset.HasValue)}; issue blocks {blocks.Count(b => b.Script.Issues?.Count > 0)}; Unpickupable writes {blocks.Sum(b => b.Script.Instructions.Count(i => i.Operation == "setState" && i.Operands.EndsWith(", Unpickupable")))}");
foreach (var block in blocks.Where(b => b.Script.UndecodedOffset.HasValue || b.Script.Issues?.Count > 0))
    Console.WriteLine($"DECODEISSUE {block.Context} offset {block.Script.UndecodedOffset}: {block.Script.DecodeNote}: {string.Join(';', block.Script.Issues?.Select(i => $"+{i.Offset:X4} {i.Reason}") ?? [])}");
foreach (var id in new[] { 34, 35, 36, 37, 38 })
{
    var runtime = new ScummScriptRuntimeState(data.ReadIndexedObjects().States) { EngineVersion = game.Version };
    runtime.GlobalVariables[ScummVars.V2.Ego] = 3;
    runtime.GlobalVariables[ScummVars.V2.ActiveObject1] = (ushort)id;
    runtime.GlobalVariables["var[0x07B]"] = (ushort)(id == 35 ? 35 : 0);
    var result = new ScummBoundedScriptExecutor().Execute(data.GlobalScript(116)!, runtime.ObjectStates, data.ReadRoom(26).Objects, runtime: runtime, startOffset: 0x39);
    Console.WriteLine($"NATIVE helper116 from0039 target {id}: picked={runtime.Inventory.Contains(id)} unresolved={result.UnresolvedReason ?? "none"} stop={result.ReachedStop}");
}
