"""Validate the separate loaded object capture without promoting primary chronology."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import struct
import sys

base=Path(__file__).parent
out=Path(sys.argv[1])
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def rows(p): return [json.loads(line) for line in p.read_text(encoding='utf-8').splitlines()]
def digest(p): return dict(path=p.as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
recipe=load(out/'recipe.json')
assert len(recipe)==1
name=recipe[0]['id']
original=load(Path('.verification/room41-certainty-20261002/room41.json'))
assert recipe[0]==next(r for r in original if r['id']==name)
packet=out/'route-1'/(name+'.json')
prior=Path('Exe/RuntimeCorrespondenceFinalSession-20261002/final-evidence/room41-1')/packet.name
assert load(packet)==load(prior)
selections=[r['selection'] for r in rows(packet.with_name(name+'-object-selections.jsonl'))]
calls=[r['boundary'] for r in rows(packet.with_name(name+'-calls.jsonl'))]
updates=[r['update'] for r in rows(packet.with_name(name+'-correspondence.jsonl'))]
dispatches={c['Call']['Id']:c for c in calls if c['Stage']=='dispatch'}
assert selections
for selection in selections:
    call,entry=selection['Dispatch'],selection['Entry']
    assert dispatches[call['Id']]['Call']==call
    assert call['Site']['Operation']=='startObject'
    assert selection['ObjectId']==entry['ObjectId']==next(b['Value'] for b in call['Operands'] if b['Role']=='target')
    assert selection['RequestedVerb']==next(b['Value'] for b in call['Operands'] if b['Role']=='verb')
    assert selection['RequestedVerb']==entry['VerbId'] or entry['IsFallback']
    # Independently check the copied selected table bytes and OBCD bounds.
    resource=next(p for p in Path('Games/loom').glob('*.LFL') if p.stem.isdigit() and int(p.stem)==selection['Site']['Container'])
    plain=bytes(b^255 for b in resource.read_bytes())
    assert plain[entry['TableOffset']]==entry['VerbId']
    assert entry['CodeOffset']+struct.unpack_from('<H',plain,entry['TableOffset']+1)[0]==entry['ScriptOffset']
    assert entry['CodeOffset']+struct.unpack_from('<H',plain,entry['CodeOffset'])[0]==entry['ResourceEnd']
    assert entry['ScriptOffset']==selection['Site']['ResourceOffset']<entry['ResourceEnd']
bindings={u['Dispatch']['Id']:u['Binding'] for u in updates if u['Binding']}
assert bindings
for binding in bindings.values():
    selection=binding['ObjectSelection']
    assert selection in selections and selection['Dispatch']==binding['Dispatch']
    assert binding['Initialization'] in calls and binding['Initialization']['Site']==selection['Site']
    assert binding['Initialization']['ParentSlot']==binding['Dispatch']['Slot']
    assert len(binding['Initialization']['Locals'])==26
    for assignment in binding['SuppliedAssignments']:
        assert assignment['Argument'] in binding['Dispatch']['Operands']
        assert assignment['Local'] in binding['Initialization']['Locals']
for u in updates:
    if u['ChildTerminated']:
        assert u['ChildRejection'] is None and u['ChildBoundaries'][-1]['ReachedStop']
    if u['ImmediateStopCorrespondence']:
        triple=u['ImmediateStopCorrespondence']
        assert triple['ChildEnd']['Sequence']<triple['HostReturn']['Sequence']<triple['CallerContinuation']['Sequence']
        assert triple['ChildEnd']['Invocation']==u['Binding']['Initialization']['Invocation']
    if u['CallerResumption']:
        chain=u['CallerResumption']
        assert chain['Yield']['ResumeOffset']==chain['RestoredLocals']['Site']['Offset']==chain['FirstInstruction']['Site']['Offset']
        assert chain['RestoredLocals']['Barrier'] is None
        assert chain['Yield']['Invocation']!=chain['FirstInstruction']['Invocation']
resumptions={u['Dispatch']['Id']:u for u in updates if u['CallerResumption']}
assert len(resumptions)==3
assert sorted(u['Dispatch']['Slot'] for u in resumptions.values())==[291,349,435]
assert sorted(u['CallerResumption']['RestoredLocals']['Call']['Id'] for u in resumptions.values())==[213,269,347]
assert all(u['Dispatch']['Site']['Script']==7 and u['Dispatch']['Site']['Offset']==0x2E and
    u['CallerResumption']['FirstInstruction']['Site']['Offset']==0x34 for u in resumptions.values())
manifest=load(out/'observer-1/source-manifest.json')
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in manifest.items())
build=(out/'build.txt').read_bytes()
text=build.decode('utf-16' if build.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
assert '0 Warning(s)' in text and '0 Error(s)' in text
summary=dict(scope='Separate all-site/key capture of the unchanged loaded raw4 recipe. Actual object entries and structural boundaries only;no successful return, numeric origins, completion or original writer resumed-chain claim. Primary legacy13 streams remain separate.',
    recipe=name,originalRecipeAndWholePacketExact=True,actualObjectSelections=len(selections),
    actualObjectBindings=len(bindings),actualFallbackBindings=sum(b['ObjectSelection']['Entry']['IsFallback'] for b in bindings.values()),
    terminatedObjectChildren=len({u['Dispatch']['Id'] for u in updates if u['ChildTerminated']}),
    immediateStructuralTriples=len({u['Dispatch']['Id'] for u in updates if u['ImmediateStopCorrespondence']}),
    observedCallerResumptions=len({u['Dispatch']['Id'] for u in updates if u['CallerResumption']}),
    childRestorations=sum(c['Stage']=='resumed-locals' for c in calls),
    rejectionCounts=dict(Counter(u['Barrier'] for u in updates if u['Barrier'])),
    selectedCallerResumptions=[dict(dispatch=u['Dispatch'],yieldBoundary=u['CallerResumption']['Yield'],
        restoredLocals=u['CallerResumption']['RestoredLocals'],firstInstruction=dict(
            sequence=u['CallerResumption']['FirstInstruction']['Sequence'],slot=u['CallerResumption']['FirstInstruction']['Slot'],
            parent=u['CallerResumption']['FirstInstruction']['ParentSlot'],invocation=u['CallerResumption']['FirstInstruction']['Invocation'],
            site=u['CallerResumption']['FirstInstruction']['Site'],pathEstimated=u['CallerResumption']['FirstInstruction']['PathEstimated']))
        for u in resumptions.values()],
    selected=[dict(dispatchId=b['Dispatch']['Id'],callerSlot=b['Dispatch']['Slot'],callerInvocation=b['Dispatch']['Invocation'],
        callerSite=b['Dispatch']['Site'],callerOperands=b['Dispatch']['Operands'],callerPathEstimated=b['Dispatch']['PathEstimated'],
        selection=b['ObjectSelection']['Entry'],requestedVerb=b['ObjectSelection']['RequestedVerb'],
        childSlot=b['Initialization']['Slot'],childParent=b['Initialization']['ParentSlot'],childInvocation=b['Initialization']['Invocation'],
        childSite=b['Initialization']['Site'],childPathEstimated=b['Initialization']['PathEstimated'],
        suppliedAssignments=b['SuppliedAssignments'],defaultLocals=sum(l['Role']=='default-zero' for l in b['Initialization']['Locals']))
        for b in bindings.values()],
    reports=[digest(out/p) for p in ('recipe.json','build.txt','run.txt','observer-1/source-manifest.json')]+[digest(packet.with_name(name+s)) for s in ('.json','-object-selections.jsonl','-calls.jsonl','-correspondence.jsonl')])
(base/'loaded-object-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print({k:v for k,v in summary.items() if k not in ('scope','selected','selectedCallerResumptions','reports')})
