"""Keep small packet digests and compare the four unchanged literal failures."""
from pathlib import Path
import hashlib
import json

base = Path('.verification/source-progression-20261001')
output = Path('Exe/SourceProgressionSession-20261001')
history = []
for folder in [output / f'routes-{i}' for i in range(1, 7)] + [output / 'boot-4']:
    recipes = []
    for path in sorted(folder.glob('*.json')):
        packet = json.loads(path.read_text(encoding='utf-8'))
        recipes.append(packet['route'])
        last = packet['snapshots'][-1]
        history.append(dict(packet=path.as_posix(),
            sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
            room=last['snapshot']['RoomId'],
            boundaries=packet['boundaries'],
            stages=[dict(stage=s['stage'], frames=s['frame'],
                room=s['snapshot']['RoomId']) for s in
                {s['stage']: s for s in packet['snapshots']}.values()],
            failure=packet.get('failure')))
    if folder.name.startswith('routes-'):
        (base / (folder.name + '.json')).write_text(
            json.dumps(recipes, indent=2) + '\n', encoding='utf-8')
(base / 'historical-packets.json').write_text(
    json.dumps(history, indent=2) + '\n', encoding='utf-8')

baseline_path = Path('Exe/DraftScopeSession-20261001/final-audits-1/literal/audit.json')
current_path = output / 'incomplete-1/audit.json'
baseline = json.loads(baseline_path.read_text(encoding='utf-8'))
current = json.loads(current_path.read_text(encoding='utf-8'))
prior = {result['owner']: result for result in baseline['results']}
keys = ('status', 'frames', 'first', 'last', 'everOpen', 'begun', 'ended', 'rooms', 'sceneTrace')
comparisons = []
for result in current['results']:
    differences = [key for key in keys if result[key] != prior[result['owner']][key]]
    assert not differences, (result['owner'], differences)
    comparisons.append({key: result[key] for key in ('owner', *keys)})
assert len(comparisons) == 4
assert baseline['sourceFiles'] == current['sourceFiles']
record = dict(baseline=baseline_path.as_posix(), current=current_path.as_posix(),
    currentSha256=hashlib.sha256(current_path.read_bytes()).hexdigest(),
    sourceFilesMatch=True, allComparedFieldsMatch=True, results=comparisons)
(base / 'incomplete-comparison.json').write_text(
    json.dumps(record, indent=2) + '\n', encoding='utf-8')
print('Four literal failures match baseline; packet digests recorded.')
