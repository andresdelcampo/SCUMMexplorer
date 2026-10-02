"""Preserve selected binding evidence, then audit optional lifetime/completion observations."""
from collections import Counter
from pathlib import Path
import hashlib
import json
import sys

out = Path(sys.argv[1])
base = Path(__file__).parent
# Reuse all existing source/value/index/path/boot/practice/numeric assertions,
# writing fresh summaries to this task's packet, never replacing earlier evidence.
source = Path('.verification/runtime-bindings-20261002/analyze_bindings.py').read_text(encoding='utf-8')
source = source.replace('base = Path(__file__).parent', "base = Path('.verification/runtime-relations-20261002')")
exec(compile(source, 'preserved-binding-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__'})

def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def rows(p): return [json.loads(line) for line in p.read_text(encoding='utf-8').splitlines()]
def digest(p): return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())
prior = Path('Exe/RuntimeBindingsSession-20261002')
routes = []
all_matches = 0
for folder in ('room41-1', 'egg-first-1', 'dye-last-1', 'preserved-1'):
    for packet in sorted((out/folder).glob('*.json')):
        name = packet.stem
        trace = packet.with_name(name+'-instructions.jsonl')
        cancellation = packet.with_name(name+'-cancellations.jsonl')
        relations = packet.with_name(name+'-relations.jsonl')
        completions = packet.with_name(name+'-completion.jsonl')
        assert rows(trace) == rows(prior/folder/trace.name)
        assert rows(cancellation) == rows(prior/folder/cancellation.name)
        rr = rows(relations)
        cc = rows(completions)
        waits = [r for r in cc if r['Site']['Script'] == 20 and r['Site']['Offset'] == 0x28
            and r['Binding']['Key'] == 'bit[0xD67]']
        counts = Counter(r['Rejection'] or 'matched own literal child' for r in waits)
        assert all(r['Binding']['Value'] == 0 for r in waits)
        assert all(r['MatchesOwnLiteralChild'] for r in waits), (name, counts)
        for r in waits:
            writer = r['Writer']
            assert writer['ParentSlot'] == r['Slot'] and writer['Slot'] != r['Slot']
            assert writer['Sequence'] < writer['ConfirmationSequence'] < r['Sequence']
            assert writer['Site']['Script'] == 31 and writer['Site']['Offset'] in (0x243, 0x31D)
            assert writer['Value'] == 0 and r['WriterEnd']['ReachedStop']
            assert len(r['Limits']) == 4
        all_matches += len(waits)
        detail = {}
        if name == 'room41-raw4-click':
            match = next(r for r in waits if r['Sequence'] == 4556)
            assert match['frame'] == 3036 and match['Slot'] == 408 and match['Run'] == 12144
            assert (match['Writer']['Sequence'], match['Writer']['ConfirmationSequence'],
                match['Writer']['Slot'], match['Writer']['Run']) == (4552, 4553, 412, 12143)
            assert match['Writer']['PathEstimated'] and match['Writer']['ValueEstimated']
            assert match['Binding']['ValueEstimated'] and not match['PathEstimated']
            instructions = rows(trace)
            assert next(r for r in instructions if r['record']['Sequence'] == 4557)['record']['PathEstimated']
            practice = next(r for r in rr if r['Kind'] == 'read' and r['Site']['Context'] == 'Room 06 local script 201'
                and r['Site']['Offset'] == 0x21 and r['frame'] == 2870 and r['Binding']['Role'] == 'source')
            assert (practice['Binding']['Key'], practice['Binding']['Value'], practice['Binding']['ValueEstimated']) == ('var[0x052]', 1, True)
            assert practice['Binding']['Index']['Value'] == 0 and not practice['Binding']['Index']['ValueEstimated']
            assert {d['Key'] for d in practice['Dependencies']} == {'var[0x052]', practice['Binding']['Index']['Key']}
            assert next(d['LocalSlot'] for d in practice['Dependencies'] if d['LocalSlot'] is not None) == practice['Slot']
            final = [r for r in rr if r['Kind'] == 'read' and r['Site']['Script'] == 22
                and r['Site']['Offset'] == 0xC3 and r['Binding']['Role'] == 'argument'][-2:]
            assert [r['Binding']['Value'] for r in final] == [4, 0]
            assert final[0]['Binding']['ValueEstimated'] and final[0]['PathEstimated']
            detail = dict(primaryCompletion=match, practiceRead=practice, finalArguments=final)
        routes.append(dict(id=name, wholePacketIdentical=True, exactBindingChronology=True,
            zeroWaits=len(waits), ownLiteralChildMatches=len(waits),
            readRelations=sum(r['Kind'] == 'read' for r in rr),
            snapshotConfirmedMoves=sum(r['Kind'] == 'write' for r in rr),
            barriers=dict(Counter(r['Barrier'] for r in rr if r['Kind'] == 'invalidate' and r['Barrier'])),
            invalidations=dict(Counter(i['Reason'] for r in rr if r['Kind'] == 'invalidate' for i in r['Invalidated'])),
            observations=detail, reports=[digest(p) for p in (packet, trace, cancellation, relations, completions)]))
assert len(routes) == 13 and all_matches == 76
expanded = out/'expanded-room41-1/room41-raw4-click-instructions.jsonl'
assert rows(expanded) == rows(prior/'expanded-room41-1'/expanded.name)
summary = dict(scope='Optional copied runtime relation lifetimes and selected zero-read/writer correspondence. All independent estimates remain. No exhaustive writer/domain transport, inherited effect, scheduler/callee/cancellation-safe completion or native090 proof.',
    recipeCount=13, fullPacketsIdentical=True, exactBindingChronology=True,
    observedZeroWaits=76, ownLiteralChildMatches=76, expandedChronologyIdentical=True,
    estimatesCleared=False, native090Completed=False, routes=routes,
    expandedInstructions=digest(expanded))
(base/'relation-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print('13 exact whole packets and binding/cancellation chronologies;76 own-child literal zero matches; boot/practice/input estimates intact;090 unresolved.')
