using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// Own-edition source/route diagnostic. Visits and activate commands are explicit exploration preparation.
var folder = args[0];
var game = new GameFolderScanner().Scan(folder);
var data = ScummDataSources.For(game);
if (args.Length > 2 && args[1] == "route")
{
    var observer = new ScummScriptObserver(o => Console.WriteLine($"OBS {o.Site?.Context ?? $"Script {o.Site?.Script}"} +{o.Site?.Offset:X4} {o.Site?.Operation} {o.Site?.Operands} end={o.ReachedStop} why={o.UnresolvedReason} vars={string.Join(';',o.Values.Where(v=>v.Key is "var[0x000]" or "var[0x009]" or "var[0x02C]" or "var[0x02D]" or "var[0x027]" or "var[0x028]"))}"),
        site=>site.Script==79 || site.Context?.Contains("Object 256",StringComparison.Ordinal)==true || site.Operation=="loadRoomWithEgo");
    var s = new ScummGameSession(game,((ScummV2DataSource)data).ReadIndexedObjects(),observer:observer,data:data);
    foreach (var line in File.ReadAllLines(args[2]).Where(l => l.Length > 0 && !l.StartsWith('#')))
    {
        Console.WriteLine("STEP " + line);
        var p=line.Split(','); int N(int at)=>int.Parse(p[at]);
        switch(p[0])
        {
            case "continue":
                var watch=System.Diagnostics.Stopwatch.StartNew();
                for(int i=0;i<N(1);i++)
                {
                    s.AdvanceFrame();
                    if(i%1000==0 || s.CurrentRoomId!=11)
                        Console.WriteLine($"CONTINUE frame={i} elapsed={watch.Elapsed} room={s.CurrentRoomId} logical={s.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x004]")} input={s.UserInputOn} cut={s.InCutscene} pending={string.Join(';',s.PendingScriptSummary)}");
                    if(i==6000 || s.CurrentRoomId!=11)
                    {
                        Console.WriteLine(s.CurrentRoomId!=11 ? "FLIGHT ROOM CHANGE" : "OBSERVATION BOUND");
                        for(int settle=0;settle<2000;settle++)s.AdvanceFrame();
                        break;
                    }
                }
                break;
            case "frames": for(int i=0;i<N(1);i++)s.AdvanceFrame();break;
            case "skip": for(int i=0;i<1200&&!s.CutsceneSkippable;i++)s.AdvanceFrame();s.PressKey(27);for(int i=0;i<1200&&s.InCutscene;i++)s.AdvanceFrame();break;
            case "visit": s.VisitRoom(game.Rooms.Single(r=>r.ResourceId==N(1)));break;
            case "activate": s.ActivateObjectVerb(N(1),N(2));break;
            case "key": s.PressPlayKey(N(1),out _);break;
            case "click": s.ClickScreen(N(1),N(2),out _);break;
            case "roomclick": s.ClickScreen(N(1)-s.ViewLeft,N(2)+s.MainScreen.Top,out _);break;
            case "objectclick":
                var exit=data.ReadRoom(s.CurrentRoomId!.Value).Objects.Last(o=>o.Id==N(1));
                Console.WriteLine($"NATIVE OBJECT CLICK {exit}");
                if(s.ClickScreen(exit.X+exit.Width/2-s.ViewLeft,exit.Y+exit.Height/2+s.MainScreen.Top,out var refused) is null)throw new Exception(refused);
                break;
            case "assertOwner": if(s.Snapshot.ObjectOwners.GetValueOrDefault(N(1))!=N(2))throw new Exception(line);break;
            case "assertWord": if(s.Snapshot.KnownGlobalVariables.GetValueOrDefault($"var[0x{N(1):X3}]")!=N(2))throw new Exception(line);break;
            case "assertRoom": if(s.CurrentRoomId!=N(1))throw new Exception(line);break;
            case "assertControl": if(!s.UserInputOn||s.InCutscene)throw new Exception(line);break;
            case "verb":
                var v=s.PlayVerbArea().Items.Single(v=>v.VerbId==N(1)&&v.Mode==1);
                s.ClickScreen(v.X+4,v.Y+3,out _);break;
            case "inventory":
                var inventory=s.CaptureInventoryStatus();
                var items=inventory.Items.Where(i=>i.OwnerId==inventory.CurrentActorId).OrderBy(i=>i.Slot).ToArray();
                var ix=Array.FindIndex(items,i=>i.ObjectId==N(1));
                if(ix<0||ix>=4)throw new Exception("Inventory item not displayed "+N(1));
                s.ClickScreen((ix%2)*176+8,s.MainScreen.Bottom+36+(ix/2)*8,out _);break;
        }
        var snap=s.Snapshot;
        Console.WriteLine($"STATE room={s.CurrentRoomId} cut={s.InCutscene} input={s.UserInputOn} camera={s.ViewLeft} sentence={ScummTextDecoder.DecodeGlyphs(s.PlaySentenceGlyphs())}");
        Console.WriteLine("OWNERS "+string.Join(',',new[]{18,26,51,82,83}.Select(id=>$"{id}={snap.ObjectOwners.GetValueOrDefault(id)}")));
        Console.WriteLine("INVENTORY "+JsonSerializer.Serialize(s.CaptureInventoryStatus().Items));
        Console.WriteLine("VARS "+string.Join(',',new[]{0,4,62,68,73,78,79,82,120,121,122,155}.Select(id=>$"{id}={snap.KnownGlobalVariables.GetValueOrDefault($"var[0x{id:X3}]")}")));
        Console.WriteLine("MESSAGES "+string.Join(';',s.Messages.OnScreen.Select(m=>m.Text)));
        Console.WriteLine("ACTORS "+JsonSerializer.Serialize(s.Actors.Where(a=>a.Room==s.CurrentRoomId).Select(a=>new{a.Number,a.X,a.Y,a.Moving,a.WalkBox,a.Unknown})));
    }
    Console.WriteLine("PENDING "+string.Join(';',s.PendingScriptSummary));
    Console.WriteLine("STATES "+string.Join(',',new[]{142,143,144,150}.Select(id=>$"{id}={s.Snapshot.ObjectStates.GetValueOrDefault(id)}")));
    foreach(var t in s.SessionTrace.TakeLast(100)) Console.WriteLine("TRACE "+t);
    foreach(var u in s.Snapshot.Uncertainties.Where(u=>u.Contains("unsupported")))Console.WriteLine("UNSUPPORTED "+u);
    return;
}
if (args.Length > 2 && args[1] == "room")
{
    var id = int.Parse(args[2]); var room = data.ReadRoom(id);
    foreach (var obj in room.Objects)
    {
        Console.WriteLine($"OBJECT {obj.Id} {ScummTextDecoder.DecodeGlyphs(room.ObjectNames.GetValueOrDefault(obj.Id) ?? [])} {JsonSerializer.Serialize(obj)}");
        foreach (var verb in data.ReadObjectVerbs(id, [obj]).GetValueOrDefault(obj.Id) ?? [])
        {
            Console.WriteLine($"VERB {verb.VerbId}");
            foreach (var i in data.ReadObjectVerb(id,obj,verb.VerbId,out _).Instructions) Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");
        }
    }
    return;
}
if (args.Length > 4 && args[1] == "object")
{
    var room = int.Parse(args[2]);
    var id = int.Parse(args[3]);
    var verb = int.Parse(args[4]);
    var script = data.ReadObjectVerb(room,data.ReadRoom(room).Objects.Last(r=>r.Id==id),verb,out _);
    foreach(var i in script.Instructions) Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");
    return;
}
if (args.Length > 2 && args[1] == "find")
{
    void Print(string label, ScummScriptDisassembly? script)
    {
        if (script is null) return;
        foreach (var i in script.Instructions.Where(i => i.Operands.Contains(args[2], StringComparison.OrdinalIgnoreCase)))
            Console.WriteLine($"{label} +{i.Offset:X4} {i.Operation} {i.Operands}");
    }
    foreach (var r in game.Resources.Where(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue)) Print($"Script {r.ResourceId}", data.GlobalScript(r.ResourceId!.Value));
    foreach (var r in game.Rooms)
    {
        var id = r.ResourceId!.Value;
        var read = data.ReadRoom(id);
        Print($"Room {id} entry",data.RoomEntryScript(id));
        Print($"Room {id} exit",data.RoomExitScript(id));
        foreach(var record in read.Objects.DistinctBy(o=>o.Id))
            foreach(var verb in data.ReadObjectVerbs(id,[record]).GetValueOrDefault(record.Id) ?? [])
                Print($"Room {id} object {record.Id} verb {verb.VerbId}",data.ReadObjectVerb(id,record,verb.VerbId,out _));
    }
    return;
}
if (args.Length > 1 && args[1] == "dump")
{
    foreach (var id in args.Skip(2).Select(int.Parse))
    {
        Console.WriteLine($"Script {id}");
        foreach (var i in data.GlobalScript(id)!.Instructions) Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");
    }
    return;
}
