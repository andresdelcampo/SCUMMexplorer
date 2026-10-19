# Background-first room previews: combined validation, 19 October 2026

Scope: the shared room selector for all supported/readable games, user-confirmed R-5.3b. Baseline master `c0823b04`; integration branch `codex/room-previews-background-first`. Product source frozen at `4348e10`, including actor helpers `4efc0ab0` (worker `2c6f1e8`) and completed-cache provider `698028d0` (worker `dd0a934`). No Core/session/opcode behavior is changed. [Run checkpoint](../../docs/checkpoints/20261019-RoomPreviews.md).

## Commands

From `.worktrees/maniaczak-status-polish`:

```powershell
rtk git merge --no-edit master
rtk proxy powershell -NoProfile -Command '$env:SCUMM_UPDATE_SUPPORT_MATRIX="1"; dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~SupportMatrixTests --verbosity minimal'
rtk proxy dotnet build SCUMMexplorer.slnx --no-restore -p:NuGetAudit=false --verbosity minimal
rtk proxy powershell -NoProfile -Command '$env:SCUMM_SOUND_TEST_ROOT="D:\Juegos\Lucas\loom ega mt32 en"; $env:SCUMM_INDY3_EGA_TEST_ROOT="D:\Juegos\Lucas\indy3 ega en"; $env:SCUMM_MT32_ROM_ROOT="D:\Juegos\Lucas\mt32"; dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore -p:NuGetAudit=false --logger "trx;LogFileName=full-suite.trx" --results-directory .verification/room-previews-final-20261019/test-results --verbosity minimal'
```

## Focused evidence

The [cache harness](../room-preview-cache-20261019/README.md) passes 32 private-cache checks plus a separate-process persistence check. It covers full-set PNG equality/freezing, folder/language isolation, stale/corrupt/incomplete archives, source/build metadata, cancellation before/during staging, failed destination replacement and concurrent readers. It creates no window and touches no game or normal-profile file.

The [LOOM actor harness](../room-card-actors-20261019/README.md) passes four detached native crops (EGA/CD rooms 2/4): 1,084 actor-layer pixels, 1,026 changed pixels, 183,236 unchanged scenery pixels. Native palettes, camera crops, depth masks/classes and inputs are preserved; fallback and cancellation checks pass. No original interpreter is run.

Matrix regeneration: 3 passed, zero failed/skipped. Solution build: zero warnings/errors, 4.38 seconds. Initial combined App build exposed a missing explicit IO import and an unused phase warning; the import and explicit final-phase cache guard resolve both. Independent lifecycle review corrected stale scan counters and cancellation of deferred scan starts before this freeze. Actual-window/full-suite results and clean promotion follow after completion; generated TRXs/captures/private caches stay ignored.
