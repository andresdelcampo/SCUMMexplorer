"""Create a new preservation runner without altering historical packets."""
from pathlib import Path

old = Path('.verification/runtime-restoration-20261002')
new = Path('.verification/runtime-boundaries-20261002')
for name in ('run-validation.ps1', 'analyze_native_fields.py', 'analyze_correspondence.py',
             'analyze_loaded_objects.py', 'analyze_camera.py', 'create_camera_audit.py',
             'run-camera.ps1', 'finalize_checks.py', 'validate_publication.py'):
    target = new/name
    if target.exists():
        raise FileExistsError(target)
    source = (old/name).read_text(encoding='utf-8').replace(old.as_posix(), new.as_posix())
    source = source.replace('RuntimeRestorationFinalSession-20261002', 'RuntimeBoundariesFinalSession4-20261002')
    if name == 'run-validation.ps1':
        source = source.replace("$filter='", "$filter='FullyQualifiedName~LoomCallBoundaryOrderTests|FullyQualifiedName~LoomInventoryDependencyTests|")
    if name == 'finalize_checks.py':
        source = source.replace('588', '622').replace('262', '296')
        source = source.replace('summary.update(newCallerIncomingIdentityCases=8,',
            'summary.update(newBoundaryPhaseCases=19, reproducedBoundaryPhaseFailures=12, newInventoryDependencyCases=15, newCallerIncomingIdentityCases=8,')
    if name == 'validate_publication.py':
        source = source.replace('588', '622').replace('262', '296').replace('a3a90ec', 'f84460f')
        source = source.replace("required = {", "required = {'a3a90ec','3f32e5f','32f1ecb','6ad392d','4b7c646',")
        source = source.replace("assert '`3f32e5f`' in current and '`32f1ecb`' in current",
            "assert '`6ad392d`' in current and '`4b7c646`' in current")
    # Require exact current-baseline streams as well as every inherited assertion.
    if name in ('analyze_correspondence.py', 'analyze_loaded_objects.py', 'analyze_camera.py'):
        source = source.replace('Exe/RuntimeObjectCorrespondenceFinalSession2-20261002',
                                'Exe/RuntimeRestorationFinalSession-20261002')
    target.write_text(source, encoding='utf-8')
print('Prepared new runners; historical scripts and packets untouched.')
