# Staff level, native score layout and Transcend (1 October 2026)

Supersedes the status interpretation in the earlier Game status/UI refinement records. Their historical runs are retained.

## Corrected native bounds

Old-v3 comparisons evaluate the following operand against the first operand, and branch on false. Script010 +0x0020 tests `var07A < input ID`: an ID equal to var07A is accepted, while the next ID falls through to011 at +0x0027. Available count is therefore **var07A**, not var07A minus one. Room01 local211 +0x02C3 initializes3, meaning C,D,E. The eight status choices C through upper C now write1..8; None is removed. This preserves the requested shifted choices' native effects: old None wrote1, now C writes1; old C wrote2, now D writes2, and so on. Upper C writes8 rather than an unnecessary9. Core still represents a source zero if encountered; no zero control is offered. Staff absence disables the note choices and displays Staff required without destroying their stored level.

The same inclusive comparison applies to022 +0x0131: after increment, it loops while slot <=15. All **sixteen** drafts belong in status. Room01 local211 +0x04BC initializes slot15 as packed211F/response0054, which023 decodes to IDs8,4,5,1: **Transcend C′ F G C**. Its label is guarded by slot15/54/00, and its sequence/flags come from the loaded data. No synthetic draft or note pattern is inserted. Native matching reaches026 for this loaded slot.

## Native artwork and controls

Staff pieces retain018's strip-rounded positions. Score objects148/149, charset0 letter glyphs16..23 and all musical glyph pairs retain018's absolute positions/colors. The staff and eight24-pixel score slices share one coordinate system at2x nearest-neighbor scale; the score starts8 source pixels to the right of the staff origin. Full level has no selection borders. Unavailable notes outline their musical pixels in white and have white borders; native score lines/letters remain visible. Keyboard focus, accessible limits and immediate pending previews remain. No external PNG is used as game art.

The status grid still uses Apply Changes/Close, live stable rows, separate Learned/Can reverse, stale/session replacement and pause/playback guards. Compact single-line Advanced origins retain full tooltips and leave usable grid space with expanded sources at820x520.

## Catalog browsing floor and its limits

Ordinary App room selection opts into `VisitRoom(..., preserveStaffLevel: true)`; reached game transitions, replay setup and source probes keep the default false. The pre-visit level is retained through catalog entry and up to32 idle arrival checks. It only raises values, waits for the existing status-edit guard, and retires on subsequent game input or explicit note editing. A room entry holding the staff gets at least the verified initial3-note level. A known, non-estimated DC5 checkpoint additionally supports7: loaded Room46 local204 +0x0024 writes DC5=1, +0x0029 starts029 with7. These source instructions are verified before enabling status.

There is **no established minimum table for every room**. Unknown per-room levels and guessed room-number ranges are not filled in; source staff loss remains source-owned. The browsing adjustment has its own Advanced provenance, distinct from explicit user edits. It sets neither possession nor Learned/room/puzzle flags and is never evidence of a validated replay prerequisite. User-selected C/D levels remain available as explicit exploration choices; automatic browsing uses the initial3 floor only when a source entry holds the staff.

## Fresh verification

Final Core **275/275**, both absolute game roots, unrelated LoomObjectLookAuditTests excluded; focused status **19/19** includes all eight native boundaries, actual Transcend matching and browsing/explicit-choice preservation. Main Release builds with zero warnings/errors.

Visible normal-clock WPF `StaffCheck`: **47/47**, `Exe/StaffCorrectionSession-20261001/ui-3/staff-check.txt`. Covers native inclusive C write, eight/no-None choices, staff-required controls, actual combo conversion, flags, Apply/Close/stale/busy/pause, minimum expanded-source layout, subsequent native refused/accepted casting and actual catalog navigation preserving8/raising1 to3. `status-transcend.png` shows the loaded row; full/four-note and minimum screenshots inspected. Synthetic WPF events/layout/media, not physical input or independent listening.

Fresh shared checks: global21/21, room20/20, required doorways16/16 (2/3/4/8/10/37), controls19/19, Input18/18, audio10/10, Blockers zero blocked random sites. Fresh all363 literal roots retain status/frame counts/reached rooms/boundaries; four established memberships and both sequence packets are identical to the scope baseline. RoomAudit76 zero gaps, all76 historical pixel hashes unchanged; no PNG refresh. Catalog remains334 starts,84 scenes/250 reactions,168/235 sites,50 outstanding owners,123 unresolved static edges.

Hashed final report summary: [check-summary.json](check-summary.json). Ignored full packets are under `Exe/StaffCorrectionSession-20261001/`. The all-root/shared runs preceded the last catalog-only settling refinement; default false paths are unchanged. Final Core and WPF navigation ran after that refinement.

Intermediate evidence retained: ui-1 had one real minimum expanded-source height failure; compact origins fixed it, ui-2 passed44 checks and ui-3 passed47 with added navigation. `tests-final.txt` used relative optional game roots, causing7 data-location failures; corrected absolute-root `tests-final-2.txt` passes275. No user process stopped, game data changed, historical artifacts refreshed, worktree operation or publication.

## Reproduction

Exact cwd: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.

```powershell
rtk proxy python -X utf8 .verification/archive/staff-correction-20261001/create_harness.py
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/staff-correction-20261001/run-ui.ps1 -Output Exe/StaffCorrectionSession-20261001/ui-fresh
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/ui-refinements-20261001/run-audits.ps1 -Output Exe/StaffCorrectionSession-20261001/audits-fresh
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/ui-refinements-20261001/summarize-audits.ps1 -Output Exe/StaffCorrectionSession-20261001/audits-fresh
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/draft-scopes-20261001/run-regressions.ps1 -Output Exe/StaffCorrectionSession-20261001/regressions-fresh
rtk proxy dotnet build SCUMMexplorer.slnx -c Release
```

Use required RTK `C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe` and bundled Python `C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe`. User executable: `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`.
