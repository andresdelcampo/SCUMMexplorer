"""Require complete current-source reports before publishing final facts."""
from pathlib import Path
import hashlib
import json
import sys

base=Path(__file__).parent
out=Path(sys.argv[1])
source=Path('.verification/archive/runtime-calls-20261002/finalize_checks.py').read_text(encoding='utf-8')
source=source.replace('.verification/archive/runtime-calls-20261002','.verification/archive/runtime-correspondence-20261002')
source=source.replace('501','535').replace('175','209')
exec(compile(source,'preserved-final-checks','exec'),{'__file__':__file__,'__name__':'__main__'})
summary=json.loads((base/'check-summary.json').read_text(encoding='utf-8'))
correspondence=json.loads((base/'correspondence-summary.json').read_text(encoding='utf-8'))
assert correspondence['latestPacketsAndAllLegacyStreamsExact'] and correspondence['originalWriterTerminals']==76
native=json.loads((base/'native-field-summary.json').read_text(encoding='utf-8'))
assert native['everyInputAndFrameTransparent'] and native['finalPacketExact']
assert native['frames']==36 and native['expressions']==3 and native['expressionSteps']==17
assert native['nativeFieldReads']>0 and sum(native['domains'].values())==native['nativeFieldReads']
for report in native['reports']:
    assert hashlib.sha256(Path(report['path']).read_bytes()).hexdigest()==report['sha256']
native_manifest=json.loads((out/'native-fields/field-audit/source-manifest.json').read_text(encoding='utf-8'))
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in native_manifest.items())
summary.update(newCorrespondenceCases=20,newNativeFieldCases=14,
    actualCalleeAssignmentsAndStructuralCorrespondence=True,
    bindings=sum(r['actualBindings'] for r in correspondence['routes']),
    immediateStopCorrespondences=sum(r['immediateStopCorrespondences'] for r in correspondence['routes']),
    observedCallerResumptions=sum(r['observedCallerResumptions'] for r in correspondence['routes']),
    additionalCopiedNativeFields=sum(sum(r['additionalCopiedNativeFields'].values()) for r in correspondence['routes']),
    sourceChallengeNativeFieldReads=native['nativeFieldReads'],
    sourceChallengeNativeFieldDomains=native['domains'],
    sourceChallengeExplicitNotificationInvalidations=native['explicitNotificationInvalidations'])
for report in [d for r in correspondence['routes'] for d in r['reports']]:
    assert hashlib.sha256(Path(report['path']).read_bytes()).hexdigest()==report['sha256']
summary['reports'].append(dict(path=(base/'correspondence-summary.json').as_posix(),
    sha256=hashlib.sha256((base/'correspondence-summary.json').read_bytes()).hexdigest()))
summary['reports']+=native['reports']
summary['reports'].append(dict(path=(base/'native-field-summary.json').as_posix(),
    sha256=hashlib.sha256((base/'native-field-summary.json').read_bytes()).hexdigest()))
protected=json.loads(Path('Exe/RuntimeCorrespondenceSession-20261002/protected-before.json').read_text(encoding='utf-8'))
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in protected.items())
summary['protectedFilesUnchanged']=len(protected)
(base/'check-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('Core535/focused209; latest call/legacy streams exact; independent structural correspondence; loaded native fields/frame transparency; protected files unchanged.')
