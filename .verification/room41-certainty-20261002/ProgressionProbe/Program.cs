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
    var session=new LoomExplorationSession(game,objects,stopAtBootInput:route.TryGetProperty("nativeBoot",out var nativeBoot)&&nativeBoot.GetBoolean());var snapshots=new List<object>();var actions=new List<object>();
    int elapsed=0; var roomChanges=new List<object>(); int? observedRoom=session.Snapshot.RoomId; int trace=session.SessionTrace.Count; bool missingRoom=false;
    void Record(string stage,int frame)
    {
        var pending=((IEnumerable)typeof(LoomExplorationSession).GetField("_pending",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!).Cast<object>()
            .Select(p=>new{label=Prop(Prop(p,"Frame")!,"Label"),number=Prop(p,"ScriptNumber"),room=Prop(p,"RoomId"),offset=Prop(p,"Offset"),
                freeze=Prop(Prop(p,"Frame")!,"FreezeCount"),delay=Prop(Prop(p,"Frame")!,"Delay"),locals=Prop(p,"Locals"),pathEstimated=Prop(p,"PathEstimated"),estimatedLocals=Prop(p,"EstimatedLocals")}).ToArray();
        var runtime=typeof(LoomExplorationSession).GetField("_runtime",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!;
        var estimatedVariables=((IEnumerable)Prop(runtime,"EstimatedVariables")!).Cast<string>().Order().ToArray();
        snapshots.Add(new{stage,frame,eventCount=((ICollection)typeof(LoomExplorationSession).GetField("_visitEvents",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!).Count,estimatedVariables,session.InCutscene,session.EgoMoving,session.CameraMoving,session.CutscenesBegun,
            snapshot=session.Snapshot,status=session.GameStatus,actors=session.Actors,pending});
    }
    void Advance()
    {
        session.AdvanceFrame(); elapsed++;
        if(observedRoom!=session.Snapshot.RoomId) {observedRoom=session.Snapshot.RoomId;roomChanges.Add(new {elapsed,room=observedRoom});}
    }
    void Frames(string stage,int count,bool settle)
    {
        int quiet=0;
        for(int f=0;f<count;f++)
        {
            if(f is 0 or 20 or 100 or 300 or 1000 or 2500)Record(stage,f);
            if(settle&&(!session.Snapshot.KnownGlobalVariables.TryGetValue("bit[0xD66]",out var activeDraft)||activeDraft==0)&&session.SessionTrace.Skip(trace).Count(t=>t.StartsWith("cutscene at "))==session.SessionTrace.Skip(trace).Count(t=>t.StartsWith("endCutscene at "))&&!session.InCutscene&&!session.EgoMoving&&!session.CameraMoving&&session.Messages.HaveMsg==0)
            {if(++quiet>=32){Record(stage,f);return;}}else quiet=0;
            Advance();
        }
        Record(stage,count);
    }
    try {
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
                Record($"draft-{slot}-before",0);
                ushort packed=session.Snapshot.KnownGlobalVariables[$"var[0x{0x32+2*slot:X3}]"];
                for(int i=0;i<4;i++) {
                    int id=((packed>>(3*i))&7)+1;
                    var note=session.VerbArea().Items.Single(v=>v.VerbId==id);
                    actions.Add(session.ClickVerbArea(note.X+1,note.Y+1));
                    Record($"draft-{slot}-click-note-{i+1}",0);
                    for(int f=0;f<step.GetProperty("gap").GetInt32();f++)Advance();
                }
                break;
            case "boot-answer":
                var answer=Enumerable.Range(0,4).Select(i=>(int)session.Snapshot.KnownGlobalVariables[$"var[0x{0x52+i:X3}]"]).ToArray();
                if(answer.Any(n=>n<0||n>11)) throw new InvalidDataException("Source boot answer has not been generated.");
                actions.Add(new {sourceBootAnswer=answer});
                foreach(int id in answer) {
                    var glyph=new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r=>r.ResourceId==69).FilePath)).Single(o=>o.Id==0x376+id);
                    actions.Add(session.Click(glyph.X+glyph.Width/2,glyph.Y+glyph.Height/2));
                    for(int f=0;f<10;f++)Advance();
                } break;
            case "walk-point":
                var walked = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r=>r.ResourceId==session.Snapshot.RoomId).FilePath)).Single(o=>o.Id==step.GetProperty("objectId").GetInt32());
                actions.Add(session.Click(walked.WalkX,walked.WalkY));break;
            case "until-touchable":
                int targetId=step.GetProperty("objectId").GetInt32();
                int timeout=step.GetProperty("count").GetInt32();
                int advanced=0;
                while(advanced<timeout && session.Snapshot.IsUntouchable(targetId)) {Advance();advanced++;}
                Record(step.GetProperty("stage").GetString()!,advanced);
                if(session.Snapshot.IsUntouchable(targetId)) {actions.Add(new {missingTouchable=targetId});missingRoom=true;}
                break;
            case "key": actions.Add(session.PressKey(step.GetProperty("key").GetInt32()));break;
            case "draft-key":
                int keySlot=step.GetProperty("slot").GetInt32();
                Record($"draft-{keySlot}-before",0);
                ushort keyPacked=session.Snapshot.KnownGlobalVariables[$"var[0x{0x32+2*keySlot:X3}]"];
                for(int i=0;i<4;i++) {
                    int noteIndex=step.TryGetProperty("reverse",out var reverse)&&reverse.GetBoolean()?3-i:i;
                    int noteId=((keyPacked>>(3*noteIndex))&7)+1;
                    actions.Add(session.PressKey("qwertyui"[noteId-1]));
                    Record($"draft-{keySlot}-key-note-{i+1}",0);
                    for(int f=0;f<step.GetProperty("gap").GetInt32();f++) {
                        Advance();
                        if(keySlot==2 && route.TryGetProperty("recordDraftGaps",out var recordGaps)&&recordGaps.GetBoolean())
                            Record($"draft-{keySlot}-gap-{i+1}-{f+1}",0);
                    }
                } break;
            case "until-override":
                string owner=step.GetProperty("owner").GetString()!;
                bool HasOverride()=>((IDictionary)typeof(LoomExplorationSession).GetField("_overrides",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!).Values.Cast<object>().Any(v=>
                    (string?)Prop(v.GetType().GetField("Item1")!.GetValue(v)!,"Label")==owner);
                int overrideWait=0;
                while(overrideWait<step.GetProperty("count").GetInt32()&&!HasOverride()) {Advance();overrideWait++;}
                Record(step.GetProperty("stage").GetString()!,overrideWait);
                if(!HasOverride()) {actions.Add(new {missingOverride=owner});missingRoom=true;}
                break;
            case "until-script":
                int scriptId=step.GetProperty("script").GetInt32();
                int waited=0;
                while(waited<step.GetProperty("count").GetInt32() && !((IEnumerable)typeof(LoomExplorationSession).GetField("_pending",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!).Cast<object>().Any(p=>(int?)Prop(p,"ScriptNumber")==scriptId)) {Advance();waited++;}
                Record(step.GetProperty("stage").GetString()!,waited);
                if(!((IEnumerable)typeof(LoomExplorationSession).GetField("_pending",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!).Cast<object>().Any(p=>(int?)Prop(p,"ScriptNumber")==scriptId)) {actions.Add(new {missingScript=scriptId});missingRoom=true;}
                break;
            case "frames": Frames(step.GetProperty("stage").GetString()!,step.GetProperty("count").GetInt32(),step.GetProperty("settle").GetBoolean());break;
            default: throw new InvalidDataException("Unknown diagnostic action "+kind);
        }
        if(missingRoom) break;
    }
    } catch(Exception failure) {
        Record("failure",0);
        File.WriteAllText(Path.Combine(args[2],route.GetProperty("id").GetString()+"-failure.json"),JsonSerializer.Serialize(new {route=route.Clone(),actions,snapshots,elapsed,roomChanges,boundaries=session.SessionTrace.Skip(trace).Where(t=>t.StartsWith("cutscene at ")||t.StartsWith("endCutscene at ")).ToArray(),trace=session.SessionTrace.Skip(trace),uncertainties=session.Snapshot.Uncertainties,failure=failure.ToString()},new JsonSerializerOptions {WriteIndented=true}));
        throw;
    }
    Record("final",0);
    var boundaries=session.SessionTrace.Skip(trace).Where(t=>t.StartsWith("cutscene at ")||t.StartsWith("endCutscene at ")).ToArray();
    File.WriteAllText(Path.Combine(args[2],route.GetProperty("id").GetString()+".json"),JsonSerializer.Serialize(new{route=route.Clone(),actions,snapshots,elapsed,roomChanges,boundaries,events=((IEnumerable)typeof(LoomExplorationSession).GetField("_visitEvents",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!).Cast<object>().ToArray(),trace=session.SessionTrace.Skip(trace),uncertainties=session.Snapshot.Uncertainties},new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine($"{route.GetProperty("id")}: room {session.Snapshot.RoomId}, scene {session.InCutscene}, {boundaries.Count(t=>t.StartsWith("cutscene at "))}/{boundaries.Count(t=>t.StartsWith("endCutscene at "))} scopes");
}
