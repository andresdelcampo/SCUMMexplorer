using SCUMMexplorer.Core.Rendering;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

var game=new GameFolderScanner().Scan(args[0]);
var objects=new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0],"00.LFL"));
var cases=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
Directory.CreateDirectory(args[2]);
object? Prop(object o,string name)=>o.GetType().GetProperty(name)!.GetValue(o);
foreach(var route in cases.EnumerateArray())
{
    var session=new LoomExplorationSession(game,objects);var snapshots=new List<object>();var actions=new List<object>();
    int trace=session.SessionTrace.Count; bool missingRoom=false;
    void Record(string stage,int frame)
    {
        var pending=((IEnumerable)typeof(LoomExplorationSession).GetField("_pending",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!).Cast<object>()
            .Select(p=>new{label=Prop(Prop(p,"Frame")!,"Label"),number=Prop(p,"ScriptNumber"),room=Prop(p,"RoomId"),offset=Prop(p,"Offset"),
                freeze=Prop(Prop(p,"Frame")!,"FreezeCount"),delay=Prop(Prop(p,"Frame")!,"Delay"),locals=Prop(p,"Locals")}).ToArray();
        snapshots.Add(new{stage,frame,session.InCutscene,session.EgoMoving,session.CameraMoving,session.CutscenesBegun,
            snapshot=session.Snapshot,actors=session.Actors,pending});
    }
    void Frames(string stage,int count,bool settle)
    {
        int quiet=0;
        for(int f=0;f<count;f++)
        {
            if(f is 0 or 20 or 100 or 300 or 1000 or 2500)Record(stage,f);
            if(settle&&!session.InCutscene&&!session.EgoMoving&&!session.CameraMoving&&session.Messages.HaveMsg==0)
            {if(++quiet>=32){Record(stage,f);return;}}else quiet=0;
            session.AdvanceFrame();
        }
        Record(stage,count);
    }
    foreach(var step in route.GetProperty("actions").EnumerateArray())
    {
        string kind=step.GetProperty("kind").GetString()!;
        switch(kind)
        {
            case "require-room":
                if(session.Snapshot.RoomId != step.GetProperty("room").GetInt32()) {
                    actions.Add(new { prerequisiteNotReached=step.GetProperty("room").GetInt32(), actualRoom=session.Snapshot.RoomId }); missingRoom=true;
                } break;
            case "visit": session.VisitRoom(game.Rooms.Single(r=>r.ResourceId==step.GetProperty("room").GetInt32()));break;
            case "verb": actions.Add(session.ActivateObjectVerb(step.GetProperty("objectId").GetInt32(),step.GetProperty("verb").GetInt32()));break;
            case "click": actions.Add(session.Click(step.GetProperty("x").GetInt32(),step.GetProperty("y").GetInt32()));break;
            case "look":
                var looked = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r=>r.ResourceId==session.Snapshot.RoomId).FilePath)).Single(o=>o.Id==step.GetProperty("objectId").GetInt32());
                actions.Add(session.Click(looked.X+looked.Width/2,looked.Y+looked.Height/2,true));break;
            case "inspect":
                actions.Add(new { directory=new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r=>r.ResourceId==session.Snapshot.RoomId).FilePath)), status=session.GameStatus });break;
            case "target":
                var record = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r=>r.ResourceId==session.Snapshot.RoomId).FilePath)).Single(o=>o.Id==step.GetProperty("objectId").GetInt32());
                actions.Add(session.Click(record.X+record.Width/2,record.Y+record.Height/2));break;
            case "draft":
                int slot=step.GetProperty("slot").GetInt32();
                ushort packed=session.Snapshot.KnownGlobalVariables[$"var[0x{0x32+2*slot:X3}]"];
                for(int i=0;i<4;i++) {
                    int id=((packed>>(3*i))&7)+1;
                    var note=session.VerbArea().Items.Single(v=>v.VerbId==id);
                    actions.Add(session.ClickVerbArea(note.X+1,note.Y+1));
                    for(int f=0;f<step.GetProperty("gap").GetInt32();f++)session.AdvanceFrame();
                }
                break;
            case "frames": Frames(step.GetProperty("stage").GetString()!,step.GetProperty("count").GetInt32(),step.GetProperty("settle").GetBoolean());break;
            default: throw new InvalidDataException("Unknown diagnostic action "+kind);
        }
        if(missingRoom) break;
    }
    Record("final",0);
    var boundaries=session.SessionTrace.Skip(trace).Where(t=>t.StartsWith("cutscene at ")||t.StartsWith("endCutscene at ")).ToArray();
    File.WriteAllText(Path.Combine(args[2],route.GetProperty("id").GetString()+".json"),JsonSerializer.Serialize(new{route=route.Clone(),actions,snapshots,boundaries,trace=session.SessionTrace.Skip(trace),uncertainties=session.Snapshot.Uncertainties},new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine($"{route.GetProperty("id")}: room {session.Snapshot.RoomId}, scene {session.InCutscene}, {boundaries.Count(t=>t.StartsWith("cutscene at "))}/{boundaries.Count(t=>t.StartsWith("endCutscene at "))} scopes");
}
