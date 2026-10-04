from pathlib import Path
base=Path('.verification/archive/room24-input-20261003')
prior=Path('.verification/archive/room36-input-20261003/check.py').read_text()
tail=prior[prior.index('code_path='):prior.index('summary=dict(')]
begin=tail.index('for offset,box'); end=tail.index('def sites',begin)
tail=tail[:begin]+'''for offset,box in [('0016',3),('0082',4)]:
    assert any(r[:3]==['Room 24 local 200','+0x'+offset,'isEqual'] and f'0x{box:04X}' in r[3] for r in code)
assert any(r[:3]==['Room 24 local 211','+0x0007','move'] and 'var[0x0C5] = 0x01BD' in r[3] for r in code)
'''+tail[end:]
tail=tail.replace("'Exe/Room10Input-20261003/cycle-2/input.json')", "'Exe/Room10Input-20261003/cycle-2/input.json','Exe/Room36Input-20261003/cycle-2/input.json')")
tail=tail.replace('len(before)==192','len(before)==196').replace('room10-input-20261003/remaining','room36-input-20261003/remaining').replace('len(after-before)==4 and len(after)==196','len(after-before)==3 and len(after)==199')
head='''"""Audit Room24 accepted clicks/keys and paired completed bodies with estimates retained."""
import hashlib,json,re,subprocess
from pathlib import Path
base=Path('.verification/archive/room24-input-20261003')
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
packet_path=Path('Exe/Room24Input-20261003/cycle-1/input.json')
direct_path=Path('Exe/Room24Input-20261003/cycle-1/direct.json')
p,d=read(packet_path),read(direct_path)
catalog_path=Path('src/SCUMMexplorer.App/loom-replays.json'); catalog=read(catalog_path)
expected=[(200,'001D','007E'),(200,'0089','00D6'),(213,'0000','0043'),(211,'0018','0045')]
boundaries=[s for n,start,end in expected for s in (f'cutscene at Room 24 local script {n} +0x{start} runs script 2',f'endCutscene at Room 24 local script {n} +0x{end} runs script 3')]
for q,frames in ((p,792),(d,690)):
    assert q['failure'] is None and q['frames']==frames and q['boundaries']==boundaries
    assert q['sourceFiles']==catalog['sourceFiles'] and q['rooms']==[]
    for r in q['records']:
        assert r['status']['CanEdit'] and r['snapshot']['RoomId']==24
        assert not any(re.search('unsupported|unmodelled',u,re.I) for u in r['snapshot']['Uncertainties'])
        for a,b in zip(q['records'][0]['status']['Drafts'],r['status']['Drafts'],strict=True):
            assert a['Notes']==b['Notes'] and a['Reversible']==b['Reversible']
    assert not q['records'][-1]['snapshot']['IsFaithful']
    assert [(r['stage'],r['snapshot']['KnownGlobalVariables']['var[0x0C5]']) for r in q['records'] if r['stage'] in ('arrival','dye','already-green')]==[('arrival',444),('dye',445),('already-green',445)]
    for a in q['actions']:
        result=a.get('result',a)
        if result.get('IsGameInput'):
            assert not any('not sent, because' in u for u in result['Uncertainties'])
    walk=[a for a in q['actions'] if 'index' in a]
    assert [(a['index'],a['x'],a['y']) for a in walk]==[(3,604,102),(4,588,111)]
    for a in walk:
        b=next(b for b in q['geometry']['Boxes'] if b['Index']==a['index'])
        assert (a['x'],a['y'])==tuple(sum(c[k] for c in b['Corners'])//4 for k in ('X','Y'))
    assert [a for a in q['actions'] if a.get('userStatusEdits')]==[dict(userStatusEdits=True,staffHeld=True,notes=2,learnedSlot=2)]
casts=[a for a in p['actions'] if 'verb' in a]
assert [(a['verb'],a['Slot'],a['reverse'],a['notes']) for a in casts]==[(62,2,False,[1,1,1,2])]*2
keys=[a for a in p['actions'] if a.get('Input','').startswith('The key ')]
assert len(keys)==8 and all(a['IsGameInput'] for a in keys)
assert [t for t in p['trace'] if t.startswith('Script 07 +0x002E: startObject')]==['Script 07 +0x002E: startObject runs object 432 verb 0x3E']*2
labels={f'Room 24 local script {n}' for n in (200,211,213,216)}|{'Room 24 Object 432 Verb 0x3E'}
body=lambda q:[e for e in q['events'] if e['SourceLabel'] in labels]
a,b=body(d),body(p)
assert len(a)==len(b)==115 and all(e['Executed'] and e['Certainty']==1 for e in b)
differences=[]
for i,(x,y) in enumerate(zip(a,b,strict=True)):
    delta={k:dict(direct=x[k],input=y[k]) for k in x if x[k]!=y[k]}
    if not delta: continue
    expectedDelta={'Certainty':dict(direct=0,input=1)}
    if i in (50,100):
        assert y['SourceLabel']=='Room 24 local script 216' and y['Offset']==213
        expectedDelta['EstimatedArgumentIndexes']=dict(direct=None,input=[0])
    assert delta==expectedDelta
    differences.append(dict(index=i,source=y['SourceLabel'],offset=y['Offset'],differences=delta))
assert len(differences)==93
'''
footer='''summary=dict(outcome='Room24 three remaining sites complete through walking and repeated dye note keys.',inputFrames=792,directFrames=690,completedScopes=4,conditionalExecutedBodyEvents=115,pairedDifferences=dict(certainty=93,estimatedArgumentIndexes=2),newSites=[dict(owner=o,offset=a) for o,a in sorted(after-before)],literalCoverage=dict(validated=169,total=235,percent=71.91,unchanged=True),auditedSiteUnion=dict(before=196,after=199,total=235,percent=84.68,remaining=36),diagnosticCycles=1,protectedExactUnstaged=57,preservedFiles=len(preserved),productionAndCatalogUnchanged=True,limitations=['Catalog arrival/default preview are exploration assumptions.','Supported staff/two notes/learned2 edits are labelled exploration setup.','Core API only; no WPF/physical input/native full-game proof.','Recorded body events are not exhaustive; all estimates retained.','No fresh single-action promotion.'],evidence={str(f):digest(f) for f in [packet_path,direct_path,literal_path,code_path,catalog_path]+prior_paths})
for name,value in [('summary.json',summary),('event-differences.json',differences),('remaining-sites.json',[dict(owner=o,offset=a) for o,a in sorted(inventory-after)])]:
    (base/name).write_text(json.dumps(value,indent=2)+'\\n',encoding='utf-8')
print(json.dumps({k:summary[k] for k in ('inputFrames','directFrames','newSites','auditedSiteUnion','protectedExactUnstaged')},indent=2))
'''
(base/'check.py').write_text(head+tail+footer,encoding='utf-8')
