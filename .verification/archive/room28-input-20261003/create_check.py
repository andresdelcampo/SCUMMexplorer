from pathlib import Path
base=Path('.verification/archive/room28-input-20261003')
prior=Path('.verification/archive/room08-input-20261003/check.py').read_text()
tail=prior[prior.index('code_path='):prior.index('summary=dict(')]
start=tail.index('for owner,offset'); end=tail.index('def sites',start)
tail=tail[:start]+'''for owner,offset,operation,operand in [('Room 28 local 200','+0x01C1','move','var[0x0CA] = 0x0001'),('Room 28 local 212','+0x001C','isEqual','0x0001'),('Room 28 local 212','+0x00C9','chainScript','0x4C')]:
    assert any(r[:3]==[owner,offset,operation] and operand in r[3] for r in code)
'''+tail[end:]
tail=tail.replace("'Exe/Room22Input-20261003/cycle-2/input.json')]","'Exe/Room22Input-20261003/cycle-2/input.json','Exe/Room08Input-20261003/cycle-1/input.json')]").replace('len(before)==205','len(before)==207').replace('room22-input-20261003/remaining','room08-input-20261003/remaining').replace('len(after-before)==2 and len(after)==207','len(after-before)==3 and len(after)==210')
head='''import hashlib,json,re,subprocess
from pathlib import Path
base=Path('.verification/archive/room28-input-20261003')
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
packet_path=Path('Exe/Room28Input-20261003/cycle-1/input.json'); direct_path=packet_path.with_name('direct.json')
p,d=read(packet_path),read(direct_path)
catalog_path=Path('src/SCUMMexplorer.App/loom-replays.json'); catalog=read(catalog_path)
expected=[('Room 28 local script 203','0000','0007')]*2+[('Room 28 local script 218','0000','00AF'),('Room 28 local script 200','0010','0262'),('Script 29','0000','00E2'),('Room 28 local script 203','0000','0007'),('Room 28 local script 210','0000','0099'),('Room 28 local script 212','0064','00C8'),('Script 76','0000','0086')]
boundaries=[s for owner,start,end in expected for s in (f'cutscene at {owner} +0x{start} runs script 2',f'endCutscene at {owner} +0x{end} runs script 3')]
for q,frames in ((p,949),(d,847)):
    assert q['failure'] is None and q['frames']==frames and q['boundaries']==boundaries
    assert q['sourceFiles']==catalog['sourceFiles'] and [r['room'] for r in q['rooms']]==[27,28]
    for r in q['records']:
        assert r['status']['CanEdit'] and r['snapshot']['RoomId']==28
        assert not any(re.search('unsupported|unmodelled',u,re.I) for u in r['snapshot']['Uncertainties'])
        for a,b in zip(q['records'][0]['status']['Drafts'],r['status']['Drafts'],strict=True):
            assert a['Notes']==b['Notes'] and a['Reversible']==b['Reversible']
    assert not q['records'][-1]['snapshot']['IsFaithful']
    assert [(r['stage'],r['snapshot']['KnownGlobalVariables'].get('var[0x0CA]'),r['snapshot']['KnownGlobalVariables'].get('bit[0xD94]')) for r in q['records'] if r['stage'] in ('arrival','gold-to-straw','sleep','fire-aftermath')]==[('arrival',0,None),('gold-to-straw',1,None),('sleep',1,1),('fire-aftermath',2,0)]
    assert [a for a in q['actions'] if a.get('userStatusEdits')]==[dict(userStatusEdits=True,staffHeld=True,notes=6,learnedSlots=[1,5])]
    for a in q['actions']:
        result=a.get('result',a)
        if result.get('IsGameInput'): assert not any('not sent, because' in u for u in result['Uncertainties'])
assert len([a for a in p['actions'] if a.get('Input','').startswith('The key ')])==8
assert [(a['verb'],a['Slot'],a['reverse'],a['notes']) for a in p['actions'] if 'verb' in a]==[(61,1,True,[3,1,1,1]),(67,5,False,[6,3,2,1])]
assert [t for t in p['trace'] if t.startswith('Script 07 +0x002E: startObject')]==[f'Script 07 +0x002E: startObject runs object {o} verb 0x{v}' for o,v in [(466,'38'),(470,'38'),(470,'3D'),(466,'38'),(466,'43')]]
labels={f'Room 28 local script {n}' for n in (200,202,203,210,211,212)}|{'Script 76'}|{f'Room 28 Object {o} Verb 0x{v}' for o,v in [(466,'38'),(466,'43'),(470,'3D')]}
body=lambda q:[e for e in q['events'] if e['SourceLabel'] in labels]
a,b=body(d),body(p)
assert len(a)==len(b)==207 and all(e['Executed'] and e['Certainty']==1 for e in b)
differences=[]
for i,(x,y) in enumerate(zip(a,b,strict=True)):
    delta={k:dict(direct=x[k],input=y[k]) for k in x if x[k]!=y[k]}
    if delta:
        assert delta=={'Certainty':dict(direct=0,input=1)}
        differences.append(dict(index=i,source=y['SourceLabel'],offset=y['Offset'],differences=delta))
assert len(differences)==199
'''
footer='''summary=dict(outcome='Room28 camera, timed fire and076 continuation complete by clicks and two keyboard casts.',inputFrames=949,directFrames=847,completedScopes=9,conditionalExecutedBodyEvents=207,certaintyDifferences=199,newSites=[dict(owner=o,offset=a) for o,a in sorted(after-before)],literalCoverage=dict(validated=169,total=235,percent=71.91,unchanged=True),auditedSiteUnion=dict(before=207,after=210,total=235,percent=89.36,remaining=25),diagnosticCycles=1,protectedExactUnstaged=57,preservedFiles=len(preserved),productionAndCatalogUnchanged=True,evidence={str(f):digest(f) for f in [packet_path,direct_path,literal_path,code_path,catalog_path]+prior_paths})
for name,value in [('summary.json',summary),('event-differences.json',differences),('remaining-sites.json',[dict(owner=o,offset=a) for o,a in sorted(inventory-after)])]:
    (base/name).write_text(json.dumps(value,indent=2)+'\\n',encoding='utf-8')
print(json.dumps({k:summary[k] for k in ('inputFrames','directFrames','newSites','auditedSiteUnion','protectedExactUnstaged')},indent=2))
'''
(base/'check.py').write_text(head+tail+footer,encoding='utf-8')
