"""Audit completed watcher input; retain the unsuccessful first cycle separately."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

base = Path('.verification/room23-input-20261002')
read = lambda p: json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
packet_path = Path('Exe/Room23Input-20261002/cycle-2/input.json')
failed_path = Path('Exe/Room23Input-20261002/cycle-1/input.json')
p, failed = read(packet_path), read(failed_path)
catalog = read('src/SCUMMexplorer.App/loom-replays.json')
assert failed['failure'] is not None and failed['boundaries'] == [] and failed['frames'] == 3041
assert [(a['x'],a['y']) for a in failed['actions']] == [(181,123)]
assert p['failure'] is None and p['frames'] == 1393 and p['sourceFiles'] == catalog['sourceFiles']
expected = [(202,'0000','01BD'), (214,'0000','018A'), (200,'0020','004D'), (200,'0084','00A8'), (200,'00B3','00EE'), (200,'0058','0079')]
assert p['boundaries'] == [s for n,start,end in expected for s in (
    f'cutscene at Room 23 local script {n} +0x{start} runs script 2',
    f'endCutscene at Room 23 local script {n} +0x{end} runs script 3')]
assert [r['stage'] for r in p['records']] == ['arrival','walk-box-3','walk-box-2','walk-box-6','walk-box-9','walk-box-10','walk-box-7']
assert [a['index'] for a in p['actions']] == [3,2,6,9,10,7]
for a in p['actions']:
    b = next(b for b in p['geometry']['Boxes'] if b['Index'] == a['index'])
    xy = (b['UpperLeft']['X'],b['UpperLeft']['Y']) if a['leftEndpoint'] else tuple(sum(c[k] for c in b['Corners'])//4 for k in ('X','Y'))
    assert (a['x'],a['y']) == xy
    assert a['result']['IsGameInput'] and not any('not sent, because' in u for u in a['result']['Uncertainties'])
assert p['actions'][0]['x'] < 125 and p['actions'][1]['x'] > 242
assert p['rooms'] == []
for r in p['records']:
    assert r['snapshot']['RoomId'] == 23 and r['status']['CanEdit']
    assert not any(re.search('unsupported|unmodelled', u, re.I) for u in r['snapshot']['Uncertainties'])
    for original,current in zip(p['records'][0]['status']['Drafts'],r['status']['Drafts'],strict=True):
        assert original['Notes'] == current['Notes'] and original['Reversible'] == current['Reversible']
assert not p['records'][-1]['snapshot']['IsFaithful']
assert [(a['X'],a['Y'],a['WalkBox']) for a in p['records'][-1]['actors'] if a['Number']==1] == [(180,120,5)]
watcher_calls = [e for e in p['events'] if e['Operation']=='chainScript' and e['SourceLabel'] in ('Room 23 local script 201','Room 23 local script 213')]
assert [(e['SourceScriptId'],e['Offset'],e['TargetId']) for e in watcher_calls] == [(201,13,202),(213,34,214)]
body = [e for e in p['events'] if e['SourceLabel'] in ('Room 23 local script 200','Room 23 local script 202','Room 23 local script 214')]
assert len(body)==139 and all(e['Executed'] and e['Certainty']==1 for e in body+watcher_calls)
code_path = Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')
assert digest(code_path)=='0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569'
inventory = {(r[0],r[1]) for line in code_path.read_text(encoding='utf-8-sig').splitlines() if len(r:=line.split('\t'))>=3 and r[2]=='cutscene'}
assert len(inventory)==235
def sites(lines):
    result=set()
    for b in lines:
        m=re.fullmatch(r'cutscene at (.+) (\+0x[0-9A-F]+) runs script 2',b)
        if not m: continue
        owner=re.sub(r'^Script (\d+)$',lambda x:f'script {int(x[1]):03}',m[1])
        result.add((owner.replace(' local script ',' local ').replace(' Object ',' object '),m[2]))
    assert result<=inventory
    return result
literal_path=Path('Exe/EstimatedRoomLoads-20261002/audits/literal/audit.json')
literal=set().union(*(sites(r['sceneTrace']) for r in read(literal_path)['results'] if r['status']=='Validated state preview'))
before=literal|set().union(*(sites(r['source'].splitlines()) for r in catalog['replays'] if 'steps' in r))
for path in ('Exe/Room34Sequences-20261002/input-2/keyboard.json','Exe/Room25Input-20261002/cycle-1/input.json'):
    before |= sites(read(path)['boundaries'])
assert len(literal)==169 and len(before)==182
assert inventory-before=={(r['owner'],r['offset']) for r in read('.verification/room25-input-20261002/remaining-sites.json')}
after=before|sites(p['boundaries'])
assert len(after-before)==6 and len(after)==188
protected=read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(f)==h.lower() for f,h in protected.items())
assert not set(subprocess.check_output(['git','diff','--cached','--name-only'],text=True).splitlines()).intersection(protected)
assert not subprocess.check_output(['git','diff','HEAD','--','src/SCUMMexplorer.Core','src/SCUMMexplorer.App/loom-replays.json'],text=True)
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name'])==f['sha256'].lower()
summary=dict(outcome='Six Room23 watcher sites complete through actual walking input; no production fix.',
    frames=1393,completedScopes=6,conditionalExecutedBodyEvents=139,watcherCalls=watcher_calls,
    inputPoints=[dict(box=a['index'],x=a['x'],y=a['y']) for a in p['actions']],
    newSites=[dict(owner=o,offset=a) for o,a in sorted(after-before)],
    literalCoverage=dict(validated=169,total=235,percent=71.91,unchanged=True),
    auditedSiteUnion=dict(before=182,after=188,total=235,percent=80.00,remaining=47),
    diagnosticCycles=2,firstCycle='Box3 centroid181,123 does not meet source x<125; zero scene starts, retained failure.',
    protectedExactUnstaged=57,productionAndCatalogUnchanged=True,
    limitations=['Default preview boot and catalog Room23 arrival are assumptions.',
        'Core input API, not WPF or physical input.', 'These watcher sites have no direct object-verb trigger; no direct-control equivalence claimed.',
        'All recorded body events retain Conditional provenance; no native full-game proof.',
        'Multi-action completion does not qualify fresh single-action gaps.'],
    evidence={str(f):digest(f) for f in (packet_path,failed_path,literal_path,code_path,Path('src/SCUMMexplorer.App/loom-replays.json'))})
(base/'summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
(base/'remaining-sites.json').write_text(json.dumps([dict(owner=o,offset=a) for o,a in sorted(inventory-after)],indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:summary[k] for k in ('frames','completedScopes','conditionalExecutedBodyEvents','newSites','literalCoverage','auditedSiteUnion','protectedExactUnstaged')},indent=2))
