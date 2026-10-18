using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;
foreach (var folder in new[]{"zak ega en","zakE ega en"})
{
    var game = new GameFolderScanner().Scan(Path.Combine(@"D:\Juegos\Lucas",folder));
    var data=ScummDataSources.For(game);
    var session=ScummGameSession.BootForPlay(game,out _);
    for(var f=0; f<2500 && !session.CaptureInventoryStatus().CanEdit;f++) session.AdvanceFrame();
    var catalog=session.CaptureInventoryStatus().Catalog;
    Console.WriteLine($"EDITION {folder}: {catalog.Count}");
    var runtime=(ScummScriptRuntimeState)typeof(ScummSession).GetField("_runtime",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(session)!;
    Console.WriteLine($"BOOT INVENTORY {string.Join(',',runtime.InventorySlots.Where(x=>x!=0))}");
    var indexed=((ScummV2DataSource)data).ReadIndexedObjects();
    foreach(var id in new[]{18,32,75,33,56,61,62,63,65,66,70,535,536,537,73,471,55,431,81,429,82,83})
        Console.WriteLine($"INDEX {id}: owner {indexed.Owners.GetValueOrDefault(id)} state {indexed.States.GetValueOrDefault(id)}");
    foreach(var group in catalog.GroupBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1))
    {
        Console.WriteLine($"GROUP {group.Key}: {group.Count()}");
        foreach(var item in group)
        {
            var room=data.ReadRoom(item.SourceRoomId);
            var obj=room.Objects.Last(o=>o.Id==item.ObjectId);
            var verbs=data.ReadObjectVerbs(item.SourceRoomId,room.Objects).GetValueOrDefault(item.ObjectId)??[];
            Console.WriteLine($" ITEM {item.ObjectId} room {item.SourceRoomId}; verbs {string.Join(',',verbs.Select(v=>v.VerbId))}");
            foreach(var verb in verbs)
            {
                var script=data.ReadObjectVerb(item.SourceRoomId,obj,verb.VerbId,out _);
                Console.WriteLine($" VERB {verb.VerbId} entry +{script.EntryOffset:X4}");
                foreach(var i in script.Instructions)Console.WriteLine($"  +{i.Offset:X4} {i.Operation} {i.Operands}");
            }
        }
    }
    if(args.Contains("helpers"))
    {
        foreach(var id in new[]{1,2,3,53,58,71,83,119,145,147})
        {
            Console.WriteLine($"GLOBAL {id}");
            foreach(var i in data.GlobalScript(id)!.Instructions)Console.WriteLine($" +{i.Offset:X4} {i.Operation} {i.Operands}");
        }
        foreach(var (rid,oid,verb) in new[]{(28,470,13),(14,298,253),(14,307,253),(30,491,33)})
        {
            var room=data.ReadRoom(rid);var obj=room.Objects.Last(x=>x.Id==oid);
            Console.WriteLine($"EXTRA {rid}/{oid}/{verb}");
            var entries=data.ReadObjectVerbs(rid,room.Objects).GetValueOrDefault(oid)??[];
            if(!entries.Any(v=>v.VerbId==verb||v.VerbId==255)){Console.WriteLine(" NO NATIVE ENTRY");continue;}
            var script=data.ReadObjectVerb(rid,obj,verb,out _);
            foreach(var i in script.Instructions)Console.WriteLine($" +{i.Offset:X4} {i.Operation} {i.Operands}");
        }
        foreach(var roomId in new[]{26,27,30,46})
        {
            Console.WriteLine($"ROOM {roomId} ENTRY");
            foreach(var i in data.RoomEntryScript(roomId)?.Instructions??[])Console.WriteLine($" +{i.Offset:X4} {i.Operation} {i.Operands}");
        }
    }
}
