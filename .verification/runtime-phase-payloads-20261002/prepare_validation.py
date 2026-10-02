"""Create fresh runners; reuse all inherited assertions without changing older packets."""
from pathlib import Path

old = Path('.verification/runtime-boundaries-20261002')
new = Path('.verification/runtime-phase-payloads-20261002')
for name in ('run-validation.ps1', 'analyze_native_fields.py', 'analyze_correspondence.py',
             'analyze_loaded_objects.py', 'analyze_camera.py', 'create_camera_audit.py',
             'run-camera.ps1', 'finalize_checks.py', 'validate_publication.py'):
    target = new/name
    if target.exists(): raise FileExistsError(target)
    source = (old/name).read_text(encoding='utf-8').replace(old.as_posix(), new.as_posix())
    source = source.replace('RuntimeBoundariesFinalSession4-20261002', 'RuntimePhasePayloadFinalSession2-20261002')
    if name == 'run-validation.ps1':
        source = source.replace("$filter='", "$filter='FullyQualifiedName~LoomCallPhasePayloadTests|FullyQualifiedName~LoomNativeObservationGapTests|")
        source = source.replace("if ($LASTEXITCODE) { throw 'Exact comparison failed.' }",
            "if ($LASTEXITCODE) { throw 'Exact comparison failed.' }\n"
            '& $rtkPath proxy $pythonPath -X utf8 .verification/runtime-phase-payloads-20261002/analyze_native_continuity.py "$Output/final-evidence"\n'
            "if ($LASTEXITCODE) { throw 'Exact native stream comparison failed.' }")
    if name == 'finalize_checks.py':
        source = source.replace('622', '654').replace('296', '328')
        # Historical phase diagnostics stay dated, hashed and at their original path.
        source = source.replace("(base/'phase-diagnostic-summary.json')", "(Path('.verification/runtime-boundaries-20261002/phase-diagnostic-summary.json'))")
        source += '''
for folder, failed, passed in (('phase-before',15,5), ('phase-final',0,101),
                               ('native-before',5,7), ('native-final',0,130)):
    path = Path('Exe/RuntimePhasePayloadSession-20261002')/folder/'tests.txt'
    raw = path.read_bytes()
    log = raw.decode('utf-16' if raw.startswith((b'\\xff\\xfe',b'\\xfe\\xff')) else 'utf-8-sig')
    assert re.search(rf'Failed:\\s+{failed},\\s+Passed:\\s+{passed}',log), folder
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
(base/'check-summary.json').write_text(json.dumps(summary,indent=2)+'\\n',encoding='utf-8')
'''
    if name == 'validate_publication.py':
        source = source.replace('622','654').replace('296','328').replace('f84460f','f649ff1')
        source = source.replace("required = {", "required = {'b65cb4e','f84460f','02c9fc1',")
        source = source.replace("assert '`6ad392d`' in current and '`4b7c646`' in current",
                                "assert '`02c9fc1`' in current")
    if name in ('analyze_correspondence.py','analyze_loaded_objects.py','analyze_camera.py'):
        source = source.replace('Exe/RuntimeRestorationFinalSession-20261002',
                                'Exe/RuntimeBoundariesFinalSession4-20261002')
    if name == 'analyze_loaded_objects.py':
        source = source.replace('exact to a3a90ec', 'exact to f649ff1')
    target.write_text(source,encoding='utf-8')
print('Fresh runner prepared; all previous packets untouched.')
