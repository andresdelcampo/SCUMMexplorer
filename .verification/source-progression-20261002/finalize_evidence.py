"""Split compact route evidence; preserve failed/early packet digests without game dumps."""
from pathlib import Path
import json, hashlib
base=Path('.verification/source-progression-20261002')
routes=json.loads((base/'route-summary.json').read_text(encoding='utf-8'))
groups={
    'room41':lambda s:'41-' in s['id'],
    'late096':lambda s:s['id'].startswith('late096'),
    'room20':lambda s:s['id'].startswith('sleep-source'),
    'native-challenge':lambda s:s['id']=='native-boot-challenge',
    'native-level4':lambda s:s['id'].startswith('native-egg'),
}
for name,select in groups.items():
    rows=[s for s in routes if select(s)]
    assert rows,name
    (base/(name+'-summary.json')).write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
history=[]
for folder in sorted(Path('Exe/SourceProgressionSession-20261002').iterdir()):
    if not folder.is_dir(): continue
    for path in sorted(folder.glob('*.json')):
        p=json.loads(path.read_text(encoding='utf-8'))
        if 'snapshots' not in p: continue
        s=p['snapshots'][-1]
        history.append(dict(packet=path.as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
            route=p['route'],room=s['snapshot']['RoomId'],boundaries=p['boundaries'],
            failure=p.get('failure'),elapsed=p['elapsed']))
(base/'historical-packets.json').write_text(json.dumps(history,indent=2)+'\n',encoding='utf-8')
print('Grouped',len(routes),'final routes; retained',len(history),'packet digests.')
(base/'route-summary.json').unlink()
