import hashlib,json,re,subprocess
from pathlib import Path
base=Path('.verification/archive/room47-input-20261003')
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
packet_path=Path('Exe/Room47Input-20261003/cycle-1/input.json'); direct_path=packet_path.with_name('direct.json')
p,d=read(packet_path),read(direct_path)
catalog_path=Path('src/SCUMMexplorer.App/loom-replays.json'); catalog=read(catalog_path)
expected=[('Room 47 local script 205','000E','019E'),('Script 100','0000','0A7B'),('Room 47 local script 205','01B7','03F1')]
boundaries=[s for owner,start,end in expected for s in (f'cutscene at {owner} +0x{start} runs script 2',f'endCutscene at {owner} +0x{end} runs script 3')]
for q in (p,d):
    assert q['failure'] is None and q['frames']==2423 and q['boundaries']==boundaries
    assert q['sourceFiles']==catalog['sourceFiles']
    assert [(r['frames'],r['room']) for r in q['rooms']]==[(496,61),(1063,47),(1438,61),(1763,47),(2178,46),(2223,47)]
    assert [(r['stage'],r['frames'],r['snapshot']['ObjectClasses']['703']) for r in q['records']]==[('arrival',348,8388608),('conversation',2149,0),('leave-room47',2219,0),('return-room47',2392,0),('return-response',2423,0)]
    for r in q['records']:
        assert r['status']['CanEdit']
        assert r['snapshot']['RoomId']==(46 if r['stage']=='leave-room47' else 47)
        assert not any(re.search('unsupported|unmodelled',u,re.I) for u in r['snapshot']['Uncertainties'])
        assert r['snapshot']['KnownGlobalVariables']['var[0x0D3]']==1
        assert 'bit[0xDC5]' not in r['snapshot']['KnownGlobalVariables'] and 'bit[0xDC6]' not in r['snapshot']['KnownGlobalVariables']
        assert r['status']['Drafts']==q['records'][0]['status']['Drafts']
    assert not q['records'][-1]['snapshot']['IsFaithful']
    assert not any(a.get('userStatusEdits') for a in q['actions'])
    for a in q['actions']:
        result=a.get('result',a)
        if result.get('IsGameInput'): assert not any('not sent, because' in u for u in result['Uncertainties'])
assert len(p['actions'])==3 and all(a['result']['IsGameInput'] for a in p['actions'])
assert [t for t in p['trace'] if t.startswith('Script 07 +0x002E: startObject')]==[f'Script 07 +0x002E: startObject runs object {o} verb 0x{v}' for o,v in [(697,'36'),(696,'38'),(681,'38')]]
labels={'Script 100','Room 47 local script 205','Room 47 local script 206','Room 47 Object 697 Verb 0x36','Room 47 Object 696 Verb 0x38','Room 46 Object 681 Verb 0x38'}
body=lambda q:[e for e in q['events'] if e['SourceLabel'] in labels]
a,b=body(d),body(p)
assert a==b and len(b)==117 and all(e['Executed'] and e['Certainty']==0 for e in b)
differences=[]
code_path=Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')
assert digest(code_path)=='0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569'
code=[line.split('\t') for line in code_path.read_text(encoding='utf-8-sig').splitlines()]
inventory={(r[0],r[1]) for r in code if len(r)>=3 and r[2]=='cutscene'}
assert len(inventory)==235
assert any(r[:3]==['script 100','+0x0A3C','setClass'] and r[3]=='0x02BF, [0x0018]' for r in code)
assert any(r[:3]==['Room 47 local 205','+0x01A2','ifClassOfIs'] and '0x02BF, [0x0098]' in r[3] for r in code)
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
prior_paths=[Path(x) for x in ('Exe/Room34Sequences-20261002/input-2/keyboard.json','Exe/Room25Input-20261002/cycle-1/input.json','Exe/Room23Input-20261002/cycle-2/input.json','Exe/Room10Input-20261003/cycle-2/input.json','Exe/Room36Input-20261003/cycle-2/input.json','Exe/Room24Input-20261003/cycle-1/input.json','Exe/Room15Input-20261003/cycle-1/input.json','Exe/Room17Input-20261003/cycle-2/input.json','Exe/Room22Input-20261003/cycle-2/input.json','Exe/Room08Input-20261003/cycle-1/input.json','Exe/Room28Input-20261003/cycle-1/input.json','Exe/Room62Input-20261003/cycle-1/input.json','Exe/Room33Input-20261003/cycle-1/input.json')]
for path in prior_paths:
    q=read(path)
    assert q['failure'] is None
    before |= sites(q['boundaries'])
assert len(literal)==169 and len(before)==213
assert inventory-before=={(r['owner'],r['offset']) for r in read('.verification/archive/room33-input-20261003/remaining-sites.json')}
after=before|sites(p['boundaries'])
assert len(after-before)==1 and len(after)==214
protected=read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(f)==h.lower() for f,h in protected.items())
rtk='C:/Users/adelc/AppData/Local/Microsoft/WinGet/Links/rtk.exe'
git=lambda *args: subprocess.check_output([rtk,'proxy','git',*args],text=True)
assert not set(git('diff','--cached','--name-only').splitlines()).intersection(protected)
assert not git('diff','HEAD','--','src/SCUMMexplorer.Core','src/SCUMMexplorer.App/loom-replays.json')
preserved=read(base/'preserved-before.json')
assert all(digest(f)==h for f,h in preserved.items())
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name'])==f['sha256'].lower()
summary=dict(outcome='Room47 Cygna Look followed by actual exits through46 completes the remaining return response.',inputFrames=2423,directFrames=2423,completedScopes=3,executedSelectedEvents=117,reachedEvents=117,certaintyDifferences=0,unknownFlags=["bit[0xDC5]","bit[0xDC6]"],newSites=[dict(owner=o,offset=a) for o,a in sorted(after-before)],literalCoverage=dict(validated=169,total=235,percent=71.91,unchanged=True),auditedSiteUnion=dict(before=213,after=214,total=235,percent=91.06,remaining=21),diagnosticCycles=1,protectedExactUnstaged=57,preservedFiles=len(preserved),productionAndCatalogUnchanged=True,evidence={str(f):digest(f) for f in [packet_path,direct_path,literal_path,code_path,catalog_path]+prior_paths})
for name,value in [('summary.json',summary),('event-differences.json',differences),('remaining-sites.json',[dict(owner=o,offset=a) for o,a in sorted(inventory-after)])]:
    (base/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:summary[k] for k in ('inputFrames','directFrames','newSites','auditedSiteUnion','protectedExactUnstaged')},indent=2))
