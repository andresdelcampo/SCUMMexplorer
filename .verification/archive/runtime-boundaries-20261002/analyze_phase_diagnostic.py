"""Retain the rejected overconstraint and require exact unrenumbered correction."""
from pathlib import Path
import hashlib
import json

def rows(p): return [json.loads(line) for line in p.open(encoding='utf-8')]
def packet(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p): return dict(path=p.as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
diag=Path('Exe/RuntimeBoundaryDiagnostic-20261002')
before=rows(diag/'route-2/old.jsonl')
rejected=rows(diag/'route-2/new.jsonl')
first=next(i for i,(a,b) in enumerate(zip(before,rejected)) if a!=b)
old,new=before[first],rejected[first]
assert old['id']==new['id']==405 and old['frame']==new['frame']==3323
assert old['binding'] is not None and new['binding'] is None
assert old['CallerRejection']==new['CallerRejection']=='partial caller end'
assert old['host']==new['host'] and old['ended']==new['ended']
prior=Path('Exe/RuntimeRestorationFinalSession-20261002/loaded-objects-2/route-1')
fixed=diag/'loaded-fixed/route-1'
stem='room41-raw4-click'
assert packet(prior/(stem+'.json'))==packet(fixed/(stem+'.json'))
for suffix in ('-calls.jsonl','-object-selections.jsonl','-correspondence.jsonl'):
    assert rows(prior/(stem+suffix))==rows(fixed/(stem+suffix)),suffix
summary=dict(scope='Tracker-only diagnostic on one unchanged actual raw4 stream; no execution or recipe patch.',
    firstDivergenceFrame=3323,dispatch=405,callerRejectionRetained='partial caller end',
    oldUpdates=len(before),overconstrainedUpdates=len(rejected),
    independentAssignmentRestored=True,entireCorrectedLoadedPacketAndStreamsExact=True,
    chronologyRenumbered=False,assertionRelaxed=False,completionClaim=False,
    reports=[digest(diag/'route-2'/name) for name in ('old.jsonl','new.jsonl',stem+'.json')]+[
        digest(fixed/(stem+suffix)) for suffix in ('.json','-calls.jsonl','-object-selections.jsonl','-correspondence.jsonl')])
Path(__file__).with_name('phase-diagnostic-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print({k:v for k,v in summary.items() if k not in ('scope','reports')})
