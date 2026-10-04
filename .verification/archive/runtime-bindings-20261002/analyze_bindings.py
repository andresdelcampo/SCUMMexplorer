"""Selected-run operand provenance; no hypothetical domain is supplied to playback.

Bindings are pre-instruction snapshots. Numeric writes are confirmed only at the
following observation in the same invocation. Missing/filtered/aliased/external
or concurrent evidence cannot be treated as a universally unique writer.
"""
from pathlib import Path
import hashlib
import json
import sys

out = Path(sys.argv[1])
base = Path(__file__).parent
prior = Path('Exe/CompletionObserverSession-20261002')

def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def rows(p): return [json.loads(line) for line in p.read_text(encoding='utf-8').splitlines()]
def digest(p): return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def ins(rs, script=None, offset=None, slot=None, context=None):
    return [row for row in rs if row['record']['Stage']=='instruction'
        and (script is None or row['record']['Script']==script)
        and (offset is None or row['record']['Offset']==offset)
        and (slot is None or row['record']['Slot']==slot)
        and (context is None or row['record']['Context']==context)]
def binding(row, role):
    return next(b for b in row['record']['OperandBindings'] if b['Role']==role)
def arguments(row):
    return [b for b in row['record']['OperandBindings'] if b['Role']=='argument']
def brief(row):
    r=row['record']
    return dict(frame=row['frame'], **{k:r[k] for k in
        ('Sequence','Slot','ParentSlot','Run','Script','Context','Offset','Operation','Operands','PathEstimated','OperandBindings')})
def same_run_next(rs):
    nxt={}
    latest={}
    for row in reversed(rs):
        r=row['record']
        if r['Run'] in latest: nxt[r['Sequence']]=latest[r['Run']]
        latest[r['Run']]=row
    return nxt
def confirm_move(row, nxt):
    dest, source = row['record']['OperandBindings']
    assert dest['Role']=='destination-before' and source['Role']=='source'
    after=nxt[row['record']['Sequence']]
    assert after['record']['Values'][dest['Key']]==source['Value']
    return after

routes=[]
zero_count=0
for folder in ('room41-1','egg-first-1','dye-last-1','preserved-1'):
    for packet in sorted((out/folder).glob('*.json')):
        current=load(packet)
        assert current==load(prior/folder/packet.name), f'Whole recipe changed: {packet}'
        trace=packet.with_name(packet.stem+'-instructions.jsonl')
        rs=rows(trace)
        previous=rows(prior/folder/trace.name)
        assert len(rs)==len(previous)
        for old,new in zip(previous,rs):
            projected=dict(new['record'])
            projected.pop('OperandBindings')
            projected['Values']={k:projected['Values'][k] for k in old['record']['Values']}
            assert dict(frame=new['frame'],record=projected)==old
        cancel_path=packet.with_name(packet.stem+'-cancellations.jsonl')
        cancellations=rows(cancel_path)
        old_cancellations=rows(prior/folder/cancel_path.name)
        assert len(cancellations)==len(old_cancellations)
        for old,new in zip(old_cancellations,cancellations):
            projected=dict(new['record']); projected.pop('OperandBindings')
            assert dict(frame=new['frame'],record=projected)==old
        cancelled={s for row in cancellations for s in row['record']['CancelledSlots']}
        nxt=same_run_next(rs)
        writes=[row for row in ins(rs) if row['record']['Operation']=='move'
            and row['record']['Operands'].startswith('bit[0xD67] =')]
        waits=[row for row in ins(rs,20,0x28) if binding(row,'condition')['Value']==0]
        for wait in waits:
            wr=wait['record']
            writer=max((w for w in writes if w['record']['Sequence']<wr['Sequence']),key=lambda w:w['record']['Sequence'])
            w=writer['record']
            assert w['Script']==31 and w['Offset'] in (0x243,0x31D)
            assert confirm_move(writer,nxt)['record']['Values']['bit[0xD67]']==0
            assert w['ParentSlot']==wr['Slot'] and w['Slot'] not in cancelled
            assert binding(wait,'condition')['BackingWord']=='var[0x0D6]'
        zero_count+=len(waits)
        details={}
        if current['route']['id'].startswith('room41-raw'):
            draft=next(s for s in reversed(current['snapshots']) if s['stage']=='draft-4-before')
            packed=draft['snapshot']['KnownGlobalVariables']['var[0x03A]']
            selected_notes=[((packed>>(3*i))&7)+1 for i in range(4)]
            final_scan=ins(rs,22,0x64)[-1]
            actual_notes=[final_scan['record']['Values'][f'var[0x{0x56+i:03X}]'] for i in range(4)]
            assert selected_notes==actual_notes==[1,4,1,1]
            final_writes=ins(rs,10,0x36)[-4:]
            for i,row in enumerate(final_writes):
                assert binding(row,'destination-before')['Key']==f'var[0x{0x56+i:03X}]'
                assert binding(row,'destination-before')['Index']['Value']==i
                assert binding(row,'source')['Value']==actual_notes[i]
                confirm_move(row,nxt)
            details['selectedRecipeInput']=dict(snapshotStage=draft['stage'],packed=f'{packed:04X}',notes=actual_notes,
                packedEstimated='var[0x03A]' in draft['estimatedVariables'],
                limits='The unchanged recipe selects mouse/key notes from this live snapshot; source-domain membership does not establish known input/index or inherited path/effects.',
                writes=[brief(row) for row in final_writes])
        if current['route']['id']=='room41-raw4-click':
            completion=next(row for row in writes if row['record']['Sequence']==4552)
            after=confirm_move(completion,nxt)
            wait=next(row for row in waits if row['record']['Sequence']==4556)
            assert completion['frame']==wait['frame']==3036
            assert completion['record']['Slot']==412 and wait['record']['Slot']==408
            assert after['record']['Sequence']==4553 and nxt[4556]['record']['Sequence']==4557
            assert binding(wait,'condition')['ValueEstimated'] and not wait['record']['PathEstimated']
            assert nxt[4556]['record']['PathEstimated']
            practice_read=next(row for row in ins(rs,context='Room 06 local script 201',offset=0x21) if row['frame']==2870)
            practice_call=next(row for row in ins(rs,slot=practice_read['record']['Slot'],offset=0x28) if row['frame']==2870)
            read=binding(practice_read,'source')
            assert (read['Key'],read['Value'],read['ValueEstimated'],read['Index']['Value'],read['Index']['ValueEstimated'])==('var[0x052]',1,True,0,False)
            confirm_move(practice_read,nxt)
            assert arguments(practice_call)[0]['Value']==1 and arguments(practice_call)[0]['ValueEstimated']
            decoder=next(row for row in ins(rs,23,5) if row['record']['ParentSlot']==practice_read['record']['Slot'])
            assert (binding(decoder,'source')['Key'],binding(decoder,'source')['Value'])==('var[0x036]',0x1200)
            assert binding(decoder,'source')['Index']['Value']==4
            decode_writes=[row for row in ins(rs,23,slot=decoder['record']['Slot']) if row['record']['Offset'] in (0x50,0x69,0x82,0x9B)]
            assert [binding(row,'source')['Value'] for row in decode_writes]==[1,1,1,2]
            for row in decode_writes: confirm_move(row,nxt)
            scan=ins(rs,22,0x64)[-1]
            sr=scan['record']
            inputs=[sr['Values'][f'var[0x{0x56+i:03X}]'] for i in range(4)]
            assert inputs==[1,4,1,1] and sr['PathEstimated']
            note_writes=ins(rs,10,0x36)[-4:]
            for i,row in enumerate(note_writes):
                dest=binding(row,'destination-before')
                assert dest['Key']==f'var[0x{0x56+i:03X}]' and dest['Index']['Value']==i and dest['Index']['ValueEstimated']
                assert binding(row,'source')['Value']==inputs[i]
                assert not binding(row,'source')['ValueEstimated'] and row['record']['PathEstimated']
                assert dest['Key'] in confirm_move(row,nxt)['record']['Estimated']
            candidates=[row for row in ins(rs,23,5) if row['record']['ParentSlot']==sr['Slot']]
            assert [binding(row,'source')['Key'] for row in candidates]==[f'var[0x{0x32+2*i:03X}]' for i in range(5)]
            selected=candidates[-1]
            assert binding(selected,'source')['Value']==0x3018
            assert binding(selected,'source')['Index']['Value']==8
            response=ins(rs,23,0xF,slot=selected['record']['Slot']).pop()
            assert (binding(response,'source')['Key'],binding(response,'source')['Value'],binding(response,'source')['Index']['Value'])==('var[0x03B]',0x4241,9)
            endpoint=ins(rs,22,0xC3,slot=sr['Slot']).pop()
            assert [b['Value'] for b in arguments(endpoint)]==[4,0]
            assert arguments(endpoint)[0]['ValueEstimated'] and endpoint['record']['PathEstimated']
            details.update(practice=dict(decoder=brief(decoder),read=brief(practice_read),call=brief(practice_call),tuple=[1,1,1,2]),
                finalInput=dict(notes=inputs,writes=[brief(row) for row in note_writes],scan=brief(scan),
                    selectedWord=brief(selected),response=brief(response),endpoint=brief(endpoint)),
                completion=dict(writer=brief(completion),afterWrite=brief(after),wait=brief(wait),afterWait=brief(nxt[4556])))
        routes.append(dict(id=current['route']['id'],fullPacketIdentical=True,priorObservationsIdentical=True,
            zeroWaits=len(waits),packet=digest(packet),instructions=digest(trace),cancellations=digest(cancel_path),observations=details))
assert len(routes)==13 and zero_count==76

# Expanded raw4 trace observes the actual boot index/table selection. It is a
# separate filtered chronology; compare whole packets and stable identities,
# never transplant its extra sequence numbers into the unchanged primary trace.
packet=out/'expanded-room41-1/room41-raw4-click.json'
assert load(packet)==load(out/'room41-1/room41-raw4-click.json')
expanded_path=packet.with_name(packet.stem+'-instructions.jsonl')
expanded=rows(expanded_path)
nxt=same_run_next(expanded)
boot=ins(expanded,context='Room 01 local script 211')
selection=next(row for row in boot if row['record']['Offset']==0x349)
selected=binding(selection,'source')
assert selected['Key']=='local[0x002]' and selected['Value']==0x1018
assert selected['Index']['Value']==0 and selected['Index']['ValueEstimated']
boot_write=next(row for row in boot if row['record']['Offset']==0x350)
assert binding(boot_write,'destination-before')['Key']=='var[0x03A]'
assert confirm_move(boot_write,nxt)['record']['Values']['var[0x03A]']==0x1018
scan=ins(expanded,22,0x64)[-1]
assert scan['record']['Values']['var[0x03A]']==0x3018
assert (0x1018 & 0xFFF)==(0x3018 & 0xFFF)
numeric=load(out/'numeric-fresh.json')
assert (numeric['numericWorlds'],numeric['flagCases'],numeric['forwardComparisons'],numeric['firstComponentOverlaps'],len(numeric['fabricated']),len(numeric['practice']))==(162,52,51,14,4,12)
assert numeric['earlyStop']['Globals']['bit[0xD67]']['Number']==1
assert numeric['terminal']['Globals']['bit[0xD67]']['Number']==0
manifest=load(out/'observer-1/source-manifest.json')
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in manifest.items())
summary=dict(scope='Pre-instruction operand bindings and confirmed selected-run values in unchanged seeded explorer recipes. Numeric domains remain separate source hypotheses. No general selected-domain transport, exhaustive writer, inherited path/effect, scheduler/callee/cancellation or090 completion proof.',
    recipeCount=13,fullPacketsIdentical=True,priorObservationsIdentical=True,observedZeroWaits=76,
    native090Completed=False,estimatesCleared=False,sourceManifest=digest(out/'observer-1/source-manifest.json'),
    bootSelection=dict(indexedRead=brief(selection),write=brief(boot_write),initialWord='1018',currentWord='3018',
        low12BitsAgree=True,limits='Observed source selection and current numeric correspondence in this recipe; intervening provenance, unknown/external/aliased/concurrent effects and other inputs are not normalized away.'),
    expandedPacket=digest(packet),expandedInstructions=digest(expanded_path),numericSourceReport=digest(out/'numeric-fresh.json'),routes=routes)
(base/'binding-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('13 identical whole packets/prior observations;76 own-child literal zero waits; raw4 selected input/index and practice argument recorded with estimates intact;090 unresolved.')
