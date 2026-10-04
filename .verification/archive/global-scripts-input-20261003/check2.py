import hashlib,json,re,subprocess
from pathlib import Path
base=Path('.verification/archive/global-scripts-input-20261003')
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
pp=Path('Exe/GlobalInput-20261003/c3/input.json'); p=read(pp)
catalog_path=Path('src/SCUMMexplorer.App/loom-replays.json'); catalog=read(catalog_path)
boundaries=['cutscene at Room 17 local script 200 +0x0022 runs script 2','endCutscene at Room 17 local script 200 +0x0042 runs script 3','cutscene at Script 66 +0x0000 runs script 2','endCutscene at Script 66 +0x0152 runs script 3']
assert p['failure'] is None and p['frames']==908 and p['boundaries']==boundaries and p['sourceFiles']==catalog['sourceFiles']
assert [(r['stage'],r['snapshot']['RoomId']) for r in p['records']]==[('arrival',17),('walk-box-8',17),('walk-box-8',56)] and [r['room'] for r in p['rooms']]==[56]
assert not any(isinstance(a,dict) and a.get('userStatusEdits') for a in p['actions'])
for a in p['actions']:
    res=a.get('result',a) if isinstance(a,dict) else a
    if isinstance(res,dict) and res.get('IsGameInput'): assert not any('not sent, because' in u for u in res['Uncertainties'])
ev=[e for e in p['events'] if e['SourceLabel']=='Script 66' and e['Operation'] in('cutscene','endCutscene')]
assert len(ev)==2 and all(e['Executed'] for e in ev)
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
def sites(lines):
    out=set()
    for line in lines:
        m=re.fullmatch(r'cutscene at (.+) (\+0x[0-9A-F]+) runs script 2',line)
        if m:
            o=re.sub(r'^Script (\d+)$',lambda x:f'script {int(x[1]):03}',m[1]); out.add((o.replace(' local script ',' local ').replace(' Object ',' object '),m[2]))
    assert out<=inventory; return out
remaining=read('.verification/archive/global-scripts-input-20261003/remaining-sites.json')
before=inventory-{(r['owner'],r['offset']) for r in remaining}; assert len(before)==229
after=before|sites(p['boundaries']); new=after-before
assert new=={('script 066','+0x0000')} and len(after)==230
protected=read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(f)==h.lower() for f,h in protected.items())
rtk='C:/Users/adelc/AppData/Local/Microsoft/WinGet/Links/rtk.exe'
git=lambda *x:subprocess.check_output([rtk,'proxy','git',*x],text=True)
assert not set(git('diff','--cached','--name-only').splitlines()).intersection(protected)
assert not git('diff','HEAD','--','src/SCUMMexplorer.Core','src/SCUMMexplorer.App/loom-replays.json')
preserved=read(base/'preserved-before2.json'); assert all(digest(f)==h for f,h in preserved.items())
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name'])==f['sha256'].lower()
summary=dict(outcome='Room17 box8 click completes script066 +0000/end0152 and loads Room56.',inputFrames=908,completedScopes=2,newSites=[dict(owner=o,offset=x) for o,x in sorted(new)],literalCoverage=dict(validated=169,total=235,unchanged=True),auditedSiteUnion=dict(before=229,after=230,total=235,percent=round(230/235*100,2),remaining=5),diagnosticCycles=3,protectedExactUnstaged=57,preservedFiles=len(preserved),evidence={str(f):digest(f) for f in [pp,code_path,catalog_path]})
for n,v in [('summary2.json',summary),('remaining-sites2.json',[dict(owner=o,offset=x) for o,x in sorted(inventory-after)])]: (base/n).write_text(json.dumps(v,indent=2)+chr(10),encoding='utf-8')
print(json.dumps({k:summary[k] for k in('newSites','auditedSiteUnion','protectedExactUnstaged')},indent=2))
