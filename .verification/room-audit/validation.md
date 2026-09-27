# Validation: call-order renderer, gap filling and script doorways (27 September 2026)

Applies `.verification/wip-rendering-and-doorways.patch` (items 3–6 of `docs/WIP_2026-09-27.md`) plus the follow-up below. Run from the repository root with `.verification\run-verify.cmd`.

- Build: `dotnet build SCUMMexplorer.slnx`: 0 warnings, 0 errors.
- Tests: **94/94 passed**, with `SCUMM_SOUND_TEST_ROOT=Games\loom` and `SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA`. That is 86 before the patch, plus 3 from the patch and 5 real-data tests in `LoomFirstVisitRenderingTests`.
- Room audit ([harness](../RoomAudit/README.md), [report](room-audit.txt), [summary](room-audit.tsv)): 76 rooms. Gaps before filling: 8 (Rooms 13, 33, 56, 58). **Gaps after filling: 0 in every room.**
- WPF check ([captures and text](../window-check-2026-09-27/window-check.txt)): the real `MainWindow` shows Room 03 → 166, Room 17 → 363–366 (no black patches), Room 56 → 779, 782, Room 58 → 790, 795 and Room 86 → 918, matching the audit. The check selects rooms through the tree; it uses no physical input.

## Follow-up change: opening frame = first load in a script

LOOM script 68 (in `56.LFL`) runs straight through two `loadRoom 0x38` calls. At `+0x00FE`, it loads Room 56 and draws 779 and 782 (face, then mouth into its colour-0 cut-out). At `+0x0551`, it returns to Room 56 and draws 781. Both used to count as opening frames (priority 3), so the seeded tie-break picked 781. `LoomSceneDrawBatches` now gives opening-frame priority only to the first literal load of each room in a script; later loads keep the script's ordinary priority. Room 56 now opens on 779, 782.

## Findings on the WIP expectations

| Check | Result |
| --- | --- |
| Room 56 opening frame 779 then 782 | Holds after the follow-up change. Covered by `Room56OpeningFrameIsTheFaceThenItsMouth`. |
| Room 58 opening frame 792 | **No evidence.** No decoded script loads Room 58 or references objects 790–795 by literal. The view uses a labelled seeded random pick (790, 795) and has no gap. Global scripts 0, 51, 55 and 116 fail to decode ("declares N bytes … past the end of"), so one of them might be the missing source. Covered by `Room58WithoutScriptEvidenceUsesALabelledRandomPickAndLeavesNoGap`. |
| Room 17 filled by one consistent local-script batch | The session already reaches local script 202 (`[364,363,365,366]`), so no estimate is needed. Covered by `Room17HolesAreFilledByOneConsistentLocalScriptBatch`. |
| Room 03 doorways 02, 04, 05, 10 | Holds. Covered by `Room03DoorwaysIncludeExitsReachedThroughLocalScripts`. |
| Room 86 cycling to 925 fills 926–928 | Holds: exactly one of 926–928 (a seeded pick among local script 202's three single-image batches) is drawn on top, and no gap remains. Covered by `Room86CyclingToTheOpenHoodFillsItsFaceCutout`. |

## Remaining limits

- No room is faithful on a fresh first visit, so every view is a state preview.
- 27 rooms show nothing from the session and fall back to `ShowUnresolvedArtwork`. These have no gaps, but the fallback does not use script batches.
- The gap test only examines stored-image rectangles. Black areas that no image covers are part of the background and are not counted.
