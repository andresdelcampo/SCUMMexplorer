param([string]$Output='Exe/RuntimePhasePayloadFinalSession2-20261002')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$pythonPath='C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (Test-Path -LiteralPath $Output) { throw 'Use a fresh preservation output base.' }
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/runtime-dependencies-20261002/run-api.ps1 -Output "$Output/checks-final"
if ($LASTEXITCODE) { throw 'Core/Release failed.' }
$filter='FullyQualifiedName~LoomCallPhasePayloadTests|FullyQualifiedName~LoomNativeObservationGapTests|FullyQualifiedName~LoomCallBoundaryOrderTests|FullyQualifiedName~LoomInventoryDependencyTests|FullyQualifiedName~LoomCallerRestorationIdentityTests|FullyQualifiedName~LoomCameraDependencyTests|FullyQualifiedName~LoomObjectCallCorrespondenceTests|FullyQualifiedName~LoomMessageDependencyTests|FullyQualifiedName~LoomCallCorrespondenceTests|FullyQualifiedName~LoomNativeFieldAccessTests|FullyQualifiedName~LoomCallObservationTests|FullyQualifiedName~LoomRemainingAccessTests|FullyQualifiedName~LoomNativeMutationCoverageTests|FullyQualifiedName~LoomImplicitAccessTests|FullyQualifiedName~LoomExpressionObservationTests|FullyQualifiedName~LoomNativeDependencyTrackerTests|FullyQualifiedName~LoomWriterContinuityTests|FullyQualifiedName~LoomSourceAccessTests|FullyQualifiedName~LoomExtendedSourceAccessTests|FullyQualifiedName~LoomObservationBoundaryTests|FullyQualifiedName~LoomCompletionObserverTests|FullyQualifiedName~LoomRuntimeRelationTests|FullyQualifiedName~LoomObservedLineageTests'
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/runtime-continuity-20261002/run-focused.ps1 -Filter $filter -Output "$Output/focused-final"
if ($LASTEXITCODE) { throw 'Focused tests failed.' }
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/runtime-continuity-20261002/run-passive-expressions.ps1 -Output "$Output/passive-final"
if ($LASTEXITCODE) { throw 'Native challenge failed.' }
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/runtime-object-correspondence-20261002/run-native-fields.ps1 -Output "$Output/native-fields"
if ($LASTEXITCODE) { throw 'Native field challenge failed.' }
& $rtkPath proxy $pythonPath -X utf8 .verification/archive/runtime-phase-payloads-20261002/analyze_native_fields.py "$Output/native-fields"
if ($LASTEXITCODE) { throw 'Native field analysis failed.' }
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/runtime-object-correspondence-20261002/run-evidence.ps1 -Output "$Output/final-evidence"
if ($LASTEXITCODE) { throw 'Evidence failed.' }
& $rtkPath proxy $pythonPath -X utf8 .verification/archive/runtime-phase-payloads-20261002/analyze_correspondence.py "$Output/final-evidence"
if ($LASTEXITCODE) { throw 'Exact comparison failed.' }
& $rtkPath proxy $pythonPath -X utf8 .verification/archive/runtime-phase-payloads-20261002/analyze_native_continuity.py "$Output/final-evidence"
if ($LASTEXITCODE) { throw 'Exact native stream comparison failed.' }
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/status-refinements-20261001/run-source-audits.ps1 -Output "$Output/audits-1"
if ($LASTEXITCODE) { throw 'Roots/memberships/sequences/Blockers failed.' }
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/certainty-protocol-20261002/run-rendering.ps1 -Output $Output
if ($LASTEXITCODE) { throw 'Rendering failed.' }
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/runtime-object-correspondence-20261002/run-loaded-objects.ps1 -Output "$Output/loaded-objects-2"
if ($LASTEXITCODE) { throw 'Separate loaded-object capture failed.' }
& $rtkPath proxy $pythonPath -X utf8 .verification/archive/runtime-phase-payloads-20261002/analyze_loaded_objects.py "$Output/loaded-objects-2"
if ($LASTEXITCODE) { throw 'Loaded-object preservation analysis failed.' }
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/runtime-phase-payloads-20261002/run-camera.ps1 -Output "$Output/camera"
if ($LASTEXITCODE) { throw 'Separate camera capture failed.' }
& $rtkPath proxy $pythonPath -X utf8 .verification/archive/runtime-phase-payloads-20261002/analyze_camera.py "$Output/camera"
if ($LASTEXITCODE) { throw 'Camera analysis failed.' }
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/completion-observer-20261002/run-final-build.ps1 -Output $Output
if ($LASTEXITCODE) { throw 'Final main Release/process check failed.' }
& $rtkPath proxy $pythonPath -X utf8 .verification/archive/runtime-phase-payloads-20261002/finalize_checks.py $Output
if ($LASTEXITCODE) { throw 'Report aggregation failed.' }
