"""Prepare one fresh sphere audit; preserve all historical packets."""
import hashlib
import json
from pathlib import Path

base = Path('.verification/sphere-catalog-20261002')
out = Path('Exe/SphereCatalog-20261002')
out.mkdir(exist_ok=False)
roots = json.loads(Path('Exe/VerificationSession-20261001/native-roots-1.json').read_text(encoding='utf-8-sig'))
sphere = [r for r in roots['roots'] if r['owner'] == 'Room 18 object 370 Verb 0x36']
assert len(sphere) == 1
(base/'sphere-root.json').write_text(json.dumps(dict(inputSha256=roots['inputSha256'], roots=sphere), indent=2)+'\n', encoding='utf-8')
protected = json.loads(Path('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json').read_text())
assert len(protected) == 57
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest() == h.lower() for p,h in protected.items())
(out/'catalog-before.json').write_bytes(Path('src/SCUMMexplorer.App/loom-replays.json').read_bytes())
print('Prepared one root; protected57 exact; original catalog saved.')
