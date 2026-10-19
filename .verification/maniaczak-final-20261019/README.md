# Maniac/Zak labels and fidelity integration — 19 October 2026

Baseline clean master `ce405e7b`; integration branch `codex/maniaczak-status-polish`. Production source is frozen at `aa1cee45`; later commits contain records only. Scope and remaining limits are in the [checkpoint](../../docs/checkpoints/20261019-LabelsFidelity-ManiacZak.md). No support levels change. Both generated support matrices are regenerated and checked rather than manually edited.

## Required commands

Run from `.worktrees/maniaczak-status-polish`:

```powershell
rtk git merge master
rtk proxy powershell -NoProfile -Command '$env:SCUMM_UPDATE_SUPPORT_MATRIX="1"; dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~SupportMatrixTests --verbosity minimal'
rtk proxy dotnet build SCUMMexplorer.slnx --no-restore -p:NuGetAudit=false --verbosity minimal
rtk proxy powershell -NoProfile -Command '$env:SCUMM_SOUND_TEST_ROOT="D:\Juegos\Lucas\loom ega mt32 en"; $env:SCUMM_INDY3_EGA_TEST_ROOT="D:\Juegos\Lucas\indy3 ega en"; $env:SCUMM_MT32_ROM_ROOT="D:\Juegos\Lucas\mt32"; dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore -p:NuGetAudit=false --logger "trx;LogFileName=full-suite.trx" --results-directory .verification/maniaczak-final-20261019/test-results --verbosity minimal'
```

## Results

Master merge: already up to date at `ce405e7b`. Matrix checks: **3 passed, zero failed/skipped**. Full solution build: **zero warnings/errors**, 4.88 seconds.

The final combined feature bundle passes **64 cases, zero failed/skipped**, 1m29s: 37 inventory-context cases, 13 native-walking cases, eight dynamic ego-room cases, two own-copy flight-arrival cases and four own-copy bus cases. Local TRX: `../zak-bus-20261019/test-results/integrated-final-focused.trx`. Broader worker bundles overlap; their counts are not additive.

Six serial actual-window processes pass: all four inventory/status routes and both Zak flight-arrival routes. There are 83 captures, ten inspected endpoints. Reproduction and limits: [inventory windows](../zak-labels-ui-20261019/README.md), [arrival windows](../zak-arrival-20261019/README.md). Native source-reference walking coverage is recorded separately in [walking evidence](../maniaczak-walking-20261019/README.md), and disclosed paid bus preparation in [bus evidence](../zak-bus-20261019/README.md). None establishes an original-runtime comparison or complete earned playthrough.

Required full suite: **1,538 passed, zero failed, five expected skips**, total 1543, 12m32s. The full TRX confirms all 64 new cases passed (37/13/8/2/4 for the classes above); none was skipped. The five skips are existing manual LOOM diagnostic audits. Master remained `ce405e7b` throughout validation; no production or test source changed after the solution build. The authoritative TRX stays local under `test-results/full-suite.trx`, with an explicit ignore entry; game assets, saves, ROMs, profiles/cache and binary output are not committed.

Master fast-forwarded cleanly to `00d92ec0`. From the normal root checkout, `rtk proxy dotnet build src/SCUMMexplorer.App/SCUMMexplorer.App.csproj --no-restore -p:NuGetAudit=false --verbosity minimal` refreshed `Exe/Debug/SCUMMexplorer.exe` with **zero warnings/errors**, 7.18 seconds. Only final promotion records follow; parent checks matching clean root/integration heads after their last fast-forward. No user process was stopped, and no worktree was removed.
