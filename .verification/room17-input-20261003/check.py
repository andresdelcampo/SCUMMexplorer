"""Audit accepted watcher input and completed scopes without promoting fresh roots."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

base = Path('.verification/room17-input-20261003')
read = lambda p: json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
packet_path = Path('Exe/Room17Input-20261003/cycle-2/input.json')
p = read(packet_path)
catalog_path = Path('src/SCUMMexplorer.App/loom-replays.json')
catalog = read(catalog_path)
pairs = [('0022','0042'),('004D','006D')]
boundaries = [line for start,end in pairs for line in (
    f'cutscene at Room 17 local script 200 +0x{start} runs script 2',
    f'endCutscene at Room 17 local script 200 +0x{end} runs script 3')]
assert p['failure'] is None and p['frames']==241 and p['boundaries']==boundaries
assert p['sourceFiles']==catalog['sourceFiles'] and p['rooms']==[]
failed_path=Path('Exe/Room17Input-20261003/cycle-1/input.json')
failed=read(failed_path)
assert failed['failure'] and failed['frames']==3041 and failed['boundaries']==[]
assert 'not sent, because the point is outside the room picture' in failed['actions'][0]['result']['Uncertainties'][0]
assert [(a['x'],a['y']) for a in p['actions']]==[(312,72),(300,90)]
exit=next(o for o in p['directory'] if o['Id']==353)
assert (exit['X']+exit['Width']//2,exit['Y']+exit['Height']//2)==(312,72)
assert (exit['WalkX'],exit['WalkY'])==(336,141)
box=next(b for b in p['geometry']['Boxes'] if b['Index']==3)
assert tuple(box['UpperLeft'][k]+(box['LowerLeft'][k]-box['UpperLeft'][k])//3 for k in ('X','Y'))==(300,90)
for a in p['actions']:
    assert a['result']['IsGameInput'] and not any('not sent, because' in u for u in a['result']['Uncertainties'])
    assert any(f"a single click at ({a['x']}, {a['y']}) runs script 5" in t for t in p['trace'])
assert not any('startObject runs object 353 verb 0x38' in t for t in p['trace'])
assert [r['stage'] for r in p['records']]==['arrival','toward-exit353','return-upper-path']
assert [[(a['X'],a['Y'],a['WalkBox']) for a in r['actors'] if a['Number']==1] for r in p['records']]==[
    [(161,82,0)],[(269,111,6)],[(311,94,3)]]
for r in p['records']:
    assert r['snapshot']['RoomId']==17 and r['status']['CanEdit']
    assert not any(re.search('unsupported|unmodelled',u,re.I) for u in r['snapshot']['Uncertainties'])
    for original,current in zip(p['records'][0]['status']['Drafts'],r['status']['Drafts'],strict=True):
        assert original['Notes']==current['Notes'] and original['Reversible']==current['Reversible']
assert not p['records'][-1]['snapshot']['IsFaithful']
body=[e for e in p['events'] if e['SourceLabel']=='Room 17 local script 200']
assert len(body)==15 and all(e['Executed'] and e['Certainty']==1 for e in body)
code_path=Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')
assert digest(code_path)=='0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569'
code=[line.split('\t') for line in code_path.read_text(encoding='utf-8-sig').splitlines()]
inventory={(r[0],r[1]) for r in code if len(r)>=3 and r[2]=='cutscene'}
assert len(inventory)==235
for offset,box in [('001B',4),('0046',5)]:
    assert any(r[:3]==['Room 17 local 200','+0x'+offset,'isEqual'] and f'0x{box:04X}' in r[3] for r in code)
assert any(r[:3]==['Room 17 entry','+0x0080','startScript'] and '0xC8' in r[3] for r in code)
def sites(lines):
    result=set()
    for line in lines:
        m=re.fullmatch(r'cutscene at (.+) (\+0x[0-9A-F]+) runs script 2',line)
        if not m: continue
        owner=re.sub(r'^Script (\d+)$',lambda x:f'script {int(x[1]):03}',m[1])
        result.add((owner.replace(' local script ',' local ').replace(' Object ',' object '),m[2]))
    assert result<=inventory
    return result
literal_path=Path('Exe/EstimatedRoomLoads-20261002/audits/literal/audit.json')
literal=set().union(*(sites(r['sceneTrace']) for r in read(literal_path)['results'] if r['status']=='Validated state preview'))
before=literal|set().union(*(sites(r['source'].splitlines()) for r in catalog['replays'] if 'steps' in r))
prior_paths=[Path(x) for x in ('Exe/Room34Sequences-20261002/input-2/keyboard.json','Exe/Room25Input-20261002/cycle-1/input.json','Exe/Room23Input-20261002/cycle-2/input.json','Exe/Room10Input-20261003/cycle-2/input.json','Exe/Room36Input-20261003/cycle-2/input.json','Exe/Room24Input-20261003/cycle-1/input.json','Exe/Room15Input-20261003/cycle-1/input.json')]
for path in prior_paths:
    q=read(path)
    assert q['failure'] is None
    before |= sites(q['boundaries'])
assert len(literal)==169 and len(before)==201
assert inventory-before=={(r['owner'],r['offset']) for r in read('.verification/room15-input-20261003/remaining-sites.json')}
after=before|sites(p['boundaries'])
assert len(after-before)==2 and len(after)==203
protected=read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(f)==h.lower() for f,h in protected.items())
rtk='C:/Users/adelc/AppData/Local/Microsoft/WinGet/Links/rtk.exe'
git=lambda *args: subprocess.check_output([rtk,'proxy','git',*args],text=True)
assert not set(git('diff','--cached','--name-only').splitlines()).intersection(protected)
assert not git('diff','HEAD','--','src/SCUMMexplorer.Core','src/SCUMMexplorer.App/loom-replays.json')
preserved=read(base/'preserved-before.json')
assert all(digest(f)==h for f,h in preserved.items())
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name'])==f['sha256'].lower()
summary=dict(outcome='Room17 watcher pair completes through visible input points; off-picture first attempt retained.',frames=241,completedScopes=2,conditionalExecutedBodyEvents=15,newSites=[dict(owner=o,offset=a) for o,a in sorted(after-before)],literalCoverage=dict(validated=169,total=235,percent=71.91,unchanged=True),auditedSiteUnion=dict(before=201,after=203,total=235,percent=86.38,remaining=32),diagnosticCycles=2,protectedExactUnstaged=57,preservedFiles=len(preserved),productionAndCatalogUnchanged=True,limitations=['Catalog arrival/default preview are exploration assumptions.','Core API only; no WPF, physical-input or native-game claim.','All recorded watcher events Conditional; no fabricated direct-script control.','No fresh single-action promotion.'],evidence={str(f):digest(f) for f in [packet_path,failed_path,literal_path,code_path,catalog_path]+prior_paths})
for name,value in [('summary.json',summary),('remaining-sites.json',[dict(owner=o,offset=a) for o,a in sorted(inventory-after)])]:
    (base/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
print(json.dumps(summary,indent=2))
