"""Fail-closed aggregation of completed Core checks and preservation packets."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import re
import sys

base=Path(__file__).parent
output=Path(sys.argv[1])
prior=Path('Exe/DraftScopeSession-20261001/final-audits-1')
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def read(p):
    b=p.read_bytes()
    return b.decode('utf-16' if b.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
def digest(p): return dict(path=p.as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
old,new=load(prior/'literal/audit.json'),load(output/'audits-1/literal/audit.json')
keys=('owner','status','frames','first','last','everOpen','begun','ended','rooms','sceneTrace')
assert len(new['results'])==363 and old['sourceFiles']==new['sourceFiles']
assert [{k:r.get(k) for k in keys} for r in new['results']]==[{k:r.get(k) for k in keys} for r in old['results']]
for name in ('established.json','sequences.json'):
    assert load(prior/name)==load(output/'audits-1'/name)
assert 'blocked: 0 instruction sites' in read(output/'audits-1/blockers.txt')
assert re.search(r'Passed:\s+305',read(output/'checks-1/core-tests.txt'))
for name in ('checks-1/release-build.txt','final-release-build.txt'):
    assert '0 Warning(s)' in read(output/name) and '0 Error(s)' in read(output/name)
assert 'Compared 76 room renders; changed: 0' in read(output/'png-comparison-2.txt')
rooms=read(output/'room-audit-2/room-audit.tsv').splitlines()[1:]
assert len(rooms)==76 and all(int(r.split('\t')[3])==0 for r in rooms)
assert 'SCUMMexplorer processes: 0' in read(output/'app-state.txt')
manifest=load(output/'observer-1/source-manifest.json')
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in manifest.items())
protocol=load(base/'protocol-summary.json')
assert protocol['recipeCount']==13 and protocol['fullPacketsIdentical']
files=('cancellation-before.txt','cancellation-after.txt','checks-1/core-tests.txt','checks-1/release-build.txt',
    'final-release-build.txt','app-state.txt','audits-1/literal/audit.json','audits-1/established.json',
    'audits-1/sequences.json','audits-1/blockers.txt','room-audit-2/room-audit.tsv',
    'room-audit-2/room-audit.txt','png-comparison-2.txt','observer-build.txt')
result=dict(core=305,newCancellationCases=7,originalFailures=3,rootCount=363,comparedFields=keys,
    rootStatuses=dict(Counter(r['status'] for r in new['results'])),sourceFingerprintsUnchanged=True,
    literalResultsUnchanged=True,membershipsUnchanged=True,sequencesUnchanged=True,fullRecipePacketsUnchanged=13,
    blockedRandomSites=0,roomRenders=76,remainingGaps=0,changedPixels=0,releaseWarnings=0,releaseErrors=0,
    appLaunched=False,reports=[digest(output/n) for n in files],sourceManifest=digest(output/'observer-1/source-manifest.json'))
(base/'check-summary.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('Core305;363 exact roots/fingerprints; memberships/sequences and13 whole packets unchanged; Blockers0;76 unchanged zero-gap renders; clean main Release; no App.')
