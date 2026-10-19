using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;
foreach (var folder in new[]{"zak ega en","zakE ega en"})
{
    var game=new GameFolderScanner().Scan(Path.Combine(@"D:\Juegos\Lucas",folder));
    var data=ScummDataSources.For(game);var session=ScummGameSession.BootForPlay(game,out _);
    var status=session.CaptureInventoryStatus();
    Console.WriteLine($"EDITION {folder}; catalog {status.Catalog.Count}; characters {string.Join(';',status.Characters.Select(x=>$"{x.ActorId} {x.Name}"))}");
    var groups=status.Catalog.GroupBy(x=>x.Name).Where(g=>g.Count()>1).ToArray();
    foreach(var group in groups) Console.WriteLine($"GROUP {group.Key}: {string.Join(',',group.Select(x=>x.ObjectId))}");
    foreach(var item in status.Catalog.Where(x=>x.Context is not null))Console.WriteLine($"CONTEXT {item.ObjectId} {item.Name}: {item.Context!.ActorId} {item.Context.Text}; {item.Context.Evidence}");
    var targets=groups.SelectMany(g=>g.Select(x=>$"0x{x.ObjectId:X4}")).ToHashSet();
    var scripts=new List<ScummScriptDisassembly>();
    scripts.AddRange(data.GlobalScriptIds().Select(data.GlobalScript).OfType<ScummScriptDisassembly>());
    foreach(var rid in game.Rooms.Where(x=>x.ResourceId.HasValue).Select(x=>x.ResourceId!.Value).Distinct())
    {
        if(data.RoomEntryScript(rid) is{} entry)scripts.Add(entry);
        if(data.RoomExitScript(rid) is{} exit)scripts.Add(exit);
        var room=data.ReadRoom(rid);var tables=data.ReadObjectVerbs(rid,room.Objects);
        foreach(var obj in room.Objects.GroupBy(x=>x.Id).Select(x=>x.Last()))
            foreach(var verb in tables.GetValueOrDefault(obj.Id)??[])
                scripts.Add(data.ReadObjectVerb(rid,obj,verb.VerbId,out _));
    }
    foreach(var script in scripts)
        foreach(var i in script.Instructions.Where(i=>i.Operation=="setOwnerOf"&&targets.Contains(i.Operands.Split(',')[0])))
            Console.WriteLine($"OWNER {script.Context??$"Script {script.ScriptId}"} +{i.Offset:X4}: {i.Operands}");
    foreach(var sid in new[]{81,145})
    {
        Console.WriteLine($"GLOBAL {sid}");
        foreach(var i in data.GlobalScript(sid)!.Instructions)Console.WriteLine($" +{i.Offset:X4} {i.Operation} {i.Operands}");
    }
}
