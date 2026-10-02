"""Require entire primary native read/notification/update streams to remain exact."""
from pathlib import Path
import hashlib
import json
import sys

out = Path(sys.argv[1])
prior = Path('Exe/RuntimeBoundariesFinalSession4-20261002/final-evidence')
reports = []
for folder in ('room41-1','egg-first-1','dye-last-1','preserved-1'):
    for packet in sorted((out/folder).glob('*.json')):
        path = packet.with_name(packet.stem+'-native.jsonl')
        old = prior/folder/path.name
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        assert digest == hashlib.sha256(old.read_bytes()).hexdigest(), path
        reports.append(dict(path=path.as_posix(),sha256=digest))
assert len(reports) == 13
summary = dict(primaryNativeStreams=13,entireReadMutationInvalidationChronologyExact=True,
    chronologyRenumbered=False,loadedLifetimeClaim=False,completionClaim=False,reports=reports)
Path(__file__).with_name('native-continuity-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('All13 primary native read/mutation/invalidation streams exact to f649ff1; no ID changes.')
