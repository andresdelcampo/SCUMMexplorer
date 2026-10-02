"""Compact evidence from the generic Core numeric analyzer; no runtime state."""
from pathlib import Path
import hashlib
import json
import re
import sys

out = Path(sys.argv[1])
base = Path(__file__).parent
def read(p):
    data=p.read_bytes()
    return data.decode('utf-16' if data.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
def digest(p): return dict(path=p.as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
j=json.loads(read(out/'numeric-clean.json'))
assert (j['bootAssignments'],j['numericWorlds'],j['flagCases'],j['forwardComparisons'],j['firstComponentOverlaps'])==(81,162,52,51,14)
assert len(j['fabricated'])==4 and len(j['practice'])==12
assert j['earlyStop']['Globals']['bit[0xD67]']['Number']==1 and j['terminal']['Globals']['bit[0xD67]']['Number']==0
tests=read(out/'checks-1/core-tests.txt')
assert re.search(r'Passed:\s+316',tests)
build=read(out/'checks-1/release-build.txt')
assert '0 Warning(s)' in build and '0 Error(s)' in build
summary={k:v for k,v in j.items() if k not in ('worlds','masks','practice','fingerprints','earlyStop','terminal')}
summary.update(core=316,practiceArgumentCases=12,earlyStopSliceD67=1,terminalSliceD67=0,
    sourceFingerprintCount=len(j['fingerprints']),releaseWarnings=0,releaseErrors=0,
    reports=[digest(out/n) for n in ('numeric-clean.json','checks-1/core-tests.txt','checks-1/release-build.txt')])
(base/'numeric-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('Core316; generic numeric evidence complete; main Release clean; no runtime estimate change.')
