"""Source-only numeric interpretation of the complete 022/023 predicate.

Enumerated hypothetical source domains are not injected into an explorer session.
The result stops before 026. Unknown response/effect/path provenance stays outside
the numeric predicate claim. Unsupported operations fail closed.
"""
from collections import Counter
from itertools import product
from pathlib import Path
import hashlib
import json
import re

base = Path(__file__).parent
code = Path('Exe/VerificationSession-20261001/native-code-1/code.tsv')
domains_path = Path('.verification/archive/room41-certainty-20261002/source-domains.json')
def digest(p):
    return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())
assert digest(code)['sha256']=='0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569'
assert digest(domains_path)['sha256']=='ccb91156982909ecd6919f9333b9dbbd4eacca347d0205b37a3600181afa3401'
domains=json.loads(domains_path.read_text(encoding='utf-8'))
rows=[line.split('\t') for line in code.read_text(encoding='utf-8-sig').splitlines()]
programs={owner:[(int(at[3:],16),op,arg) for o,at,op,arg in rows if o==owner]
    for owner in ('script 022','script 023')}
variable=re.compile(r'(var|local|bit)\[0x([0-9A-Fa-f]+)(?:\+(.+))?\]')
def key(operand, globals_, locals_):
    m=variable.fullmatch(operand)
    assert m, operand
    kind,at,index=m.groups()
    return f'{kind}[0x{int(at,16)+(read(index,globals_,locals_) if index else 0):03X}]'
def read(operand, globals_, locals_):
    if operand.startswith('0x'): return int(operand,16)
    k=key(operand,globals_,locals_)
    return (locals_ if k.startswith('local[') else globals_)[k]
def write(operand, value, globals_, locals_, writes):
    k=key(operand,globals_,locals_)
    if k.startswith('bit['): value=int(bool(value))
    (locals_ if k.startswith('local[') else globals_)[k]=value
    if not k.startswith('local['): writes.add(k)
def execute(owner, globals_, arguments, start=0, branches=None, writes=None):
    branches=[] if branches is None else branches
    writes=set() if writes is None else writes
    locals_={f'local[0x{i:03X}]':0 for i in range(26)}
    locals_.update({f'local[0x{i:03X}]':v for i,v in enumerate(arguments)})
    instructions=programs[owner]
    by_offset={at:i for i,(at,_,_) in enumerate(instructions)}
    pc=start
    for _ in range(10000):
        i=by_offset[pc]
        at,op,arg=instructions[i]
        if op=='stopObjectCode': return None
        following=instructions[i+1][0]
        branch=re.search(r'->\s*0x([0-9A-Fa-f]+)',arg)
        if branch:
            target=int(branch[1],16)
            if op=='jumpRelative': pc=target; continue
            parts=arg.split(', branch')[0].split(', ')
            left=read(parts[0],globals_,locals_)
            if op=='notEqualZero': condition=left!=0
            elif op=='isEqual': condition=left==read(parts[1],globals_,locals_)
            elif op=='isLess':
                # Old-v3 follows b OP a with signed16 operands, branch on false.
                right=read(parts[1],globals_,locals_)
                signed=lambda v:v-65536 if v&0x8000 else v
                condition=signed(right)<signed(left)
            else: raise AssertionError((owner,at,op))
            branches.append(dict(owner=owner,offset=at,slot=locals_['local[0x000]'],condition=condition))
            pc=following if condition else target
            continue
        if op in ('startScript','chainScript'):
            target,arglist=arg.split(', [')
            values=[read(v,globals_,locals_) for v in arglist.removesuffix(']').split(', ') if v]
            if op=='startScript':
                assert int(target,16)==23 and owner=='script 022'
                execute('script 023',globals_,values,writes=writes)
            else:
                assert int(target,16)==26 and owner=='script 022'
                return dict(offset=at,slot=values[0],reverse=values[1])
        elif op=='increment': write(arg,(read(arg,globals_,locals_)+1)&65535,globals_,locals_,writes)
        elif op in ('move','multiply','and','divide'):
            dest,_,operand=arg.split(' ')
            value=read(operand,globals_,locals_)
            if op!='move':
                current=read(dest,globals_,locals_)
                value={'multiply':lambda:current*value,'and':lambda:current&value,'divide':lambda:current//value}[op]()
            write(dest,value&65535 if isinstance(value,int) else value,globals_,locals_,writes)
        else: raise AssertionError((owner,at,op,arg))
        pc=following
    raise AssertionError('Bounded numeric predicate did not terminate')

tables=domains['tables']
def world(words,input_notes):
    state={f'var[0x{0x32+2*i:03X}]':w for i,w in enumerate(words)}
    state.update({f'var[0x{0x33+2*i:03X}]':f'opaque response for slot {i}' for i in range(5)})
    state.update({f'var[0x{0x56+i:03X}]':n for i,n in enumerate(input_notes)})
    return state
def scan(words,input_notes):
    state=world(words,input_notes)
    branches=[]
    endpoint=execute('script 022',state,[],start=0x64,branches=branches)
    assert endpoint is not None
    return endpoint,branches

mask_cases=[]
for table in tables:
    for alternative,word in enumerate(table['packed']):
        for flags in (0,0x2000,0x4000,0x6000):
            state=world([0]*5,[0]*4)
            state[f'var[0x{0x32+2*table["slot"]:03X}]']=word|flags
            writes=set()
            execute('script 023',state,[table['slot']],writes=writes)
            notes=[state[f'var[0x{0x52+i:03X}]'] for i in range(4)]
            assert notes==table['notes'][alternative]
            assert state['bit[0xD6A]']==int(bool(word&0x1000))
            assert writes=={'var[0x065]','bit[0xD69]','bit[0xD6A]','bit[0xD6B]',*[f'var[0x{0x52+i:03X}]' for i in range(4)]}
            mask_cases.append(dict(slot=table['slot'],alternative=alternative,flags=flags,notes=notes,
                learned=state['bit[0xD69]'],used=state['bit[0xD6B]'],canReverse=state['bit[0xD6A]']))
worlds=list(product(*(t['packed'] for t in tables)))
assert len(worlds)==81
assert len(mask_cases)==52
endpoints=Counter()
comparison_cases=set()
for words in worlds:
    notes=tables[4]['notes'][tables[4]['packed'].index(words[4])]
    for reverse in (False,True):
        endpoint,branches=scan(words,notes[::-1] if reverse else notes)
        assert endpoint==dict(offset=0x125 if reverse else 0xC3,slot=4,reverse=int(reverse))
        endpoints[(endpoint['offset'],endpoint['slot'],endpoint['reverse'])]+=1
        for b in branches:
            if b['slot']<4 and b['offset'] in (0x7D,0x92,0xA7,0xBC,0xDF,0xF4,0x109,0x11E) and not b['condition']:
                comparison_cases.add((reverse,b['slot'],words[4],words[b['slot']],b['offset']))
forward=[c for c in comparison_cases if not c[0]]
assert len(forward)==51
assert sum(c[4] not in (0x7D,0xDF) for c in forward)==14
expected={(c['earlierSlot'],tables[4]['packed'][c['inputAlternative']],
    tables[c['earlierSlot']]['packed'][c['candidateAlternative']],int(c['comparisonOffset'][3:],16)) for c in domains['comparisons']}
assert {(c[1],c[2],c[3],c[4]) for c in forward}==expected

# Losing the INPUT tuple's correlations fabricates drafts absent from source.
input_tuples={tuple(n) for n in tables[4]['notes']}
cartesian=set(product(*({n[i] for n in input_tuples} for i in range(4))))
fabricated=[]
for table in tables[:4]:
    for alternative,notes in enumerate(table['notes']):
        for reverse in (False,True):
            if reverse and not table['packed'][alternative]&0x1000: continue
            candidate=tuple(notes[::-1] if reverse else notes)
            if candidate not in cartesian-input_tuples: continue
            words=list(worlds[0])
            words[table['slot']]=table['packed'][alternative]
            endpoint,_=scan(words,candidate)
            assert endpoint['slot']<4
            fabricated.append(dict(input=candidate,earlierSlot=endpoint['slot'],reverse=endpoint['reverse']))
assert len(fabricated)==4 and len(cartesian)==54 and len(input_tuples)==3
unknown_input_words=list(worlds[0])
unknown_input_words[3]=tables[3]['packed'][2]
unknown_input_endpoint,_=scan(unknown_input_words,[1,1,1,1])
assert unknown_input_endpoint['slot']==3

result=dict(source=digest(code),sourceDomains=digest(domains_path),
    scope='Instruction-level source-only numeric predicate from022 +0064 to chain026, with complete023 decoding; no D70 gate, session, scheduler, cancellation, selected runtime input/index, dispatch or native090 proof.',
    bootWorlds=len(worlds),forwardAndReverseInputWorlds=2*len(worlds),flagProjectionCases=len(mask_cases),
    endpoints=[dict(offset=at,slot=slot,reverse=rev,count=n) for (at,slot,rev),n in sorted(endpoints.items())],
    priorComparisons=51,priorFirstComponentOverlaps=14,priorPredicatesReproduced=True,
    numericCalleeWrites=sorted(writes),maskCases=mask_cases,
    correlatedInputTuples=len(input_tuples),cartesianInputTuples=len(cartesian),fabricatedInputs=fabricated,
    unrestrictedInputCounterexample=dict(input=[1,1,1,1],endpoint=unknown_input_endpoint),
    pathAndEffectCertainty='Unchanged and independent: hypothetical identical numeric endpoints do not make conditional callees or external state effects reached.',
    native090Completed=False)
(base/'predicate-summary.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(f'{len(worlds)*2} correlated numeric scan worlds reach slot4;52 flag projections;51 prior predicate cases reproduced. No runtime certainty changed.')
