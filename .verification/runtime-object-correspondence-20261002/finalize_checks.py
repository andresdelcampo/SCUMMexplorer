"""Require the fresh full-source baseline and every inherited preservation assertion."""
from pathlib import Path
import hashlib
import json
import sys

base=Path(__file__).parent
out=Path(sys.argv[1])
source=Path('.verification/runtime-correspondence-20261002/finalize_checks.py').read_text(encoding='utf-8')
source=source.replace('.verification/runtime-correspondence-20261002','.verification/runtime-object-correspondence-20261002')
source=source.replace('535','568').replace('209','242')
exec(compile(source,'preserved-final-checks','exec'),{'__file__':__file__,'__name__':'__main__'})
summary=json.loads((base/'check-summary.json').read_text(encoding='utf-8'))
extra=json.loads((base/'object-message-summary.json').read_text(encoding='utf-8'))
assert extra['allLegacyPacketsCallsChronologiesCompletionExact'] and extra['recipeCount']==13
assert extra['totalBindings']>=366 and extra['totalStructuralTriples']>=55
assert extra['callerResumptions']==extra['originalWriterResumedChains']==0
assert summary['sourceChallengeNativeFieldReads']==11 and summary['sourceChallengeExplicitNotificationInvalidations']==11
loaded=json.loads((base/'loaded-object-summary.json').read_text(encoding='utf-8'))
assert loaded['originalRecipeAndWholePacketExact'] and loaded['recipe']=='room41-raw4-click'
assert loaded['actualObjectSelections']==loaded['actualObjectBindings']==16
assert loaded['terminatedObjectChildren']==14 and loaded['immediateStructuralTriples']==9
assert loaded['observedCallerResumptions']==3 and loaded['childRestorations']==195
assert loaded['rejectionCounts']=={'call correspondence boundary bound reached':1}
for report in loaded['reports']: assert hashlib.sha256(Path(report['path']).read_bytes()).hexdigest()==report['sha256']
loaded_manifest=json.loads((out/'loaded-objects-2/observer-1/source-manifest.json').read_text(encoding='utf-8'))
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in loaded_manifest.items())
protected=json.loads(Path('Exe/RuntimeObjectCorrespondenceSession-20261002/protected-before.json').read_text(encoding='utf-8'))
assert len(protected)==57 and all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in protected.items())
reports=[report for route in extra['routes'] for report in route['reports']]
for report in reports: assert hashlib.sha256(Path(report['path']).read_bytes()).hexdigest()==report['sha256']
summary.update(newObjectCorrespondenceCases=21,newMessageDependencyCases=12,
    actualObjectSelections=extra['totalObjectSelections'],actualObjectBindings=extra['totalObjectBindings'],
    actualFallbackObjectBindings=extra['totalFallbackBindings'],copiedMessageFields=extra['copiedMessageFields'],
    nativeMessageFieldProvenanceUnknown=True,loadedMessageLifetimeClaim=False,protectedFilesUnchanged=57,
    separateLoadedObjectBindings=16,separateLoadedObjectTerminations=14,separateLoadedStructuralTriples=9,
    separateLoadedCallerResumptions=3,separateLoadedChildRestorations=195,separateLoadedBoundaryRejections=1)
summary['reports']+=reports+[dict(path=(base/'object-message-summary.json').as_posix(),
    sha256=hashlib.sha256((base/'object-message-summary.json').read_bytes()).hexdigest())]
summary['reports']+=loaded['reports']+[dict(path=(base/'loaded-object-summary.json').as_posix(),
    sha256=hashlib.sha256((base/'loaded-object-summary.json').read_bytes()).hexdigest())]
(base/'check-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('Core568/focused242;original366 bindings/55 triples retained;new object selections separate;all57 protected files unchanged.')
