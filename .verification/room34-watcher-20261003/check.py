import hashlib,json,re,subprocess
from pathlib import Path
base=Path('.verification/room34-watcher-20261003')
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
packet_path=Path('Exe/Room34Watcher-20261003/cycle-2/input.json'); direct_path=packet_path.with_name('direct.json')
p,d=read(packet_path),read(direct_path)
catalog_path=Path('src/SCUMMexplorer.App/loom-replays.json'); catalog=read(catalog_path)
failed_paths=[Path(f'Exe/Room34Watcher-20261003/cycle-{c}/{m}.json') for c in (1,2) for m in ('direct','input')]
for f in failed_paths:
    q=read(f); assert q['frames']==3041 and q['sourceFiles']==catalog['sourceFiles']
    assert not any(a.get('userStatusEdits') for a in q['actions'])
    if 'cycle-1' in str(f):
        assert 'source-087: bounded settling failed' in q['failure']
        assert q['boundaries']==['cutscene at Script 87 +0x0000 runs script 2']
        assert [r['room'] for r in q['rooms']]==[34,68,38,37]
        assert q['records'][-1]['snapshot']['RoomId']==37
        assert q['records'][-1]['snapshot']['KnownGlobalVariables']['bit[0xD9D]']==1
    else:
        assert 'exit-599: bounded settling failed' in q['failure']
        assert q['boundaries']==[] and q['rooms']==[]
        assert q['records'][-1]['snapshot']['RoomId']==36
        assert all(r['snapshot']['ObjectClasses']['599']==8388610 for r in q['records'])
        ego=next(a for a in q['records'][-1]['actors'] if a['Number']==1)
        assert (ego['X'],ego['Y'],ego['WalkBox'])==(472,126,12)
        assert not any(t=='Script 07 +0x002E: startObject runs object 599 verb 0x38' for t in q['trace'])
differences=[]
code_path=Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')
assert digest(code_path)=='0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569'
code=[line.split('\t') for line in code_path.read_text(encoding='utf-8-sig').splitlines()]
inventory={(r[0],r[1]) for r in code if len(r)>=3 and r[2]=='cutscene'}
assert len(inventory)==235
assert any(r[:3]==['Room 36 local 200','+0x0094','setClass'] and r[3]=='0x0257, [0x0018]' for r in code)
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
prior_paths=[Path(x) for x in ('Exe/Room34Sequences-20261002/input-2/keyboard.json','Exe/Room25Input-20261002/cycle-1/input.json','Exe/Room23Input-20261002/cycle-2/input.json','Exe/Room10Input-20261003/cycle-2/input.json','Exe/Room36Input-20261003/cycle-2/input.json','Exe/Room24Input-20261003/cycle-1/input.json','Exe/Room15Input-20261003/cycle-1/input.json','Exe/Room17Input-20261003/cycle-2/input.json','Exe/Room22Input-20261003/cycle-2/input.json','Exe/Room08Input-20261003/cycle-1/input.json','Exe/Room28Input-20261003/cycle-1/input.json','Exe/Room62Input-20261003/cycle-1/input.json','Exe/Room33Input-20261003/cycle-1/input.json','Exe/Room47Input-20261003/cycle-1/input.json','Exe/Room20Input-20261003/cycle-2/input.json')]
for path in prior_paths:
    q=read(path)
    assert q['failure'] is None
    before |= sites(q['boundaries'])
assert len(literal)==169 and len(before)==215
assert inventory-before=={(r['owner'],r['offset']) for r in read('.verification/room20-input-20261003/remaining-sites.json')}
after=before
assert len(after-before)==0 and len(after)==215
protected=read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(f)==h.lower() for f,h in protected.items())
rtk='C:/Users/adelc/AppData/Local/Microsoft/WinGet/Links/rtk.exe'
git=lambda *args: subprocess.check_output([rtk,'proxy','git',*args],text=True)
assert not set(git('diff','--cached','--name-only').splitlines()).intersection(protected)
assert not git('diff','HEAD','--','src/SCUMMexplorer.Core','src/SCUMMexplorer.App/loom-replays.json')
preserved=read(base/'preserved-before.json')
assert all(digest(f)==h for f,h in preserved.items())
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name'])==f['sha256'].lower()
summary=dict(outcome='Room34 watcher route stopped after two failed prerequisite attempts; no coverage credit.',framesPerCapture=3041,failedCaptures=4,completeRecipes=0,nextExperiment="Later session: Room36 box9 watcher opens599, then actual exit599, firewood086, Look610/087, Opening617 and exits back to34, Healing586, camera watcher. Preserve source setup and stop after two cycles.",newSites=[dict(owner=o,offset=a) for o,a in sorted(after-before)],literalCoverage=dict(validated=169,total=235,percent=71.91,unchanged=True),auditedSiteUnion=dict(before=215,after=215,total=235,percent=91.49,remaining=20),diagnosticCycles=2,protectedExactUnstaged=57,preservedFiles=len(preserved),productionAndCatalogUnchanged=True,evidence={str(f):digest(f) for f in [packet_path,direct_path,literal_path,code_path,catalog_path]+prior_paths+failed_paths})
for name,value in [('summary.json',summary),('event-differences.json',differences),('remaining-sites.json',[dict(owner=o,offset=a) for o,a in sorted(inventory-after)])]:
    (base/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:summary[k] for k in ('framesPerCapture','failedCaptures','newSites','auditedSiteUnion','protectedExactUnstaged')},indent=2))
