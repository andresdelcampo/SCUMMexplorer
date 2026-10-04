"""Assert observed outcomes, pair input modes, and hash fresh evidence."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import sys

base = Path(__file__).parent
output = Path('Exe/Room41CertaintySession-20261002')
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p): return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def text(p):
    data = p.read_bytes()
    return data.decode('utf-16' if data.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig')
def sample(s):
    keys = ['var[0x062]', 'var[0x064]', 'var[0x067]', 'var[0x07A]',
            'var[0x03A]', 'var[0x03B]', 'var[0x065]', 'bit[0xD6C]', 'bit[0xD6F]', 'bit[0xD70]']
    keys += [f'var[0x{i:03X}]' for i in range(0x56, 0x5A)]
    return dict(stage=s['stage'], frame=s['frame'], eventCount=s['eventCount'],
        values={k:s['snapshot']['KnownGlobalVariables'].get(k) for k in keys},
        estimatedKeys=[k for k in keys if k in s['estimatedVariables']],
        ego=next({k:a.get(k) for k in ('Room','X','Y','Moving','Estimate')} for a in s['actors'] if a['Number']==1))

packets = {}
for folder in ('room41-1','egg-first-1','dye-last-1','preserved-1'):
    for p in sorted((output/folder).glob('*.json')):
        d = load(p)
        assert not d.get('failure')
        assert not any(any(k in a for k in ('prerequisiteNotReached','missingOverride','missingScript','missingTouchable')) for a in d['actions'])
        packets[d['route']['id']] = (p, d)
assert len(packets)==13
for name in ('level4-egg-first.json','level4-dye-last.json','preserved-recipes.json'):
    assert (base/name).read_bytes()==(Path('.verification/archive/native-prerequisites-20261002')/name).read_bytes()
summaries = []
for name,(p,d) in packets.items():
    last = d['snapshots'][-1]
    begun = sum(b.startswith('cutscene at ') for b in d['boundaries'])
    ended = sum(b.startswith('endCutscene at ') for b in d['boundaries'])
    room = last['snapshot']['RoomId']
    if name.startswith('room41-raw'):
        level = int(name[10])
        assert (room,begun,ended)==(41,15 if level==4 else 12,14 if level==4 else 11)
        before = next(s for s in d['snapshots'] if s['stage']=='draft-4-before')
        events = d['events'][before['eventCount']:]
        scan = [e for e in events if e['SourceLabel']=='Script 22' and e['Offset']==0x69]
        assert [e['Certainty'] for e in scan]==([1]*5 if level==4 else [0,0,1,1,1])
        assert any(e['SourceLabel']=='Script 90' and e['Offset']==5 and e['TargetId']==42 and e['Certainty']==1 for e in events)
        assert not any(c['room']==42 for c in d['roomChanges'])
    elif name=='direct41-source-level4-skip204': assert (room,begun,ended)==(43,15,15)
    elif name.startswith('native-level4-'):
        assert (room,begun,ended)==(62 if 'dye-wheel' in name else 6,9,9)
        v=last['snapshot']['KnownGlobalVariables']
        assert v['var[0x07A]']==4 and v['bit[0xD6F]']==0
        assert 'var[0x07A]' in last['estimatedVariables']
        assert [v[f'var[0x{i:03X}]'] for i in (0x32,0x34,0x36)]==[0x7282,0x7400,0x7200]
    elif name=='late096-reached44': assert (room,begun,ended)==(43,4,3)
    elif name=='late096-source-skip095': assert (room,begun,ended)==(46,4,4)
    else: assert name.startswith('sleep-source') and (room,begun,ended)==(18,7,7)
    stages=[sample(s) for s in d['snapshots'] if s['stage'].startswith('draft-4') or s['stage']=='final'
            or name=='room41-raw4-click' and s['stage'] in ('draft-2-before','draft-2-key-note-1','draft-2-key-note-2')]
    start=next((s['eventCount'] for s in d['snapshots'] if s['stage']=='draft-4-before'),len(d['events']))
    interesting=[e for e in d['events'][start:] if e['SourceLabel'] in ('Script 10','Script 20','Script 22','Script 26','Script 90') and e['Operation'] in ('startScript','chainScript','cutscene','loadRoom','endCutscene')]
    summaries.append(dict(id=name, packet=digest(p), elapsed=d['elapsed'], room=room, begun=begun, ended=ended,
        boundaries=d['boundaries'], roomChanges=d['roomChanges'], stages=stages,
        events=[{k:e.get(k) for k in ('SourceLabel','Offset','Operation','Operands','Certainty','TargetId','EstimatedArgumentIndexes')} for e in interesting]))

for level in (4,6):
    a=packets[f'room41-raw{level}-click'][1]; b=packets[f'room41-raw{level}-key'][1]
    assert a['elapsed']==b['elapsed'] and a['roomChanges']==b['roomChanges'] and a['boundaries']==b['boundaries']
    # Script005's mouse and keyboard entry branches have different caller
    # offsets. Their downstream source events must agree exactly.
    assert [e for e in a['events'] if e['SourceScriptId']!=5]==[e for e in b['events'] if e['SourceScriptId']!=5]
    av=[sample(s) for s in a['snapshots'] if s['stage'].startswith('draft-4')]
    bv=[sample(s) for s in b['snapshots'] if s['stage'].startswith('draft-4')]
    for x,y in zip(av,bv):
        x.pop('stage'); y.pop('stage'); assert x==y

traces_path=output/'certainty-1/traces.json'
traces=load(traces_path)
for r in traces:
    native4='raw4' in r['packet']
    assert r['gate']['PathEstimated']==native4
    assert r['scan']['UnresolvedReason'] is None
    assert [t for t in r['scan']['Trace'] if 'branch at' in t]==[
        f"branch at +0x{'0000' if native4 else '007D'} reads a seeded pick; later steps are estimates"]
    if native4:
        assert r['dye']['Trace'][0]=='branch at +0x000A reads a seeded pick; later steps are estimates'
source=load(base/'source-domains.json')
assert len(source['comparisons'])==51
(base/'route-summary.json').write_text(json.dumps(dict(routes=summaries,
    pairedInputModesHaveIdenticalDownstreamEventsStateTimingAndRooms=True, isolatedTracePacket=digest(traces_path),
    isolatedTraces=traces, sourceDomainPacket=digest(base/'source-domains.json')),indent=2)+'\n',encoding='utf-8')
print('13 fresh routes verified; click/key pairs identical; isolated provenance and51 domain comparisons verified.')

if '--checks' in sys.argv:
    prior=Path('Exe/DraftScopeSession-20261001/final-audits-1')
    old=load(prior/'literal/audit.json'); new=load(output/'audits-1/literal/audit.json')
    keys=('owner','status','frames','first','last','everOpen','begun','ended','rooms','sceneTrace')
    assert len(new['results'])==363 and new['sourceFiles']==old['sourceFiles']
    assert [{k:r.get(k) for k in keys} for r in new['results']]==[{k:r.get(k) for k in keys} for r in old['results']]
    for name in ('established.json','sequences.json'): assert load(prior/name)==load(output/'audits-1'/name)
    assert 'blocked: 0 instruction sites' in text(output/'audits-1/blockers.txt')
    assert 'Passed:   289' in text(output/'checks-1/core-tests.txt')
    assert '0 Warning(s)' in text(output/'checks-1/release-build.txt') and '0 Error(s)' in text(output/'checks-1/release-build.txt')
    assert '0 Warning(s)' in text(output/'checks-1/final-release-build.txt') and '0 Error(s)' in text(output/'checks-1/final-release-build.txt')
    files=('checks-1/core-tests.txt','checks-1/release-build.txt','checks-1/final-release-build.txt','audits-1/literal/audit.json','audits-1/established.json','audits-1/sequences.json','audits-1/blockers.txt')
    check=dict(core=289,rootCount=363,rootStatuses=dict(Counter(r['status'] for r in new['results'])),comparedFields=keys,
        sourceFingerprintsUnchanged=True,rootResultsUnchanged=True,membershipsUnchanged=True,sequencesUnchanged=True,blockedRandomSites=0,
        releaseWarnings=0,releaseErrors=0,reports=[digest(output/n) for n in files])
    (base/'check-summary.json').write_text(json.dumps(check,indent=2)+'\n',encoding='utf-8')
    print('Core289;363 roots/fingerprints/memberships/sequences unchanged; Blockers0; Release clean.')
