from pathlib import Path
import json
def visit(n): return dict(kind='visit',room=n)
def verb(n,v): return dict(kind='verb',objectId=n,verb=v)
def frames(stage,n=3000,settle=True): return dict(kind='frames',stage=stage,count=n,settle=settle)
routes=[]
for entry in (369,371):
    for target in (390,391):
        routes.append(dict(id=f'room20-source{entry}-response{target}',actions=[visit(18),verb(entry,54),frames('source-arrival'),dict(kind='require-room',room=20),verb(target,72),frames('response'),dict(kind='inspect')]))
routes.append(dict(id='room41-learned-wind-target634',actions=[visit(9),verb(271,56),frames('staff'),visit(13),verb(325,54),frames('learn-wind'),visit(41),frames('arrival',32,False),dict(kind='target',objectId=634),frames('aim'),dict(kind='draft',slot=4,gap=1),frames('cast'),dict(kind='inspect')]))
Path('.verification/archive/staff-correction-20261001/more-routes.json').write_text(json.dumps(routes,indent=2)+'\n',encoding='utf-8')
