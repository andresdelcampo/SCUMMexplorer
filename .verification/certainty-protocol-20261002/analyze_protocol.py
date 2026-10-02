"""Verify transparent recipes and bind observed completion to slot identity.

These are selected seeded explorer runs, never universal domain/cancellation proofs.
"""
from pathlib import Path
from collections import Counter
import hashlib
import json
import sys

base = Path(__file__).parent
output = Path(sys.argv[1])
prior = Path('Exe/CertaintyProvenanceSession-20261002')
def load(p): return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def digest(p): return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def brief(row):
    r = row['record']
    return dict(frame=row['frame'], slot=r['Slot'], parentSlot=r['ParentSlot'], run=r['Run'], script=r['Script'],
        context=r['Context'], offset=r['Offset'], operation=r['Operation'], operands=r['Operands'], pathEstimated=r['PathEstimated'],
        variables={k:dict(value=r['Values'].get(k), estimated=k in r['Estimated']) for k in
            ('local[0x000]','local[0x001]','bit[0xD6C]','bit[0xD67]','var[0x064]','var[0x07A]',
             'var[0x052]','var[0x053]','var[0x054]','var[0x055]','var[0x056]','var[0x057]','var[0x058]','var[0x059]')})

results = []
for folder in ('room41-1','egg-first-1','dye-last-1','preserved-1'):
    for packet in sorted((output/folder).glob('*.json')):
        current = load(packet)
        assert current == load(prior/folder/packet.name), f'Recipe changed: {packet}'
        trace = packet.with_name(packet.stem+'-instructions.jsonl')
        scheduler = packet.with_name(packet.stem+'-scheduler.jsonl')
        records = [json.loads(line) for line in trace.read_text(encoding='utf-8').splitlines()]
        prior_trace = prior/folder/trace.name
        previous_records = [json.loads(line) for line in prior_trace.read_text(encoding='utf-8').splitlines()]
        assert len(records)==len(previous_records)
        for previous,observed in zip(previous_records,records):
            projected = dict(observed['record'])
            projected.pop('Slot')
            projected.pop('ParentSlot')
            projected['Values'] = {k:projected['Values'][k] for k in previous['record']['Values']}
            assert dict(frame=observed['frame'],record=projected)==previous
        record_indexes = {id(row):index for index,row in enumerate(records)}
        session = [json.loads(line) for line in scheduler.read_text(encoding='utf-8').splitlines()]
        runs, enters, cancelled = {}, {}, set()
        for row in session:
            r = row['record']
            if r['Stage']=='enter':
                previous = enters.setdefault(r['Slot'], r)
                assert (previous['ParentSlot'],previous['Number'])==(r['ParentSlot'],r['Number'])
            elif r['Stage']=='cancel': cancelled.update(r['Slots'])
        for row in records:
            r = row['record']
            assert r['Slot'] in enters
            runs.setdefault(r['Run'], []).append(row)
        writers, waits = [], []
        for run in runs.values():
            for i,row in enumerate(run):
                r=row['record']
                if r['Stage']!='instruction': continue
                if r['Operation']=='move' and r['Operands'].startswith(('bit[0xD67] =','bit[0xD6C] =')):
                    assert i+1<len(run)
                    writers.append((record_indexes[id(row)], row, run[i+1]))
                if r['Script']==20 and r['Offset']==0x28:
                    assert i+1<len(run)
                    waits.append((record_indexes[id(row)], row, run[i+1]))
        # Every observed zero wait gets the last actual write and its causal caller.
        zero_waits, bindings = [], []
        for index,row,following in waits:
            r=row['record']
            if r['Values'].get('bit[0xD67]')!=0: continue
            writer_index,writer,after=max((w for w in writers if w[0]<index and w[1]['record']['Operands'].startswith('bit[0xD67] =')), key=lambda w:w[0])
            wr=writer['record']
            assert after['record']['Values']['bit[0xD67]']==0
            assert wr['Script']==31 and wr['Offset'] in (0x243,0x31D)
            assert wr['ParentSlot']==r['Slot'], 'Last completion must belong to this waiter in these selected recipes'
            assert wr['Slot'] not in cancelled
            start=next(rr for rr in records if rr['record']['Slot']==wr['Slot'] and rr['record']['Stage']=='instruction' and rr['record']['Offset']==0)
            zero_waits.append(dict(waiter=r['Slot'],writer=wr['Slot'],site=wr['Offset'],estimated=r['PathEstimated']))
            if not r['PathEstimated'] and following['record']['PathEstimated']:
                bindings.append(dict(start=brief(start), completion=brief(writer), completionAfter=brief(after),
                    wait=brief(row), waitAfter=brief(following)))
        detail={}
        if current['route']['id']=='room41-raw4-click':
            first=next(b for b in bindings if b['wait']['frame']==3036)
            assert first['completion']['frame']==3036
            assert first['wait']['offset']==0x28 and first['waitAfter']['offset']==0x2D
            practice=next(row for row in records if row['record']['Script']==31 and row['record']['Stage']=='instruction'
                and row['record']['Offset']==0x227 and row['record']['PathEstimated'])
            pr=practice['record']
            practice_start=next(row for row in records if row['record']['Slot']==pr['Slot'] and row['record']['Stage']=='begin')
            parent=enters[pr['ParentSlot']]
            assert parent['Number']==30
            caller=enters[parent['ParentSlot']]
            assert caller['Label']=='Room 06 local script 201'
            original=load('.verification/certainty-provenance-20261002/trace-summary.json')
            old_pair=next(r for r in original['routes'] if r['id']==current['route']['id'])['observations']['completionBeforeWait']
            assert old_pair['before']['frame']<first['wait']['frame']
            assert old_pair['before']['Run']!=first['completion']['run']
            detail=dict(firstPracticeStart=brief(practice_start), practiceCaller=caller,
                practiceZero=brief(practice), firstCompletionWait=first,
                previousFramePairing=dict(frame=old_pair['before']['frame'],run=old_pair['before']['Run'],
                    correction='Previous analyzer excluded same-frame writers. Actual last write and own child completion occur at3036 before the wait.'))
        result=dict(id=current['route']['id'],packet=digest(packet),instructions=digest(trace),scheduler=digest(scheduler),
            fullPacketIdentical=True,priorInstructionTraceIdentical=True,room=current['snapshots'][-1]['snapshot']['RoomId'],elapsed=current['elapsed'],
            observedSlots=len(enters),cancelledSlots=len(cancelled),zeroWaits=len(zero_waits),
            writerSites=dict(Counter(f'{w["site"]:04X}' for w in zero_waits)),
            allObservedZeroWaitsBoundToOwnUncancelledChild=True,
            firstTaintBinding=[{stage:{k:v for k,v in item.items() if k!='variables'} for stage,item in b.items()} for b in bindings[:1]],
            observations=detail)
        results.append(result)
assert len(results)==13
manifest=load(output/'observer-1/source-manifest.json')
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in manifest.items())
summary=dict(scope='Actual selected explorer slot/caller/writer chronology across resumptions. No numeric domain, all-path, external effect, cancellation-safe protocol or original-game proof.',
    recipeCount=13,fullPacketsIdentical=True,productionManifest=digest(output/'observer-1/source-manifest.json'),routes=results,
    native090Completed=False,estimatesCleared=False)
(base/'protocol-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
for r in results: print(r['id'], 'identical', r['room'], 'bound zero waits',r['zeroWaits'])
