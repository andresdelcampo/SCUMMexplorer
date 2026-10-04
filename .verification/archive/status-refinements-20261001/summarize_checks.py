from pathlib import Path
from collections import Counter
import json,hashlib

output=Path('Exe/StatusRefinementsSession-20261001')
prior=Path('Exe/DraftScopeSession-20261001/final-audits-1')
audits=output/'audits-1'
def read(path):
    data=path.read_bytes()
    return data.decode('utf-16' if data.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
def load(path): return json.loads(read(path))
old=load(prior/'literal/audit.json')['results']
new=load(audits/'literal/audit.json')['results']
keys=('owner','status','frames','rooms','sceneTrace')
assert [{key:r.get(key) for key in keys} for r in new]==[{key:r.get(key) for key in keys} for r in old]
assert len(new)==363
for name in ('established.json','sequences.json'):
    assert load(prior/name)==load(audits/name), name
blockers=read(audits/'blockers.txt')
assert 'blocked: 0 instruction sites' in blockers
ui=read(output/'ui-2/staff-check.txt')
assert 'Failures: 0' in ui and not any(line.startswith('FAIL ') for line in ui.splitlines())
assert sum(line.startswith('PASS ') for line in ui.splitlines())==62
controls=read(output/'navigation-1/controls/room-controls-check.txt')
assert 'Failures: 0' in controls and sum(line.startswith('PASS ') for line in controls.splitlines())==19
doorways=read(output/'navigation-1/doorways.txt')
assert 'failed: 0' in doorways and sum(line.startswith('PASS ') for line in doorways.splitlines())==16
assert 'Passed:   277' in read(output/'tests-final.txt')
assert '0 Warning(s)' in read(output/'release-final.txt') and '0 Error(s)' in read(output/'release-final.txt')
rooms=load(Path('.verification/archive/status-refinements-20261001/room-comparison.json'))
assert rooms['rooms']==76 and rooms['changed']==['room-20.bmp']
assert 'changed: 0' in read(output/'png-comparison.txt')
files=['tests-final.txt','release-final.txt','ui-2/staff-check.txt','navigation-1/doorways.txt','navigation-1/controls/room-controls-check.txt','audits-1/literal/audit.json','audits-1/established.json','audits-1/sequences.json','audits-1/blockers.txt','room-after-1/room-audit.txt','png-comparison.txt']
summary=dict(core=277,wpf=62,doorways=16,controls=19,rootCount=363,
    rootStatuses=dict(Counter(r['status'] for r in new)),rootResultsUnchanged=True,
    membershipsUnchanged=True,sequencesUnchanged=True,blockedRandomSites=0,
    rooms=rooms,remainingGaps=0,releaseWarnings=0,releaseErrors=0,
    reports=[dict(path=(output/name).as_posix(),sha256=hashlib.sha256((output/name).read_bytes()).hexdigest()) for name in files])
Path('.verification/archive/status-refinements-20261001/check-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in summary.items() if k not in ('reports','rooms')}))
