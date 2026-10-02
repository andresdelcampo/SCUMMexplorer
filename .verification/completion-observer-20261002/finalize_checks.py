"""Fail-closed fresh preservation checks for the production observation API."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import re
import sys

base=Path(__file__).parent
out=Path(sys.argv[1])
prior=Path('Exe/DraftScopeSession-20261001/final-audits-1')
def read(p):
    b=p.read_bytes()
    return b.decode('utf-16' if b.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
def load(p): return json.loads(read(p))
def digest(p): return dict(path=p.as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
old,new=load(prior/'literal/audit.json'),load(out/'audits-1/literal/audit.json')
keys=('owner','status','frames','first','last','everOpen','begun','ended','rooms','sceneTrace')
assert len(new['results'])==363 and old['sourceFiles']==new['sourceFiles']
assert [{k:r.get(k) for k in keys} for r in old['results']]==[{k:r.get(k) for k in keys} for r in new['results']]
for name in ('established.json','sequences.json'): assert load(prior/name)==load(out/'audits-1'/name)
assert 'blocked: 0 instruction sites' in read(out/'audits-1/blockers.txt')
assert re.search(r'Passed:\s+321',read(out/'checks-1/core-tests.txt'))
for name in ('checks-1/release-build.txt','final-release-build.txt','observer-build.txt'):
    assert '0 Warning(s)' in read(out/name) and '0 Error(s)' in read(out/name)
assert 'Compared 76 room renders; changed: 0' in read(out/'png-comparison-2.txt')
rooms=read(out/'room-audit-2/room-audit.tsv').splitlines()[1:]
assert len(rooms)==76 and all(int(row.split('\t')[3])==0 for row in rooms)
assert 'SCUMMexplorer processes: 0' in read(out/'app-state.txt')
manifest=load(out/'observer-1/source-manifest.json')
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in manifest.items())
observer=load(base/'observer-summary.json')
assert observer['recipeCount']==13 and observer['fullPacketsIdentical'] and observer['priorInstructionValuesIdentical'] and observer['observedZeroWaits']==76
files=('checks-1/core-tests.txt','checks-1/release-build.txt','final-release-build.txt','app-state.txt',
    'observer-build.txt','audits-1/literal/audit.json','audits-1/established.json','audits-1/sequences.json',
    'audits-1/blockers.txt','room-audit-2/room-audit.tsv','room-audit-2/room-audit.txt','png-comparison-2.txt')
summary=dict(core=321,newObserverCases=5,rootCount=363,comparedFields=keys,rootStatuses=dict(Counter(r['status'] for r in new['results'])),
    sourceFingerprintsUnchanged=True,literalResultsUnchanged=True,membershipsUnchanged=True,sequencesUnchanged=True,
    fullRecipePacketsUnchanged=13,priorInstructionValuesUnchanged=True,observedZeroWaits=76,
    blockedRandomSites=0,roomRenders=76,remainingGaps=0,changedPixels=0,releaseWarnings=0,releaseErrors=0,
    appLaunched=False,estimatesCleared=False,native090Completed=False,reports=[digest(out/n) for n in files],
    sourceManifest=digest(out/'observer-1/source-manifest.json'))
(base/'check-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('Core321;363 exact roots/fingerprints; memberships/sequences unchanged;Blockers0;76 unchanged zero-gap renders;main Release clean;App0.')
