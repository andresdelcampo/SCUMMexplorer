from pathlib import Path
p=Path('.verification/archive/room20-input-20261003/check.py').read_text()
head=p[:p.index('expected=')].replace('room20-input','room34-watcher').replace('Room20Input','Room34Watcher')
body='''failed_paths=[Path(f'Exe/Room34Watcher-20261003/cycle-{c}/{m}.json') for c in (1,2) for m in ('direct','input')]
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
'''
tail=p[p.index('code_path='):]
start=tail.index('assert any(r[:3]'); end=tail.index('def sites',start)
tail=tail[:start]+'''assert any(r[:3]==['Room 36 local 200','+0x0094','setClass'] and r[3]=='0x0257, [0x0018]' for r in code)
'''+tail[end:]
tail=tail.replace("'Exe/Room47Input-20261003/cycle-1/input.json')]","'Exe/Room47Input-20261003/cycle-1/input.json','Exe/Room20Input-20261003/cycle-2/input.json')]").replace('len(before)==214','len(before)==215').replace('room47-input-20261003/remaining','room20-input-20261003/remaining').replace("after=before|sites(p['boundaries'])","after=before").replace('len(after-before)==1 and len(after)==215','len(after-before)==0 and len(after)==215')
tail=tail.replace('Room20 Waking scene completes through source Sleep16, conversation18, crystal entrance and keyboard response.','Room34 watcher route stopped after two failed prerequisite attempts; no coverage credit.').replace('inputFrames=3263,directFrames=3161,completedScopes=8,executedConditionalEvents=154,certaintyOnlyDifferences=153,certaintyAndArgumentEstimateDifferences=1','framesPerCapture=3041,failedCaptures=4,completeRecipes=0,nextExperiment="Later session: Room36 box9 watcher opens599, then actual exit599, firewood086, Look610/087, Opening617 and exits back to34, Healing586, camera watcher. Preserve source setup and stop after two cycles."').replace('before=214,after=215','before=215,after=215').replace("('inputFrames','directFrames','newSites','auditedSiteUnion','protectedExactUnstaged')","('framesPerCapture','failedCaptures','newSites','auditedSiteUnion','protectedExactUnstaged')")
Path('.verification/archive/room34-watcher-20261003/check.py').write_text(head+body+tail,encoding='utf-8')
