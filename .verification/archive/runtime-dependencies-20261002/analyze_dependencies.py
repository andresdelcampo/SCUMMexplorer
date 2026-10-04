"""Require all prior preservation assertions before inspecting additional copied records."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import sys

out = Path(sys.argv[1])
base = Path(__file__).parent
source = Path('.verification/archive/runtime-access-20261002/analyze_access.py').read_text(encoding='utf-8')
source = source.replace("Path('.verification/archive/runtime-access-20261002')", "Path('.verification/archive/runtime-dependencies-20261002')")
exec(compile(source, 'preserved-access-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__'})

def rows(p): return [json.loads(line) for line in p.read_text(encoding='utf-8').splitlines()]
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p): return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())

prior = Path('Exe/RuntimeAccessFinalSession-20261002/final-evidence')
routes = []
for folder in ('room41-1', 'egg-first-1', 'dye-last-1', 'preserved-1'):
    for packet in sorted((out/folder).glob('*.json')):
        name = packet.stem
        instructions = packet.with_name(name+'-instructions.jsonl')
        cancellations = packet.with_name(name+'-cancellations.jsonl')
        assert load(packet) == load(prior/folder/packet.name)
        assert rows(instructions) == rows(prior/folder/instructions.name)
        assert rows(cancellations) == rows(prior/folder/cancellations.name)
        ii = {r['record']['Sequence']: r['record'] for r in rows(instructions)}
        aa = rows(packet.with_name(name+'-access.jsonl'))
        rr = rows(packet.with_name(name+'-relations.jsonl'))
        cc = rows(packet.with_name(name+'-completion.jsonl'))
        for write in (r for r in rr if r['Kind'] == 'write'):
            confirm = ii[write['ConfirmationSequence']]
            site = write['Site']
            assert confirm['Run'] == write['Run'] and confirm['Slot'] == write['Slot']
            assert confirm['ParentSlot'] == write['ParentSlot']
            assert confirm['Stage'] in ('instruction', 'end')
            assert all(confirm[k] == site[k] for k in ('Script', 'Context', 'Container', 'ResourceOffset'))
        waits = [r for r in cc if r['Site']['Script'] == 20 and r['Site']['Offset'] == 0x28]
        for wait in waits:
            assert wait['MatchesOwnLiteralChild'] and wait['WriterTerminated']
            assert wait['WriterEnd']['Run'] == wait['Writer']['Run']
            assert wait['WriterEnd']['ParentSlot'] == wait['Writer']['ParentSlot'] == wait['Slot']
        native = [d for a in aa for d in a['access'].get('NativeDependencies', [])]
        for dependency in native:
            if dependency['Selector']['Value'] is None:
                assert dependency['Value'] is None and dependency['ValueEstimated'] is None
            if dependency['Domain'] in ('isSoundRunning', 'isScriptRunning', 'getDist', 'findObject', 'isActorInBox'):
                assert dependency['Value'] is None and dependency['UnresolvedReason']
        new_operand_records = [a for a in aa if a['access']['Bindings'] and a['Site']['Operation'] in (
            'actorOps', 'putActor', 'putActorInRoom', 'animateActor', 'setState', 'setClass', 'ifClassOfIs', 'print', 'startObject')]
        assert all(a['access']['Barrier'] is not None for a in new_operand_records)
        routes.append(dict(id=name, wholePacketAndLatestChronologyExact=True,
            newEventOperandRecords=len(new_operand_records), nativeDependencies=len(native),
            nativeDomains=dict(Counter(d['Domain'] for d in native)),
            unresolvedNativeDependencies=sum(d['Value'] is None for d in native),
            nativeValueEstimates=sum(d['ValueEstimated'] is True for d in native),
            zeroWaits=len(waits), independentOwnInvocationTerminations=len(waits),
            reports=[digest(packet.with_name(name+suffix)) for suffix in ('-access.jsonl', '-completion.jsonl')]))
assert len(routes) == 13 and sum(r['zeroWaits'] for r in routes) == 76
assert sum(r['newEventOperandRecords'] for r in routes) > 0 and sum(r['nativeDependencies'] for r in routes) > 0
expanded = out/'expanded-room41-1/room41-raw4-click-instructions.jsonl'
assert rows(expanded) == rows(prior/'expanded-room41-1'/expanded.name)
summary = dict(scope='Selected copied numeric/event/native dependencies and resource/causal boundary precision only; no native lifetime/domain/effect transport, callee binding/return, resumed terminal continuity, protocol or090 proof.',
    recipeCount=13, latestWholePacketsAndChronologiesExact=True, zeroWaits=76,
    independentOwnInvocationTerminations=76, estimatesCleared=False, native090Completed=False,
    routes=routes, expandedChronologyExact=True, expandedInstructions=digest(expanded))
(base/'dependency-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print('13 exact latest packets/chronologies;76 separate own-invocation/parent ends; copied event/native dependencies retain all barriers and estimates.')
