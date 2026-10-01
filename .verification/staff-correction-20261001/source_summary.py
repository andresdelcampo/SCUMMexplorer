from pathlib import Path
lines = Path('Exe/VerificationSession-20261001/native-code-1/code.tsv').read_text(encoding='utf-8').splitlines()
sources = {}
for line in lines:
    parts = line.split('\t')
    if len(parts) == 4: sources.setdefault(parts[0], []).append(parts)
for source, rows in sources.items():
    if any(r[2] in ('startScript','chainScript') and r[3].startswith('0x1D,') for r in rows):
        print(source)
        for r in rows:
            if r[2] in ('startScript','chainScript','loadRoom','loadRoomWithEgo') or 'var[0x07A]' in r[3]: print('\t'.join(r[1:]))
for name in ('Room 47 entry','Room 46 entry','Room 41 entry','Room 41 object 633 Verb 0x39','Room 41 object 633 Verb 0x41','Room 41 local 203','Room 41 local 212','script 096'):
    print(name)
    for r in sources.get(name,[]): print('\t'.join(r[1:]))
