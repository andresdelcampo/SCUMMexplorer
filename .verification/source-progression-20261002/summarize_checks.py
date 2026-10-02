"""Assert fresh regressions, preserving source fingerprints and scope timing."""
from pathlib import Path
from collections import Counter
import json, hashlib

output=Path('Exe/SourceProgressionSession-20261002')
prior=Path('Exe/DraftScopeSession-20261001/final-audits-1')
def read(path):
    data=path.read_bytes()
    return data.decode('utf-16' if data.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
def load(path): return json.loads(read(path))
old=load(prior/'literal/audit.json')
new=load(output/'audits-1/literal/audit.json')
keys=('owner','status','frames','first','last','everOpen','begun','ended','rooms','sceneTrace')
assert len(new['results'])==363
assert new['sourceFiles']==old['sourceFiles']
assert [{k:r.get(k) for k in keys} for r in new['results']]==[{k:r.get(k) for k in keys} for r in old['results']]
for name in ('established.json','sequences.json'):
    assert load(prior/name)==load(output/'audits-1'/name), name
assert 'blocked: 0 instruction sites' in read(output/'audits-1/blockers.txt')
for report,count in (('ui-1/staff-check.txt',62),('navigation-1/controls/room-controls-check.txt',19),('input-1/input-check.txt',18)):
    data=read(output/report)
    assert 'Failures: 0' in data and sum(l.startswith('PASS ') for l in data.splitlines())==count, report
data=read(output/'navigation-1/doorways.txt')
assert 'failed: 0' in data and sum(l.startswith('PASS ') for l in data.splitlines())==16
assert 'Passed:   289' in read(output/'checks-2/core-tests.txt')
assert '0 Warning(s)' in read(output/'release-final.txt') and '0 Error(s)' in read(output/'release-final.txt')
assert 'changed: 0' in read(output/'png-comparison.txt')
rooms=read(output/'room-audit-1/room-audit.txt')
assert 'remaining gaps: 0' in rooms.lower() or 'gaps after: 0' in rooms.lower(), rooms[-500:]
files=['checks-2/core-tests.txt','release-final.txt','ui-1/staff-check.txt','navigation-1/doorways.txt','navigation-1/controls/room-controls-check.txt','input-1/input-check.txt','audits-1/literal/audit.json','audits-1/established.json','audits-1/sequences.json','audits-1/blockers.txt','room-audit-1/room-audit.txt','png-comparison.txt']
record=dict(core=289,wpf=62,doorways=16,controls=19,input=18,rootCount=363,
    rootStatuses=dict(Counter(r['status'] for r in new['results'])),comparedFields=keys,
    sourceFingerprintsUnchanged=True,rootResultsUnchanged=True,membershipsUnchanged=True,sequencesUnchanged=True,
    blockedRandomSites=0,rooms=76,remainingGaps=0,pixelsUnchanged=76,releaseWarnings=0,releaseErrors=0,
    reports=[dict(path=(output/name).as_posix(),sha256=hashlib.sha256((output/name).read_bytes()).hexdigest()) for name in files])
Path('.verification/source-progression-20261002/check-summary.json').write_text(json.dumps(record,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in record.items() if k!='reports'}))
