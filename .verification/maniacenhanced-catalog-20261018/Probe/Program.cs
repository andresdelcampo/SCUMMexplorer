using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
void Dump(ScummScriptDisassembly? script)
{
    if (script is null) return;
    foreach (var op in script.Instructions)
        Console.WriteLine($"{script.Context ?? $"Script {script.ScriptId}"} +0x{op.Offset:X4}: {op.Operation} {op.Operands}");
}
if (args.ElementAtOrDefault(1) == "script") { Dump(data.GlobalScript(int.Parse(args[2]))); return; }
if (args.ElementAtOrDefault(1) == "object")
{
    var room = int.Parse(args[2]); var id = int.Parse(args[3]);
    var directory = data.ReadRoom(room);
    foreach (var record in directory.Objects.Where(o => o.Id == id))
        foreach (var verb in data.ReadObjectVerbs(room, directory.Objects).GetValueOrDefault(id) ?? [])
        { Console.WriteLine($"VERB {verb.VerbId}"); Dump(data.ReadObjectVerb(room, record, verb.VerbId, out _)); }
    return;
}
var session = ScummGameSession.BootForPlay(game, out _);
ManiacCutsceneSetup.PrepareIfNeeded(game, session);
for (var frame = 0; frame < 2500 && !session.CaptureInventoryStatus().CanEdit; frame++) session.AdvanceFrame();
var status = session.CaptureInventoryStatus();
var indexed = ((ScummV2DataSource)data).ReadIndexedObjects();
Console.WriteLine($"CATALOG {status.Catalog.Count}; capacity {status.Capacity}; characters {string.Join(',', status.Characters.Select(c => c.ActorId))}");
foreach (var item in status.Catalog) Console.WriteLine($"ITEM {item.ObjectId}; room {item.SourceRoomId}; indexed {indexed.States.GetValueOrDefault(item.ObjectId)}; {item.Name}; {item.Evidence}");
foreach (var note in session.InventoryCatalogNotes) Console.WriteLine("AUDIT " + note);
var blockCount = 0;
var incompleteCount = 0;
void Sites(ScummScriptDisassembly? script)
{
    if (script is null) return;
    blockCount++;
    if (script.DecodeNote is not null || script.Issues is { Count: > 0 })
    {
        incompleteCount++;
        Console.WriteLine($"DECODE {script.Context ?? $"Script {script.ScriptId}"}: {script.DecodeNote}; {string.Join(';', (script.Issues ?? []).Select(i => $"{i.Offset:X4} {i.Reason}"))}");
    }
    if (args.ElementAtOrDefault(1) == "calls")
    {
        for (var i = 0; i < script.Instructions.Count; i++)
        {
            var op = script.Instructions[i];
            if (op.Operation is not ("startScript" or "chainScript") || op.Operands != "0x74" && !op.Operands.StartsWith("var[")) continue;
            foreach (var item in script.Instructions.Skip(Math.Max(0, i - 4)).Take(Math.Min(5, i + 1)))
                Console.WriteLine($"CALL {script.Context ?? $"Script {script.ScriptId}"} +0x{item.Offset:X4}: {item.Operation} {item.Operands}");
        }
        return;
    }
    foreach (var op in script.Instructions.Where(i => i.Operation == "pickupObject" || i.Operation == "setState" && i.Operands.Contains("pickupable", StringComparison.OrdinalIgnoreCase)))
        Console.WriteLine($"SITE {script.Context ?? $"Script {script.ScriptId}"} +0x{op.Offset:X4}: {op.Operation} {op.Operands}");
}
foreach (var id in data.GlobalScriptIds()) Sites(data.GlobalScript(id));
foreach (var room in game.Rooms.Select(r => r.ResourceId).OfType<int>())
{
    var directory = data.ReadRoom(room);
    Sites(data.RoomEntryScript(room)); Sites(data.RoomExitScript(room));
    foreach (var id in data.RoomLocalScriptIds(room)) Sites(data.RoomLocalScript(room, id));
    var verbs = data.ReadObjectVerbs(room, directory.Objects);
    foreach (var record in directory.Objects.GroupBy(o => o.Id).Select(g => g.Last()))
        foreach (var verb in verbs.GetValueOrDefault(record.Id) ?? [])
            Sites(data.ReadObjectVerb(room, record, verb.VerbId, out _));
}
Console.WriteLine($"BLOCKS {blockCount}; incomplete {incompleteCount}");
