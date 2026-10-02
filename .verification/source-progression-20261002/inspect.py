from pathlib import Path
import json, sys

if sys.argv[1] == 'code':
    terms = sys.argv[2:]
    for line in Path('Exe/VerificationSession-20261001/native-code-1/code.tsv').read_text(encoding='utf-8').splitlines():
        if any(term in line.split('\t')[0] for term in terms):
            print(line)
elif sys.argv[1] == 'find-code':
    for line in Path('Exe/VerificationSession-20261001/native-code-1/code.tsv').read_text(encoding='utf-8').splitlines():
        if all(term in line for term in sys.argv[2:]): print(line)
elif sys.argv[1] == 'trace':
    for name in sys.argv[2:]:
        packet = json.loads(Path(name).read_text(encoding='utf-8'))
        print(name)
        for line in packet['trace'] + packet['uncertainties']:
            if line.startswith(('Room 01', 'Script 113', 'Script 114')):
                continue
            if any(term in line for term in ('seeded', 'estimate', 'possible', 'not modeled', 'unresolved')):
                print(line)
elif sys.argv[1] == 'actions':
    packet = json.loads(Path(sys.argv[2]).read_text(encoding='utf-8'))
    def walk(value):
        if isinstance(value, dict):
            for key, child in value.items():
                if key == 'Trace':
                    for line in child:
                        if 'branch' in line or 'estimate' in line:
                            print(line)
                else:
                    walk(child)
        elif isinstance(value, list):
            for child in value:
                walk(child)
    walk(packet['actions'])
elif sys.argv[1] == 'input':
    p=json.loads(Path(sys.argv[2]).read_text(encoding='utf-8'))
    for a in p['actions']:
        if 'Input' in a: print(a['Input'], a.get('ObjectId'), a.get('VerbId'), a.get('Uncertainties'))
        if 'directory' in a:
            print([o for o in a['directory'] if o['Id'] in (270,876)])
    for s in p['snapshots']:
        if s['frame']==0 or s['stage']=='final':
            v=s['snapshot']['KnownGlobalVariables']
            print(s['stage'],{k:v.get(k) for k in ('var[0x064]','var[0x067]','var[0x062]','bit[0xD61]','bit[0xD66]','bit[0xD6B]')},s['status']['Drafts'][0])
elif sys.argv[1] == 'events':
    p=json.loads(Path(sys.argv[2]).read_text(encoding='utf-8'))
    for e in p.get('events',[]):
        if any(term in str(e) for term in sys.argv[3:]): print(e)
elif sys.argv[1] == 'state':
    p=json.loads(Path(sys.argv[2]).read_text(encoding='utf-8'))
    s=p['snapshots'][-1]
    print({k:v for k,v in s['snapshot']['KnownGlobalVariables'].items() if k in sys.argv[3:]})
    print('estimated:',[v for v in s['estimatedVariables'] if v in sys.argv[3:]])
    print('pending:',s['pending'])
