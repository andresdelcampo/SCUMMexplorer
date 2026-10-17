# Maniac Mansion classic S5: first-visit object audit

| | |
|---|---|
| Family / stage | ManiacClassic / S5 |
| Fixture and edition | `D:/Juegos/Lucas/mmansion ega en` (DOS, English, SCUMM v1) |
| Date and author | 2026-10-17, ManiacClassic S5 agent |
| Commits | `bf1baee` (classic costume bridge), shared `a653f84`, `6d54f0f`, `0e0fb78`, `0ab757a`, `7672066`, `9eae4fa`, then this record/test commit |
| Registry change | Unchanged: Objects remains Partial; Full additionally requires S6 click responses. |

## What was done

The family audit uses the shared `ScummRoomFirstVisit` path, records every room's settling outcome, selected images, actors, remaining colour-0 gaps and unresolved execution, and exports local BMP captures. Its static comparison ranks room entry/local scripts by instruction count and lists the twelve heaviest rooms' state operations beside their reached selection. It does not force story variables or fill pixels.

The reviewed `ManiacFirstVisit` provider fingerprints the menu before delivering the S4b party inputs. The classic live actor path now accepts costume format 0x57, retains native limb animation, and uses the current actor's identity/palette for colours. `ScummClassicCostumeAnimationTests` checks all available poses against the accepted static inspector's pixels and transparency (more than 100 poses), separately pinning actor-dependent colours. Cropped-pose agreement does not prove scene placement: the scene tests additionally require substantial actor pixels and on-scene bounds in Room 44.

## Commands run (through RTK)

Working directory: `.worktrees/maniac-classic-s5`.

```powershell
rtk proxy dotnet run --project .verification/maniacclassic-objects-20261017/RoomAudit/ManiacClassicRoomAudit.csproj -p:NuGetAudit=false -- "D:\Juegos\Lucas\mmansion ega en" .verification/maniacclassic-objects-20261017/baseline 44 1 2
rtk proxy dotnet run --project .verification/maniacclassic-objects-20261017/RoomAudit/ManiacClassicRoomAudit.csproj -p:NuGetAudit=false -- "D:\Juegos\Lucas\mmansion ega en" .verification/maniacclassic-objects-20261017/final
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~LoomCostumeAnimationTests|FullyQualifiedName~LoomCdRunningSetupTests|FullyQualifiedName~ScummManiacClassicFirstVisitTests|FullyQualifiedName~ScummClassicCostumeAnimationTests" --verbosity quiet
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~ScummManiacClassicFirstVisitTests|DisplayName~ManiacClassic" --verbosity quiet
```

The testhost required process permissions beyond the sandbox; two stalled sandbox test launches were stopped before the successful targeted run. No user profile was opened or changed. `NuGetAudit=false` avoids unavailable remote audit metadata; no package dependency changed.

## Results (numbers)

Baseline before the shared S5 adaptation: Rooms 01, 02 and 44 all failed with `InvalidDataException: Invalid old-v3 object-state index.` The existing first-visit builder selected the v3 index/room decoder for these v1 resources. These three errors are retained here; the zero counted gaps in that failed audit are not successful room coverage.

Intermediate audit after the index/room adaptation: 52 rooms opened, 51 settled and Room 30 hit the frame limit. All actor costumes were unreadable. After adding format 0x57, metadata listed actors but the Room 44 capture exposed incorrect placement and an incomplete initial pose; metadata counts were not accepted as visible actor evidence. The static cropped-pose test could not reveal a translation error. The native v1 draw origin comes from `base-costume.cpp`, not the old-bundle v2/v3 origin.

Focused regression: **13 passed, 0 failed, 0 skipped**, including the three room-state cases, native coin-slot background diagnosis, classic pose/palette tests, LOOM EGA costume animation and LOOM CD running setup. Full integration build/suite and real-window evidence are recorded by the orchestrator at the S5 Maniac/Zak checkpoint.

After the final arrival/queue commit, the family filter above passed **6 tests, 0 failed, 0 skipped**, covering first-visit states, the native coin-slot findings, the classic boot and family conformance. No test or build warning remained.

Final frozen-source audit: **52/52 rooms opened**, **51 settled**, **1 frame-limit preview (Room 30)**, **0 failed runs**, **0 filtered unsupported/unresolved execution items**. The detector reports **51 rooms without findings** and **six retained findings in Room 18**, described below. The source-correct index-only baseline has the same six findings; the earlier generic mask-15 baseline (three rooms/eight gaps) is superseded. Actor layers exist in 37 rooms. Room 44's three complete sprites write 978, 1014 and 1032 pixels (actors 1, 3 and 5), with body bounds inside the room; its final capture was inspected. No image fill occurred. [Full room evidence](final/room-audit.md).

## Comparison with ScummVM (source files and date; runtime comparison if any)

Read 2026-10-17: ScummVM [`object.cpp`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/object.cpp), `findObject` and `drawRoomObjects`, and [`script_v2.cpp`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp). In v1/v2, drawing and parent matching use the intrinsic-state mask; native object search also rejects the untouchable state bit. These engine rules are handled by the shared first-visit implementation, not family exceptions.

Classic costume source: [`costume.cpp`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/costume.cpp) (`ClassicCostumeLoader`, `drawLimb`, `byleRLEDecode_C64`, native Y adjustment) and [`base-costume.cpp`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/base-costume.cpp) (`drawCostume`: v1 starts the limb origin at 0,0). Actor colours use the existing source-derived tables, without changing the S2 acceptance or claiming a new costume comparison with a running game.

The supplied ScummVM executable was not launched, as required by the run instructions. Source-level cross-checks and explorer captures do not constitute the stage playbook's original-game picture comparison.

The [twelve-room comparison](final/source-comparison.md) is ranked by entry/local instruction count and includes Room 30 and Room 51, the heaviest entries. It lists source offsets, state branches and child-script starts alongside reached active images. A branch's named object need not be displayed when the branch was not taken. Global/index state remains authoritative; this static comparison is not an assertion that every named object should appear simultaneously.

## Differences and uncertainties kept

An arbitrary room arrival from boot is a labelled state preview; it has not played the earliest normal route. Seeded party selection, arrival and ambient phase are assumptions, not independently observed original gameplay. A gap detector covers stored-image rectangles and cannot prove an entire picture visually complete. S6 has not been started.

The six detector findings in Room 18 are native black coin-slot candidates: every 8-by-8 rectangle already has 58/64 black pixels in the background, and the object composite equals that background. They have no usable stored fill. The detector's six findings are retained, with the source check in `ScummManiacClassicFirstVisitTests`; no pixels or story state were altered. Dark-room/flashlight colour fidelity remains unverified; the audit reports the source lights flag and does not claim original darkness rendering.

## User checks (exact steps and where; none if none)

None required to run this audit. Original-game first-visit picture comparison remains unperformed and is not claimed.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Maniac script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [family tracker](../../docs/maniac/SUPPORT_STATUS.md), and [engine object-state rules](../../docs/engine/SCUMM_OBJECT_STATES.md).
