"""Derive finite boot draft alternatives from the pinned disassembly.

This is read-only research, not a replacement runtime certainty policy.
"""
from pathlib import Path
import hashlib
import json
import re
from collections import Counter

base = Path(__file__).parent
code = Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')
code_hash = hashlib.sha256(code.read_bytes()).hexdigest()
assert code_hash == '0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569'
rows = [line.split('\t') for line in code.read_text(encoding='utf-8-sig').splitlines()]
init = [r for r in rows if r[0] == 'Room 01 local 211']
tables = {}
table = None
random_site = None
for owner, offset, op, operands in init:
    if op == 'setVarRange' and operands.startswith('local[0x002], count=3,'):
        table = (offset, [int(x, 16) for x in re.findall(r'0x([0-9A-Fa-f]+)', operands)[1:]])
        assert len(table[1]) == 3
    elif op == 'getRandomNr' and operands == 'local[0x001], 0x02':
        random_site = offset
    elif op == 'move' and operands.endswith('= local[0x000]'):
        match = re.fullmatch(r'var\[0x032\+0x([0-9A-Fa-f]+)\] = local\[0x000\]', operands)
        if match:
            word_index = int(match[1], 16)
            tables[word_index // 2] = dict(slot=word_index // 2, table=table[0],
                random=random_site, write=offset, packed=table[1])
    elif op == 'setVarRange' and operands.startswith('var[0x032+0x000], count=2,'):
        words = [int(x,16) for x in re.findall(r'0x([0-9A-Fa-f]+)', operands)[2:]]
        assert len(words) == 2
        tables[0] = dict(slot=0, table=offset, random=None, write=offset, packed=words[:1])

def notes(word):
    return [((word >> (3*i)) & 7) + 1 for i in range(4)]

for slot in range(5):
    row = tables[slot]
    row['notes'] = [notes(w) for w in row['packed']]
    row['allCanReverse'] = all(w & 0x1000 for w in row['packed'])

comparisons = []
for earlier in range(4):
    for input_index, input_notes in enumerate(tables[4]['notes']):
        for candidate_index, word in enumerate(tables[earlier]['packed']):
            for reverse in (False, True):
                if reverse and not word & 0x1000:
                    continue
                candidate_notes = notes(word)[::-1] if reverse else notes(word)
                mismatch = next((i for i,(a,b) in enumerate(zip(input_notes, candidate_notes)) if a != b), None)
                assert mismatch is not None, (earlier, input_index, candidate_index, reverse)
                comparisons.append(dict(earlierSlot=earlier, inputAlternative=input_index,
                    candidateAlternative=candidate_index, reverse=reverse, firstMismatch=mismatch,
                    comparisonOffset=f'+0x{(0xDF if reverse else 0x7D)+0x15*mismatch:04X}'))

assert len(comparisons) == 51
out = dict(source=dict(path=code.as_posix(), sha256=code_hash),
    scope='Room01 local211 boot alternatives for slots0..4; source025/034 OR flags preserve low12 note bits. No whole-game or runtime certainty claim.',
    tables=[tables[i] for i in range(5)],
    comparisons=comparisons,
    mismatchCounts=dict(Counter(r['comparisonOffset'] for r in comparisons)),
    conclusions=['All three slot4 alternatives differ from every earlier slot0..3 alternative in forward matching and in every source-capable reverse match.',
                 'Disjointness does not make the selected slot4 notes, learned/used bits, input buffer, index or inherited path known in the current executor.'])
(base / 'source-domains.json').write_text(json.dumps(out, indent=2)+'\n', encoding='utf-8')
print(f'{len(comparisons)} source alternative pairs reject earlier candidates; no collisions.')
