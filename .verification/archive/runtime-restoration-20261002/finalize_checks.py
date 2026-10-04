"""Publish only complete current-source preservation, without rewriting previous packets."""
from pathlib import Path
import hashlib
import json
import sys

source = Path('.verification/archive/runtime-object-correspondence-20261002/finalize_checks.py').read_text(encoding='utf-8')
source = source.replace('.verification/archive/runtime-object-correspondence-20261002', '.verification/archive/runtime-restoration-20261002')
source = source.replace('568', '588').replace('242', '262')
exec(compile(source, 'preserved-final-checks', 'exec'), {'__file__': __file__, '__name__': '__main__'})
base = Path(__file__).parent
out = Path(sys.argv[1])
summary = json.loads((base/'check-summary.json').read_text(encoding='utf-8'))
preserved = json.loads((base/'preservation-summary.json').read_text(encoding='utf-8'))
camera = json.loads((base/'camera-summary.json').read_text(encoding='utf-8'))
assert preserved['primaryEntireLatestStreamsExact'] and camera['recipeAndWholePacketExact']
assert not camera['loadedCameraLifetimeClaim']
protected = json.loads(Path('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json').read_text(encoding='utf-8'))
assert len(protected)==57 and all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in protected.items())
for report in preserved['reports']+camera['reports']:
    assert hashlib.sha256(Path(report['path']).read_bytes()).hexdigest()==report['sha256'], report['path']
summary.update(newCallerIncomingIdentityCases=8, reproducedIncomingIdentityFailures=5, newCameraDependencyCases=12,
    primaryEntireLatestStreamsExact=True, separateEntireLoadedObjectCallSelectionCorrespondenceExact=True,
    nativeCameraFieldProvenanceUnknown=True, loadedCameraLifetimeClaim=False,
    separateLoadedCameraWaits=camera['cameraWaits'], separateLoadedCameraFields=camera['copiedFields'])
summary['reports'] += preserved['reports']+camera['reports']
summary['reports'] += [dict(path=(base/name).as_posix(), sha256=hashlib.sha256((base/name).read_bytes()).hexdigest())
    for name in ('preservation-summary.json','camera-summary.json')]
(base/'check-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print('Current Core588/focused262;all latest primary/separate loaded object records exact;57 protected files unchanged.')
