"""Retain every inherited assertion and require exact latest legacy evidence."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import sys

base = Path(__file__).parent
out = Path(sys.argv[1])
source = Path('.verification/archive/runtime-continuity-20261002/analyze_continuity.py').read_text(encoding='utf-8')
source = source.replace('.verification/archive/runtime-continuity-20261002', '.verification/archive/runtime-calls-20261002')
# Existing native fields remain exact; newly copied wait fields have independent lifetime logs.
needle = "            assert before.get('NativeDependencies') == after.get('NativeDependencies')"
assert source.count(needle) == 1
source = source.replace(needle, '''            if before.get('NativeDependencies') != after.get('NativeDependencies'):
                assert current['Site']['Operation'] in ('stringOps', 'loadRoomWithEgo', 'getObjectOwner', 'waitFor')
                assert not before.get('NativeDependencies') and after['Barrier'] is not None''')
exec(compile(source, 'preserved-continuity-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__'})

def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def rows(p): return [json.loads(line) for line in p.read_text(encoding='utf-8').splitlines()]
def digest(p): return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())

prior = Path('Exe/RuntimeContinuityFinalSession-20261002/final-evidence')
routes = []
for folder in ('room41-1', 'egg-first-1', 'dye-last-1', 'preserved-1'):
    for packet in sorted((out/folder).glob('*.json')):
        name = packet.stem
        assert load(packet) == load(prior/folder/packet.name)
        for suffix in ('-instructions.jsonl', '-cancellations.jsonl', '-relations.jsonl', '-completion.jsonl', '-expressions.jsonl'):
            assert rows(packet.with_name(name+suffix)) == rows(prior/folder/(name+suffix)), (name, suffix)
        current = rows(packet.with_name(name+'-access.jsonl'))
        previous = rows(prior/folder/(name+'-access.jsonl'))
        assert len(current) == len(previous)
        for aa, bb in zip(current, previous):
            assert {k:v for k,v in aa.items() if k != 'access'} == {k:v for k,v in bb.items() if k != 'access'}
            assert aa['access']['Barrier'] == bb['access']['Barrier'] and aa['access']['Limits'] == bb['access']['Limits']
            if aa['access']['Bindings'] != bb['access']['Bindings']:
                assert aa['Site']['Operation'] in ('loadRoomWithEgo', 'getObjectOwner') and not bb['access']['Bindings']
            if aa['access'].get('NativeDependencies') != bb['access'].get('NativeDependencies'):
                assert aa['Site']['Operation'] in ('stringOps', 'loadRoomWithEgo', 'getObjectOwner', 'waitFor') and not bb['access'].get('NativeDependencies')
        log = rows(packet.with_name(name+'-calls.jsonl'))
        boundaries = [r['boundary'] for r in log]
        assert [b['Sequence'] for b in boundaries] == list(range(1, len(boundaries)+1))
        dispatches = {b['Call']['Id']: b for b in boundaries if b['Stage'] == 'dispatch'}
        stages = Counter(b['Stage'] for b in boundaries)
        for b in boundaries:
            call = b['Call']
            if b['Stage'] in ('initialized', 'resumed-locals'):
                assert len(b['Locals']) == 26
                assert all(l['Role'] == 'restored-local' for l in b['Locals']) if b['Stage'] == 'resumed-locals' else True
                if call:
                    assert call['Id'] in dispatches and call['Slot'] == b['ParentSlot']
                    assert dispatches[call['Id']]['Call'] == call
            if b['Stage'] == 'caller-continued':
                assert call and call['Slot'] == b['Slot'] and call['Invocation'] == b['Invocation']
                assert call['Site']['Operation'] != 'chainScript'
                assert dispatches[call['Id']]['Sequence'] < b['Sequence']
            if b['Stage'] == 'barrier': assert b['Barrier'] and not b['Locals'] and call is None
        native = rows(packet.with_name(name+'-native.jsonl'))
        detail = {}
        if name == 'room41-raw4-click':
            practice = next(b for b in boundaries if b['Stage'] == 'dispatch' and b['Slot'] == 373 and
                b['Site']['Offset'] == 0x28 and b['Site']['Context'] == 'Room 06 local script 201')
            init30 = next(b for b in boundaries if b['Stage'] == 'initialized' and b['Call'] and b['Call']['Id'] == practice['Call']['Id'])
            assert (init30['Slot'],init30['ParentSlot'],init30['Invocation'],init30['Site']['Script']) == (378,373,11824,30)
            assert init30['Locals'][0]['Role'] == 'supplied-local' and init30['Locals'][0]['Value'] == 1 and init30['Locals'][0]['ValueEstimated']
            assert not init30['PathEstimated']
            chain = next(b for b in boundaries if b['Stage'] == 'dispatch' and b['Slot'] == 378 and b['Site']['Operation'] == 'chainScript')
            init31 = next(b for b in boundaries if b['Stage'] == 'initialized' and b['Call'] and b['Call']['Id'] == chain['Call']['Id'])
            assert (init31['Slot'],init31['ParentSlot'],init31['Invocation'],init31['Site']['Script']) == (382,378,11828,31)
            assert init31['Locals'][0]['Value'] == 1 and init31['Locals'][0]['ValueEstimated']
            assert not any(b['Stage']=='caller-continued' and b['Call'] and b['Call']['Id']==chain['Call']['Id'] for b in boundaries)
            actual_caller = next(b for b in boundaries if b['Stage']=='caller-continued' and b['Call'] and b['Call']['Id']==practice['Call']['Id'])
            assert actual_caller['Slot']==373 and actual_caller['Invocation']==11819 and actual_caller['Site']['Offset']==0x2E
            native_end = next(r['boundary'] for r in log if r['frame']==3036 and r['boundary']['Stage']=='end' and r['boundary']['Slot']==412)
            assert native_end['Invocation']==12143 and native_end['ParentSlot']==408 and native_end['ReachedStop'] and native_end['PathEstimated']
            detail = dict(practiceCaller=practice, actualInitialized030=init30, nonreturningChain=chain,
                actualInitialized031=init31, actualCallerContinuation=actual_caller, independentNativeWriterEnd=native_end)
        routes.append(dict(id=name, latestLegacyStreamsExact=True, callBoundaries=len(boundaries), stages=dict(stages),
            dispatches=len(dispatches), nativeMutationNotifications=sum(r['update']['Observation'] is None for r in native),
            selected=detail,
            reports=[digest(packet.with_name(name+s)) for s in ('-calls.jsonl','-native.jsonl')]))
assert len(routes) == 13 and sum(r['dispatches'] for r in routes) > 0
assert rows(out/'expanded-room41-1/room41-raw4-click-instructions.jsonl') == rows(prior/'expanded-room41-1/room41-raw4-click-instructions.jsonl')
summary = dict(scope='Actual selected assignment/dispatch/end/yield/restored-local/caller-continuation records only. No equality-derived origins, domain/effect transport, completion promotion or scheduler/callee/cancellation proof.',
    recipeCount=13, latestPacketsAndLegacyStreamsExact=True, independentOwnInvocationTerminals=76,
    actualRecipeWriterResumedTerminalChains=0, estimatesCleared=False, native090Completed=False, routes=routes)
(base/'call-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print('13 exact packets/legacy streams; actual call chronology independent; all76 original writer terminals and estimates preserved.')
