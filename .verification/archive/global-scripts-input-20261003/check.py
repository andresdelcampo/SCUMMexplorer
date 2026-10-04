import hashlib,json,re,subprocess
from pathlib import Path
base=Path('.verification/archive/global-scripts-input-20261003')
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
pp=Path('Exe/GlobalInput-20261003/a9/input.json'); p=read(pp)
catalog_path=Path('src/SCUMMexplorer.App/loom-replays.json'); catalog=read(catalog_path)
sites_expected=[('13','0000','0052'),('11','0000','0071'),('12','0000','004A'),('22','0138','0151'),('05','01C0','01DB'),('04','0000','0206')]
boundaries=[s for n,a,b in sites_expected for s in (f'cutscene at Script {n} +0x{a} runs script 2',f'endCutscene at Script {n} +0x{b} runs script 3')]
assert p['failure'] is None and p['frames']==943 and p['boundaries']==boundaries and p['sourceFiles']==catalog['sourceFiles']
st=[(r['stage'],r['snapshot']['RoomId']) for r in p['records']]
assert st[-2:]==[('save-screen',70),('save-cancel',70)] and all(r==34 for _,r in st[:-2])
edits=[a for a in p['actions'] if isinstance(a,dict) and a.get('userStatusEdits')]
assert edits==[dict(userStatusEdits=True,staffHeld=True,notes=3),dict(userStatusEdits=True,notes=7)]
keys=[a for a in p['actions'] if isinstance(a,dict) and 'key' in a]
assert [a['key'] for a in keys]==[113,117,113]
for a in p['actions']:
    res=a.get('result',a) if isinstance(a,dict) else a
    if isinstance(res,dict) and res.get('IsGameInput'): assert not any('not sent, because' in u for u in res['Uncertainties'])
wrong=[a for a in p['actions'] if isinstance(a,dict) and 'wrong' in a][0]['wrong']; assert wrong==[1,2,1,2]
for lab in ('Script 04','Script 05','Script 11','Script 12','Script 13','Script 22'):
    ev=[e for e in p['events'] if e['SourceLabel']==lab and e['Operation'] in('cutscene','endCutscene')]
    assert ev and all(e['Executed'] for e in ev)
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
remaining=read('.verification/archive/room02-input-20261003/remaining-sites.json')
before=inventory-{(r['owner'],r['offset']) for r in remaining}; assert len(before)==223
after=before|sites(p['boundaries']); new=after-before
assert new=={('script 004','+0x0000'),('script 005','+0x01C0'),('script 011','+0x0000'),('script 012','+0x0000'),('script 013','+0x0000'),('script 022','+0x0138')} and len(after)==229
protected=read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(f)==h.lower() for f,h in protected.items())
rtk='C:/Users/adelc/AppData/Local/Microsoft/WinGet/Links/rtk.exe'
git=lambda *x:subprocess.check_output([rtk,'proxy','git',*x],text=True)
assert not set(git('diff','--cached','--name-only').splitlines()).intersection(protected)
assert not git('diff','HEAD','--','src/SCUMMexplorer.Core','src/SCUMMexplorer.App/loom-replays.json')
preserved=read(base/'preserved-before.json'); assert all(digest(f)==h for f,h in preserved.items())
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name'])==f['sha256'].lower()
summary=dict(outcome='Six global-script sites complete via accepted keys and clicks; F5 leaves the session in the Room70 save menu.',inputFrames=943,completedScopes=6,newSites=[dict(owner=o,offset=x) for o,x in sorted(new)],literalCoverage=dict(validated=169,total=235,unchanged=True),auditedSiteUnion=dict(before=223,after=229,total=235,percent=round(229/235*100,2),remaining=6),diagnosticCycles=2,protectedExactUnstaged=57,preservedFiles=len(preserved),evidence={str(f):digest(f) for f in [pp,code_path,catalog_path]})
for n,v in [('summary.json',summary),('remaining-sites.json',[dict(owner=o,offset=x) for o,x in sorted(inventory-after)])]: (base/n).write_text(json.dumps(v,indent=2)+chr(10),encoding='utf-8')
print(json.dumps({k:summary[k] for k in('newSites','auditedSiteUnion','protectedExactUnstaged')},indent=2))
