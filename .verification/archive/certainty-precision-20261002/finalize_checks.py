from pathlib import Path
from collections import Counter
import hashlib
import json

base = Path(__file__).parent
output = Path('Exe/CertaintyPrecisionSession-20261002')
prior = Path('Exe/DraftScopeSession-20261001/final-audits-1')
def load(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))
def text(p):
    b = p.read_bytes()
    return b.decode('utf-16' if b.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig')
def digest(p):
    return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())

old = load(prior / 'literal/audit.json')
new = load(output / 'literal-final/audit.json')
keys = ('owner', 'status', 'frames', 'first', 'last', 'everOpen', 'begun', 'ended', 'rooms', 'sceneTrace')
assert len(new['results']) == 363 and new['sourceFiles'] == old['sourceFiles']
assert [{k:r.get(k) for k in keys} for r in new['results']] == [{k:r.get(k) for k in keys} for r in old['results']]
for name in ('established.json', 'sequences.json'):
    assert load(prior / name) == load(output / 'audits-1' / name)
assert 'blocked: 0 instruction sites' in text(output / 'audits-1/blockers.txt')
assert 'Passed:   298' in text(output / 'checks-2/core-tests.txt')
for name in ('checks-2/release-build.txt', 'final-release-build.txt'):
    assert '0 Warning(s)' in text(output / name) and '0 Error(s)' in text(output / name)
assert 'Compared 76 room renders; changed: 0' in text(output / 'png-comparison.txt')
room_rows = text(output / 'room-audit-1/room-audit.tsv').splitlines()[1:]
assert len(room_rows) == 76 and all(int(r.split('\t')[3]) == 0 for r in room_rows)
files = ('checks-2/core-tests.txt', 'checks-2/release-build.txt', 'final-release-build.txt',
    'literal-final/audit.json', 'audits-1/established.json', 'audits-1/sequences.json', 'audits-1/blockers.txt',
    'room-audit-1/room-audit.tsv', 'room-audit-1/room-audit.txt', 'png-comparison.txt')
engine_files = ('src/SCUMMexplorer.Core/Formats/LoomActors.cs', 'src/SCUMMexplorer.Core/Formats/LoomBoundedScriptExecutor.cs',
    'tests/SCUMMexplorer.Tests/LoomPaletteCertaintyTests.cs')
result = dict(core=298, paletteCases=9, rootCount=363, comparedFields=keys,
    rootStatuses=dict(Counter(r['status'] for r in new['results'])), sourceFingerprintsUnchanged=True,
    literalResultsUnchanged=True, membershipsUnchanged=True, sequencesUnchanged=True,
    blockedRandomSites=0, roomRenders=76, remainingGaps=0, changedPixels=0,
    releaseWarnings=0, releaseErrors=0, appLaunched=False,
    reports=[digest(output / n) for n in files], sources=[digest(Path(n)) for n in engine_files])
(base / 'check-summary.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print('Core298; nine palette cases;363 exact roots; memberships/sequences unchanged; Blockers0;76 unchanged zero-gap renders; Release clean.')
