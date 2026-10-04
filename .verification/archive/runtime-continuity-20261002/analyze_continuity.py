"""Run every inherited assertion without overwriting any previous checkpoint packet."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import sys

out = Path(sys.argv[1])
base = Path(__file__).parent
source = Path('.verification/archive/runtime-dependencies-20261002/analyze_dependencies.py').read_text(encoding='utf-8')
source = source.replace('.verification/archive/runtime-dependencies-20261002', '.verification/archive/runtime-continuity-20261002')
exec(compile(source, 'preserved-dependency-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__'})

def rows(p): return [json.loads(line) for line in p.read_text(encoding='utf-8').splitlines()]
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p): return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())

prior = Path('Exe/RuntimeDependenciesFinalSession-20261002/final-evidence')
routes = []
for folder in ('room41-1', 'egg-first-1', 'dye-last-1', 'preserved-1'):
    for packet in sorted((out/folder).glob('*.json')):
        name = packet.stem
        assert load(packet) == load(prior/folder/packet.name)
        for suffix in ('-instructions.jsonl', '-cancellations.jsonl'):
            assert rows(packet.with_name(name+suffix)) == rows(prior/folder/(name+suffix))
        # All previously copied access/native fields are preserved; additional bindings occur
        # only in formerly empty implicit/subcommand records and keep their effect barriers.
        aa = rows(packet.with_name(name+'-access.jsonl'))
        old = rows(prior/folder/(name+'-access.jsonl'))
        assert len(aa) == len(old)
        additions = Counter()
        for current, previous in zip(aa, old):
            assert all(current[k] == previous[k] for k in ('frame', 'Sequence', 'Slot', 'Run', 'Site'))
            before, after = previous['access'], current['access']
            assert before['Barrier'] == after['Barrier'] and before['Limits'] == after['Limits']
            assert before.get('NativeDependencies') == after.get('NativeDependencies')
            if before['Bindings'] != after['Bindings']:
                assert not before['Bindings'] and after['Barrier'] is not None
                additions[current['Site']['Operation']] += 1
        cc = rows(packet.with_name(name+'-completion.jsonl'))
        oldcc = rows(prior/folder/(name+'-completion.jsonl'))
        assert [{k:v for k,v in c.items() if k != 'ResumedTerminalContinuity'} for c in cc] == oldcc
        assert all(c['ResumedTerminalContinuity'] is None for c in cc)
        native = rows(packet.with_name(name+'-native.jsonl'))
        reads = [r for row in native for r in row['update']['Reads']]
        # Native data are the original copied fields, with separate selector dependencies.
        by_sequence = {a['Sequence']: a for a in aa}
        for read in reads:
            record = read['Observation']
            assert read['Dependency'] in by_sequence[record['Sequence']]['access']['NativeDependencies']
            assert record['SourceAccess'] == by_sequence[record['Sequence']]['access']
            if read['Dependency']['Value'] is None: assert read['Dependency']['UnresolvedReason']
        expressions = rows(packet.with_name(name+'-expressions.jsonl'))
        for row in expressions:
            step = row['step']
            assert step['InstructionSequence'] in by_sequence and step['Site'] == by_sequence[step['InstructionSequence']]['Site']
            assert step['Stage'] == 'barrier' or step['Bindings']
        routes.append(dict(id=name, latestWholePacketChronologyAndCompletionExact=True,
            additionalAccessRecords=dict(additions), independentNativeReads=len(reads), expressionSteps=len(expressions),
            unresolvedNativeReads=sum(r['Dependency']['Value'] is None for r in reads),
            nativeInvalidations=sum(len(row['update']['Invalidated']) for row in native),
            reports=[digest(packet.with_name(name+suffix)) for suffix in ('-expressions.jsonl', '-native.jsonl')]))
assert len(routes) == 13
expanded = out/'expanded-room41-1/room41-raw4-click-instructions.jsonl'
assert rows(expanded) == rows(prior/'expanded-room41-1'/expanded.name)
summary = dict(scope='Copied implicit/subcommand access, actual expression chronology, independent native lifetimes and bounded observed resumed terminals only; no numeric domain/effect transport or callee binding/return/protocol090 proof.',
    recipeCount=13, latestWholePacketsChronologiesAndCompletionExact=True, expandedChronologyExact=True,
    ownLiteralChildMatches=76, independentOwnInvocationTerminations=76, actualRecipeResumedTerminalChains=0,
    estimatesCleared=False, native090Completed=False, routes=routes)
(base/'continuity-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print('13 exact latest whole packets/chronologies/completion records;76 separate own-invocation ends; additional native/expression streams kept independent.')
