"""Read the checkpoint's source instructions without exporting game code to git."""
from pathlib import Path
import sys

path = Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')
names = sys.argv[1:]
for line in path.read_text(encoding='utf-8').splitlines():
    owner = line.split('\t', 1)[0]
    if any(owner == name or (name.endswith('*') and owner.startswith(name[:-1])) or (name.startswith('?') and name[1:] in line) for name in names):
        print(line)
