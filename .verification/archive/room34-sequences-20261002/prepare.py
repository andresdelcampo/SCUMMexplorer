"""Prepare a bounded source-action route; preserve historical and unrelated files."""
import hashlib
import json
from pathlib import Path

base = Path('.verification/archive/room34-sequences-20261002')
out = Path('Exe/Room34Sequences-20261002')
out.mkdir(exist_ok=False)
protected = json.loads(Path('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json').read_text())
assert len(protected) == 57
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest() == h.lower() for p, h in protected.items())
(out/'catalog-before.json').write_bytes(Path('src/SCUMMexplorer.App/loom-replays.json').read_bytes())
actions = [dict(kind='visit', room=34)]
for stage, verb in [('wake-first', 0x44), ('change-appearances', 0x4C),
                    ('wake-disguised', 0x44), ('wake-again', 0x44), ('wake-persistent', 0x44)]:
    actions += [dict(kind='verb', objectId=561, verb=verb),
                dict(kind='frames', stage=stage, count=3000, settle=True)]
(base/'route.json').write_text(json.dumps([dict(id='room34-repeated-waking', actions=actions)], indent=2)+'\n')
print('Prepared Room34 route; protected57 exact; original catalog saved.')
