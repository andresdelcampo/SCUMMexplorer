"""Retain every historical assertion, then compare the latest packet exactly."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import sys

base = Path(__file__).parent
out = Path(sys.argv[1])
source = Path('.verification/archive/runtime-calls-20261002/analyze_calls.py').read_text(encoding='utf-8')
source = source.replace('.verification/archive/runtime-calls-20261002', '.verification/archive/runtime-correspondence-20261002')
# The inherited continuity comparison permits only appended selected fields, keeping every
# old dependency, barrier and limit exact. New draw operands were previously empty/barred.
needle = "assert current['Site']['Operation'] in ('stringOps', 'loadRoomWithEgo', 'getObjectOwner', 'waitFor')\n                assert not before.get('NativeDependencies') and after['Barrier'] is not None"
assert source.count(needle) == 1
source = source.replace(needle, """assert current['Site']['Operation'] in ('stringOps', 'loadRoomWithEgo', 'getObjectOwner', 'waitFor', 'drawObject', 'cursorCommand', 'getActorX', 'getActorY')
                old_dependencies = before.get('NativeDependencies') or []
                assert after['NativeDependencies'][:len(old_dependencies)] == old_dependencies""")
source = source.replace("('loadRoomWithEgo', 'getObjectOwner') and not bb['access']['Bindings']", "('loadRoomWithEgo', 'getObjectOwner', 'drawObject') and not bb['access']['Bindings']")
needle = "assert aa['Site']['Operation'] in ('stringOps', 'loadRoomWithEgo', 'getObjectOwner', 'waitFor') and not bb['access'].get('NativeDependencies')"
assert source.count(needle) == 1
source = source.replace(needle, """assert aa['Site']['Operation'] in ('stringOps', 'loadRoomWithEgo', 'getObjectOwner', 'waitFor', 'drawObject', 'cursorCommand', 'getActorX', 'getActorY')
                old_dependencies = bb['access'].get('NativeDependencies') or []
                assert aa['access']['NativeDependencies'][:len(old_dependencies)] == old_dependencies""")
exec(compile(source, 'preserved-call-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__'})

def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def rows(p): return [json.loads(line) for line in p.read_text(encoding='utf-8').splitlines()]
def digest(p): return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())

prior = Path('Exe/RuntimeCallsFinalSession-20261002/final-evidence')
routes = []
for folder in ('room41-1', 'egg-first-1', 'dye-last-1', 'preserved-1'):
    for packet in sorted((out/folder).glob('*.json')):
        name = packet.stem
        assert load(packet) == load(prior/folder/packet.name)
        for suffix in ('-instructions.jsonl','-cancellations.jsonl','-relations.jsonl','-completion.jsonl','-expressions.jsonl','-calls.jsonl'):
            assert rows(packet.with_name(name+suffix)) == rows(prior/folder/(name+suffix)), (name,suffix)
        current = rows(packet.with_name(name+'-access.jsonl'))
        previous = rows(prior/folder/(name+'-access.jsonl'))
        assert len(current) == len(previous)
        additions = Counter()
        for aa, bb in zip(current, previous):
            assert {k:v for k,v in aa.items() if k != 'access'} == {k:v for k,v in bb.items() if k != 'access'}
            a, b = aa['access'], bb['access']
            assert a['Barrier'] == b['Barrier'] and a['Limits'] == b['Limits']
            if a['Bindings'] != b['Bindings']:
                assert aa['Site']['Operation'] == 'drawObject' and not b['Bindings'] and a['Barrier'] is not None
            old_native = b.get('NativeDependencies') or []
            new_native = a.get('NativeDependencies') or []
            assert new_native[:len(old_native)] == old_native
            if new_native != old_native:
                assert aa['Site']['Operation'] in ('drawObject','cursorCommand','getActorX','getActorY')
                for d in new_native[len(old_native):]:
                    assert d['Domain'] in ('object-position-x','object-position-y','draw-peer-effects','user-input')
                    additions[d['Domain']] += 1
        calls = [r['boundary'] for r in rows(packet.with_name(name+'-calls.jsonl'))]
        dispatches = {b['Call']['Id']:b for b in calls if b['Stage'] == 'dispatch'}
        log = rows(packet.with_name(name+'-correspondence.jsonl'))
        updates = [r['update'] for r in log]
        assert [u['Event'] for u in updates] == list(range(1,len(updates)+1))
        for u in updates:
            if u['Binding'] is not None:
                binding = u['Binding']
                call = binding['Dispatch']
                assert dispatches[call['Id']]['Call'] == call
                initialization = binding['Initialization']
                assert initialization in calls and initialization['Stage']=='initialized'
                assert initialization['Call'] == call and initialization['ParentSlot']==call['Slot']
                for assignment in binding['SuppliedAssignments']:
                    assert assignment['Argument'] in call['Operands'] and assignment['Local'] in initialization['Locals']
                    assert assignment['Local']['Role']=='supplied-local'
            if u['ChildTerminated']:
                assert u['ChildRejection'] is None and u['ChildBoundaries'][-1]['ReachedStop']
            if u['ImmediateStopCorrespondence'] is not None:
                proof=u['ImmediateStopCorrespondence']
                assert u['ChildTerminated'] and u['CallerRejection'] is None
                assert proof['ChildEnd']['Sequence'] < proof['HostReturn']['Sequence'] < proof['CallerContinuation']['Sequence']
                assert proof['ChildEnd']['Invocation'] == u['Binding']['Initialization']['Invocation']
            if u['CallerResumption'] is not None:
                proof=u['CallerResumption']
                assert proof['Yield']['ResumeOffset']==proof['RestoredLocals']['Site']['Offset']==proof['FirstInstruction']['Site']['Offset']
                assert proof['Yield']['Invocation'] != proof['FirstInstruction']['Invocation']
        selected={}
        if name=='room41-raw4-click':
            practice=next(u for u in updates if u['Binding'] and u['Dispatch']['Slot']==373 and u['Dispatch']['Site']['Offset']==0x28)
            assert (practice['Binding']['Initialization']['Slot'],practice['Binding']['Initialization']['Invocation'])==(378,11824)
            assert practice['Binding']['SuppliedAssignments'][0]['Local']['ValueEstimated']
            chain=next(u for u in updates if u['Binding'] and u['Dispatch']['Slot']==378 and u['Dispatch']['Site']['Operation']=='chainScript')
            assert chain['Binding']['Initialization']['Slot']==382
            assert not any(u['ImmediateStopCorrespondence'] for u in updates if u['Dispatch'] and u['Dispatch']['Id']==practice['Dispatch']['Id'])
            selected=dict(actualPracticeBinding=practice,actualChainBinding=chain)
        routes.append(dict(id=name,latestPacketsAndAllLegacyStreamsExact=True,callBoundaries=len(calls),
            correspondenceUpdates=len(updates),actualBindings=len({u['Dispatch']['Id'] for u in updates if u['Binding']}),
            immediateStopCorrespondences=len({u['Dispatch']['Id'] for u in updates if u['ImmediateStopCorrespondence'] is not None}),
            observedCallerResumptions=len({u['Dispatch']['Id'] for u in updates if u['CallerResumption'] is not None}),
            additionalCopiedNativeFields=dict(additions),selected=selected,
            reports=[digest(packet.with_name(name+s)) for s in ('-correspondence.jsonl','-calls.jsonl','-native.jsonl')]))
assert len(routes)==13
summary=dict(scope='Actual assignments and separately bounded structural correspondence only; no numeric origins, domain/effect transport, successful return or completion promotion.',
    recipeCount=13,latestPacketsAndAllLegacyStreamsExact=True,originalWriterTerminals=76,originalWriterResumedChains=0,
    estimatesCleared=False,native090Completed=False,routes=routes)
(base/'correspondence-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('13 latest packets/all legacy streams exact; actual bindings and structural correspondences independent; 76 original terminals unchanged.')
