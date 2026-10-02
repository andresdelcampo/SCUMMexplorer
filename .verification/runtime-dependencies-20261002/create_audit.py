"""Record new copied dependencies using the exact previous native recipe harness."""
from pathlib import Path
import runpy
import sys

out = Path(sys.argv[1])
expanded = '--expanded' in sys.argv[2:]
sys.argv = ['create_audit.py', str(out)] + (['--expanded'] if expanded else [])
runpy.run_path('.verification/runtime-access-20261002/create_audit.py', run_name='__main__')
p = out / 'Program.cs'
source = p.read_text(encoding='utf-8')
old = 'match.WriterEnd.Sequence,match.WriterEnd.ReachedStop,'
assert source.count(old) == 1
source = source.replace(old, 'match.WriterEnd.Sequence,Run=match.WriterEnd.Invocation,match.WriterEnd.ParentSlot,match.WriterEnd.ReachedStop,')
p.write_text(source, encoding='utf-8')
print('Selected native dependencies and independent end invocation/parent recorded; all recipe actions unchanged.')
