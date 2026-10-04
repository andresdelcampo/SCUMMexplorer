import hashlib,json,re,subprocess
from pathlib import Path
base=Path('.verification/archive/room33-input-20261003')
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
packet_path=Path('Exe/Room33Input-20261003/cycle-1/input.json'); direct_path=packet_path.with_name('direct.json')
p,d=read(packet_path),read(direct_path)
catalog_path=Path('src/SCUMMexplorer.App/loom-replays.json'); catalog=read(catalog_path)
expected=[(205,'0000','00B0'),(200,'0006','005E'),(205,'0000','00B0'),(202,'0006','005E')]
boundaries=[s for n,start,end in expected for s in (f'cutscene at Room 33 local script {n} +0x{start} runs script 2',f'endCutscene at Room 33 local script {n} +0x{end} runs script 3')]
for q,frames in ((p,706),(d,604)):
    assert q['failure'] is None and q['frames']==frames and q['boundaries']==boundaries
    assert q['sourceFiles']==catalog['sourceFiles'] and q['rooms']==[]
    for r in q['records']:
        assert r['status']['CanEdit'] and r['snapshot']['RoomId']==33
        assert not any(re.search('unsupported|unmodelled',u,re.I) for u in r['snapshot']['Uncertainties'])
        for a,b in zip(q['records'][0]['status']['Drafts'],r['status']['Drafts'],strict=True):
            assert a['Notes']==b['Notes'] and a['Reversible']==b['Reversible']
    assert not q['records'][-1]['snapshot']['IsFaithful']
    assert [(r['stage'],r['snapshot']['KnownGlobalVariables']['bit[0xD81]']) for r in q['records'] if r['stage'] in ('arrival','untwist','twist')]==[('arrival',0),('untwist',1),('twist',0)]
    assert [a for a in q['actions'] if a.get('userStatusEdits')]==[dict(userStatusEdits=True,staffHeld=True,notes=4,learnedSlot=4)]
    for a in q['actions']:
        result=a.get('result',a)
        if result.get('IsGameInput'): assert not any('not sent, because' in u for u in result['Uncertainties'])
assert len([a for a in p['actions'] if a.get('Input','').startswith('The key ')])==8
assert [(a['verb'],a['Slot'],a['reverse'],a['notes']) for a in p['actions'] if 'verb' in a]==[(66,4,True,[1,1,4,1]),(65,4,False,[1,4,1,1])]
assert [t for t in p['trace'] if t.startswith('Script 07 +0x002E: startObject')]==[f'Script 07 +0x002E: startObject runs object {o} verb 0x{v}' for o,v in [(544,'42'),(544,'41')]]
labels={f'Room 33 local script {n}' for n in (200,201,202,203,205)}|{f'Room 33 Object {o} Verb 0x{v}' for o,v in [(544,'42'),(544,'41')]}
body=lambda q:[e for e in q['events'] if e['SourceLabel'] in labels]
a,b=body(d),body(p)
assert len(a)==len(b)==63 and all(e['Executed'] for e in b) and [i for i,e in enumerate(b) if e['Certainty']==0]==[0] and all(e['Certainty']==1 for e in b[1:])
differences=[]
for i,(x,y) in enumerate(zip(a,b,strict=True)):
    delta={k:dict(direct=x[k],input=y[k]) for k in x if x[k]!=y[k]}
    if delta:
        expectedDelta={'Certainty':dict(direct=0,input=1)}
        if i in (10,41):
            assert y['SourceLabel']=='Room 33 local script 205' and y['Offset']==109
            expectedDelta['EstimatedArgumentIndexes']=dict(direct=None,input=[0])
        assert delta==expectedDelta
        differences.append(dict(index=i,source=y['SourceLabel'],offset=y['Offset'],differences=delta))
assert len(differences)==62
code_path=Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')
assert digest(code_path)=='0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569'
code=[line.split('\t') for line in code_path.read_text(encoding='utf-8-sig').splitlines()]
inventory={(r[0],r[1]) for r in code if len(r)>=3 and r[2]=='cutscene'}
assert len(inventory)==235
for owner,value in [(201,1),(203,0)]:
    assert any(r[:3]==[f'Room 33 local {owner}','+0x0000','move'] and f'bit[0xD81] = 0x{value:04X}' in r[3] for r in code)
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
prior_paths=[Path(x) for x in ('Exe/Room34Sequences-20261002/input-2/keyboard.json','Exe/Room25Input-20261002/cycle-1/input.json','Exe/Room23Input-20261002/cycle-2/input.json','Exe/Room10Input-20261003/cycle-2/input.json','Exe/Room36Input-20261003/cycle-2/input.json','Exe/Room24Input-20261003/cycle-1/input.json','Exe/Room15Input-20261003/cycle-1/input.json','Exe/Room17Input-20261003/cycle-2/input.json','Exe/Room22Input-20261003/cycle-2/input.json','Exe/Room08Input-20261003/cycle-1/input.json','Exe/Room28Input-20261003/cycle-1/input.json','Exe/Room62Input-20261003/cycle-1/input.json')]
for path in prior_paths:
    q=read(path)
    assert q['failure'] is None
    before |= sites(q['boundaries'])
assert len(literal)==169 and len(before)==212
assert inventory-before=={(r['owner'],r['offset']) for r in read('.verification/archive/room62-input-20261003/remaining-sites.json')}
after=before|sites(p['boundaries'])
assert len(after-before)==1 and len(after)==213
protected=read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(f)==h.lower() for f,h in protected.items())
rtk='C:/Users/adelc/AppData/Local/Microsoft/WinGet/Links/rtk.exe'
git=lambda *args: subprocess.check_output([rtk,'proxy','git',*args],text=True)
assert not set(git('diff','--cached','--name-only').splitlines()).intersection(protected)
assert not git('diff','HEAD','--','src/SCUMMexplorer.Core','src/SCUMMexplorer.App/loom-replays.json')
preserved=read(base/'preserved-before.json')
assert all(digest(f)==h for f,h in preserved.items())
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name'])==f['sha256'].lower()
summary=dict(outcome='Room33 remaining Twist scene completes after source Untwist through two keyboard casts.',inputFrames=706,directFrames=604,completedScopes=4,executedSelectedEvents=63,conditionalEvents=62,reachedEntryEvents=1,certaintyDifferences=62,argumentEstimateDifferences=2,newSites=[dict(owner=o,offset=a) for o,a in sorted(after-before)],literalCoverage=dict(validated=169,total=235,percent=71.91,unchanged=True),auditedSiteUnion=dict(before=212,after=213,total=235,percent=90.64,remaining=22),diagnosticCycles=1,protectedExactUnstaged=57,preservedFiles=len(preserved),productionAndCatalogUnchanged=True,evidence={str(f):digest(f) for f in [packet_path,direct_path,literal_path,code_path,catalog_path]+prior_paths})
for name,value in [('summary.json',summary),('event-differences.json',differences),('remaining-sites.json',[dict(owner=o,offset=a) for o,a in sorted(inventory-after)])]:
    (base/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:summary[k] for k in ('inputFrames','directFrames','newSites','auditedSiteUnion','protectedExactUnstaged')},indent=2))
