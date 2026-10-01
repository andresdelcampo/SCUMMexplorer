"""Build read-only diagnostics; actions never set status, state, actor or certainty."""
from pathlib import Path
import json

base = Path('.verification/source-progression-20261001')
original = Path('.verification/staff-correction-20261001/RouteProbe')
probe = base/'ProgressionProbe'
probe.mkdir(parents=True, exist_ok=True)
code = (original/'Program.cs').read_text(encoding='utf-8')
code = code.replace('new LoomExplorationSession(game,objects)', 'new LoomExplorationSession(game,objects,completeBoot:!route.TryGetProperty("nativeBoot",out var nativeBoot)||!nativeBoot.GetBoolean())')
code = code.replace('int trace=session.SessionTrace.Count;', 'int elapsed=0; var roomChanges=new List<object>(); int? observedRoom=session.Snapshot.RoomId; int trace=session.SessionTrace.Count;')
code = code.replace('snapshot=session.Snapshot,actors=session.Actors,pending', 'snapshot=session.Snapshot,status=session.GameStatus,actors=session.Actors,pending')
code = code.replace('snapshots.Add(new{stage,frame,', '''var runtime=typeof(LoomExplorationSession).GetField("_runtime",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!;
        var estimatedVariables=((IEnumerable)Prop(runtime,"EstimatedVariables")!).Cast<string>().Order().ToArray();
        snapshots.Add(new{stage,frame,estimatedVariables,''')
code = code.replace('    void Frames(string stage,int count,bool settle)', '''    void Advance()
    {
        session.AdvanceFrame(); elapsed++;
        if(observedRoom!=session.Snapshot.RoomId) {observedRoom=session.Snapshot.RoomId;roomChanges.Add(new {elapsed,room=observedRoom});}
    }
    void Frames(string stage,int count,bool settle)''')
code = code.replace('            session.AdvanceFrame();','            Advance();')
code = code.replace('f++)session.AdvanceFrame();','f++)Advance();')
code = code.replace('if(settle&&!session.InCutscene', 'if(settle&&session.SessionTrace.Skip(trace).Count(t=>t.StartsWith("cutscene at "))==session.SessionTrace.Skip(trace).Count(t=>t.StartsWith("endCutscene at "))&&!session.InCutscene')
code = code.replace('            case "frames":', '''            case "boot-answer":
                var answer=Enumerable.Range(0,4).Select(i=>(int)session.Snapshot.KnownGlobalVariables[$"var[0x{0x52+i:X3}]"]).ToArray();
                if(answer.Any(n=>n<1||n>12)) throw new InvalidDataException("Source boot answer has not been generated.");
                actions.Add(new {sourceBootAnswer=answer});
                foreach(int id in answer) {
                    var glyph=new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r=>r.ResourceId==69).FilePath)).Single(o=>o.Id==0x375+id);
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
            case "frames":''')
code = code.replace('actions,snapshots,boundaries,trace=', 'actions,snapshots,elapsed,roomChanges,boundaries,trace=')
code = code.replace('    foreach(var step in route.GetProperty("actions").EnumerateArray())', '    try {\n    foreach(var step in route.GetProperty("actions").EnumerateArray())')
code = code.replace('    Record("final",0);', '''    } catch(Exception failure) {
        Record("failure",0);
        File.WriteAllText(Path.Combine(args[2],route.GetProperty("id").GetString()+"-failure.json"),JsonSerializer.Serialize(new {route=route.Clone(),actions,snapshots,elapsed,roomChanges,boundaries=session.SessionTrace.Skip(trace).Where(t=>t.StartsWith("cutscene at ")||t.StartsWith("endCutscene at ")).ToArray(),trace=session.SessionTrace.Skip(trace),uncertainties=session.Snapshot.Uncertainties,failure=failure.ToString()},new JsonSerializerOptions {WriteIndented=true}));
        throw;
    }
    Record("final",0);''')
(probe/'Program.cs').write_text(code, encoding='utf-8')
(probe/'ProgressionProbe.csproj').write_text((original/'RouteProbe.csproj').read_text(encoding='utf-8'),encoding='utf-8')

def visit(n): return dict(kind='visit',room=n)
def verb(n,v): return dict(kind='verb',objectId=n,verb=v)
def frames(stage,n=3000,settle=True): return dict(kind='frames',stage=stage,count=n,settle=settle)
def aim(n,look=False): return dict(kind='look' if look else 'target',objectId=n)
def ready(n,stage): return dict(kind='until-touchable',objectId=n,stage=stage,count=3000)
def route(name,actions): return dict(id=name,actions=actions+[dict(kind='inspect')])
staff=[visit(9),verb(271,56),frames('staff')]
level5=[visit(13),verb(323,56),frames('level5')]
learn=[visit(10),verb(295,56),frames('052'),dict(kind='require-room',room=13),aim(325,True),frames('native-learn4')]
approach=[visit(41),ready(632,'632-ready'),aim(632,True),ready(633,'633-ready'),dict(kind='walk-point',objectId=633),frames('approach',40,False),aim(633),frames('633-aim',1,False),dict(kind='draft',slot=4,gap=8),frames('cast')]
routes4=[route('native13-lesson-after052',staff+learn),
         route('level6-walk-native41',staff+level5+[visit(28),verb(470,61),frames('level6')]+approach),
         route('level6-native-lesson-cast41',staff+learn+level5+[visit(28),verb(470,61),frames('level6')]+approach)]
(base/'routes-final.json').write_text(json.dumps(routes4,indent=2)+'\n',encoding='utf-8')
late_setup=[visit(44),verb(653,54),frames('095'),verb(656,54),frames('staff45'),verb(655,56),frames('return43')]
later=[route('late096-source-recheck',late_setup+[verb(639,56),frames('096',3000,False)]),
       route('late096-revisit43',late_setup+[visit(43),verb(639,56),frames('096',3000,False)]),
       route('fresh18-return-certainty',[visit(18),verb(370,54),frames('065',3000,False)]),
       route('fresh38-087',[visit(38),verb(610,54),frames('087',3000,False)])]
for entry in (369,371):
    for target in (390,391):
        later.append(route(f'transient20-source{entry}-response{target}',[visit(18),verb(entry,54),frames('arrival'),dict(kind='require-room',room=20),verb(target,72),frames('response')]))
(base/'later-routes.json').write_text(json.dumps(later,indent=2)+'\n',encoding='utf-8')
boot=route('native-boot-check',[visit(69),frames('intro',4,False),dict(kind='inspect'),aim(0x36C),frames('challenge',32,False),dict(kind='boot-answer'),frames('opening',5000,False)])
boot['nativeBoot']=True
(base/'boot-routes.json').write_text(json.dumps([boot],indent=2)+'\n',encoding='utf-8')
roots=json.loads(Path('Exe/VerificationSession-20261001/native-roots-1.json').read_text(encoding='utf-8'))
roots={key:roots[key] for key in ('inputSha256','roots')}
roots['roots']=[r for r in roots['roots'] if (r['room'],r['objectId'],r['verb']) in ((18,370,54),(20,390,72),(20,391,72),(38,610,54))]
assert len(roots['roots'])==4
(base/'incomplete-roots.json').write_text(json.dumps(roots,indent=2)+'\n',encoding='utf-8')
