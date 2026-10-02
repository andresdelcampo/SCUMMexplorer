"""Audit accepted watcher input and completed scopes without promoting fresh roots."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

base = Path('.verification/room22-input-20261003')
read = lambda p: json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
packet_path = Path('Exe/Room22Input-20261003/cycle-2/input.json')
p = read(packet_path)
catalog_path = Path('src/SCUMMexplorer.App/loom-replays.json')
catalog = read(catalog_path)
pairs = [('0083','00D3'),('002F','0078')]
boundaries = [line for start,end in pairs for line in (
    f'cutscene at Room 22 local script 200 +0x{start} runs script 2',
    f'endCutscene at Room 22 local script 200 +0x{end} runs script 3')]
assert p['failure'] is None and p['frames']==235 and p['boundaries']==boundaries
assert p['sourceFiles']==catalog['sourceFiles'] and p['rooms']==[]
assert [(a['index'],a['x'],a['y']) for a in p['actions']]==[(11,97,69),(10,90,64)]
for a in p['actions']:
    box=next(b for b in p['geometry']['Boxes'] if b['Index']==a['index'])
    assert (a['x'],a['y'])==tuple(sum(c[k] for c in box['Corners'])//4 for k in ('X','Y'))
    assert a['result']['IsGameInput'] and not any('not sent, because' in u for u in a['result']['Uncertainties'])
    assert any(f"a single click at ({a['x']}, {a['y']}) runs script 5" in t for t in p['trace'])
assert [r['stage'] for r in p['records']]==['arrival','walk-box-11','walk-box-10']
assert [[(a['X'],a['Y'],a['WalkBox']) for a in r['actors'] if a['Number']==1] for r in p['records']]==[
    [(194,66,13)],[(108,56,9)],[(120,66,12)]]
for r in p['records']:
    assert r['snapshot']['RoomId']==22 and r['status']['CanEdit']
    assert not any(re.search('unsupported|unmodelled',u,re.I) for u in r['snapshot']['Uncertainties'])
    for original,current in zip(p['records'][0]['status']['Drafts'],r['status']['Drafts'],strict=True):
        assert original['Notes']==current['Notes'] and original['Reversible']==current['Reversible']
assert not p['records'][-1]['snapshot']['IsFaithful']
body=[e for e in p['events'] if e['SourceLabel']=='Room 22 local script 200']
assert len(body)==23 and all(e['Executed'] and e['Certainty']==1 for e in body)
failed_path=Path('Exe/Room22Input-20261003/cycle-1/input.json')
failed=read(failed_path)
assert failed['failure'] and failed['frames']==3041 and failed['boundaries']==boundaries[:2]
assert [(a['index'],a['x'],a['y']) for a in failed['actions']]==[(10,90,64)]
code_path=Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')
assert digest(code_path)=='0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569'
code=[line.split('\t') for line in code_path.read_text(encoding='utf-8-sig').splitlines()]
inventory={(r[0],r[1]) for r in code if len(r)>=3 and r[2]=='cutscene'}
assert len(inventory)==235
for offset,box in [('0028',10),('007C',11)]:
    assert any(r[:3]==['Room 22 local 200','+0x'+offset,'isEqual'] and f'0x{box:04X}' in r[3] for r in code)
assert any(r[:3]==['Room 22 entry','+0x0101','startScript'] and '0xC8' in r[3] for r in code)
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
prior_paths=[Path(x) for x in ('Exe/Room34Sequences-20261002/input-2/keyboard.json','Exe/Room25Input-20261002/cycle-1/input.json','Exe/Room23Input-20261002/cycle-2/input.json','Exe/Room10Input-20261003/cycle-2/input.json','Exe/Room36Input-20261003/cycle-2/input.json','Exe/Room24Input-20261003/cycle-1/input.json','Exe/Room15Input-20261003/cycle-1/input.json','Exe/Room17Input-20261003/cycle-2/input.json')]
for path in prior_paths:
    q=read(path)
    assert q['failure'] is None
    before |= sites(q['boundaries'])
assert len(literal)==169 and len(before)==203
assert inventory-before=={(r['owner'],r['offset']) for r in read('.verification/room17-input-20261003/remaining-sites.json')}
after=before|sites(p['boundaries'])
assert len(after-before)==2 and len(after)==205
protected=read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(f)==h.lower() for f,h in protected.items())
rtk='C:/Users/adelc/AppData/Local/Microsoft/WinGet/Links/rtk.exe'
git=lambda *args: subprocess.check_output([rtk,'proxy','git',*args],text=True)
assert not set(git('diff','--cached','--name-only').splitlines()).intersection(protected)
assert not git('diff','HEAD','--','src/SCUMMexplorer.Core','src/SCUMMexplorer.App/loom-replays.json')
preserved=read(base/'preserved-before.json')
assert all(digest(f)==h for f,h in preserved.items())
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name'])==f['sha256'].lower()
summary=dict(outcome='Room22 watcher pair completes from incoming box11 then box10; first failed order retained.',frames=235,completedScopes=2,conditionalExecutedBodyEvents=23,newSites=[dict(owner=o,offset=a) for o,a in sorted(after-before)],literalCoverage=dict(validated=169,total=235,percent=71.91,unchanged=True),auditedSiteUnion=dict(before=203,after=205,total=235,percent=87.23,remaining=30),diagnosticCycles=2,protectedExactUnstaged=57,preservedFiles=len(preserved),productionAndCatalogUnchanged=True,limitations=['Catalog arrival/default preview are exploration assumptions.','Core API only; no WPF, physical-input or native-game claim.','All recorded watcher events Conditional; no fabricated direct-script control.','No fresh single-action promotion.'],evidence={str(f):digest(f) for f in [packet_path,failed_path,literal_path,code_path,catalog_path]+prior_paths})
for name,value in [('summary.json',summary),('remaining-sites.json',[dict(owner=o,offset=a) for o,a in sorted(inventory-after)])]:
    (base/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
print(json.dumps(summary,indent=2))
