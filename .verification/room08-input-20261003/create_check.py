from pathlib import Path
base=Path('.verification/room08-input-20261003')
prior=Path('.verification/room22-input-20261003/check.py').read_text()
tail=prior[prior.index('code_path='):prior.index('summary=dict(')]
start=tail.index('for offset,box'); end=tail.index('def sites',start)
tail=tail[:start]+'''for owner,offset,variable,value in [(212,'0010','D7D',1),(211,'000D','D7D',0),(213,'000D','D7E',1),(214,'0010','D7E',0)]:
    assert any(r[:3]==[f'Room 08 local {owner}','+0x'+offset,'move'] and f'bit[0x{variable}] = 0x{value:04X}' in r[3] for r in code)
'''+tail[end:]
tail=tail.replace("'Exe/Room17Input-20261003/cycle-2/input.json')]","'Exe/Room17Input-20261003/cycle-2/input.json','Exe/Room22Input-20261003/cycle-2/input.json')]").replace('len(before)==203','len(before)==205').replace('room17-input-20261003/remaining','room22-input-20261003/remaining').replace('len(after)==205','len(after)==207')
head='''import hashlib,json,re,subprocess
from pathlib import Path
base=Path('.verification/room08-input-20261003')
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
packet_path=Path('Exe/Room08Input-20261003/cycle-1/input.json'); direct_path=packet_path.with_name('direct.json')
p,d=read(packet_path),read(direct_path)
catalog_path=Path('src/SCUMMexplorer.App/loom-replays.json'); catalog=read(catalog_path)
expected=[(212,'000B','0062'),(211,'0008','0066'),(213,'0008','0066'),(214,'000B','0062')]
boundaries=[s for n,start,end in expected for s in (f'cutscene at Room 08 local script {n} +0x{start} runs script 2',f'endCutscene at Room 08 local script {n} +0x{end} runs script 3')]
for q,frames in ((p,1024),(d,820)):
    assert q['failure'] is None and q['frames']==frames and q['boundaries']==boundaries
    assert q['sourceFiles']==catalog['sourceFiles'] and q['rooms']==[]
    for r in q['records']:
        assert r['status']['CanEdit'] and r['snapshot']['RoomId']==8
        assert not any(re.search('unsupported|unmodelled',u,re.I) for u in r['snapshot']['Uncertainties'])
        for a,b in zip(q['records'][0]['status']['Drafts'],r['status']['Drafts'],strict=True):
            assert a['Notes']==b['Notes'] and a['Reversible']==b['Reversible']
    assert not q['records'][-1]['snapshot']['IsFaithful']
    assert [(r['stage'],r['snapshot']['KnownGlobalVariables'].get('bit[0xD7D]'),r['snapshot']['KnownGlobalVariables'].get('bit[0xD7E]')) for r in q['records'] if r['stage'] in ('arrival','bleach-left','restore-left','dye-right','restore-right')]==[('arrival',None,None),('bleach-left',1,None),('restore-left',0,None),('dye-right',0,1),('restore-right',0,0)]
    assert [a for a in q['actions'] if a.get('userStatusEdits')]==[dict(userStatusEdits=True,staffHeld=True,notes=2,learnedSlot=2)]
    for a in q['actions']:
        result=a.get('result',a)
        if result.get('IsGameInput'): assert not any('not sent, because' in u for u in result['Uncertainties'])
assert len([a for a in p['actions'] if a.get('Input','').startswith('The key ')])==16
assert [(a['verb'],a['Slot'],a['reverse'],a['notes']) for a in p['actions'] if 'verb' in a]==[(63,2,True,[2,1,1,1]),(62,2,False,[1,1,1,2]),(62,2,False,[1,1,1,2]),(63,2,True,[2,1,1,1])]
assert [t for t in p['trace'] if t.startswith('Script 07 +0x002E: startObject')]==[f'Script 07 +0x002E: startObject runs object {o} verb 0x{v}' for o,v in [(256,'3F'),(256,'3E'),(257,'3E'),(257,'3F')]]
labels={f'Room 08 local script {n}' for n in (207,211,212,213,214,217)}|{f'Room 08 Object {o} Verb 0x{v}' for o in (256,257) for v in ('3E','3F')}
body=lambda q:[e for e in q['events'] if e['SourceLabel'] in labels]
a,b=body(d),body(p)
assert len(a)==len(b)==79 and all(e['Executed'] and e['Certainty']==1 for e in b)
differences=[]
for i,(x,y) in enumerate(zip(a,b,strict=True)):
    delta={k:dict(direct=x[k],input=y[k]) for k in x if x[k]!=y[k]}
    if delta:
        assert delta=={'Certainty':dict(direct=0,input=1)}
        differences.append(dict(index=i,source=y['SourceLabel'],offset=y['Offset'],differences=delta))
assert len(differences)==76
'''
footer='''summary=dict(outcome='Two Room08 tapestry restoration sites complete via four accepted keyboard casts.',inputFrames=1024,directFrames=820,completedScopes=4,conditionalExecutedBodyEvents=79,certaintyDifferences=76,newSites=[dict(owner=o,offset=a) for o,a in sorted(after-before)],literalCoverage=dict(validated=169,total=235,percent=71.91,unchanged=True),auditedSiteUnion=dict(before=205,after=207,total=235,percent=88.09,remaining=28),diagnosticCycles=1,protectedExactUnstaged=57,preservedFiles=len(preserved),productionAndCatalogUnchanged=True,evidence={str(f):digest(f) for f in [packet_path,direct_path,literal_path,code_path,catalog_path]+prior_paths})
for name,value in [('summary.json',summary),('event-differences.json',differences),('remaining-sites.json',[dict(owner=o,offset=a) for o,a in sorted(inventory-after)])]:
    (base/name).write_text(json.dumps(value,indent=2)+'\\n',encoding='utf-8')
print(json.dumps({k:summary[k] for k in ('inputFrames','directFrames','newSites','auditedSiteUnion','protectedExactUnstaged')},indent=2))
'''
(base/'check.py').write_text(head+tail+footer,encoding='utf-8')
