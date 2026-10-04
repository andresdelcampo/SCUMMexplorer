from pathlib import Path
p=Path('.verification/archive/room33-input-20261003/check.py').read_text()
head=p[:p.index('expected=')].replace('room33-input','room47-input').replace('Room33Input','Room47Input')
body='''expected=[('Room 47 local script 205','000E','019E'),('Script 100','0000','0A7B'),('Room 47 local script 205','01B7','03F1')]
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
'''
tail=p[p.index('code_path='):]
start=tail.index('for owner,value'); end=tail.index('def sites',start)
tail=tail[:start]+'''assert any(r[:3]==['script 100','+0x0A3C','setClass'] and r[3]=='0x02BF, [0x0018]' for r in code)
assert any(r[:3]==['Room 47 local 205','+0x01A2','ifClassOfIs'] and '0x02BF, [0x0098]' in r[3] for r in code)
'''+tail[end:]
tail=tail.replace("'Exe/Room62Input-20261003/cycle-1/input.json')]","'Exe/Room62Input-20261003/cycle-1/input.json','Exe/Room33Input-20261003/cycle-1/input.json')]").replace('len(before)==212','len(before)==213').replace('room62-input-20261003/remaining','room33-input-20261003/remaining').replace('len(after)==213','len(after)==214')
tail=tail.replace('Room33 remaining Twist scene completes after source Untwist through two keyboard casts.','Room47 Cygna Look followed by actual exits through46 completes the remaining return response.').replace('inputFrames=706,directFrames=604,completedScopes=4,executedSelectedEvents=63,conditionalEvents=62,reachedEntryEvents=1,certaintyDifferences=62,argumentEstimateDifferences=2','inputFrames=2423,directFrames=2423,completedScopes=3,executedSelectedEvents=117,reachedEvents=117,certaintyDifferences=0,unknownFlags=["bit[0xDC5]","bit[0xDC6]"]').replace('before=212,after=213','before=213,after=214').replace('percent=90.64,remaining=22','percent=91.06,remaining=21')
Path('.verification/archive/room47-input-20261003/check.py').write_text(head+body+tail,encoding='utf-8')
