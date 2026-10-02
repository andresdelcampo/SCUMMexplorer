"""Audit separate selected camera copies; no loaded lifetime or completion claim."""
from pathlib import Path
import hashlib
import json
import sys

out = Path(sys.argv[1])
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p): return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())
recipe = load(out/'recipe.json')
original = load(Path('.verification/room41-certainty-20261002/room41.json'))
assert len(recipe) == 1 and recipe[0] == next(r for r in original if r['id']=='room41-raw4-click')
packet = out/'route-1/room41-raw4-click.json'
prior = Path('Exe/RuntimeRestorationFinalSession-20261002/final-evidence/room41-1')/packet.name
assert load(packet) == load(prior)
path = packet.with_name(packet.stem+'-camera.jsonl')
rows = [json.loads(line) for line in path.open(encoding='utf-8')]
previous_camera = Path('Exe/RuntimeRestorationFinalSession-20261002/camera/route-1')/path.name
assert path.read_bytes() == previous_camera.read_bytes(), 'separate camera chronology changed'
assert len(rows) == 55
for row in rows:
    observation = row['observation']
    assert observation['Site']['Operation']=='waitFor' and observation['Site']['Operands']=='camera'
    access = observation['SourceAccess']
    assert access['Barrier'] and access['NativeDependencies'][0]['Domain']=='camera-wait'
    fields = access['NativeDependencies'][1:]
    assert [d['Domain'] for d in fields] == ['camera-destination-x','camera-mode','camera-follows','camera-moving-to-actor']
    for field in fields:
        assert field['Value'] is not None and field['ValueEstimated'] is None and field['UnresolvedReason']
        assert field['Selector'] in access['Bindings']
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in load(out/'observer-1/source-manifest.json').items())
raw = (out/'build.txt').read_bytes()
build = raw.decode('utf-16' if raw.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
assert '0 Warning(s)' in build and '0 Error(s)' in build
summary = dict(scope='Separate unchanged loaded raw4 recipe with camera-only copied observations; native provenance stays unknown, no loaded lifetime or completion claim.',
    recipe='room41-raw4-click', recipeAndWholePacketExact=True, entirePreviousCameraChronologyExact=True,
    cameraWaits=len(rows), copiedFields=4*len(rows),
    loadedCameraLifetimeClaim=False, estimatesCleared=False, native090Completed=False,
    selected=[dict(frame=row['frame'], observation={key:row['observation'][key] for key in
              ('Sequence','Slot','ParentSlot','Invocation','Site','PathEstimated','SourceAccess')}) for row in rows[:3]],
    reports=[digest(out/p) for p in ('recipe.json','build.txt','run.txt','observer-1/source-manifest.json')]+[digest(packet),digest(path)])
Path(__file__).with_name('camera-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print({k:v for k,v in summary.items() if k not in ('scope','reports','selected')})
