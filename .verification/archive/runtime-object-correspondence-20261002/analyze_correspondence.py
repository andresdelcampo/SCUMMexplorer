"""Run every inherited assertion, permitting only selected appended message fields."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import sys

base = Path(__file__).parent
out = Path(sys.argv[1])
source = Path('.verification/archive/runtime-correspondence-20261002/analyze_correspondence.py').read_text(encoding='utf-8')
source = source.replace('.verification/archive/runtime-correspondence-20261002', '.verification/archive/runtime-object-correspondence-20261002')
source = source.replace("('drawObject','cursorCommand','getActorX','getActorY')", "('drawObject','cursorCommand','getActorX','getActorY','waitFor')")
source = source.replace("('object-position-x','object-position-y','draw-peer-effects','user-input')",
    "('object-position-x','object-position-y','draw-peer-effects','user-input','message-have-msg','message-talk-delay','message-talking-actor')")
exec(compile(source, 'preserved-correspondence-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__'})

def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def rows(p): return [json.loads(line) for line in p.read_text(encoding='utf-8').splitlines()]
def digest(p): return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())

prior = Path('Exe/RuntimeCorrespondenceFinalSession-20261002/final-evidence')
routes = []
message_domains = ('message-have-msg', 'message-talk-delay', 'message-talking-actor')
for folder in ('room41-1', 'egg-first-1', 'dye-last-1', 'preserved-1'):
    for packet in sorted((out/folder).glob('*.json')):
        name = packet.stem
        assert load(packet) == load(prior/folder/packet.name)
        for suffix in ('-instructions.jsonl','-cancellations.jsonl','-relations.jsonl','-completion.jsonl','-expressions.jsonl','-calls.jsonl'):
            assert rows(packet.with_name(name+suffix)) == rows(prior/folder/(name+suffix)), (name,suffix)
        accesses = rows(packet.with_name(name+'-access.jsonl'))
        previous = rows(prior/folder/(name+'-access.jsonl'))
        assert len(accesses) == len(previous)
        message_reads = Counter()
        for aa, bb in zip(accesses, previous):
            assert {k:v for k,v in aa.items() if k != 'access'} == {k:v for k,v in bb.items() if k != 'access'}
            a, b = aa['access'], bb['access']
            assert {k:v for k,v in a.items() if k != 'NativeDependencies'} == {k:v for k,v in b.items() if k != 'NativeDependencies'}
            before, after = b.get('NativeDependencies') or [], a.get('NativeDependencies') or []
            assert after[:len(before)] == before
            if after != before:
                assert aa['Site']['Operation'] == 'waitFor' and aa['Site']['Operands'] == 'message'
                assert [d['Domain'] for d in after[len(before):]] == list(message_domains)
                for d in after[len(before):]:
                    assert d['Value'] is not None and d['ValueEstimated'] is None and d['UnresolvedReason']
                    assert d['Selector'] in a['Bindings']
                    message_reads[d['Domain']] += 1
        calls = [r['boundary'] for r in rows(packet.with_name(name+'-calls.jsonl'))]
        instructions = rows(packet.with_name(name+'-instructions.jsonl'))
        actual_selections = [r['selection'] for r in rows(packet.with_name(name+'-object-selections.jsonl'))]
        dispatches = {b['Call']['Id']: b['Call'] for b in calls if b['Stage'] == 'dispatch'}
        for selection in actual_selections:
            call, entry = selection['Dispatch'], selection['Entry']
            assert dispatches[call['Id']] == call and call['Site']['Operation'] == 'startObject'
            assert selection['ObjectId'] == entry['ObjectId'] == next(b['Value'] for b in call['Operands'] if b['Role']=='target')
            assert selection['RequestedVerb'] == next(b['Value'] for b in call['Operands'] if b['Role']=='verb')
            assert selection['RequestedVerb'] == entry['VerbId'] or entry['IsFallback']
            assert selection['Site']['ResourceOffset'] == entry['ScriptOffset'] < entry['ResourceEnd']
        updates = [r['update'] for r in rows(packet.with_name(name+'-correspondence.jsonl'))]
        # Every original script binding/triple must still appear exactly as a structural
        # record; only new optional object bindings can carry decoder selections.
        for old in (r['update'] for r in rows(prior/folder/(name+'-correspondence.jsonl'))):
            if old['Binding'] is None: continue
            assert any(u['Binding'] == old['Binding'] for u in updates)
            if old['ImmediateStopCorrespondence'] is not None:
                assert any(u['ImmediateStopCorrespondence'] == old['ImmediateStopCorrespondence'] for u in updates)
        object_bindings = [u['Binding'] for u in updates if u['Binding'] and u['Binding'].get('ObjectSelection')]
        for binding in object_bindings:
            selection = binding['ObjectSelection']
            assert selection in actual_selections and selection['Dispatch'] == binding['Dispatch']
            assert selection['Site'] == binding['Initialization']['Site']
            assert binding['Initialization'] in calls
        native = rows(packet.with_name(name+'-native.jsonl'))
        notification_invalidations = sum(len(r['update']['Invalidated']) for r in native
            if r['update']['Observation'] is None and r['update']['Barrier']=='notified native mutation (including same value)')
        routes.append(dict(id=name,legacyPacketsCallsInstructionsAndCompletionExact=True,
            originalScriptBindingsAndTriplesRetained=True,
            actualObjectSelections=len(actual_selections),
            actualObjectBindings=len({b['Dispatch']['Id'] for b in object_bindings}),
            fallbackObjectBindings=len({b['Dispatch']['Id'] for b in object_bindings if b['ObjectSelection']['Entry']['IsFallback']}),
            actualBindings=len({u['Dispatch']['Id'] for u in updates if u['Binding']}),
            immediateStructuralTriples=len({u['Dispatch']['Id'] for u in updates if u['ImmediateStopCorrespondence']}),
            observedCallerResumptions=len({u['Dispatch']['Id'] for u in updates if u['CallerResumption']}),
            copiedMessageFields=dict(message_reads),notificationInvalidations=notification_invalidations,
            reports=[digest(packet.with_name(name+s)) for s in ('-object-selections.jsonl','-correspondence.jsonl','-native.jsonl')]))
assert len(routes)==13
summary=dict(scope='Actual decoder selections and independent structural correspondence; native message copies retain unknown provenance. No origins, successful returns, loaded native lifetimes or completion promotion.',
    recipeCount=13,allLegacyPacketsCallsChronologiesCompletionExact=True,
    originalScriptBindings=366,originalImmediateTriples=55,originalWriterTerminals=76,originalWriterResumedChains=0,
    totalObjectSelections=sum(r['actualObjectSelections'] for r in routes),
    totalObjectBindings=sum(r['actualObjectBindings'] for r in routes),
    totalFallbackBindings=sum(r['fallbackObjectBindings'] for r in routes),
    totalBindings=sum(r['actualBindings'] for r in routes),
    totalStructuralTriples=sum(r['immediateStructuralTriples'] for r in routes),
    callerResumptions=sum(r['observedCallerResumptions'] for r in routes),
    copiedMessageFields=sum(sum(r['copiedMessageFields'].values()) for r in routes),
    estimatesCleared=False,native090Completed=False,routes=routes)
(base/'object-message-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print({k:v for k,v in summary.items() if k not in ('scope','routes')})
