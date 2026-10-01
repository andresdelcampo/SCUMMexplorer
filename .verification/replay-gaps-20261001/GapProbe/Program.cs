using System.Collections;
using System.Reflection;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

var game=new GameFolderScanner().Scan(args[0]);
var objects=new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0],"00.LFL"));
var audit=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
Directory.CreateDirectory(args[2]);
var results=new List<object>();
object? Prop(object o,string n)=>o.GetType().GetProperty(n)!.GetValue(o);
object DescribePending(object pending)
{
    var script=(LoomScriptDisassembly)Prop(pending,"Script")!;int offset=(int)Prop(pending,"Offset")!;var frame=Prop(pending,"Frame")!;
    return new{label=Prop(frame,"Label"),number=Prop(pending,"ScriptNumber"),room=Prop(pending,"RoomId"),offset=$"+0x{offset:X4}",
        next=script.Instructions.FirstOrDefault(i=>i.Offset==offset),freeze=Prop(frame,"FreezeCount"),delay=Prop(frame,"Delay"),locals=Prop(pending,"Locals")};
}
foreach(var root in audit.GetProperty("results").EnumerateArray().Where(r=>r.GetProperty("status").GetString()!.StartsWith("Incomplete:")))
{
    var s=new LoomExplorationSession(game,objects);int room=root.GetProperty("room").GetInt32(),obj=root.GetProperty("objectId").GetInt32(),verb=root.GetProperty("verb").GetInt32();
    s.VisitRoom(game.Rooms.Single(r=>r.ResourceId==room));int trace=s.SessionTrace.Count,baseline=s.CutscenesBegun;
    if(obj!=0)s.ActivateObjectVerb(obj,verb);
    var checkpoints=new List<object>();
    for(int f=0;f<=3000;f++)
    {
        if(f is 0 or 100 or 1000 or 2500 or 3000)
        {
            var pending=((IEnumerable)typeof(LoomExplorationSession).GetField("_pending",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(s)!).Cast<object>().Select(DescribePending).ToArray();
            checkpoints.Add(new{frame=f,room=s.Snapshot.RoomId,s.InCutscene,s.EgoMoving,s.CameraMoving,begun=s.CutscenesBegun-baseline,pending,
                actors=s.Actors.Where(a=>a.Room==s.Snapshot.RoomId).Select(a=>new{a.Number,a.X,a.Y,a.WalkBox,a.Moving,a.IgnoreBoxes}),sounds=s.Snapshot.ActiveSounds,variables=s.Snapshot.KnownGlobalVariables});
        }
        if(f<3000)s.AdvanceFrame();
    }
    var result=new{owner=root.GetProperty("owner").GetString(),room,obj,verb,checkpoints,
        sceneTrace=s.SessionTrace.Skip(trace).Where(t=>t.StartsWith("cutscene at ")||t.StartsWith("endCutscene at ")),uncertainties=s.Snapshot.Uncertainties,lastTrace=s.SessionTrace.TakeLast(40)};
    results.Add(result);Console.WriteLine($"{results.Count}: {root.GetProperty("owner")}: room {s.Snapshot.RoomId}, scene {s.InCutscene}, ego {s.EgoMoving}");
    File.WriteAllText(Path.Combine(args[2],$"gap-{results.Count:00}.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
}
File.WriteAllText(Path.Combine(args[2],"gaps.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
