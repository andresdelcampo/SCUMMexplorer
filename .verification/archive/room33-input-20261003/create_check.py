from pathlib import Path
p=Path('.verification/archive/room62-input-20261003/check.py').read_text()
p=p.replace("base=Path('.verification/archive/room62-input-20261003')","base=Path('.verification/archive/room33-input-20261003')").replace("packet_path=Path('Exe/Room62Input-20261003/cycle-1/input.json')","packet_path=Path('Exe/Room33Input-20261003/cycle-1/input.json')").replace('Room 62','Room 33')
p=p.replace("[(201,'0000','007B'),(202,'0000','007B'),(204,'0000','007B')]","[(205,'0000','00B0'),(200,'0006','005E'),(205,'0000','00B0'),(202,'0006','005E')]").replace('((p,692),(d,539))','((p,706),(d,604))').replace("['RoomId']==62","['RoomId']==33")
start=p.index("    assert [(r['stage']"); end=p.index('    for a in q[',start)
p=p[:start]+'''    assert [(r['stage'],r['snapshot']['KnownGlobalVariables']['bit[0xD81]']) for r in q['records'] if r['stage'] in ('arrival','untwist','twist')]==[('arrival',0),('untwist',1),('twist',0)]
    assert [a for a in q['actions'] if a.get('userStatusEdits')]==[dict(userStatusEdits=True,staffHeld=True,notes=4,learnedSlot=4)]
'''+p[end:]
p=p.replace("')])==12","')])==8").replace("[(61,1,True,[3,1,1,1]),(60,1,False,[1,1,1,3]),(61,1,True,[3,1,1,1])]","[(66,4,True,[1,1,4,1]),(65,4,False,[1,4,1,1])]").replace("[(808,'3D'),(808,'3C'),(809,'3D')]","[(544,'42'),(544,'41')]").replace('(201,202,204,206)','(200,201,202,203,205)')
p=p.replace("all(e['Executed'] and e['Certainty']==1 for e in b)","all(e['Executed'] for e in b) and [i for i,e in enumerate(b) if e['Certainty']==0]==[0] and all(e['Certainty']==1 for e in b[1:])")
p=p.replace("        assert delta=={'Certainty':dict(direct=0,input=1)}","        expectedDelta={'Certainty':dict(direct=0,input=1)}\n        if i in (10,41):\n            assert y['SourceLabel']=='Room 33 local script 205' and y['Offset']==109\n            expectedDelta['EstimatedArgumentIndexes']=dict(direct=None,input=[0])\n        assert delta==expectedDelta").replace('len(differences)==63','len(differences)==62')
start=p.index('for owner,offset'); end=p.index('def sites',start)
p=p[:start]+'''for owner,value in [(201,1),(203,0)]:
    assert any(r[:3]==[f'Room 33 local {owner}','+0x0000','move'] and f'bit[0xD81] = 0x{value:04X}' in r[3] for r in code)
'''+p[end:]
p=p.replace("'Exe/Room28Input-20261003/cycle-1/input.json')]","'Exe/Room28Input-20261003/cycle-1/input.json','Exe/Room62Input-20261003/cycle-1/input.json')]").replace('len(before)==210','len(before)==212').replace('room28-input-20261003/remaining','room62-input-20261003/remaining').replace('len(after-before)==2 and len(after)==212','len(after-before)==1 and len(after)==213')
p=p.replace('Room62 two gold/straw sites complete via three accepted keyboard casts.','Room33 remaining Twist scene completes after source Untwist through two keyboard casts.').replace('inputFrames=692,directFrames=539,completedScopes=3,conditionalExecutedBodyEvents=63,certaintyDifferences=63','inputFrames=706,directFrames=604,completedScopes=4,executedSelectedEvents=63,conditionalEvents=62,reachedEntryEvents=1,certaintyDifferences=62,argumentEstimateDifferences=2').replace('before=210,after=212','before=212,after=213').replace('percent=90.21,remaining=23','percent=90.64,remaining=22')
Path('.verification/archive/room33-input-20261003/check.py').write_text(p,encoding='utf-8')
