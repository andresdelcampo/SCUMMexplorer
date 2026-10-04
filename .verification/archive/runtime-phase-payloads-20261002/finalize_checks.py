"""Publish only complete current-source preservation, without rewriting previous packets."""
from pathlib import Path
import hashlib
import json
import sys

source = Path('.verification/archive/runtime-object-correspondence-20261002/finalize_checks.py').read_text(encoding='utf-8')
source = source.replace('.verification/archive/runtime-object-correspondence-20261002', '.verification/archive/runtime-phase-payloads-20261002')
source = source.replace('568', '654').replace('242', '328')
exec(compile(source, 'preserved-final-checks', 'exec'), {'__file__': __file__, '__name__': '__main__'})
base = Path(__file__).parent
out = Path(sys.argv[1])
summary = json.loads((base/'check-summary.json').read_text(encoding='utf-8'))
preserved = json.loads((base/'preservation-summary.json').read_text(encoding='utf-8'))
camera = json.loads((base/'camera-summary.json').read_text(encoding='utf-8'))
assert preserved['primaryEntireLatestStreamsExact'] and camera['recipeAndWholePacketExact']
assert not camera['loadedCameraLifetimeClaim']
assert camera['entirePreviousCameraChronologyExact'] and camera['cameraWaits'] == 55 and camera['copiedFields'] == 220
protected = json.loads(Path('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json').read_text(encoding='utf-8'))
assert len(protected)==57 and all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in protected.items())
for report in preserved['reports']+camera['reports']:
    assert hashlib.sha256(Path(report['path']).read_bytes()).hexdigest()==report['sha256'], report['path']
summary.update(newBoundaryPhaseCases=19, reproducedBoundaryPhaseFailures=12, newInventoryDependencyCases=15, newCallerIncomingIdentityCases=8, reproducedIncomingIdentityFailures=5, newCameraDependencyCases=12,
    primaryEntireLatestStreamsExact=True, separateEntireLoadedObjectCallSelectionCorrespondenceExact=True,
    nativeCameraFieldProvenanceUnknown=True, loadedCameraLifetimeClaim=False,
    separateLoadedCameraWaits=camera['cameraWaits'], separateLoadedCameraFields=camera['copiedFields'])
summary['reports'] += preserved['reports']+camera['reports']
summary['reports'] += [dict(path=(base/name).as_posix(), sha256=hashlib.sha256((base/name).read_bytes()).hexdigest())
    for name in ('preservation-summary.json','camera-summary.json')]
import re
diagnostics = Path('Exe/RuntimeBoundariesSession-20261002')
for folder, failed, passed in (('order-before',10,5), ('child-phase-before',2,1),
                               ('child-phase-final',0,80), ('independent-chain-final',0,81), ('inventory-final',0,60)):
    path = diagnostics/folder/'tests.txt'
    raw = path.read_bytes()
    log = raw.decode('utf-16' if raw.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
    assert re.search(rf'Failed:\s+{failed},\s+Passed:\s+{passed}',log), folder
    summary['reports'].append(dict(path=path.as_posix(),sha256=hashlib.sha256(raw).hexdigest()))
summary.update(nativeInventoryFieldProvenanceUnknown=True,loadedInventoryLifetimeClaim=False,
               stringContentsLengthLifetimeRemainOpaque=True)
diagnostic = json.loads((Path('.verification/archive/runtime-boundaries-20261002/phase-diagnostic-summary.json')).read_text(encoding='utf-8'))
assert diagnostic['entireCorrectedLoadedPacketAndStreamsExact'] and not diagnostic['chronologyRenumbered']
for report in diagnostic['reports']:
    assert hashlib.sha256(Path(report['path']).read_bytes()).hexdigest()==report['sha256']
summary['reports'] += diagnostic['reports']
summary['reports'].append(dict(path=(Path('.verification/archive/runtime-boundaries-20261002/phase-diagnostic-summary.json')).as_posix(),
    sha256=hashlib.sha256((Path('.verification/archive/runtime-boundaries-20261002/phase-diagnostic-summary.json')).read_bytes()).hexdigest()))
summary['opaqueCallerEndAndChildAssignmentRemainIndependent']=True
(base/'check-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print('Current Core654/focused328;all latest primary/separate loaded object records exact;57 protected files unchanged.')

for folder, failed, passed in (('phase-before',15,5), ('phase-final',0,101),
                               ('native-before',5,7), ('native-final',0,130)):
    path = Path('Exe/RuntimePhasePayloadSession-20261002')/folder/'tests.txt'
    raw = path.read_bytes()
    log = raw.decode('utf-16' if raw.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
    assert re.search(rf'Failed:\s+{failed},\s+Passed:\s+{passed}',log), folder
    summary['reports'].append(dict(path=path.as_posix(),sha256=hashlib.sha256(raw).hexdigest()))
summary.update(newPhasePayloadCases=20,reproducedPhasePayloadFailures=15,
               newNativeObservationGapCases=12,reproducedNativeObservationGapFailures=5)
native_continuity = json.loads((base/'native-continuity-summary.json').read_text(encoding='utf-8'))
assert native_continuity['primaryNativeStreams'] == 13 and native_continuity['entireReadMutationInvalidationChronologyExact']
for report in native_continuity['reports']:
    assert hashlib.sha256(Path(report['path']).read_bytes()).hexdigest() == report['sha256']
summary['reports'] += native_continuity['reports']
summary['reports'].append(dict(path=(base/'native-continuity-summary.json').as_posix(),
    sha256=hashlib.sha256((base/'native-continuity-summary.json').read_bytes()).hexdigest()))
summary['primaryEntireNativeReadMutationChronologyExact'] = True
(base/'check-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
