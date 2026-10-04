"""Qualify input evidence without altering captures, certainty or literal coverage."""
import collections
import hashlib
import json
import re
import subprocess
from pathlib import Path

base = Path('.verification/archive/room34-sequences-20261002')
out = Path('Exe/Room34Sequences-20261002')
read = lambda p: json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
direct, keyboard = [read(out/'input-2'/f'{mode}.json') for mode in ('direct', 'keyboard')]
catalog = read('src/SCUMMexplorer.App/loom-replays.json')
assert catalog == read(out/'catalog-before.json')
for p in (direct, keyboard):
    assert p['failure'] is None and p['sourceFiles'] == catalog['sourceFiles']
    assert p['records'][-1]['status']['CanEdit']
    assert not p['records'][-1]['snapshot']['IsFaithful']
    assert not any(re.search('unsupported|unmodelled', u, re.I) for u in p['records'][-1]['snapshot']['Uncertainties'])
    setup = next(a for a in p['actions'] if a.get('userStatusEdits'))
    assert setup == dict(userStatusEdits=True, staffHeld=True, notes=6, learnedSlots=[5, 10])
    original = {d['Slot']: d for d in p['records'][0]['status']['Drafts']}
    for r in p['records']:
        if not r['stage'].endswith('-before'): continue
        assert r['status']['StaffHeld'] and r['status']['AvailableNoteCount'] >= 6
        for d in r['status']['Drafts']:
            assert d['Notes'] == original[d['Slot']]['Notes']
            assert d['Reversible'] == original[d['Slot']]['Reversible']
            if d['Slot'] in (5, 10): assert d['Learned']
    v = p['records'][-1]['snapshot']['KnownGlobalVariables']
    assert [v[k] for k in ('var[0x0D1]', 'bit[0xD98]', 'bit[0xDCC]')] == [2, 1, 1]
assert direct['boundaries'] == keyboard['boundaries'] and len(keyboard['boundaries']) == 18
labels = {'Room 34 local script 201', 'Room 34 local script 202', 'Script 85'}
body = [[e for e in p['events'] if e['SourceLabel'] in labels] for p in (direct, keyboard)]
assert len(body[0]) == len(body[1]) == 127
# Keep every full event intact. Record the exact differing field, never relabel it.
differences = []
for i, (d, k) in enumerate(zip(*body, strict=True)):
    changed = {field: dict(direct=d[field], keyboard=k[field]) for field in d if d[field] != k[field]}
    assert changed == {'Certainty': dict(direct=0, keyboard=1)}
    assert d['Executed'] and k['Executed']
    differences.append(dict(eventIndex=i, source=k['SourceLabel'], offset=k['Offset'], differences=changed))
dispatch = [e for e in keyboard['events'] if e['Operation'] == 'startObject' and e['ResolvedArguments'] in ([561,68], [561,76])]
assert [e['ResolvedArguments'] for e in dispatch] == [[561,68],[561,76],[561,68],[561,68],[561,68]]
assert all(e['SourceLabel'] == 'Script 07' and e['Offset'] == 46 and e['Executed'] and e['Certainty'] == 1 for e in dispatch)
code = Path('.verification/fallback-evidence/code.tsv').read_text(encoding='utf-8-sig').splitlines()
inventory = {(row[0], row[1]) for line in code if len(row := line.split('\t')) >= 3 and row[2] == 'cutscene'}
assert len(inventory) == 235
def sites(boundaries):
    result = set()
    for b in boundaries:
        m = re.fullmatch(r'cutscene at (.+) (\+0x[0-9A-F]+) runs script 2', b)
        if not m: continue
        owner = re.sub(r'^Script (\d+)$', lambda x: f'script {int(x[1]):03}', m[1])
        owner = owner.replace(' local script ', ' local ').replace(' Object ', ' object ')
        result.add((owner, m[2]))
    assert result <= inventory
    return result
literal_path = 'Exe/EstimatedRoomLoads-20261002/audits/literal/audit.json'
literal = set().union(*(sites(r['sceneTrace']) for r in read(literal_path)['results'] if r['status'] == 'Validated state preview'))
assert len(literal) == 169
sequence_sites = set()
for r in catalog['replays']:
    if 'steps' in r: sequence_sites |= sites(r['source'].splitlines())
before = literal | sequence_sites
new = sites(keyboard['boundaries']) - before
assert new == {('Room 34 local 201', '+0x0046'), ('Room 34 local 201', '+0x0134'), ('Room 34 local 201', '+0x0197'), ('Room 34 local 202', '+0x0000')}
after = before | new
protected = read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected) == 57 and all(digest(p) == h.lower() for p, h in protected.items())
assert not set(subprocess.check_output(['git','diff','--cached','--name-only'], text=True).splitlines()).intersection(protected)
assert not subprocess.check_output(['git','diff','HEAD','--','src/SCUMMexplorer.Core','src/SCUMMexplorer.App/loom-replays.json'], text=True)
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name']) == f['sha256'].lower()
summary = dict(outcome='Five accepted keyboard casts complete the same Room34 scene progression as direct responses; no production fix.',
    directFrames=direct['frames'], keyboardFrames=keyboard['frames'], matchedBoundaries=18, matchedSceneScopes=9,
    bodyEventCount=127, bodyDifferences='Only Certainty differs: all direct Reached(0), all keyboard Conditional(1); every event remains Executed.',
    dispatches=dispatch, newSites=[dict(owner=o, offset=a) for o,a in sorted(new)],
    literalCoverage=dict(validated=169, total=235, percent=round(169/235*100,2), unchanged=True),
    auditedSiteUnion=dict(before=len(before), after=len(after), total=235, percent=round(len(after)/235*100,2),
        remaining=len(inventory-after), definition='Union of completed literal previews, the two established audited catalog sequences, and this completed input comparison; not native full-game certainty.'),
    protectedExactUnstaged=57, productionAndCatalogUnchanged=True,
    limitations=['Catalog Room34 arrival and default preview boot remain assumed.', 'Supported staff/notes/learned edits are explicit exploration choices.',
        'All keyboard body events retain Conditional provenance; no estimate clearing.', 'Core input API only; no WPF or physical-input verification.',
        'No frame-by-frame timing equivalence; input/walking/note animation legitimately adds time.'],
    evidence={str(p):digest(p) for p in [out/'input-2/direct.json',out/'input-2/keyboard.json',Path(literal_path),Path('src/SCUMMexplorer.App/loom-replays.json')]})
(base/'summary.json').write_text(json.dumps(summary, indent=2)+'\n',encoding='utf-8')
(base/'event-differences.json').write_text(json.dumps(differences, indent=2)+'\n',encoding='utf-8')
(base/'remaining-sites.json').write_text(json.dumps([dict(owner=o, offset=a) for o,a in sorted(inventory-after)],indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:summary[k] for k in ('matchedSceneScopes','newSites','literalCoverage','auditedSiteUnion','protectedExactUnstaged')},indent=2))
