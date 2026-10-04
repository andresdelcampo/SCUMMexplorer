"""Save/check protected work without staging or executing it."""
from pathlib import Path
import hashlib
import json
import sys

out=Path(sys.argv[1])
assert not out.exists()
files=[Path('.verification/verb-area/VerbAreaCheck/Program.cs'),Path('tests/SCUMMexplorer.Tests/LoomObjectLookAuditTests.cs')]
for folder in ('.verification/object-click-room17','.verification/room-object-interaction'):
    files.extend(p for p in Path(folder).rglob('*') if p.is_file())
manifest={p.as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(files)}
out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(f'{len(manifest)} protected files hashed; no contents changed.')
