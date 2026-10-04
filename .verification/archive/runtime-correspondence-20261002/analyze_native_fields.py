"""Require loaded native field evidence and every-frame transparency without replacing prior streams."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import sys

base=Path(__file__).parent
out=Path(sys.argv[1])
fields=json.loads((out/'fields.json').read_text(encoding='utf-8-sig'))
prior=json.loads(Path('Exe/RuntimeCallsFinalSession-20261002/passive-final/passive-expressions.json').read_text(encoding='utf-8-sig'))
assert fields['transparent'] and fields['sourceInputOnly'] and fields['frames']==36
assert fields['packet']==prior['packet'] and len(fields['steps'])==17
expressions=[o for o in fields['outer'] if o['Site'] is not None and o['Site']['Operation']=='expression']
assert [o['Site']['Offset'] for o in expressions]==[0x22,0x49,0x56]
domains=('user-input','object-position-x','object-position-y')
reads=[r for u in fields['native'] for r in u['Reads'] if r['Dependency']['Domain'] in domains]
assert reads and all(r['Observation']['Site']['Operation'] in ('drawObject','cursorCommand','getActorX','getActorY') for r in reads)
for read in reads:
    assert read['Dependency'] in read['Observation']['SourceAccess']['NativeDependencies']
    if read['Dependency']['Value'] is None: assert read['Dependency']['UnresolvedReason']
manifest=json.loads((out/'field-audit/source-manifest.json').read_text(encoding='utf-8'))
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in manifest.items())
raw=(out/'build.txt').read_bytes()
build=raw.decode('utf-16' if raw.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
assert '0 Warning(s)' in build and '0 Error(s)' in build
def digest(p): return dict(path=p.as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
selected=[]
seen=set()
for read in reads:
    site=read['Observation']['Site']
    key=(site['Script'],site['Context'],site['Offset'],read['Dependency']['Domain'])
    if key in seen: continue
    seen.add(key)
    selected.append(dict(site=site,slot=read['Observation']['Slot'],invocation=read['Observation']['Invocation'],
        pathEstimated=read['Observation']['PathEstimated'],dependency=read['Dependency']))
summary=dict(scope='Separate unchanged native challenge actions through36 frames; copied native field accesses only. Stored drawing coordinates are not walk points/query results; no origins, effects, estimate clearing, prerequisites or opening completion.',
    everyInputAndFrameTransparent=True,finalPacketExact=True,frames=36,expressions=3,expressionSteps=17,
    nativeFieldReads=len(reads),domains=dict(Counter(r['Dependency']['Domain'] for r in reads)),
    explicitNotificationInvalidations=sum(len(u['Invalidated']) for u in fields['native'] if u['Observation'] is None),
    selected=selected,reports=[digest(out/p) for p in ('fields.json','build.txt','run.txt','field-audit/source-manifest.json')])
(base/'native-field-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print(f"Loaded native fields: {len(reads)} reads {summary['domains']}; unchanged36 frames/3 expressions/17 steps, every input/frame transparent.")
