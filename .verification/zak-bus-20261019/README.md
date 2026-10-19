# Native paid bus segment — ZakClassic/ZakEnhanced, 19 October 2026

Fixtures: `D:/Juegos/Lucas/zak ega en` and `D:/Juegos/Lucas/zakE ega en`, independently read through their own data sources. This resumes the parked bus gap with new source evidence and corrected native walking. It is not a blind repeat of the old overlapping/stale-sentence clicks. No engine fix specific to the bus, source helper shortcut, owner/flag/currency edit, game/profile/save write, ScummVM execution or junction is used.

## Scope and result

Knife pickup and CashCard recovery retain the explicitly disclosed Room2/Room1 exploration preparation. Room3 is also an explicit street preview. From there, ordinary native floor walking, a cleared sentence and source-supported knife/bus Use wake the driver. CashCard/reader Use charges the native 8-unit fare and the game's own bus script reaches physical Room 8/logical 145 with control. This establishes the paid bus segment, not an earned whole-game journey to every prerequisite.

Both independent console probes pass with the same observed checkpoints: awake driver at frame 1335, charged fare at 1374 (12250→12242), airport control at 1425. The ticket, knife and card identities are preserved. Four new regression cases cover native payment/arrival and an unrelated-tool reader refusal in each edition. They pass in the combined 48-case new-feature run (zero failed/skipped,1m55s). Decoded source anchors live in [the Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md); this evidence records execution and its limits.

## Reproduction

From the integration worktree:

```powershell
rtk proxy dotnet run --project .verification/zak-bus-20261019/Probe/ZakBusProbe.csproj -p:NuGetAudit=false -- "D:\Juegos\Lucas\zak ega en" script 52 54
rtk proxy dotnet run --project .verification/zak-bus-20261019/Probe/ZakBusProbe.csproj -p:NuGetAudit=false -- "D:\Juegos\Lucas\zak ega en" run
rtk proxy dotnet run --project .verification/zak-bus-20261019/Probe/ZakBusProbe.csproj -p:NuGetAudit=false -- "D:\Juegos\Lucas\zakE ega en" run
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter FullyQualifiedName~ScummZakBusTests
```

Local ignored outputs: `room3-classic.txt`, `scripts-classic.txt`, `run-classic-1.txt`, `run-enhanced-1.txt` and `test-results/integrated-focused.trx`. All native uncertainties, including seeded random background contexts, are retained in the console logs; they are not original-runtime visual/timing evidence. The current successful path uses source-derived visible object centers and clears the previous action with native Walk before composing Use. Both old failed preparations remain in their dated flight evidence.

Registry levels unchanged. Parent owns final actual-window integration, required full suite, matrix regeneration and checkpoint.
