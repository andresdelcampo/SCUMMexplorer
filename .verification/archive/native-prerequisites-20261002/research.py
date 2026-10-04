"""Read loaded source/probe packets; never alter runtime state."""
from pathlib import Path
import json, sys

code = Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')
if sys.argv[1] == 'code':
    for line in code.read_text(encoding='utf-8').splitlines():
        if any(term in line.split('\t')[0] for term in sys.argv[2:]):
            print(line)
elif sys.argv[1] == 'find':
    for line in code.read_text(encoding='utf-8').splitlines():
        if all(term in line for term in sys.argv[2:]): print(line)
elif sys.argv[1] == 'read':
    lines = Path(sys.argv[2]).read_text(encoding='utf-8-sig').splitlines()
    for i in range(int(sys.argv[3])-1, min(int(sys.argv[4]), len(lines))):
        print(f'{i+1}: {lines[i]}')
elif sys.argv[1] == 'packet':
    p = json.loads(Path(sys.argv[2]).read_text(encoding='utf-8'))
    print('elapsed',p['elapsed'],'boundaries',p['boundaries'],'room changes',p['roomChanges'])
    for s in p['snapshots']:
        if s['frame'] != 0 and s['stage'] != 'final': continue
        v = s['snapshot']['KnownGlobalVariables']
        keys = ['var[0x07A]','var[0x032]','var[0x034]','var[0x036]','var[0x03A]','var[0x03B]','bit[0xD68]','bit[0xD69]','bit[0xD6F]']
        print(s['stage'], 'room',s['snapshot']['RoomId'], {k:v.get(k) for k in keys}, 'estimated', [k for k in keys if k in s['estimatedVariables']])
    for line in p['trace']:
        if any(t in line for t in sys.argv[3:]): print(line)
elif sys.argv[1] == 'events':
    p=json.loads(Path(sys.argv[2]).read_text(encoding='utf-8'))
    for e in p['events']:
        if any(t in str(e) for t in sys.argv[3:]): print(e)
