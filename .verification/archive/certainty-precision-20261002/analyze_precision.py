"""Read-only certainty research. Never supplies state to a live runtime.

The practice CFG follows only one bit; other conditions take both edges and
callee/concurrent effects are deliberately outside its claim. Finite draft
projections retain complete words, not independent note-component products.
"""
from pathlib import Path
from collections import Counter
import hashlib
import json
import re

base = Path(__file__).parent
output = Path('Exe/CertaintyPrecisionSession-20261002')
prior = Path('Exe/Room41CertaintySession-20261002')
code = Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')

def load(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))

def digest(p):
    return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())

assert digest(code)['sha256'] == '0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569'
rows = [line.split('\t') for line in code.read_text(encoding='utf-8-sig').splitlines()]
domains_path = Path('.verification/archive/room41-certainty-20261002/source-domains.json')
domains = load(domains_path)
assert len(domains['comparisons']) == 51

def cfg_bit_exits(owner, bit, initial):
    instructions = [r for r in rows if r[0] == owner]
    index = {int(r[1][3:], 16): i for i, r in enumerate(instructions)}
    pending = [(0, initial)]
    seen = set()
    exits = set()
    while pending:
        pc, value = pending.pop()
        if (pc, value) in seen:
            continue
        seen.add((pc, value))
        i = index[pc]
        _, at, op, operands = instructions[i]
        if op == 'move' and operands.startswith(bit + ' = '):
            value = int(operands.split(' = ')[1], 16) != 0
        if op in ('stopObjectCode', 'chainScript') or (op == 'stopScript' and operands == '0x00'):
            exits.add((at, op, int(value)))
            continue
        following = int(instructions[i + 1][1][3:], 16)
        branch = re.search(r'->\s*0x([0-9A-Fa-f]+)', operands)
        if op == 'jumpRelative':
            assert branch
            pending.append((int(branch[1], 16), value))
        elif branch:
            target = int(branch[1], 16)
            if operands.startswith(bit + ',') and op in ('equalZero', 'notEqualZero'):
                condition = (value == 0) if op == 'equalZero' else (value != 0)
                pending.append((following if condition else target, value))
            else:
                pending.extend([(following, value), (target, value)])
        else:
            pending.append((following, value))
    return dict(initial=initial, visitedStates=len(seen), exits=[dict(offset=at, operation=op, value=v)
        for at, op, v in sorted(exits)])

practice = [cfg_bit_exits('script 031', 'bit[0xD6C]', initial) for initial in (0, 1)]
handshake_exits = cfg_bit_exits('script 031', 'bit[0xD67]', 0)
assert {e['value'] for e in handshake_exits['exits']} == {0, 1}
assert all(e['value'] == 0 for run in practice for e in run['exits'])
writers = [dict(owner=o, offset=at, operation=op, operands=arg) for o, at, op, arg in rows
    if op == 'move' and arg.startswith('bit[0xD6C] = ')]
assert [(r['owner'], r['offset']) for r in writers] == [('script 030', '+0x0000'), ('script 031', '+0x0227')]

mask_rows = [r for r in rows if r[0] == 'script 023' and r[2] == 'and']
masks = [int(r[3].split(' &= ')[1], 16) for r in mask_rows]
assert masks == [0x2000, 0x1000, 0x4000, 7, 0x38, 0x1C0, 0xE00]
projections = []
for table in domains['tables']:
    words = table['packed']
    projections.append(dict(slot=table['slot'], bootWords=words,
        masks=[dict(source=r[1], mask=m, possibleMaskedValues=sorted({w & m for w in words}))
            for r, m in zip(mask_rows, masks)],
        optionalLearnedWrite=sorted({int(bool((w | flag) & 0x2000)) for w in words for flag in (0, 0x2000)}),
        optionalUsedWrite=sorted({int(bool((w | flag) & 0x4000)) for w in words for flag in (0, 0x4000)})))

# Correlated four-note rejection may be invariant even when the first individual
# comparison is inconclusive. This is why per-note singleton masks cannot fix022.
first_overlaps = sum(c['firstMismatch'] > 0 for c in domains['comparisons'])
assert first_overlaps == 14
assert all((w & 0xFFF) == ((w | flag) & 0xFFF) for t in domains['tables'] for w in t['packed']
    for flag in (0, 0x2000, 0x4000, 0x6000))

keys = ['bit[0xD6C]', 'bit[0xD70]', 'var[0x064]', 'var[0x067]', 'var[0x062]', 'var[0x07A]']
keys.append('bit[0xD67]')
keys += [f'var[0x{i:03X}]' for i in range(0x56, 0x5A)]
def sample(s):
    v = s['snapshot']['KnownGlobalVariables']
    estimates = s['estimatedVariables']
    return dict(stage=s['stage'], eventCount=s['eventCount'],
        variables={k:dict(value=v.get(k), estimated=k in estimates) for k in keys},
        actorPlacement=s['actors'][0]['Estimate'],
        actorPalette=s['actors'][0]['Look'].get('PaletteEstimate'))

route_results = []
for folder in ('room41-1', 'egg-first-1', 'dye-last-1', 'preserved-1'):
    for p in sorted((output / folder).glob('*.json')):
        d = load(p)
        name = d['route']['id']
        final = d['snapshots'][-1]
        room = final['snapshot']['RoomId']
        begun = sum(t.startswith('cutscene at ') for t in d['boundaries'])
        ended = sum(t.startswith('endCutscene at ') for t in d['boundaries'])
        expected = (41, 15, 14) if name.startswith('room41-raw4') else (
            (41, 12, 11) if name.startswith('room41-raw6') else (
            (43, 15, 15) if name.startswith('direct41') else (
            ((62 if 'dye-wheel' in name else 6), 9, 9) if name.startswith('native-level4') else (
            (43, 4, 3) if name == 'late096-reached44' else (
            (46, 4, 4) if name == 'late096-source-skip095' else (18, 7, 7))))))
        assert (room, begun, ended) == expected, name
        assert not any('prerequisiteNotReached' in a or 'missingOverride' in a for a in d['actions'])
        old = load(prior / folder / p.name)
        assert d['elapsed'] == old['elapsed'] and d['boundaries'] == old['boundaries']
        assert d['roomChanges'] == old['roomChanges']
        if name.startswith('native-level4'):
            v = final['snapshot']['KnownGlobalVariables']
            assert v['var[0x07A]'] == 4 and 'var[0x07A]' in final['estimatedVariables'] and v['bit[0xD6F]'] == 0
            assert [v[f'var[0x{i:03X}]'] for i in (0x32, 0x34, 0x36)] == [0x7282, 0x7400, 0x7200]
        stages = [s for s in d['snapshots'] if s['stage'].startswith('draft-') or s['stage'] == 'final']
        changes = []
        for s in stages:
            before = next((x for x in old['snapshots'] if x['stage'] == s['stage']), None)
            if before:
                now = sample(s)
                was = sample(before)
                delta = {k:dict(before=was['variables'][k], after=now['variables'][k]) for k in keys
                    if was['variables'][k] != now['variables'][k]}
                actor_delta = {k:dict(before=was[k], after=now[k]) for k in ('actorPlacement', 'actorPalette') if was[k] != now[k]}
                if delta or actor_delta:
                    changes.append(dict(stage=s['stage'], variables=delta, actor=actor_delta))
        observations = [sample(s) for s in stages if s['stage'] in ('final', 'draft-4-before',
            'draft-2-key-note-1', 'draft-2-key-note-2', 'draft-2-key-note-3', 'draft-2-key-note-4')]
        route_results.append(dict(id=name, packet=digest(p), elapsed=d['elapsed'], room=room,
            begun=begun, ended=ended, timingRoomsAndScopesUnchanged=True, observations=observations, changes=changes))
assert len(route_results) == 13
raw4 = load(output / 'room41-1/room41-raw4-click.json')
dye2 = next(s for s in raw4['snapshots'] if s['stage'] == 'draft-2-key-note-2')
assert dye2['actors'][0]['Estimate'] is None and dye2['actors'][0]['Look']['PaletteEstimate']
assert all(k not in dye2['estimatedVariables'] for k in ('var[0x062]', 'var[0x057]'))
final_gate = next(s for s in raw4['snapshots'] if s['stage'] == 'draft-4-before')
assert all(k in final_gate['estimatedVariables'] for k in keys[:6])

for level in (4, 6):
    click = load(output / f'room41-1/room41-raw{level}-click.json')
    key = load(output / f'room41-1/room41-raw{level}-key.json')
    assert [e for e in click['events'] if e['SourceScriptId'] != 5] == [e for e in key['events'] if e['SourceScriptId'] != 5]
    assert click['elapsed'] == key['elapsed'] and click['roomChanges'] == key['roomChanges']

traces_path = output / 'certainty-2/traces.json'
traces = load(traces_path)
for r in traces:
    native4 = 'raw4' in r['packet']
    assert r['gate']['PathEstimated'] == native4
    assert r['scan']['UnresolvedReason'] is None
    assert [t for t in r['scan']['Trace'] if 'branch at' in t] == [
        f"branch at +0x{'0000' if native4 else '007D'} reads a seeded pick; later steps are estimates"]

gap_packet_path = output / 'gaps-1/room41-raw4-click.json'
gap_packet = load(gap_packet_path)
assert gap_packet['elapsed'] == raw4['elapsed'] and gap_packet['events'] == raw4['events']
assert gap_packet['boundaries'] == raw4['boundaries'] and gap_packet['roomChanges'] == raw4['roomChanges']
handshake_path = output / 'handshake-gaps-1/traces.json'
handshake = load(handshake_path)
assert len(handshake) == 30
assert all(r['PathEstimated'] == r['pendingPathEstimated'] for r in handshake)
gaps = [s for s in gap_packet['snapshots'] if s['stage'].startswith('draft-2-gap')]
slot20 = lambda s: next(p for p in s['pending'] if p['number'] == 20)
transition_index = next(i for i in range(1, len(gaps)) if not slot20(gaps[i-1])['pathEstimated']
    and slot20(gaps[i])['pathEstimated'])
before, after = gaps[transition_index - 1:transition_index + 1]
assert before['stage'] == 'draft-2-gap-2-5' and after['stage'] == 'draft-2-gap-2-6'
assert before['snapshot']['KnownGlobalVariables']['bit[0xD67]'] == 1 and 'bit[0xD67]' not in before['estimatedVariables']
assert after['snapshot']['KnownGlobalVariables']['bit[0xD67]'] == 0 and 'bit[0xD67]' in after['estimatedVariables']
assert slot20(before)['offset'] == 0x28 and slot20(after)['offset'] == 0x60
assert all(k not in after['estimatedVariables'] for k in ('var[0x062]', 'var[0x056]', 'var[0x057]'))
transition = dict(before=sample(before), after=sample(after),
    pendingBefore=slot20(before), pendingAfter=slot20(after),
    inference='Source020 +0028 reads D67 before increment002D; the first recorded path transition follows031 completion. Snapshots bound the frame, not the intermediate instruction.')

result = dict(source=digest(code), sourceDomains=digest(domains_path),
    practice=dict(writers=writers, instructionOnlyExits=practice,
        scope='Script031 instruction CFG including resumed frames; only D6C branch semantics. No callee, scheduler, cancellation or native-completion proof.',
        consequence='All completed instruction-only paths leave D6C0; suspended/stopped/concurrent paths and inherited path estimates still require provenance.'),
    handshake=dict(packet=digest(gap_packet_path), isolated=digest(handshake_path), firstRecordedTransition=transition,
        instructionOnlyExits=handshake_exits,
        consequence='D67 completion-wait branch taints020 despite known entered notes. Script031 early stop0019 can leave D671; blanket completion clearing is unsound.'),
    packedMasks=projections, comparisons=51, collisions=0, firstComponentOverlaps=first_overlaps,
    finiteDomainConsequence='Retain tuple correlations across the whole rejection predicate. Do not infer selected notes or path certainty from disjointness.',
    isolatedTraces=digest(traces_path), routes=route_results,
    native090Completed=False)
(base / 'precision-summary.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print('13 routes preserved; palette false dependency removed; native090 still Conditional. Practice CFG and tuple/mask domains verified.')
