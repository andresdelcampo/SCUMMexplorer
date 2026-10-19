# Zak engine fidelity: classify the five capped starts

| | |
|---|---|
| Family / stage | ZakClassic and ZakEnhanced / retained S7 engine fidelity |
| Fixtures and editions | `D:/Juegos/Lucas/zak ega en` (DOS v1 English), `zakE ega en` (DOS v2 English) |
| Date and author | 2026-10-18, Maniac/Zak orchestrator |
| Commits | Shared native-distance fix `6194eea`; final checkpoint lists family regression commits |
| Registry change | Unchanged, Cutscenes Partial: a bounded derived list is not a hand-validated catalog |

## What was done

Reproduced the five S7 capped starts independently in each edition, inspecting native pending-slot instructions without changing variables or owners. The actor-wait cases exposed raw native approach distances passed as pixels: a four-unit approach generated an unaligned destination, and an eight-pixel step never arrived. The v1/v2 opcode owner corrected distance conversion in its own partial, with independent boundary and arrival tests. No walk cap was raised, no actor teleported and no story prerequisite assigned.

## Commands run (through RTK)

Cwd `.worktrees/maniaczak-fidelity`; repeat each run with `zak ega en` and `zakE ega en`:

```powershell
rtk proxy dotnet build .verification/zak-fidelity-20261018/SceneCheck/SceneCheck.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet Exe/SceneCheck/Debug/net10.0/SceneCheck.dll "D:/Juegos/Lucas/zakE ega en"
rtk proxy dotnet Exe/SceneCheck/Debug/net10.0/SceneCheck.dll "D:/Juegos/Lucas/zakE ega en" one 2 0 0
rtk proxy dotnet Exe/SceneCheck/Debug/net10.0/SceneCheck.dll "D:/Juegos/Lucas/zakE ega en" dump 104 105 96 78
rtk proxy dotnet Exe/SceneCheck/Debug/net10.0/SceneCheck.dll "D:/Juegos/Lucas/zakE ega en" runs
```

Final integration, same cwd:

```powershell
rtk proxy git merge master
$env:SCUMM_UPDATE_SUPPORT_MATRIX="1"
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~SupportMatrixTests" --verbosity quiet
Remove-Item Env:SCUMM_UPDATE_SUPPORT_MATRIX
rtk proxy dotnet build SCUMMexplorer.slnx --no-restore -p:NuGetAudit=false --verbosity quiet
$env:SCUMM_SOUND_TEST_ROOT="D:\Juegos\Lucas\loom ega mt32 en"
$env:SCUMM_INDY3_EGA_TEST_ROOT="D:\Juegos\Lucas\indy3 ega en"
$env:SCUMM_MT32_ROM_ROOT="D:\Juegos\Lucas\mt32"
rtk proxy dotnet test SCUMMexplorer.slnx --no-build --no-restore --verbosity quiet --logger "trx;LogFileName=maniaczak-fidelity-full.trx" --results-directory ".verification/zak-fidelity-20261018/test-results"
```

The default diagnostic deliberately samples through the unchanged 6000-frame bound; it is not the production completion check. `runs` uses the actual planner and stops at its production completion condition. Full raw logs and decoded script dumps are retained locally and ignored; compact before summaries and exact production results are committed.

## Results (numbers)

| Trigger room/object/decimal verb | Before, each edition | After, each edition |
|---|---|---|
| `2/0/0` | 6000; Script 126 waiting for actor 12 | Completes at 646 frames |
| `11/0/0` | 6000; Script 104 owner loop | Still bounded at 6000; source waits while object 51 is room-owned |
| `11/249/1` | 6000; Script 105 waiting for actor 9 | Actor approach now returns; subsequent Script 104 owner wait remains, 6000 |
| `14/55/11` | 6000; Script 96 actor approach | Completes at 907 frames |
| `23/410/253` | 6000; Script 78 terminal loop | Still bounded at 6000, deliberately waits for Escape/restart |

Two of the five formerly capped starts now finish in each edition. One further actor wait is removed but the surrounding flight sequence still waits for a native object-owner change. On the failed enhanced Room 2 approach, actor 12 reached x=47,112 at frame 6000 while actor 7 stayed at x=456; the corrected native distance avoids that runaway. These are source exploration previews, not earned story routes or original-interpreter timings.

Per-family regression results and own-edition source offsets are in the enhanced/classic scene records added by the worker; final solution/matrix/full-suite/window results are in the [checkpoint](../../docs/checkpoints/20261018-Fidelity-ManiacZak.md).

Integrated focused run: **65 passed, zero failed/skipped**, with four-family Play and LOOM EGA/CD safeguards. Solution build: zero warnings/errors. Matrix checks: three passed, levels unchanged. Full suite against master `9430cb5`: **1286 passed, zero failed, five skipped**, 1291 total, **7m37s** (18 October 15:14:14–15:21:52 CEST), with all three external fixture settings. Read-only independent review found no actionable engine/rendering/App issue; four actual-window lighting checks pass in the linked lighting record. Local reports are `test-results/fidelity-focused.trx` and `test-results/maniaczak-fidelity-full.trx`.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Official `script_v2.cpp` dispatches actor approach to `script_v5.cpp::walkActorToActor`, whose v1/v2 distance is multiplied by eight before the later-engine width-sentinel branch. `actor.cpp::Actor_v2::actorWalkStep` steps native positions. Read 18 October 2026; [shared opcode evidence](../maniacclassic-fidelity-20261018/README.md) records the source and edge tests. No ScummVM executable was run. The wait classifications additionally use each fixture's own Scripts 104/105/96/78, not cross-edition offsets.

## Differences and uncertainties kept

The flight wait is not resolved by inventing a ticket pickup; the ending is not declared finished by silently pressing Escape. Cutscenes remains Partial, and no full-story prerequisite solver, catalog or seed is added. The earlier S7 evidence stays intact.

Initial diagnostic build used the Snapshot property as a method, corrected before execution. A no-restore test command in a fresh worktree returned without executing tests; restored and reran with explicit execution/counts. Parent palette tests initially indexed the actor snapshot list as if it included actor zero, producing three false failures; actor-number selection fixed the tests. A diagnostic rebuild attempted to replace a DLL still used by our earlier diagnostic; the build failed with file locks. Only the two stale diagnostic sessions were stopped, the original one finished, and the rebuild then passed with zero warnings/errors. Their interrupted output was excluded from the production results, which were regenerated with the successfully rebuilt planner mode. The first interpretation of the owner-loop branch was reversed; actual owner observations and conditional-jump semantics establish that owner 15 keeps waiting, and the final classification corrects it.

## User checks (exact steps and where; none if none)

Optional: in either Zak copy, use the scene picker for Room 2 Entry or Room 14 flashlight Switch off response and let it finish without Escape. Flight previews retain their source-state limitation; a terminal ending deliberately waits. Original-game timing/listening checks remain separate.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md), [support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries), [engine script map](../../docs/engine/SCUMM_SCRIPT_MAP.md), and the final fidelity checkpoint. Other families and game/profile data were untouched.
