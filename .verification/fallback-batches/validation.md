# Validation: script-backed nothing-visible fallback (27 September 2026)

## Change

`RoomObjectSelection.ShowUnresolvedArtwork(batches, seed)` now uses the room's `LoomSceneDrawBatches` before it falls back to stored images. It runs only when a partial session leaves nothing visible (unchanged caller check in `MainWindow.CloseArtworkGaps`, the RoomAudit harness and `LoomFirstVisitRenderingTests`).

1. An empty position (no active peer, no active image overlapping it) is covered by a batch when a batch draws one of its images. Each round shows the empty position whose best batch has the highest priority: opening frame 3, entry script 2, other scripts 1. Ties among that position's best batches are ordered by source and picked with `Random(seed)`, seed = room number. The chosen batch is applied as a whole (as in `FillGaps`), except that it does not replace images already visible elsewhere.
2. Positions that no batch covers keep the old behaviour: their first stored image in directory order, if nothing overlaps them. The old pick was deterministic, not random, so it is now labelled as such: "first stored image for this position; no decoded script draws it". It was kept unchanged so that the before/after comparison below isolates the effect of the batches.
3. Cut-outs opened by these images are then closed with the `FillGaps` logic (batch, then seeded random choice).

Every image shown is returned as a `RoomArtworkEstimate` with its source. The room note in the app lists these sources. All of this is a **state preview**: a static batch shows a possible frame. It does not show which branch runs.

## Results

- Build: 0 warnings, 0 errors. Tests: **100/100** (94 + 3 unit tests in `RoomObjectSelectionTests` + 3 real-data cases in `LoomFirstVisitRenderingTests.NothingVisibleFallbackOpensOnTheStrongestScriptBatch` for Rooms 02, 38 and 30). `run-verify.cmd`: BUILD_EXIT=0, TEST_EXIT=0, AUDIT_EXIT=0.
- RoomAudit: still 0 gaps in every room (8 before filling, 0 after).
- Fallback comparison ([table](fallback-compare.tsv), [before](fallback-before.tsv), [after](fallback-after.tsv), [script sources](fallback-after-script-sources.txt)): of the 27 rooms, **11 now take at least one image from a script batch** (02, 19, 30, 38, 43, 46, 51, 65, 69, 70, 86). The other 16 have no batch for any empty position and are unchanged. The image set changed in 5 rooms:

| Room | Before | After | Source of the new pick |
| --- | --- | --- | --- |
| 19 | 379 | 385 | seeded pick among five priority-1 batches for that position (scripts 65 and 94, Room 19 local script 201) |
| 30 | 504, 519 | 520, 504 | Room 30 entry script +0x002E (priority 2) beats script 72 and local script 200 (priority 1) |
| 38 | 612, 619 | 614, 619 | opening frame of Room 38 in script 86 +0x0549 |
| 51 | 744–749, 762 | 744–746, 753–755, 762 | two local script 200 batches (seeded) |
| 86 | 918 | 925, 927 | seeded pick among script 111's priority-1 batches: 925 (open hood), whose face cut-out is then closed by local script 202's 927 |

  In Rooms 02, 43, 46, 65, 69 and 70 the set is the same as before, but those images are now labelled with the script that draws them. Examples: Room 02's 157 is the opening frame of script 106, and Room 65's 830 is the opening frame of script 72.

- WPF check ([captures and text](window-check/window-check.txt)): the real `MainWindow` shows Room 02 → 155, 157, 158; Room 30 → 504, 520; Room 38 → 614, 619; Room 86 → 925, 927; and Room 01 → the same 24 first-stored images as before. These match the audit, and the room note names each source.

## Limits

- Rooms 19, 51 and 86 still rest on seeded ties between equally weak (priority-1) batches. Room 86 now opens on the open hood rather than 918. The pick is labelled, but it is still an estimate.
- 16 rooms (01, 03, 04, 08, 09, 16, 18, 20, 23, 25, 36, 37, 41, 42, 44, 64) have no decoded batch for their empty positions and still show the labelled first stored image.
- `Room86CyclingToTheOpenHoodFillsItsFaceCutout` now starts on 925, so the cycling loop does not run. The test still checks that the face cut-out is filled and that no gap remains.
