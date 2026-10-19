# Background-first room previews: combined validation, 19 October 2026

Scope: the shared room selector for all supported/readable games, user-confirmed R-5.3b. Baseline master `c0823b04`; integration branch `codex/room-previews-background-first`. Final product source is `4421f0d`, including actor helpers `4efc0ab0` (worker `2c6f1e8`) and completed-cache provider `698028d0` (worker `dd0a934`). Core/tests remain identical to the initial `4348e10` freeze: the final App-only fix was built with BuildProjectReferences=false without rebuilding the backend binaries under the running full suite. No Core/session/opcode behavior is changed. [Run checkpoint](../../docs/checkpoints/20261019-RoomPreviews.md).

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

Matrix regeneration: 3 passed, zero failed/skipped. Initial solution build: zero warnings/errors, 4.38 seconds. Final App-only build: zero warnings/errors, 5.88 seconds. Initial combined App build exposed a missing explicit IO import and an unused phase warning; the import and explicit final-phase cache guard resolve both. Independent lifecycle review corrected stale scan counters and cancellation of deferred scan starts before the initial freeze.

The [window packet](../room-previews-20261019/README.md) passes all 1,002 pure backgrounds across 14 families and eight representative actual-window cold/warm batches covering 15 rooms. Final LOOM early-click dispatch is 18 ms; exact native/cache pixels and actor preservation pass, with filled main-room face details. Its initial genuine heartbeat failure and synchronous 2,679 ms early-click diagnostic are retained. The final maximum heartbeat gap is 2,065 ms, so this is no universal latency guarantee. Final Zak early-click dispatch is 15 ms, maximum heartbeat gap 82 ms; actual drawer interaction, warm native prewarming and canceled-game-switch isolation pass. 36 captures include retained failures; worker inspected seven, parent additionally checked the final LOOM background/initialized pair and Zak drawer image (inspection counts overlap).

Every supplied family has exact plain-background coverage; native/click coverage is limited to the representative room batches. Full normal file-dialog startup, all-family native scans and original-interpreter visual acceptance are unclaimed. Generated TRXs/captures/private caches stay ignored.

## Final regression and assembly

Required full suite: **1,657 passed, zero failed, five expected skips**, 1,662 total, **17m26s**, successful exit 0. The ignored `test-results/full-suite.trx` retains the result with all three external roots configured by the command above. No new xUnit case was needed for App-only behavior: the dedicated cache, native actor and actual-window harnesses provide the new behavioral acceptance.

After the suite, `git merge --no-edit master` reports already up to date (`c0823b04`). The final assembled solution builds with **zero warnings/errors**, **6.57 seconds**. Matrix regeneration is repeated with `SCUMM_UPDATE_SUPPORT_MATRIX=1` and `--no-build`: **3 passed, zero failed/skipped**, 20 ms; published levels remain unchanged. `git diff --quiet 4421f0d -- src tests` succeeds. Subsequent edits are records only.

## Clean promotion

The normal master checkout was clean and unchanged at `c0823b04`; `rtk git merge --ff-only codex/room-previews-background-first` fast-forwards to accepted checkpoint `8b7b7718`. From that normal root, `rtk proxy dotnet build src/SCUMMexplorer.App/SCUMMexplorer.App.csproj --no-restore -p:NuGetAudit=false --verbosity minimal` passes with **zero warnings/errors**, **6.15 seconds**, refreshing `Exe/Debug/SCUMMexplorer.exe`. No user process, profile, game data or ScummVM is touched. The final promotion record is a documentation-only commit, also fast-forwarded; both checkouts finish clean and matching, with source/tests still identical to `4421f0d`.
