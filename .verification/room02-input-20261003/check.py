import hashlib,json,re,subprocess
from pathlib import Path
base=Path('.verification/room02-input-20261003')
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
pp=Path('Exe/Room02Input-20261003/cycle-2/input.json'); p=read(pp)
catalog_path=Path('src/SCUMMexplorer.App/loom-replays.json'); catalog=read(catalog_path)
ends=[('001D','003E'),('0049','006A'),('001D','003E'),('0075','0092'),('009D','00BA')]
boundaries=[s for a,b in ends for s in (f'cutscene at Script 45 +0x{a} runs script 2',f'endCutscene at Script 45 +0x{b} runs script 3')]
assert p['failure'] is None and p['frames']==535 and p['boundaries']==boundaries and p['sourceFiles']==catalog['sourceFiles']
assert [r['stage'] for r in p['records']]==['arrival','walk-box-4','walk-box-5','walk-box-10','walk-box-10','walk-box-11']
for r in p['records']: assert r['status']['CanEdit'] and r['snapshot']['RoomId']==2
assert not any(a.get('userStatusEdits') for a in p['actions'])
for a in p['actions']:
    res=a.get('result',a)
    if res.get('IsGameInput'): assert not any('not sent, because' in u for u in res['Uncertainties'])
ev=[e for e in p['events'] if e['SourceLabel']=='Script 45']
assert len(ev)>=20 and all(e['Executed'] for e in ev if e['Operation'] in('cutscene','endCutscene','putActor','walkActorTo'))
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
remaining=read('.verification/room09-fix-20261003/remaining-sites.json')
before=inventory-{(r['owner'],r['offset']) for r in remaining}; assert len(before)==219
after=before|sites(p['boundaries']); new=after-before
assert new=={('script 045',o) for o in ('+0x001D','+0x0049','+0x0075','+0x009D')} and len(after)==223
protected=read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(f)==h.lower() for f,h in protected.items())
rtk='C:/Users/adelc/AppData/Local/Microsoft/WinGet/Links/rtk.exe'
git=lambda *x:subprocess.check_output([rtk,'proxy','git',*x],text=True)
assert not set(git('diff','--cached','--name-only').splitlines()).intersection(protected)
assert not git('diff','HEAD','--','src/SCUMMexplorer.Core','src/SCUMMexplorer.App/loom-replays.json')
preserved=read(base/'preserved-before.json'); assert all(digest(f)==h for f,h in preserved.items())
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name'])==f['sha256'].lower()
summary=dict(outcome='Room02 script045 four box-change scenes complete via accepted clicks.',inputFrames=535,completedScopes=5,newSites=[dict(owner=o,offset=x) for o,x in sorted(new)],literalCoverage=dict(validated=169,total=235,unchanged=True),auditedSiteUnion=dict(before=219,after=223,total=235,percent=round(223/235*100,2),remaining=12),diagnosticCycles=2,protectedExactUnstaged=57,preservedFiles=len(preserved),evidence={str(f):digest(f) for f in [pp,code_path,catalog_path]})
for n,v in [('summary.json',summary),('remaining-sites.json',[dict(owner=o,offset=x) for o,x in sorted(inventory-after)])]: (base/n).write_text(json.dumps(v,indent=2)+chr(10),encoding='utf-8')
print(json.dumps({k:summary[k] for k in('newSites','auditedSiteUnion','protectedExactUnstaged')},indent=2))
