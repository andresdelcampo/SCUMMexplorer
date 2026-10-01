from pathlib import Path
import json
base=Path('.verification/staff-correction-20261001')
probe=base/'RouteProbe'
probe.mkdir(parents=True,exist_ok=True)
code=Path('.verification/draft-scopes-20261001/DraftProbe/Program.cs').read_text(encoding='utf-8')
code=code.replace('case "target":', '''case "look":
                var looked = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r=>r.ResourceId==session.Snapshot.RoomId).FilePath)).Single(o=>o.Id==step.GetProperty("objectId").GetInt32());
                actions.Add(session.Click(looked.X+looked.Width/2,looked.Y+looked.Height/2,true));break;
            case "inspect":
                actions.Add(new { directory=new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r=>r.ResourceId==session.Snapshot.RoomId).FilePath)), status=session.GameStatus });break;
            case "target":''')
code=code.replace('int trace=session.SessionTrace.Count;', 'int trace=session.SessionTrace.Count; bool missingRoom=false;')
code=code.replace('case "visit":', '''case "require-room":
                if(session.Snapshot.RoomId != step.GetProperty("room").GetInt32()) {
                    actions.Add(new { prerequisiteNotReached=step.GetProperty("room").GetInt32(), actualRoom=session.Snapshot.RoomId }); missingRoom=true;
                } break;
            case "visit":''')
code=code.replace('    Record("final",0);', '    Record("final",0);')
code=code.replace('        }\n    }\n    Record("final",0);','        }\n        if(missingRoom) break;\n    }\n    Record("final",0);')
(probe/'Program.cs').write_text(code,encoding='utf-8')
(probe/'RouteProbe.csproj').write_text(Path('.verification/draft-scopes-20261001/DraftProbe/DraftProbe.csproj').read_text(),encoding='utf-8')
def visit(n): return dict(kind='visit',room=n)
def verb(n,v): return dict(kind='verb',objectId=n,verb=v)
def target(n,look=False): return dict(kind='look' if look else 'target',objectId=n)
def frames(stage,n=3000,settle=True): return dict(kind='frames',stage=stage,count=n,settle=settle)
staff=[visit(9),verb(271,56),frames('staff')]
routes=[]
for slot in (4,9):
    for obj in (632,634):
        routes.append(dict(id=f'room41-target{obj}-slot{slot}',actions=staff+[visit(41),frames('arrival',32,False),dict(kind='inspect'),target(obj),frames('aim'),dict(kind='draft',slot=slot,gap=1),frames('cast')]))
routes.append(dict(id='room41-native-look632-then634',actions=staff+[visit(41),frames('arrival',32,False),target(632,True),frames('look632'),target(634),frames('aim634'),dict(kind='draft',slot=4,gap=1),frames('cast')]))
routes.append(dict(id='room41-source-level4',actions=staff+[verb(270,58),frames('egg-open'),visit(41),frames('arrival',32,False),target(634),frames('aim'),dict(kind='draft',slot=4,gap=1),frames('cast'),dict(kind='inspect')]))
late=json.loads(Path('.verification/draft-scopes-20261001/late-1.json').read_text(encoding='utf-8'))
for route in late:
    route=dict(id=route['id']+'-fresh',actions=route['actions'])
    route['actions'] += [dict(kind='inspect')]
    routes.append(route)
(base/'routes.json').write_text(json.dumps(routes,indent=2)+'\n',encoding='utf-8')
