import hashlib,json,re,subprocess
from pathlib import Path
base=Path('.verification/global-scripts-input-20261003')
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
pp=Path('Exe/GlobalInput-20261003/l1/input.json'); p=read(pp)
catalog_path=Path('src/SCUMMexplorer.App/loom-replays.json'); catalog=read(catalog_path)
assert p['failure'] is None and p['frames']==9610 and p['sourceFiles']==catalog['sourceFiles']
assert [r['stage'] for r in p['records']][-4:]==['in34','gate-first','after-first','gate207'] and p['rooms'][-1]['room']==34
assert 'cutscene at Room 34 local script 207 +0x0079 runs script 2' in p['boundaries'] and 'endCutscene at Room 34 local script 207 +0x0100 runs script 3' in p['boundaries']
assert sum(t.startswith('cutscene at') for t in p['boundaries'])==sum(t.startswith('endCutscene at') for t in p['boundaries'])
edits=[a for a in p['actions'] if isinstance(a,dict) and a.get('userStatusEdits')]; assert len(edits)==1 and edits[0]['staffHeld'] is True and set(edits[0]['learnedSlots'])=={0,5,10,11}  # labelled supported staff/notes setup only
for a in p['actions']:
    res=a.get('result',a) if isinstance(a,dict) else a
    if isinstance(res,dict) and res.get('IsGameInput'): assert not any('not sent, because' in u for u in res['Uncertainties'])
ev=[e for e in p['events'] if e['SourceLabel']=='Room 34 local script 207' and e['Operation'] in('cutscene','endCutscene')]
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
    extra=out-inventory; assert extra<={('Room 45 object 656 Verb 0x38','+0x0005')},extra  # same code as the inventoried Verb 0x36 entry, not a separate site
    return out&inventory
def sites(lines):
    out=set()
    for line in lines:
        m=re.fullmatch(r'cutscene at (.+) (\+0x[0-9A-F]+) runs script 2',line)
        if m:
            o=re.sub(r'^Script (\d+)$',lambda x:f'script {int(x[1]):03}',m[1]); out.add((o.replace(' local script ',' local ').replace(' Object ',' object '),m[2]))
    extra=out-inventory; assert extra<={('Room 45 object 656 Verb 0x38','+0x0005')},extra  # same code as the inventoried Verb 0x36 entry, not a separate site
    return out&inventory
remaining=read('.verification/global-scripts-input-20261003/remaining-sites5.json')
before=inventory-{(r['owner'],r['offset']) for r in remaining}; assert len(before)==233
after=before|sites(p['boundaries']); new=after-before
assert new=={('Room 34 local 207','+0x0079')} and len(after)==234
protected=read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(f)==h.lower() for f,h in protected.items())
rtk='C:/Users/adelc/AppData/Local/Microsoft/WinGet/Links/rtk.exe'
git=lambda *x:subprocess.check_output([rtk,'proxy','git',*x],text=True)
assert not set(git('diff','--cached','--name-only').splitlines()).intersection(protected)
assert not git('diff','HEAD','--','src/SCUMMexplorer.Core','src/SCUMMexplorer.App/loom-replays.json')
preserved=read(base/'preserved-before2.json'); assert all(digest(f)==h for f,h in preserved.items())
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name'])==f['sha256'].lower()
summary=dict(outcome='Route3 prefix to scene 087 (D9D), catalog hop to Room43, actual clicks 650/639/653+Escape/656/655/639 (096), 684 Look (097) into Room34 layout A, two clicks on 559 (098 then 207) run local207 +0079/end0100',inputFrames=9610,completedScopes=0,newSites=[dict(owner=o,offset=x) for o,x in sorted(new)],literalCoverage=dict(validated=169,total=235,unchanged=True),auditedSiteUnion=dict(before=233,after=234,total=235,percent=round(234/235*100,2),remaining=1),diagnosticCycles=2,protectedExactUnstaged=57,preservedFiles=len(preserved),evidence={str(f):digest(f) for f in [pp,code_path,catalog_path]})
for n,v in [('summary6.json',summary),('remaining-sites6.json',[dict(owner=o,offset=x) for o,x in sorted(inventory-after)])]: (base/n).write_text(json.dumps(v,indent=2)+chr(10),encoding='utf-8')
print(json.dumps({k:summary[k] for k in('newSites','auditedSiteUnion','protectedExactUnstaged')},indent=2))
