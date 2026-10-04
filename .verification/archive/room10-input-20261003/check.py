"""Qualify only completed input evidence; keep literal and audited coverage separate."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

base = Path('.verification/archive/room10-input-20261003')
read = lambda p: json.loads(Path(p).read_text(encoding='utf-8-sig'))
digest = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
paths = [Path(f'Exe/Room10Input-20261003/{cycle}/{mode}.json') for cycle in ('cycle-1','cycle-2') for mode in ('direct','input')]
failed_direct, failed_input, direct, p = map(read, paths)
catalog_path = Path('src/SCUMMexplorer.App/loom-replays.json')
catalog = read(catalog_path)
expected = [('local script 200','0039','0102'),('local script 200','0110','01AB'),
    ('local script 212','0000','007C'),('Object 296 Verb 0x3B','000C','005D'),('local script 213','0000','002F')]
boundaries = [s for owner,start,end in expected for s in (
    f'cutscene at Room 10 {owner} +0x{start} runs script 2',
    f'endCutscene at Room 10 {owner} +0x{end} runs script 3')]
for packet,frames in ((failed_direct,3657),(failed_input,3740)):
    assert packet['failure'] and 'missing endCutscene at Room 10 local script 213 +0x002F' in packet['failure']
    assert packet['frames']==frames and packet['boundaries']==boundaries[:8]
for packet,frames in ((direct,1113),(p,1266)):
    assert packet['failure'] is None and packet['frames']==frames
    assert packet['boundaries']==boundaries and packet['sourceFiles']==catalog['sourceFiles']
    assert [r['room'] for r in packet['rooms']]==[5,10]
    for r in packet['records']:
        assert r['status']['CanEdit'] and not r['stage'].endswith('timeout')
        assert not any(re.search('unsupported|unmodelled',u,re.I) for u in r['snapshot']['Uncertainties'])
        for original,current in zip(packet['records'][0]['status']['Drafts'],r['status']['Drafts'],strict=True):
            assert original['Notes']==current['Notes'] and original['Reversible']==current['Reversible']
    assert not packet['records'][-1]['snapshot']['IsFaithful']
    stages={r['stage']:r for r in packet['records']}
    for stage,values in {'open':(308,None,1),'feeding-close':(308,None,1),
        'leave-for-room5':(309,1,0),'return-from-room5':(309,1,0),'close':(307,1,0)}.items():
        # Missing DD2 in early snapshots is deliberately not treated as known zero.
        g=stages[stage]['snapshot']['KnownGlobalVariables']
        assert tuple(g.get(k) for k in ('var[0x0D5]','bit[0xDD2]','bit[0xDD3]'))==values
    assert [(a['X'],a['Y'],a['WalkBox']) for a in stages['close']['actors'] if a['Number']==1]==[(62,95,1)]
    assert [a for a in packet['actions'] if a.get('userStatusEdits')]==[dict(userStatusEdits=True,staffHeld=True,notes=3,learnedSlot=0)]
for a in p['actions']:
    result=a.get('result',a)
    if 'IsGameInput' in result:
        assert result['IsGameInput'] and not any('not sent, because' in u for u in result['Uncertainties'])
walks=[a for a in p['actions'] if 'index' in a]
assert [(a['index'],a['x'],a['y']) for a in walks]==[(3,182,53),(7,280,110)]
for a in walks:
    box=next(b for b in p['geometry']['Boxes'] if b['Index']==a['index'])
    assert (a['x'],a['y'])==tuple(sum(c[k] for c in box['Corners'])//4 for k in ('X','Y'))
casts=[a for a in p['actions'] if 'reverse' in a]
assert [(a['verb'],a['Slot'],a['reverse'],a['notes']) for a in casts]==[(58,0,False,[3,1,3,2]),(59,0,True,[2,3,1,3]),(59,0,True,[2,3,1,3])]
keys=[a for a in p['actions'] if a.get('Input','').startswith('The key ')]
assert len(keys)==12
dispatch=[t for t in p['trace'] if re.fullmatch(r'Script 07 \+0x002E: startObject runs object (296|285|202) verb 0x(3A|3B|38)',t)]
assert dispatch==[f'Script 07 +0x002E: startObject runs object {o} verb 0x{v}' for o,v in [(296,'3A'),(296,'3B'),(285,'38'),(202,'38'),(296,'3B')]]
labels={f'Room 10 local script {n}' for n in (200,211,212,213)}|{'Room 10 Object 296 Verb 0x3A','Room 10 Object 296 Verb 0x3B'}
body=lambda q:[e for e in q['events'] if e['SourceLabel'] in labels]
a,b=body(direct),body(p)
assert len(a)==len(b)==86 and all(e['Executed'] and e['Certainty']==1 for e in b)
differences=[]
for i,(x,y) in enumerate(zip(a,b,strict=True)):
    delta={k:dict(direct=x[k],input=y[k]) for k in x if x[k]!=y[k]}
    if delta:
        assert delta=={'Certainty':dict(direct=0,input=1)}
        differences.append(dict(index=i,source=y['SourceLabel'],offset=y['Offset'],differences=delta))
assert len(differences)==30
loads=[e for e in p['events'] if e['SourceLabel'] in ('Room 10 Object 285 Verb 0x38','Room 05 Object 202 Verb 0x38')]
assert [(e['Operation'],e['Offset'],e['TargetId']) for e in loads]==[('startScript',0,214),('loadRoomWithEgo',3,5),('loadRoomWithEgo',0,10)]
assert all(e['Executed'] and e['Certainty']==1 for e in loads)
code_path=Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')
assert digest(code_path)=='0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569'
code=[line.split('\t') for line in code_path.read_text(encoding='utf-8-sig').splitlines()]
inventory={(r[0],r[1]) for r in code if len(r)>=3 and r[2]=='cutscene'}
assert len(inventory)==235
for owner,offset,operation,operand in [
    ('Room 10 local 200','+0x0032','isEqual','0x0003'),('Room 10 local 200','+0x0106','isEqual','0x0007'),
    ('Room 10 local 212','+0x0035','move','bit[0xDD3] = 0x0001'),
    ('Room 10 local 214','+0x0005','move','bit[0xDD3] = 0x0000'),
    ('Room 10 local 214','+0x000A','move','bit[0xDD2] = 0x0001'),
    ('Room 10 local 213','+0x0023','move','var[0x0D5] = 0x0133')]:
    assert any(r[:3]==[owner,offset,operation] and operand in r[3] for r in code)
def sites(lines):
    result=set()
    for line in lines:
        m=re.fullmatch(r'cutscene at (.+) (\+0x[0-9A-F]+) runs script 2',line)
        if not m: continue
        owner=re.sub(r'^Script (\d+)$',lambda x:f'script {int(x[1]):03}',m[1])
        result.add((owner.replace(' local script ',' local ').replace(' Object ',' object '),m[2]))
    assert result<=inventory
    return result
literal_path=Path('Exe/EstimatedRoomLoads-20261002/audits/literal/audit.json')
literal=set().union(*(sites(r['sceneTrace']) for r in read(literal_path)['results'] if r['status']=='Validated state preview'))
before=literal|set().union(*(sites(r['source'].splitlines()) for r in catalog['replays'] if 'steps' in r))
prior_paths=[Path(x) for x in ('Exe/Room34Sequences-20261002/input-2/keyboard.json','Exe/Room25Input-20261002/cycle-1/input.json','Exe/Room23Input-20261002/cycle-2/input.json')]
for path in prior_paths:
    q=read(path)
    assert q['failure'] is None
    before |= sites(q['boundaries'])
assert len(literal)==169 and len(before)==188
assert inventory-before=={(r['owner'],r['offset']) for r in read('.verification/archive/room23-input-20261002/remaining-sites.json')}
after=before|sites(p['boundaries'])
assert len(after-before)==4 and len(after)==192
protected=read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(f)==h.lower() for f,h in protected.items())
assert not set(subprocess.check_output(['rtk','proxy','git','diff','--cached','--name-only'],text=True).splitlines()).intersection(protected)
assert not subprocess.check_output(['rtk','proxy','git','diff','HEAD','--','src/SCUMMexplorer.Core',str(catalog_path)],text=True)
for f in catalog['sourceFiles']: assert digest(Path('Games/loom')/f['name'])==f['sha256'].lower()
# Check previous evidence against its recorded hashes without rewriting old summaries.
for topic in ('room23-input-20261002','room25-input-20261002','room34-sequences-20261002'):
    for f,h in read(f'.verification/{topic}/summary.json')['evidence'].items():
        assert digest(f)==h
summary=dict(outcome='Four Room10 sites complete through actual walking, spell keys and source exit/return; no production fix.',
    directFrames=1113,inputFrames=1266,completedScopes=5,conditionalExecutedBodyEvents=86,
    bodyDifferences=dict(count=30,only='Certainty: direct Reached; input Conditional'),dispatch=dispatch,
    sourceStateStages={r['stage']:{k:r['snapshot']['KnownGlobalVariables'].get(k) for k in ('var[0x0D5]','bit[0xDD2]','bit[0xDD3]')} for r in p['records'] if r['stage'] in ('arrival','open','feeding-close','leave-for-room5','return-from-room5','close')},
    newSites=[dict(owner=o,offset=a) for o,a in sorted(after-before)],
    literalCoverage=dict(validated=169,total=235,percent=71.91,unchanged=True),
    auditedSiteUnion=dict(before=188,after=192,total=235,percent=81.70,remaining=43),
    diagnosticCycles=2,firstCycle='Wrong expected close branch: feeding response completed; local213 absent. Retained failed pair contributes no coverage.',
    protectedExactUnstaged=57,productionAndCatalogUnchanged=True,
    limitations=['Default preview boot and initial catalog Room10 arrival are assumptions.',
        'Supported status API staff/three notes/learned slot0 are labelled exploration choices.',
        'Core input API only; no WPF, physical input, visual, audio or native full-game proof.',
        'All input body events retain Conditional provenance; direct control differs in 30 certainty fields.',
        'Recorded body events are not exhaustive instruction or writer observations.',
        'Multi-action completion does not qualify fresh single-action gaps.'],
    evidence={str(f):digest(f) for f in paths+prior_paths+[literal_path,code_path,catalog_path]})
for name,value in [('summary.json',summary),('event-differences.json',differences),('remaining-sites.json',[dict(owner=o,offset=a) for o,a in sorted(inventory-after)])]:
    (base/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:summary[k] for k in ('directFrames','inputFrames','completedScopes','conditionalExecutedBodyEvents','newSites','literalCoverage','auditedSiteUnion','protectedExactUnstaged')},indent=2))
