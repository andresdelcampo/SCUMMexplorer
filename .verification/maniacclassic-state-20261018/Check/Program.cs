using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

var folder = args.FirstOrDefault() ?? @"D:\Juegos\Lucas\mmansion ega en";
var game = new GameFolderScanner().Scan(folder);
var data = ScummDataSources.For(game);
if(args.ElementAtOrDefault(1) == "route")
{
    var session = SCUMMexplorer.Tests.ManiacClassicStateRoute.AcquireKey(folder);
    Console.WriteLine($"Native key route: room={session.CurrentRoomId}, owner55={session.Snapshot.ObjectOwners[55]}, control={session.PlayerHasControl}");
    foreach(var key in new[]{"var[0x02F]","var[0x030]","var[0x031]"}) Console.WriteLine($"{key}={session.Snapshot.KnownGlobalVariables[key]}");
    var before=session.CaptureInventoryStatus();
    foreach(var actor in before.Characters) Console.WriteLine($"Eligible actor {actor.ActorId}: {actor.Name}; {actor.Evidence}");
    foreach(var item in before.Items) Console.WriteLine($"Held object {item.ObjectId}: {item.Name}; owner={item.OwnerId}; slot={item.Slot}; room={item.RetainedRoomId}");
    var target=before.Characters.Single(actor=>actor.Name=="Syd").ActorId;
    var invalid=session.ApplyInventoryTransfers(before,[new(54,target)]);
    Console.WriteLine($"Room item reassignment: applied={invalid.Applied}; refusal={invalid.Refusal}");
    var result=session.ApplyInventoryTransfers(before,[new(55,target)]);
    Console.WriteLine($"Apply held key: applied={result.Applied}; owner={session.Snapshot.ObjectOwners[55]}; refusal={result.Refusal}");
    if(!result.Applied || result.Undo is null) throw new InvalidOperationException(result.Refusal);
    var stale=session.ApplyInventoryTransfers(before,[new(55,5)]);
    Console.WriteLine($"Stale snapshot: applied={stale.Applied}; refusal={stale.Refusal}");
    var undo=session.UndoInventoryTransfers(result.Undo);
    Console.WriteLine($"Undo: applied={undo.Applied}; owner={session.Snapshot.ObjectOwners[55]}; refusal={undo.Refusal}");
    if(!undo.Applied || session.Snapshot.ObjectOwners[55]!=3 || invalid.Applied || stale.Applied) throw new InvalidOperationException("State round trip/guard failed.");
    Console.WriteLine("PASS: explicit choice, not a played scene; native acquisition precedes the edit.");
    return;
}
foreach(var id in data.GlobalScriptIds())
{
    var script = data.GlobalScript(id);
    if(script is null) continue;
    var hits = script.Instructions.Where(i => i.Operands.Contains("var[0x030]") || i.Operands.Contains("var[0x031]") || i.Operands.Contains("var[0x02F]") || (id == 1 && i.Operation == "actorOps") || (id == 4 && i.Offset >= 0xB0 && i.Offset <= 0x130) || id == 114 || id == 146).ToArray();
    if(hits.Length == 0) continue;
    Console.WriteLine($"Script {id}");
    foreach(var i in hits) Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");
}
foreach(var id in new[]{260,263,403})
{
    var obj=data.ReadRoom(45).Objects.Single(o=>o.Id==id);
    var script=data.ReadObjectVerb(45,obj,0xFF,out _);
    Console.WriteLine($"Room45 object{id} fallback");
    foreach(var i in script.Instructions) Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");
}
