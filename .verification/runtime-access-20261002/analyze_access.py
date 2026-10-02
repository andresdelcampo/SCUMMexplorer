"""Keep every historical assertion, then check copied access/lineage/terminal evidence."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import sys

out = Path(sys.argv[1])
base = Path(__file__).parent
source = Path('.verification/runtime-relations-20261002/analyze_relations.py').read_text(encoding='utf-8')
source = source.replace("Path('.verification/runtime-relations-20261002')", "Path('.verification/runtime-access-20261002')")
source = source.replace('snapshotConfirmedMoves=', 'snapshotConfirmedWrites=')
exec(compile(source, 'preserved-relation-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__'})

def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def rows(p): return [json.loads(line) for line in p.read_text(encoding='utf-8').splitlines()]
def digest(p): return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())

prior = Path('Exe/RuntimeRelationsSession-20261002/final-evidence')
routes = []
for folder in ('room41-1', 'egg-first-1', 'dye-last-1', 'preserved-1'):
    for packet in sorted((out/folder).glob('*.json')):
        name = packet.stem
        trace = packet.with_name(name+'-instructions.jsonl')
        cancellation = packet.with_name(name+'-cancellations.jsonl')
        access = packet.with_name(name+'-access.jsonl')
        relation = packet.with_name(name+'-relations.jsonl')
        completion = packet.with_name(name+'-completion.jsonl')
        assert load(packet) == load(prior/folder/packet.name)
        assert rows(trace) == rows(prior/folder/trace.name)
        assert rows(cancellation) == rows(prior/folder/cancellation.name)
        aa, rr, cc = rows(access), rows(relation), rows(completion)
        reads = {r['Id']: r for r in rr if r['Kind'] == 'read'}
        writes = {r['Id']: r for r in rr if r['Kind'] == 'write'}
        for r in reads.values():
            if r['IndexRead'] is not None:
                index = reads[r['IndexRead']]
                assert index['Binding'] == r['Binding']['Index']
                assert index['Sequence'] == r['Sequence'] and index['Slot'] == r['Slot']
        for w in writes.values():
            for source_id in w['SourceReads']:
                source_read = reads[source_id]
                assert source_read['Sequence'] == w['Sequence'] and source_read['Slot'] == w['Slot']
            assert w['Sequence'] < w['ConfirmationSequence']
        waits = [c for c in cc if c['Site']['Script'] == 20 and c['Site']['Offset'] == 0x28]
        assert all(c['MatchesOwnLiteralChild'] and c['WriterTerminated'] and c['TerminalRejection'] is None for c in waits)
        detail = {}
        if name == 'room41-raw4-click':
            primary = next(c for c in waits if c['Sequence'] == 4556)
            assert primary['frame'] == 3036 and primary['Writer']['Sequence'] == 4552
            assert primary['Writer']['ConfirmationSequence'] == 4553
            assert primary['WriterEnd']['Sequence'] < primary['Sequence']
            # The selected mask/division/increment are captured numbers with original estimates,
            # not source-world membership or an inferred callee-local argument binding.
            final_word = [r for r in reads.values() if r['Site']['Script'] == 23 and r['Site']['Offset'] == 5
                and r['Binding']['Role'] == 'source'][-1]
            assert (final_word['Binding']['Key'], final_word['Binding']['Value'], final_word['Binding']['Index']['Value']) == ('var[0x03A]', 0x3018, 8)
            index = reads[final_word['IndexRead']]
            assert index['Binding']['ValueEstimated']
            index_origin = writes[index['Origin']] if index['Origin'] is not None else None
            assert index_origin is not None and index_origin['Site']['Operation'] == 'multiply'
            final_args = [r for r in reads.values() if r['Site']['Script'] == 22 and r['Site']['Offset'] == 0xC3 and r['Binding']['Role'] == 'argument'][-2:]
            assert [r['Binding']['Value'] for r in final_args] == [4, 0]
            detail = dict(primaryCompletion=primary, selectedWord=final_word, selectedIndex=index,
                selectedIndexOrigin=index_origin, finalArguments=final_args)
        routes.append(dict(id=name, priorWholePacketAndChronologyExact=True,
            accessRecords=len(aa), accessOperations=dict(Counter(a['Site']['Operation'] for a in aa)),
            accessBarriers=dict(Counter(a['access']['Barrier'] for a in aa if a['access']['Barrier'])),
            confirmedWrites=dict(Counter(w['Site']['Operation'] for w in writes.values())),
            recursiveIndexReads=sum(r['IndexRead'] is not None for r in reads.values()),
            notifiedMutations=sum(r['Kind'] == 'invalidate' and r['Barrier'] == 'runtime global mutation (including same value)' for r in rr),
            zeroWaits=len(waits), independentTerminations=sum(c['WriterTerminated'] for c in waits),
            observations=detail, reports=[digest(p) for p in (access, relation, completion)]))
assert len(routes) == 13 and sum(r['zeroWaits'] for r in routes) == 76
assert any(r['confirmedWrites'].get('multiply', 0) for r in routes)
assert any(r['notifiedMutations'] for r in routes)
summary = dict(scope='Copied numeric access and observed arithmetic/index/argument lineage only; no general runtime domains, inherited effects, exhaustive raw writers, scheduler/callee/cancellation-safe protocol or090 proof.',
    recipeCount=13, priorWholePacketsAndChronologyExact=True, zeroWaits=76,
    independentTerminations=76, estimatesCleared=False, native090Completed=False, routes=routes)
(base/'access-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print('13 exact packets/legacy chronologies;76 own-child matches with independent source-consistent terminations; observed arithmetic/index lineage and mutations; all estimates retained.')
