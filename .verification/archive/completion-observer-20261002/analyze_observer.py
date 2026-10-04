"""Compare production observations with protocol evidence in actual instruction order.

Selected observed literal writers only; missing/aliased/external writers and
scheduler/callee/cancellation universality remain outside this evidence.
"""
from pathlib import Path
import hashlib
import json
import sys

out=Path(sys.argv[1])
prior=Path('Exe/CertaintyProtocolSession-20261002')
base=Path(__file__).parent
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def rows(p): return [json.loads(line) for line in p.read_text(encoding='utf-8').splitlines()]
def digest(p): return dict(path=p.as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def legacy(r): return r['Script'] in (10,20,22,23,30,31) or any(k in (r['Operands'] or '') for k in ('bit[0xD6C]','bit[0xD67]'))
def brief(row):
    r=row['record']
    return {k:r[k] for k in ('Sequence','Slot','ParentSlot','Run','Script','Context','Stage','Offset','Operation','PathEstimated')} | dict(frame=row['frame'])
results=[]
zero_count=0
for folder in ('room41-1','egg-first-1','dye-last-1','preserved-1'):
    for packet in sorted((out/folder).glob('*.json')):
        current=load(packet)
        assert current==load(prior/folder/packet.name), f'Whole recipe changed: {packet}'
        trace=packet.with_name(packet.stem+'-instructions.jsonl')
        records=rows(trace)
        previous=rows(prior/folder/trace.name)
        selected=[row for row in records if legacy(row['record'])]
        assert len(selected)==len(previous)
        run_map={}
        for old,new in zip(previous,selected):
            oldr,newr=old['record'],new['record']
            assert run_map.setdefault(oldr['Run'],newr['Run'])==newr['Run']
            assert old['frame']==new['frame']
            for k in ('Stage','Script','Context','Offset','Operation','Operands','PathEstimated','Estimated','Resume','Stopped','Unresolved'):
                assert oldr[k]==newr[k], (packet.name,k,oldr,newr)
            assert oldr['Values']=={k:newr['Values'][k] for k in oldr['Values']}
        assert len(set(run_map.values()))==len(run_map)
        assert all(a['record']['Sequence']<b['record']['Sequence'] for a,b in zip(records,records[1:]))
        cancellations=rows(packet.with_name(packet.stem+'-cancellations.jsonl'))
        cancelled={s for row in cancellations for s in row['record']['CancelledSlots']}
        runs={}
        for row in records: runs.setdefault(row['record']['Run'],[]).append(row)
        writes=[]
        waits=[]
        for run in runs.values():
            for i,row in enumerate(run):
                r=row['record']
                if r['Stage']!='instruction': continue
                if r['Operation']=='move' and (r['Operands'] or '').startswith('bit[0xD67] = '):
                    assert i+1<len(run)
                    writes.append((row,run[i+1]))
                if r['Script']==20 and r['Offset']==0x28 and r['Values'].get('bit[0xD67]')==0:
                    assert i+1<len(run)
                    waits.append((row,run[i+1]))
        bindings=[]
        for wait,following in waits:
            wr=wait['record']
            writer,after=max((w for w in writes if w[0]['record']['Sequence']<wr['Sequence']),key=lambda w:w[0]['record']['Sequence'])
            r=writer['record']
            assert r['Script']==31 and r['Offset'] in (0x243,0x31D)
            assert after['record']['Values']['bit[0xD67]']==0
            assert r['ParentSlot']==wr['Slot'] and r['Slot'] not in cancelled
            bindings.append(dict(writer=brief(writer),afterWrite=brief(after),wait=brief(wait),afterWait=brief(following)))
        zero_count+=len(bindings)
        detail={}
        if current['route']['id']=='room41-raw4-click':
            binding=next(b for b in bindings if not b['wait']['PathEstimated'] and b['afterWait']['PathEstimated'])
            assert binding['writer']['frame']==binding['wait']['frame']==3036
            assert binding['writer']['Offset']==0x31D and binding['wait']['Offset']==0x28 and binding['afterWait']['Offset']==0x2D
            practice=next(row for row in records if row['record']['Context']=='Room 06 local script 201' and row['record']['Offset']==0x28
                and row['record']['Stage']=='instruction')
            assert practice['frame']==2870
            detail=dict(firstCompletionBinding=binding,practiceCaller=brief(practice))
        results.append(dict(id=current['route']['id'],room=current['snapshots'][-1]['snapshot']['RoomId'],elapsed=current['elapsed'],
            fullPacketIdentical=True,priorInstructionValuesIdentical=True,zeroWaits=len(bindings),
            observations=detail,packet=digest(packet),instructions=digest(trace),cancellations=digest(packet.with_name(packet.stem+'-cancellations.jsonl'))))
assert len(results)==13 and zero_count==76
manifest=load(out/'observer-1/source-manifest.json')
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in manifest.items())
summary=dict(scope='Production opt-in copied observations of these13 selected explorer recipes; actual sequence and stable causal slot identities. Observed literal writers only, no universal external/aliased/concurrent/cancellation safety or runtime domain/path/effect promotion.',
    recipeCount=13,fullPacketsIdentical=True,priorInstructionValuesIdentical=True,observedZeroWaits=zero_count,
    native090Completed=False,estimatesCleared=False,sourceManifest=digest(out/'observer-1/source-manifest.json'),routes=results)
(base/'observer-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('13 whole packets and all prior instruction values identical;76 observed zero waits bind own child; native write/read both3036; no estimates cleared.')
