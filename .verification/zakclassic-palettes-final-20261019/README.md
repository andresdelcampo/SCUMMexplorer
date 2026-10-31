# ZakClassic palette contexts — final integration, 19 October 2026

Baseline `5634143c`; integration `codex/maniaczak-status-polish`. Palette-provider source `97bd82ae` (worker `6b0fe2c`), App `166f6b2` plus actual-window label correction `96dfb06`. Concurrent master changes through `ea89e1ef` are preserved through merges (including `77588bdf`, `8f454be3`, `be8d6e87`, `9beb9f61`). Scope, decisions and remaining limits: [checkpoint](../../docs/archive/checkpoints/20261019-Palettes-ManiacZak.md).

## Reproduction

From `.worktrees/maniaczak-status-polish`:

```powershell
rtk git merge --no-edit master
rtk proxy powershell -NoProfile -Command '$env:SCUMM_UPDATE_SUPPORT_MATRIX="1"; dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~SupportMatrixTests --verbosity minimal'
rtk proxy dotnet build SCUMMexplorer.slnx --no-restore -p:NuGetAudit=false --verbosity minimal
rtk proxy powershell -NoProfile -Command '$env:SCUMM_SOUND_TEST_ROOT="D:\Juegos\Lucas\loom ega mt32 en"; $env:SCUMM_INDY3_EGA_TEST_ROOT="D:\Juegos\Lucas\indy3 ega en"; $env:SCUMM_MT32_ROM_ROOT="D:\Juegos\Lucas\mt32"; dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore -p:NuGetAudit=false --logger "trx;LogFileName=full-suite.trx" --results-directory .verification/zakclassic-palettes-final-20261019/test-results --verbosity minimal'
```

## Results

Final required suite on frozen source/tests **`d600d53b`**: **1,657 passed, zero failed, five expected skips (1,662 total)**, exit zero, **16m51s**. All **46 new palette cases** are confirmed Passed in `test-results/full-suite.trx`; zero errors/timeouts. Master remained `ea89e1ef` through validation. Later changes are documentation only, including the user-requested tracker consolidation and changelog archive. A further matrix regeneration after that cleanup passes all three checks. Four actual-window processes pass; no WPF process remains. Clean master promotion and normal executable refresh are recorded in the checkpoint.

Final matrix regeneration after the latest concurrent source merge: **3 passed, zero failed/skipped**. Current full solution build: **zero warnings/errors**, 4.23 seconds. The other orchestrator's checkpoint `ea89e1ef` corrects its Monkey2 `xUnit2029` assertion-style warning; the repeated matrix-triggered test rebuild is now warning-free too. This orchestrator did not edit that family's test.

Dedicated worker packets overlap and are not additive: transformations 18 passed (eight new + ten existing) plus one conformance check; Mars five passed; context/native/legacy packet 29 passed, reviewed pointer/LOOM EGA/CD/conformance packet 49 passed, and frozen fast context packet 32 passed. All **46 new cases** pass in the final suite: eight transformation, five Mars and 33 provider/context cases.

The provider's cold native derivation is deliberately bounded and can take tens of seconds; the App loads it in the background with progress/cancellation and keeps default artwork visible. Source pairs, native opening operations and estimated room previews retain distinct evidence. The unchanged 24/11 global decision counts are not promoted from contextual samples. [Provider commands/cost/limits](../zakclassic-palette-core-20261019/README.md), [transformation proof](../zakclassic-transform-palettes-20261019/README.md), [Mars proof and stopped route](../zakclassic-mars-palettes-20261019/README.md).

Three final serial actual-window routes pass with 17 accepted captures; six inspected by the parent and eight by the worker, with overlap. They compare actual pose dimensions/pixels with Core, check default restoration, plain selected/open labels, Advanced-only provenance, cancellation/stale-result handling and compact layout, and preserve native state/clock. The first failed packet is retained separately and diagnosed, not treated as acceptance. [Full commands/results/cost and native pixel distinction](../zakclassic-palette-ui-20261019/README.md), integrated `bf74687c` from worker `19a75c16`.

The complete suite on `84dee6e` passed: **1,653 passed, zero failed, five expected skips**, 20m42s, with **all 46 new cases confirmed Passed** in the TRX. Its report is retained as `test-results/full-suite-masterbe8.trx`. During that run master advanced to `9beb9f61` with language selection; merge `1432a725` preserves it, matrices/build were repeated and another suite was started (later superseded, below). The shared window-load changes also pass a fourth serial actual-window process: ZakClassic, 15 new captures, four worker/two parent inspections with overlap, native pixels/labels/cancellation/layout/state checks unchanged. Window evidence-only commit `8639e9ff` from worker `a915a655` records the build (zero warnings/errors, 12.01 seconds), cold/cancelled load (88.80 seconds), 1,357 heartbeats and maximum 253 ms gap. Overall four successful windows and 32 accepted captures; no WPF process remains.

The still earlier run on `fb226f83` was deliberately stopped after master changed: its verified integration testhost alone was terminated, leaving the separate root testhost untouched. Its partial report (1,539 passed, zero failed, five skipped; 19m12s) is retained locally as `test-results/full-suite-superseded-master8f.trx`; the aborted run is not acceptance. Raw TRXs/logs/captures and private profiles/cache stay local and ignored; no game assets, ROMs, saves or original-runtime comparison are shipped or claimed.

Master advanced again to clean checkpoint `ea89e1ef` (records plus the assertion-style correction; no production change). The in-progress `1432a725` suite was deliberately stopped by terminating only the exact-path integration testhost PID 8128. Its partial report, **1,255 passed, zero failed, two skips**, 8m44s, is retained as `full-suite-superseded-master9b.trx`, explicitly aborted and not acceptance. Merge `d600d53b` imports the latest checkpoint; matrices/build were repeated before the final passing suite above. The passing post-language window packet tests identical production files; it remains valid without repeating its cold load.

Clean master remained `ea89e1ef` before `git merge --ff-only codex/maniaczak-status-polish` promoted `3cc640e4`. From the root checkout, `rtk proxy dotnet build src/SCUMMexplorer.App/SCUMMexplorer.App.csproj --no-restore -p:NuGetAudit=false --verbosity minimal` refreshed `Exe/Debug/SCUMMexplorer.exe`: **zero warnings/errors, 8.05 seconds**. No application was stopped. Only the final promotion record follows; source/tests remain identical to `d600d53b`. Final parent checks verify matching clean heads after that record's fast-forward.
