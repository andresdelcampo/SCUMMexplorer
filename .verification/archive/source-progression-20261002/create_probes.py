"""Read-only source diagnostics; no game state, certainty or browsing-policy writes."""
from pathlib import Path
import json

base = Path('.verification/archive/source-progression-20261002')
probe = base / 'ProgressionProbe'
probe.mkdir(exist_ok=True)
old = Path('.verification/archive/source-progression-20261001/ProgressionProbe')
code = (old / 'Program.cs').read_text(encoding='utf-8')
code = code.replace('completeBoot:!route.TryGetProperty("nativeBoot",out var nativeBoot)||!nativeBoot.GetBoolean()', 'stopAtBootInput:route.TryGetProperty("nativeBoot",out var nativeBoot)&&nativeBoot.GetBoolean()')
code = code.replace('locals=Prop(p,"Locals")', 'locals=Prop(p,"Locals"),pathEstimated=Prop(p,"PathEstimated"),estimatedLocals=Prop(p,"EstimatedLocals")')
code = code.replace('if(settle&&session.SessionTrace', 'if(settle&&(!session.Snapshot.KnownGlobalVariables.TryGetValue("bit[0xD66]",out var activeDraft)||activeDraft==0)&&session.SessionTrace')
code = code.replace('case "frames":', '''case "key": actions.Add(session.PressKey(step.GetProperty("key").GetInt32()));break;
            case "draft-key":
                int keySlot=step.GetProperty("slot").GetInt32();
                ushort keyPacked=session.Snapshot.KnownGlobalVariables[$"var[0x{0x32+2*keySlot:X3}]"];
                for(int i=0;i<4;i++) {
                    int noteId=((keyPacked>>(3*i))&7)+1;
                    actions.Add(session.PressKey("qwertyui"[noteId-1]));
                    for(int f=0;f<step.GetProperty("gap").GetInt32();f++)Advance();
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
                while(waited<step.GetProperty("count").GetInt32() && !session.IsScriptRunning(scriptId)) {Advance();waited++;}
                Record(step.GetProperty("stage").GetString()!,waited);
                if(!session.IsScriptRunning(scriptId)) {actions.Add(new {missingScript=scriptId});missingRoom=true;}
                break;
            case "frames":''')
# IsScriptRunning is private: query pending slots read-only instead.
code = code.replace('session.IsScriptRunning(scriptId)', '((IEnumerable)typeof(LoomExplorationSession).GetField("_pending",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!).Cast<object>().Any(p=>(int?)Prop(p,"ScriptNumber")==scriptId)')
(probe/'Program.cs').write_text(code, encoding='utf-8')
(probe/'ProgressionProbe.csproj').write_text((old/'ProgressionProbe.csproj').read_text(encoding='utf-8'),encoding='utf-8')

def visit(n): return dict(kind='visit',room=n)
def verb(n,v): return dict(kind='verb',objectId=n,verb=v)
def frames(stage,n=3000,settle=True): return dict(kind='frames',stage=stage,count=n,settle=settle)
def key(n): return dict(kind='key',key=n)
def route(name,actions): return dict(id=name,actions=actions+[dict(kind='inspect')])
def write(name,routes): (base/name).write_text(json.dumps(routes,indent=2)+'\n',encoding='utf-8')
native=json.loads(Path('.verification/archive/source-progression-20261001/routes-final.json').read_text(encoding='utf-8'))[-1]
arrival=native['actions'][:]
index=next(i for i,a in enumerate(arrival) if a.get('room')==41)
arrival[index:index+1]=[visit(37),verb(603,56),frames('source-arrival41'),dict(kind='require-room',room=41)]
write('native41.json',[route('native41-source37-arrival',arrival[:-1])])
egg=[visit(9),verb(271,56),frames('staff'),dict(kind='walk-point',objectId=270),frames('egg-approach',80,False),dict(kind='look',objectId=270),frames('egg-lesson'),dict(kind='target',objectId=270),frames('egg-aim',1,False),dict(kind='draft',slot=0,gap=8),frames('native-level4')]
write('level4.json',[route('native-egg-level4',egg)])
write('level4-trial.json',[route('native-egg-after-trial',egg[:3]+[visit(8),verb(255,56),frames('source-trial',5000),dict(kind='require-room',room=9)]+egg[3:])])
egg_keys=egg[:3]+[visit(8),verb(255,56),frames('source-trial',5000),dict(kind='require-room',room=9)]+egg[3:]
egg_keys=[dict(a,kind='draft-key') if a['kind']=='draft' else a for a in egg_keys]
write('level4-keys.json',[route('native-egg-after-trial-keys',egg_keys)])
egg_skip=egg_keys[:-1]+[dict(kind='until-override',owner='Script 47',stage='047-override',count=1000),key(27),frames('native-level4')]
write('level4-skip.json',[route('native-egg-source-skip047',egg_skip)])
# Source abortCutscene executes the recorded override; no pending slot is changed here.
skipped=arrival[:-2]+[dict(kind='until-override',owner='Room 41 local script 204',stage='204-override',count=1000),key(27),frames('source090')]
write('native41-skip.json',[route('native41-source-skip204',skipped)])
direct=skipped[:]
draft_index=next(i for i,a in enumerate(direct) if a['kind']=='draft')
direct[draft_index]=verb(633,65)
write('direct41-skip.json',[route('direct41-source-skip204',direct)])
late=[visit(43),verb(639,56),frames('source-arrival44'),dict(kind='require-room',room=44),verb(653,54)]
tail=[frames('095'),dict(kind='require-room',room=45),verb(656,54),frames('staff45'),verb(655,56),frames('return43'),dict(kind='require-room',room=43),verb(639,56),frames('096')]
write('late.json',[route('late096-reached44',late+tail),route('late096-source-skip095',late+[frames('095-override',2,False),key(27)]+tail)])
sleep=[visit(16),verb(350,71),frames('source-sleep'),visit(18)]
write('room20.json',[route(f'sleep-source{entry}-response{target}',sleep+[verb(entry,54),frames('arrival20'),dict(kind='require-room',room=20),verb(target,72),frames('response20')]) for entry in (369,371) for target in (390,391)])
boot=json.loads(Path('.verification/archive/source-progression-20261001/boot-routes.json').read_text(encoding='utf-8'))
boot[0]['actions']=boot[0]['actions'][1:]
boot[0]['id']='native-boot-challenge'
write('boot.json',boot)
# Native challenge glyph fallback handlers use 0..11, not the distaff's 1..8 IDs.
code=code.replace('n<1||n>12','n<0||n>11')
code=code.replace('o.Id==0x375+id','o.Id==0x376+id')
code=code.replace('boundaries,trace=session.SessionTrace', 'boundaries,events=((IEnumerable)typeof(LoomExplorationSession).GetField("_visitEvents",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!).Cast<object>().ToArray(),trace=session.SessionTrace')
(probe/'Program.cs').write_text(code,encoding='utf-8')
