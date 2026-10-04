"""Generate fresh, source-input-only diagnostic routes. No runtime writes."""
from pathlib import Path
import json, shutil

base=Path(__file__).parent
old=Path('.verification/archive/source-progression-20261002')
(base/'ProgressionProbe').mkdir(exist_ok=True)
for name in ('Program.cs','ProgressionProbe.csproj'):
    shutil.copyfile(old/'ProgressionProbe'/name,base/'ProgressionProbe'/name)
program=base/'ProgressionProbe'/'Program.cs'
text=program.read_text(encoding='utf-8')
text=text.replace('int noteId=((keyPacked>>(3*i))&7)+1;', 'int noteIndex=step.TryGetProperty("reverse",out var reverse)&&reverse.GetBoolean()?3-i:i;\n                    int noteId=((keyPacked>>(3*noteIndex))&7)+1;')
program.write_text(text,encoding='utf-8')

def visit(n):return dict(kind='visit',room=n)
def verb(n,v):return dict(kind='verb',objectId=n,verb=v)
def frames(s,n=3000,settle=True):return dict(kind='frames',stage=s,count=n,settle=settle)
def req(n):return dict(kind='require-room',room=n)
def action(k,n):return dict(kind=k,objectId=n)
def cast(slot):return dict(kind='draft-key',slot=slot,gap=8)
def route(n,a):return dict(id=n,actions=a+[dict(kind='inspect')])
def write(n,r): (base/n).write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8')

staff=[visit(9),verb(271,56),frames('staff')]
# Learning may be requested directly; native use must traverse the input helpers.
dye=staff+[visit(6),verb(205,54),frames('dye-lesson'),action('walk-point',207),frames('dye-approach',80,False),action('target',207),frames('dye-aim',1,False),cast(2),frames('dye-cast')]
wheel=staff+[visit(62),verb(810,54),frames('wheel-lesson'),action('walk-point',808),frames('wheel-approach',80,False),action('target',808),frames('wheel-aim',1,False),cast(1),frames('wheel-cast')]
write('lessons.json',[route('native-dye',dye),route('native-wheel',wheel)])
write('room41-controls.json',json.loads((old/'native41.json').read_text())+json.loads((old/'native41-skip.json').read_text())+json.loads((old/'direct41-skip.json').read_text()))
write('preserved-recipes.json',json.loads((old/'late.json').read_text())+json.loads((old/'room20.json').read_text()))
wheel_variants=[]
for target in (808,809):
    for reverse in (False,True):
        steps=wheel[:6]+[action('walk-point',target),frames('wheel-approach',80,False),action('target',target),frames('wheel-aim',1,False),dict(cast(1),reverse=reverse),frames('wheel-cast')]
        wheel_variants.append(route(f'wheel-{target}-reverse{reverse}',steps))
write('wheel-variants.json',wheel_variants)
egg=json.loads((old/'level4-skip.json').read_text())[0]['actions']
combined=dye+wheel[3:6]+[action('walk-point',808),frames('wheel-approach',80,False),action('target',808),frames('wheel-aim',1,False),dict(cast(1),reverse=True),frames('wheel-cast')]+egg[3:-1]
write('level4-combined.json',[route('native-level4-all-used',combined)])
# Run the existing clean trial/egg recipe first, before estimated cast side
# effects can taint trial control flow. Later native transformations test the
# equivalent gates at the point where the final missing use is established.
egg_first=egg[:-1]+dye[3:]+wheel[3:6]+[action('walk-point',808),frames('wheel-approach',80,False),action('target',808),frames('wheel-aim',1,False),dict(cast(1),reverse=True),frames('wheel-cast')]
write('level4-egg-first.json',[route('native-level4-egg-dye-wheel',egg_first)])
dye_last=egg[:-1]+wheel[3:6]+[action('walk-point',808),frames('wheel-approach',80,False),action('target',808),frames('wheel-aim',1,False),dict(cast(1),reverse=True),frames('wheel-cast')]+dye[3:]
write('level4-dye-last.json',[route('native-level4-egg-wheel-dye',dye_last)])
native=json.loads((old/'native41.json').read_text())[0]['actions']
# Keep reached052/325 learning and reached603 arrival; omit both direct
# high-level response shortcuts. The newly executed029(4) supplies notes.
start10=next(i for i,a in enumerate(native) if a.get('room')==10)
repeat13=next(i for i,a in enumerate(native) if a.get('room')==13 and a['kind']=='visit')
start37=next(i for i,a in enumerate(native) if a.get('room')==37)
native4=egg_first+native[start10:repeat13]+native[start37:-1]
write('room41-level4.json',[route('native41-source-level4',native4)])
skip=next(i for i,a in enumerate(native4) if a['kind']=='draft' and a['slot']==4)
native4skip=native4[:skip+1]+[dict(kind='until-override',owner='Room 41 local script 204',stage='204-override',count=1000),dict(kind='key',key=27),frames('source090')]
write('room41-level4-skip.json',[route('native41-source-level4-skip204',native4skip)])
direct4=native4skip[:]
direct4[skip]=verb(633,65)
write('room41-level4-direct.json',[route('direct41-source-level4-skip204',direct4)])
