"""Validate source routes and retain compact evidence, including failed controls."""
from pathlib import Path
from collections import Counter
import hashlib, json, sys

base=Path(__file__).parent
output=Path('Exe/NativePrerequisitesSession-20261002')
source_code=Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')
assert hashlib.sha256(source_code.read_bytes()).hexdigest()=='0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569'
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p):return dict(path=p.as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def summarize(p):
    data=load(p)
    stages={}
    for s in data['snapshots']:
        v=s['snapshot']['KnownGlobalVariables']
        stages[s['stage']]=dict(frame=s['frame'],room=s['snapshot']['RoomId'],
            level=v.get('var[0x07A]'),levelEstimated='var[0x07A]' in s['estimatedVariables'],
            failedChallenge=v.get('bit[0xD6F]'),
            draftWords=[dict(slot=i,packed=v.get(f'var[0x{0x32+2*i:03X}]'),
                estimated=f'var[0x{0x32+2*i:03X}]' in s['estimatedVariables']) for i in range(5)],
            responseWord=v.get('var[0x03B]'),responseWordEstimated='var[0x03B]' in s['estimatedVariables'],
            responseScratch=v.get('var[0x065]'),responseScratchEstimated='var[0x065]' in s['estimatedVariables'])
    boundaries=data['boundaries']
    events=[{k:e.get(k) for k in ('SourceLabel','Offset','Operation','Certainty','Operands','TargetId','EstimatedArgumentIndexes')}
        for e in data.get('events',[]) if e['Operation'] in ('startScript','chainScript','loadRoom','loadRoomWithEgo','cutscene','endCutscene')
        and any(t in e['SourceLabel'] for t in ('Script 22','Script 23','Script 25','Script 26','Script 34','Script 90','Room 62 local script 205','Room 06 local script 200','270 Verb 0x3A'))]
    stops=[a for a in data['actions'] if any(k in a for k in ('prerequisiteNotReached','missingScript','missingOverride','missingTouchable'))]
    return dict(id=data['route']['id'],packet=digest(p),elapsed=data['elapsed'],stages=stages,
        begun=sum(b.startswith('cutscene at ') for b in boundaries),ended=sum(b.startswith('endCutscene at ') for b in boundaries),
        boundaries=boundaries,roomChanges=data['roomChanges'],stops=stops,events=events,
        uncertainties=[u for u in data['uncertainties'] if 'possible Room' in u or 'not modeled' in u],failure=data.get('failure'))

rows=[]
for folder in sorted(output.iterdir()):
    if folder.is_dir():
        for p in sorted(folder.glob('*.json')):
            d=load(p)
            if 'snapshots' in d: rows.append(summarize(p))
byid={r['id']:r for r in rows}
for name,room,context in (('native-level4-egg-dye-wheel',62,'Room 62 local script 205'),('native-level4-egg-wheel-dye',6,'Room 06 local script 200')):
    r=byid[name];s=r['stages']['final']
    assert not r['stops'] and r['failure'] is None
    assert (s['room'],s['level'],s['levelEstimated'],s['failedChallenge'],r['begun'],r['ended'])==(room,4,True,0,9,9),r['id']
    assert all(w['packed']&0x6000==0x6000 for w in s['draftWords'][:3])
    assert s['draftWords'][1]['estimated'] and s['draftWords'][2]['estimated']
    assert any(e['SourceLabel']==context and e['Operation']=='chainScript' and e['TargetId']==29 and '0x0004' in e['Operands'] and e['Certainty']==1 for e in r['events'])
for name,scopes,room in (('native41-source37-arrival',(12,11),41),('native41-source-skip204',(12,11),41),('direct41-source-skip204',(12,12),43),('native41-source-level4',(15,14),41),('native41-source-level4-skip204',(15,14),41),('direct41-source-level4-skip204',(15,15),43)):
    r=byid[name]
    assert not r['stops'] and (r['begun'],r['ended'])==scopes and r['stages']['final']['room']==room,name
    if name.startswith('native41'):
        assert any(e['SourceLabel']=='Script 90' and e['Offset']==5 and e['Certainty']==1 and e['TargetId']==42 for e in r['events'])
        assert not any(c['room']==42 for c in r['roomChanges'])
for name in ('late096-source-skip095',*[f'sleep-source{a}-response{b}' for a in (369,371) for b in (390,391)]):
    r=byid[name]
    assert not r['stops'] and r['begun']==r['ended'],name
assert byid['late096-source-skip095']['stages']['final']['room']==46
assert byid['late096-reached44']['begun']==4 and byid['late096-reached44']['ended']==3
for group,select in (('level4',lambda r:r['id'].startswith('native-level4')),
                     ('room41',lambda r:'41-' in r['id']),
                     ('lessons',lambda r:r['id'].startswith(('native-dye','native-wheel','wheel-'))),
                     ('preserved',lambda r:r['id'].startswith(('late096','sleep-source')))):
    chosen=[r for r in rows if select(r)]
    assert chosen,group
    (base/(group+'-summary.json')).write_text(json.dumps(chosen,indent=2)+'\n',encoding='utf-8')
print('Validated',len(rows),'packets; native level4 in both orders, conservative Room41 controls, preserved096/20.')

if '--checks' in sys.argv:
    prior=Path('Exe/DraftScopeSession-20261001/final-audits-1')
    old=load(prior/'literal/audit.json');new=load(output/'audits-1/literal/audit.json')
    keys=('owner','status','frames','first','last','everOpen','begun','ended','rooms','sceneTrace')
    assert len(new['results'])==363 and new['sourceFiles']==old['sourceFiles']
    assert [{k:r.get(k) for k in keys} for r in new['results']]==[{k:r.get(k) for k in keys} for r in old['results']]
    for name in ('established.json','sequences.json'):assert load(prior/name)==load(output/'audits-1'/name),name
    def read(p):
        data=p.read_bytes()
        return data.decode('utf-16' if data.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
    assert 'blocked: 0 instruction sites' in read(output/'audits-1/blockers.txt')
    assert 'Passed:   289' in read(output/'checks-1/core-tests.txt')
    assert '0 Warning(s)' in read(output/'checks-1/release-build.txt') and '0 Error(s)' in read(output/'checks-1/release-build.txt')
    files=('checks-1/core-tests.txt','checks-1/release-build.txt','audits-1/literal/audit.json','audits-1/established.json','audits-1/sequences.json','audits-1/blockers.txt')
    check=dict(core=289,rootCount=363,rootStatuses=dict(Counter(r['status'] for r in new['results'])),comparedFields=keys,
        sourceFingerprintsUnchanged=True,rootResultsUnchanged=True,membershipsUnchanged=True,sequencesUnchanged=True,blockedRandomSites=0,
        releaseWarnings=0,releaseErrors=0,sourceCode=digest(source_code),reports=[digest(output/n) for n in files])
    (base/'check-summary.json').write_text(json.dumps(check,indent=2)+'\n',encoding='utf-8')
    print('Core289;363 exact root results, all fingerprints/memberships/sequences unchanged; Blockers0; Release clean.')
