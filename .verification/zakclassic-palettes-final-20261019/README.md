# ZakClassic palette contexts — final integration, 19 October 2026

Baseline `5634143c`; integration `codex/maniaczak-status-polish`. Palette-provider source `97bd82ae` (worker `6b0fe2c`), App `166f6b2` plus actual-window label correction `96dfb06`. Concurrent master changes `77588bdf`, `8f454be3` and `be8d6e87` are preserved through merges. Scope, decisions and remaining limits: [checkpoint](../../docs/checkpoints/20261019-Palettes-ManiacZak.md).

## Reproduction

From `.worktrees/maniaczak-status-polish`:

```powershell
rtk git merge --no-edit master
rtk proxy powershell -NoProfile -Command '$env:SCUMM_UPDATE_SUPPORT_MATRIX="1"; dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~SupportMatrixTests --verbosity minimal'
rtk proxy dotnet build SCUMMexplorer.slnx --no-restore -p:NuGetAudit=false --verbosity minimal
rtk proxy powershell -NoProfile -Command '$env:SCUMM_SOUND_TEST_ROOT="D:\Juegos\Lucas\loom ega mt32 en"; $env:SCUMM_INDY3_EGA_TEST_ROOT="D:\Juegos\Lucas\indy3 ega en"; $env:SCUMM_MT32_ROM_ROOT="D:\Juegos\Lucas\mt32"; dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore -p:NuGetAudit=false --logger "trx;LogFileName=full-suite.trx" --results-directory .verification/zakclassic-palettes-final-20261019/test-results --verbosity minimal'
```

## Results

Final matrix regeneration after the latest concurrent source merge: **3 passed, zero failed/skipped**. Current full solution build: **zero warnings/errors**, 7.00 seconds. The preceding matrix-triggered test rebuild reports the pre-existing imported Monkey2 `xUnit2029` assertion-style warning; its family files are left to the other orchestrator.

Dedicated worker packets overlap and are not additive: transformations 18 passed (eight new + ten existing) plus one conformance check; Mars five passed; context/native/legacy packet 29 passed, reviewed pointer/LOOM EGA/CD/conformance packet 49 passed, and frozen fast context packet 32 passed. There are **46 new cases** to confirm in the full suite: eight transformation, five Mars and 33 provider/context cases.

The provider's cold native derivation is deliberately bounded and can take tens of seconds; the App loads it in the background with progress/cancellation and keeps default artwork visible. Source pairs, native opening operations and estimated room previews retain distinct evidence. The unchanged 24/11 global decision counts are not promoted from contextual samples. [Provider commands/cost/limits](../zakclassic-palette-core-20261019/README.md), [transformation proof](../zakclassic-transform-palettes-20261019/README.md), [Mars proof and stopped route](../zakclassic-mars-palettes-20261019/README.md).

Three final serial actual-window routes pass with 17 accepted captures; six inspected by the parent and eight by the worker, with overlap. They compare actual pose dimensions/pixels with Core, check default restoration, plain selected/open labels, Advanced-only provenance, cancellation/stale-result handling and compact layout, and preserve native state/clock. The first failed packet is retained separately and diagnosed, not treated as acceptance. [Full commands/results/cost and native pixel distinction](../zakclassic-palette-ui-20261019/README.md), integrated `bf74687c` from worker `19a75c16`.

Production source frozen at merge `84dee6e`; master was `be8d6e87` at the replacement full-suite start. The preceding run on `fb226f83` was deliberately stopped after master changed: its verified integration testhost alone was terminated, leaving the separate root testhost untouched. Its partial report (1,539 passed, zero failed, five skipped; 19m12s) is retained locally as `test-results/full-suite-superseded-master8f.trx`; the aborted run is not acceptance. A fresh required suite validates the latest merged source. Final counters and clean master/normal-executable promotion follow after completion. Raw TRXs/logs/captures and private profiles/cache stay local and ignored; no game assets, ROMs, saves or original-runtime comparison are shipped or claimed.
