"""Check preserved historical documentation, publication hashes and protected staging."""
from pathlib import Path
import ast
import difflib
import hashlib
import json
import subprocess

base=Path(__file__).parent
rtk=r'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'

def git(*args):
    return subprocess.check_output([rtk,'proxy','git',*args]).decode('utf-8')

for script in base.glob('*.py'):
    ast.parse(script.read_text(encoding='utf-8'),filename=str(script))

for name in ('CHANGELOG.md','LOOM_CUTSCENE_DISCOVERY.md','LOOM_EGA_PLAN.md','LOOM_SCRIPT_MAP.md','NEXT_SESSION_PROMPT.md'):
    p=Path('docs')/name
    old=git('show',f'5e2c0c4:{p.as_posix()}').replace('\r\n','\n')
    current=p.read_text(encoding='utf-8')
    if name=='NEXT_SESSION_PROMPT.md':
        marker='Next-session request (ready to copy):\n\n'
        old=old.split(marker,1)[0]
        current=current.split(marker,1)[0]
    changes=difflib.SequenceMatcher(None,old.splitlines(keepends=True),current.splitlines(keepends=True),autojunk=False)
    assert all(tag in ('equal','insert') for tag,*_ in changes.get_opcodes()),name

checks=json.loads((base/'check-summary.json').read_text(encoding='utf-8'))
for report in checks['reports']+[checks['sourceManifest']]:
    assert hashlib.sha256(Path(report['path']).read_bytes()).hexdigest()==report['sha256'],report['path']
protected=json.loads(Path('Exe/RuntimeCorrespondenceSession-20261002/protected-before.json').read_text(encoding='utf-8'))
assert len(protected)==57
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in protected.items())
staged=git('diff','--cached','--name-only').splitlines()
for name in staged:
    assert name not in ('.verification/verb-area/VerbAreaCheck/Program.cs','tests/SCUMMexplorer.Tests/LoomObjectLookAuditTests.cs')
    assert not name.startswith(('.verification/object-click-room17/','.verification/room-object-interaction/'))
old_packets=git('diff','--name-only','5e2c0c4','--','.verification').splitlines()
assert all(name=='.verification/verb-area/VerbAreaCheck/Program.cs' or name.startswith('.verification/archive/runtime-correspondence-20261002/') for name in old_packets)
print(f'Historical documentation retained;{len(list(base.glob("*.py")))} Python scripts parsed;report hashes exact;57 protected files unchanged/unstaged;old packets unchanged.')
