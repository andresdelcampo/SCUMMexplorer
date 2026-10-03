import hashlib,json,re,subprocess
from pathlib import Path
base=Path('.verification/room09-fix-20261003')
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
pp=Path('Exe/Room09Fix-20261003/cycle-1/input.json'); dp=pp.with_name('direct.json')
p,d=read(pp),read(dp)
catalog_path=Path('src/SCUMMexplorer.App/loom-replays.json'); catalog=read(catalog_path)
expected=[('Room 09 Object 273 Verb 0x4D','000D','0076'),('Room 09 local script 223','0000','02C4'),('Room 09 local script 224','0000','041B')]
boundaries=[s for o,a,b in expected for s in (f'cutscene at {o} +0x{a} runs script 2',f'endCutscene at {o} +0x{b} runs script 3')]
for q,frames in ((p,3300),(d,3147)):
    assert q['failure'] is None and q['frames']==frames and q['boundaries'][6:]==boundaries and q['boundaries'][:6][-2:]==['cutscene at Script 101 +0x0000 runs script 2','endCutscene at Script 101 +0x08B3 runs script 3']
    assert q['sourceFiles']==catalog['sourceFiles'] and [r['room'] for r in q['rooms']]==[11,7,9,59,9]
    for r in q['records']:
        assert r['status']['CanEdit']
        for a,b in zip(q['records'][0]['status']['Drafts'],r['status']['Drafts'],strict=True): assert a['Notes']==b['Notes'] and a['Reversible']==b['Reversible']
    assert [(a['verb'],a['Slot'],a['reverse'],a['notes']) for a in q['actions'] if 'verb' in a]==[(77,11,False,[6,1,1,6]),(79,12,True,[1,1,1,7]),(81,13,True,[1,7,1,7])]
    assert [a for a in q['actions'] if a.get('userStatusEdits')]==[dict(userStatusEdits=True,staffHeld=True,notes=7,learnedSlots=[11,12,13])]
    for a in q['actions']:
        res=a.get('result',a)
        if res.get('IsGameInput'): assert not any('not sent, because' in u for u in res['Uncertainties'])
    assert not q['records'][-1]['snapshot']['IsFaithful']
assert [t for t in p['trace'] if t.startswith('Script 07 +0x002E: startObject')][-3:]==[f'Script 07 +0x002E: startObject runs object 273 verb 0x{v}' for v in ('4D','4F','51')]
assert len([a for a in p['actions'] if a.get('Input','').startswith('The key ')])==12
labels={'Room 09 local script 223','Room 09 local script 224','Script 101','Room 09 Object 273 Verb 0x4D','Room 09 Object 273 Verb 0x4F','Room 09 Object 273 Verb 0x51'}
body=lambda q:[e for e in q['events'] if e['SourceLabel'] in labels]
a,b=body(d),body(p)
assert len(a)==len(b)==252 and all(e['Executed'] for e in a+b)
differences=[]
for i,(x,y) in enumerate(zip(a,b,strict=True)):
    delta={k:dict(direct=x[k],input=y[k]) for k in x if x[k]!=y[k]}
    if delta:
        assert set(delta)=={'Certainty'}; differences.append(dict(index=i,source=y['SourceLabel'],offset=y['Offset'],differences=delta))
assert len(differences)==3
code_path=Path('Exe/VerificationSession-20261001/native-code-1/code.tsv'); assert digest(code_path)=='0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569'
code=[l.split('\t') for l in code_path.read_text(encoding='utf-8-sig').splitlines()]
inventory={(r[0],r[1]) for r in code if len(r)>=3 and r[2]=='cutscene'}; assert len(inventory)==235
def sites(lines):
    out=set()
    for line in lines:
        m=re.fullmatch(r'cutscene at (.+) (\+0x[0-9A-F]+) runs script 2',line)
        if m:
            o=re.sub(r'^Script (\d+)$',lambda x:f'script {int(x[1]):03}',m[1]); out.add((o.replace(' local script ',' local ').replace(' Object ',' object '),m[2]))
    assert out<=inventory; return out
remaining=read('.verification/room34-watcher-20261003/remaining-sites.json')
before=inventory-{(r['owner'],r['offset']) for r in remaining}; assert len(before)==215
after=before|sites(p['boundaries']); new=after-before
assert new=={('Room 09 local 223','+0x0000'),('Room 09 local 224','+0x0000'),('Room 09 object 273 Verb 0x4D','+0x000D'),('script 101','+0x0000')} and len(after)==219
protected=read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(f)==h.lower() for f,h in protected.items())
rtk='C:/Users/adelc/AppData/Local/Microsoft/WinGet/Links/rtk.exe'
git=lambda *x:subprocess.check_output([rtk,'proxy','git',*x],text=True)
assert not set(git('diff','--cached','--name-only').splitlines()).intersection(protected)
assert not git('diff','HEAD','--','src/SCUMMexplorer.Core','src/SCUMMexplorer.App/loom-replays.json')
preserved=read(base/'preserved-before.json'); assert all(digest(f)==h for f,h in preserved.items())
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name'])==f['sha256'].lower()
summary=dict(outcome='Room09 prefix plus three casts complete via accepted input.',inputFrames=3300,directFrames=3147,completedScopes=6,recordedBodyEvents=252,certaintyOnlyDifferences=3,newSites=[dict(owner=o,offset=x) for o,x in sorted(new)],literalCoverage=dict(validated=169,total=235,unchanged=True),auditedSiteUnion=dict(before=215,after=219,total=235,percent=round(219/235*100,2),remaining=16),diagnosticCycles=1,protectedExactUnstaged=57,preservedFiles=len(preserved),evidence={str(f):digest(f) for f in [pp,dp,code_path,catalog_path]})
for n,v in [('summary.json',summary),('event-differences.json',differences),('remaining-sites.json',[dict(owner=o,offset=x) for o,x in sorted(inventory-after)])]: (base/n).write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:summary[k] for k in('newSites','auditedSiteUnion','protectedExactUnstaged')},indent=2))
