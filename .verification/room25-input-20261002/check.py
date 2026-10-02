"""Qualify Room25 game-input evidence without changing historical captures."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

base = Path('.verification/room25-input-20261002')
out = Path('Exe/Room25Input-20261002/cycle-1')
read = lambda p: json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
direct, input_run = [read(out / f'{mode}.json') for mode in ('direct', 'input')]
catalog = read('src/SCUMMexplorer.App/loom-replays.json')
expected = [
    ('Room 25 local script 200', '0058', '007E'),
    ('Script 69', '0000', '07ED'),
    ('Room 25 local script 204', '0000', '03C5'),
    ('Room 25 local script 205', '0000', '00B0'),
    ('Room 25 local script 206', '0000', '010C'),
    ('Room 25 local script 208', '0000', '0060'),
]
boundaries = [s for label, start, end in expected for s in (
    f'cutscene at {label} +0x{start} runs script 2',
    f'endCutscene at {label} +0x{end} runs script 3')]
flags = ['bit[0xD92]', 'bit[0xD93]', 'bit[0xDA6]', 'bit[0xDA7]', 'bit[0xDA8]', 'bit[0xDBB]']
for p in (direct, input_run):
    assert p['failure'] is None and p['sourceFiles'] == catalog['sourceFiles']
    assert p['boundaries'] == boundaries
    assert p['records'][-1]['snapshot']['RoomId'] == 24
    assert p['records'][-1]['status']['CanEdit']
    assert not p['records'][-1]['snapshot']['IsFaithful']
    assert [r['stage'] for r in p['records']] == ['arrival', 'walk-box-4', 'walk-box-3', 'lamb-first', 'lamb-second', 'lamb-third', 'leave']
    assert all(not any(re.search('unsupported|unmodelled', u, re.I) for u in r['snapshot']['Uncertainties']) for r in p['records'])
    assert [r['room'] for r in p['rooms']] == [55, 25, 24, 25, 24]
    assert all(p['records'][-1]['snapshot']['KnownGlobalVariables'][f] == 1 for f in flags)
    assert [a['index'] for a in p['actions'][:2]] == [4, 3]
    for a in p['actions'][:2]:
        b = next(b for b in p['geometry']['Boxes'] if b['Index'] == a['index'])
        assert (a['x'], a['y']) == tuple(sum(c[k] for c in b['Corners']) // 4 for k in ('X', 'Y'))
    # No Game Status edits, note rewrites or reverse-flag edits on this route.
    original = p['records'][0]['status']['Drafts']
    for r in p['records']:
        for before, after in zip(original, r['status']['Drafts'], strict=True):
            assert before['Notes'] == after['Notes'] and before['Reversible'] == after['Reversible']
assert all(a['result']['IsGameInput'] for a in input_run['actions'])
assert all(not any('not sent, because' in u for u in a['result']['Uncertainties']) for a in input_run['actions'])
assert [a['result']['IsGameInput'] for a in direct['actions']] == [True, True, False, False, False, False]
labels = {label for label, _, _ in expected}
bodies = [[e for e in p['events'] if e['SourceLabel'] in labels] for p in (direct, input_run)]
assert bodies[0] == bodies[1] and len(bodies[0]) == 208
assert all(e['Executed'] for e in bodies[1])
dispatch = [e for e in input_run['events'] if e['Operation'] == 'startObject' and e['ResolvedArguments'] in ([447, 54], [446, 55])]
assert [(e['SourceLabel'], e['Offset'], e['ResolvedArguments'], e['Executed']) for e in dispatch] == [
    ('Script 07', 46, [447, 54], True)] * 3 + [('Script 06', 347, [446, 55], True)]
calls = [e for e in input_run['events'] if e['Operation'] == 'startScript' and e['TargetId'] in (204,205,206,208) and 'Object' in e['SourceLabel']]
assert [e['TargetId'] for e in calls] == [204,205,206,208]
assert all(e['Executed'] for e in calls)
code_path = Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')
assert digest(code_path) == '0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569'
inventory = {(r[0], r[1]) for line in code_path.read_text(encoding='utf-8-sig').splitlines() if len(r := line.split('\t')) >= 3 and r[2] == 'cutscene'}
assert len(inventory) == 235
def sites(lines):
    result = set()
    for b in lines:
        m = re.fullmatch(r'cutscene at (.+) (\+0x[0-9A-F]+) runs script 2', b)
        if not m: continue
        owner = re.sub(r'^Script (\d+)$', lambda x: f'script {int(x[1]):03}', m[1])
        result.add((owner.replace(' local script ', ' local ').replace(' Object ', ' object '), m[2]))
    assert result <= inventory
    return result
literal_path = Path('Exe/EstimatedRoomLoads-20261002/audits/literal/audit.json')
literal = set().union(*(sites(r['sceneTrace']) for r in read(literal_path)['results'] if r['status'] == 'Validated state preview'))
before = literal | set().union(*(sites(r['source'].splitlines()) for r in catalog['replays'] if 'steps' in r)) | sites(read('Exe/Room34Sequences-20261002/input-2/keyboard.json')['boundaries'])
assert len(literal) == 169 and len(before) == 176
assert inventory - before == {(r['owner'], r['offset']) for r in read('.verification/room34-sequences-20261002/remaining-sites.json')}
after = before | sites(input_run['boundaries'])
new = after - before
assert len(new) == 6 and len(after) == 182
protected = read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected) == 57 and all(digest(p) == h.lower() for p,h in protected.items())
assert not set(subprocess.check_output(['git', 'diff', '--cached', '--name-only'], text=True).splitlines()).intersection(protected)
assert not subprocess.check_output(['git', 'diff', 'HEAD', '--', 'src/SCUMMexplorer.Core', 'src/SCUMMexplorer.App/loom-replays.json'], text=True)
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name']) == f['sha256'].lower()
summary = dict(outcome='Six Room25 sites complete through actual walking, repeated lamb double-clicks and exit input; no production fix.',
    directFrames=direct['frames'], inputFrames=input_run['frames'], matchedScopes=6, identicalRecordedBodyEvents=208,
    dispatches=dispatch, downstreamObjectCalls=calls,
    newSites=[dict(owner=o, offset=a) for o,a in sorted(new)],
    literalCoverage=dict(validated=169, total=235, percent=round(169/235*100,2), unchanged=True),
    auditedSiteUnion=dict(before=176, after=182, total=235, percent=round(182/235*100,2), remaining=53),
    protectedExactUnstaged=57, productionAndCatalogUnchanged=True,
    limitations=['Default preview boot and catalog Room25 arrival are assumptions.',
        'Public Core input API, not WPF or physical input.', 'Direct control shares the two initial walking actions.',
        'All estimates are retained; recorded body equivalence is not whole-runtime or frame equivalence.',
        'Multi-action completion does not qualify fresh single-action gaps.'],
    evidence={str(p):digest(p) for p in [out/'direct.json', out/'input.json', literal_path, code_path, Path('src/SCUMMexplorer.App/loom-replays.json')]})
(base/'summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
(base/'remaining-sites.json').write_text(json.dumps([dict(owner=o, offset=a) for o,a in sorted(inventory-after)], indent=2)+'\n', encoding='utf-8')
print(json.dumps({k:summary[k] for k in ('matchedScopes','identicalRecordedBodyEvents','newSites','literalCoverage','auditedSiteUnion','protectedExactUnstaged')}, indent=2))
