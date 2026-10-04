"""Preserve bounded diagnostics; failed recipes add no coverage."""
from pathlib import Path
import hashlib,json,subprocess
base=Path('.verification/archive/room09-input-20261003')
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
paths=[Path(f'Exe/Room09Input-20261003/cycle-{n}/{mode}.json') for n in (1,2) for mode in ('direct','input')]
early=['cutscene at Script 89 +0x0000 runs script 2','endCutscene at Script 89 +0x0074 runs script 3']
later=early+['cutscene at Room 09 local script 209 +0x0000 runs script 2','endCutscene at Room 09 local script 209 +0x0190 runs script 3','cutscene at Script 101 +0x0000 runs script 2','endCutscene at Script 101 +0x08B3 runs script 3']
catalog=read('src/SCUMMexplorer.App/loom-replays.json')
for i,p in enumerate(map(read,paths)):
    assert p['failure'] and p['sourceFiles']==catalog['sourceFiles']
    assert p['frames']==(3322 if i<2 else 1768) and p['boundaries']==(early if i<2 else later)
    assert ('missing endCutscene at Script 101' if i<2 else 'Expected three source draft slots') in p['failure']
    assert [r['room'] for r in p['rooms']]==([11,7,9] if i<2 else [11,7,9,59,9])
    assert not any(a.get('userStatusEdits') for a in p['actions'])
    for a in p['actions']:
        result=a.get('result',a)
        if result.get('IsGameInput'): assert not any('not sent, because' in u for u in result['Uncertainties'])
    if i>=2:
        r=next(r for r in p['records'] if r['stage']=='late-conversation')
        assert r['status']['CanEdit'] and r['snapshot']['RoomId']==9
        assert [r['snapshot']['KnownGlobalVariables'][k] for k in ('bit[0xD7B]','bit[0xDAC]','bit[0xDAD]','bit[0xDAE]')]==[1,1,1,0]
        assert [(d['Slot'],d['ForwardVerb'],d['ReverseVerb'],d['Reversible']) for d in r['status']['Drafts'] if d['Slot'] in (11,12,13)]==[(11,77,0,False),(12,78,79,True),(13,80,81,True)]
        assert 'Script 07 +0x002E: startObject runs object 272 verb 0x36' in p['trace']
    for r in p['records']:
        for a,b in zip(p['records'][0]['status']['Drafts'],r['status']['Drafts'],strict=True):
            assert a['Notes']==b['Notes'] and a['Reversible']==b['Reversible']
preserved=read(base/'preserved-before.json'); assert all(digest(f)==h for f,h in preserved.items())
protected=read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(f)==h.lower() for f,h in protected.items())
rtk='C:/Users/adelc/AppData/Local/Microsoft/WinGet/Links/rtk.exe'
git=lambda *args:subprocess.check_output([rtk,'proxy','git',*args],text=True)
assert not set(git('diff','--cached','--name-only').splitlines()).intersection(protected)
assert not git('diff','HEAD','--','src/SCUMMexplorer.Core','src/SCUMMexplorer.App/loom-replays.json')
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name'])==f['sha256'].lower()
summary=dict(outcome='No gain: prerequisite conversation completes, then probe draft-direction assertion fails.',diagnosticCycles=2,cycle1Frames=3322,cycle2Frames=1768,cycle2CompletedPrefixScopes=3,coverage=dict(audited=205,freshSingleAction=169,total=235,unchanged=True),protectedExactUnstaged=57,preservedFiles=len(preserved),nextExperiment='Select by ForwardVerb OR ReverseVerb: slot11 forward4D, slot12 reverse4F, slot13 reverse51. Supported staff/seven notes/learned11,12,13. Rerun actual50->7->9 and double-click272 before paired casts in a later session.',evidence={str(p):digest(p) for p in paths})
(base/'summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print(json.dumps(summary,indent=2))
