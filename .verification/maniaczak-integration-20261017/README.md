# Maniac/Zak S4b integration — 17 October 2026

Scope: ManiacClassic, ManiacEnhanced, ZakClassic and ZakEnhanced. The family records establish bounded boot acceptance; this record covers integration regression checks. No support-matrix cells change.

Validated source head: `b21c3b5d51714415e9360ed5cee604750c149503`, containing master `ea101d8bae6015855879ac8a95e19cfd57f078a5` through merge `05d2c59`. Final checkpoint documentation is committed afterwards.

Build/test working directory:
`D:\Documents\Personal\Code Projects\SCUMMexplorer\.worktrees\maniac-enhanced-s4b`

Commands (PowerShell, through RTK):

```powershell
rtk proxy git merge master
$env:SCUMM_UPDATE_SUPPORT_MATRIX="1"
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter FullyQualifiedName~SupportMatrixTests --verbosity quiet
Remove-Item Env:SCUMM_UPDATE_SUPPORT_MATRIX
rtk proxy dotnet build SCUMMexplorer.slnx --no-restore -p:NuGetAudit=false --verbosity minimal
$env:SCUMM_SOUND_TEST_ROOT="D:\Juegos\Lucas\loom ega mt32 en"
$env:SCUMM_INDY3_EGA_TEST_ROOT="D:\Juegos\Lucas\indy3 ega en"
$env:SCUMM_MT32_ROM_ROOT="D:\Juegos\Lucas\mt32"
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --verbosity quiet
```

Matrix regeneration: **3 passed, 0 failed, 0 skipped**; README/REQUIREMENTS tables unchanged. Final solution build: **0 warnings, 0 errors**. The new worktree initially lacked the app's assets file; `rtk proxy dotnet build SCUMMexplorer.slnx --ignore-failed-sources -p:NuGetAudit=false --verbosity minimal` restored existing dependencies successfully. NuGet audit was disabled because its metadata endpoint was unavailable; no dependencies changed.

App artifact: `Exe\Debug\SCUMMexplorer.exe` (beside `SCUMMexplorer.dll`). Test artifact: `Exe\SCUMMexplorer.Tests\Debug\net10.0\SCUMMexplorer.Tests.dll`. Local command output is in ignored `matrix.txt`, `build.txt` and `full-suite.txt` beside this record.

Full suite: **1125 passed, 0 failed, 5 skipped, 1130 total**, reported duration **5 m 7 s**. The command exited 0 with all three fixture variables above set. Skipped tests are not counted as verified behavior.

No ScummVM executable was run. No Games junction was created and no worktree was removed. Original-game visual, listening and timing comparisons are not claimed. S5, S6, S7 and S9 remain next work; see the [checkpoint](../../docs/checkpoints/20261017-S4b-ManiacZak.md).
