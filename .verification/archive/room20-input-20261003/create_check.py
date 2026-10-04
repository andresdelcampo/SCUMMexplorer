from pathlib import Path
p=Path('.verification/archive/room47-input-20261003/check.py').read_text()
head=p[:p.index('expected=')].replace('room47-input','room20-input').replace('Room47Input','Room20Input').replace('cycle-1','cycle-2')
body='''expected=[('Room 16 local script 204','0072','0128'),('Room 18 local script 205','0000','0316'),('Script 56','0000','0025'),('Script 57','0000','0029'),('Room 20 local script 206','0000','00A9'),('Room 20 local script 207','000B','0548'),('Script 58','0000','0018'),('Script 59','0010','003F')]
boundaries=[s for owner,start,end in expected for s in (f'cutscene at {owner} +0x{start} runs script 2',f'endCutscene at {owner} +0x{end} runs script 3')]
for q,frames,times in ((p,3263,[650,1660,3124]),(d,3161,[599,1609,3022])):
    assert q['failure'] is None and q['frames']==frames and q['boundaries']==boundaries
    assert q['sourceFiles']==catalog['sourceFiles']
    assert [(r['frames'],r['room']) for r in q['rooms']]==list(zip(times,[18,20,18]))
    for r in q['records']:
        assert r['status']['CanEdit']
        assert not any(re.search('unsupported|unmodelled',u,re.I) for u in r['snapshot']['Uncertainties'])
        for x,y in zip(q['records'][0]['status']['Drafts'],r['status']['Drafts'],strict=True):
            assert x['Notes']==y['Notes'] and x['Reversible']==y['Reversible']
    assert [(r['stage'],r['snapshot']['KnownGlobalVariables'].get('bit[0xD8F]','missing')) for r in q['records'] if r['stage'] in ('arrival','sleep','wake')]==[('arrival','missing'),('sleep',1),('wake',0)]
    assert [(r['stage'],r['snapshot']['ObjectClasses']['371']) for r in q['records'] if r['stage'] in ('enter-room18','reveal-crystal')]==[('enter-room18',8388609),('reveal-crystal',1)]
    assert q['records'][-1]['snapshot']['RoomId']==18 and not q['records'][-1]['snapshot']['IsFaithful']
    assert [a for a in q['actions'] if a.get('userStatusEdits')]==[dict(userStatusEdits=True,staffHeld=True,notes=5,learnedSlot=7)]
    for a in q['actions']:
        result=a.get('result',a)
        if result.get('IsGameInput'): assert not any('not sent, because' in u for u in result['Uncertainties'])
assert len([a for a in p['actions'] if a.get('Input','').startswith('The key ')])==8
assert [(a['verb'],a['Slot'],a['reverse'],a['notes']) for a in p['actions'] if 'verb' in a]==[(71,7,False,[5,1,5,1]),(72,7,True,[1,5,1,5])]
assert [t for t in p['trace'] if t.startswith('Script 07 +0x002E: startObject')]==[f'Script 07 +0x002E: startObject runs object {o} verb 0x{v}' for o,v in [(350,'47'),(346,'38'),(372,'36'),(371,'36'),(390,'48')]]
labels={'Room 16 local script 204','Room 18 local script 205','Room 18 local script 206'}|{f'Room 20 local script {n}' for n in (204,206,207,208,209,216,217)}|{f'Script {n}' for n in (56,57,58,59)}|{f'Room {r:02} Object {o} Verb 0x{v}' for r,o,v in ((16,350,'47'),(16,346,'38'),(18,372,'36'),(18,371,'36'),(20,390,'48'))}
body=lambda q:[e for e in q['events'] if e['SourceLabel'] in labels]
a,b=body(d),body(p)
assert len(a)==len(b)==154 and all(e['Executed'] and e['Certainty']==1 for e in b)
differences=[]
for i,(x,y) in enumerate(zip(a,b,strict=True)):
    delta={k:dict(direct=x[k],input=y[k]) for k in x if x[k]!=y[k]}
    expectedDelta={'Certainty':dict(direct=0,input=1)}
    if i==74:
        assert y['SourceLabel']=='Room 20 local script 204' and y['Offset']==173
        expectedDelta['EstimatedArgumentIndexes']=dict(direct=None,input=[0])
    assert delta==expectedDelta
    differences.append(dict(index=i,source=y['SourceLabel'],offset=y['Offset'],differences=delta))
failed_paths=[Path('Exe/Room20Input-20261003/cycle-1')/f'{m}.json' for m in ('direct','input')]
for f in failed_paths:
    q=read(f); assert 'enter-room20: bounded settling failed' in q['failure'] and len(q['boundaries'])==2
    assert q['records'][-1]['snapshot']['RoomId']==18
    assert not any(t=='Script 07 +0x002E: startObject runs object 369 verb 0x36' for t in q['trace'])
'''
tail=p[p.index('code_path='):]
start=tail.index('assert any(r[:3]'); end=tail.index('def sites',start)
tail=tail[:start]+'''assert any(r[:3]==['Room 16 local 204','+0x00F2','move'] and 'bit[0xD8F]' in r[3] for r in code)
assert any(r[:3]==['Room 20 local 206','+0x00A3','move'] and r[3]=='bit[0xD8F] = 0x0000' for r in code)
assert any(r[:3]==['Room 18 local 205','+0x02DE','setClass'] and r[3]=='0x0173, [0x0018]' for r in code)
'''+tail[end:]
tail=tail.replace("'Exe/Room33Input-20261003/cycle-1/input.json')]","'Exe/Room33Input-20261003/cycle-1/input.json','Exe/Room47Input-20261003/cycle-1/input.json')]").replace('len(before)==213','len(before)==214').replace('room33-input-20261003/remaining','room47-input-20261003/remaining').replace('len(after)==214','len(after)==215')
tail=tail.replace('Room47 Cygna Look followed by actual exits through46 completes the remaining return response.','Room20 Waking scene completes through source Sleep16, conversation18, crystal entrance and keyboard response.').replace('inputFrames=2423,directFrames=2423,completedScopes=3,executedSelectedEvents=117,reachedEvents=117,certaintyDifferences=0,unknownFlags=["bit[0xDC5]","bit[0xDC6]"]','inputFrames=3263,directFrames=3161,completedScopes=8,executedConditionalEvents=154,certaintyOnlyDifferences=153,certaintyAndArgumentEstimateDifferences=1').replace('before=213,after=214','before=214,after=215').replace('percent=91.06,remaining=21','percent=91.49,remaining=20').replace('diagnosticCycles=1','diagnosticCycles=2').replace(']+prior_paths}',']+prior_paths+failed_paths}')
Path('.verification/archive/room20-input-20261003/check.py').write_text(head+body+tail,encoding='utf-8')
