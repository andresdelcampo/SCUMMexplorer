# Validation: evidence for the 16 first-stored-image rooms and Room 58 (27 September 2026)

## Question

Before this change, 16 rooms (01, 03, 04, 08, 09, 16, 18, 20, 23, 25, 36, 37, 41, 42, 44, 64) showed "first stored image for this position; no decoded script draws it" when the partial session left nothing visible. `LoomSceneDrawBatches` only knew literal `drawObject` runs. The task was to find what else in decoded code makes these rooms' objects visible, or hidden, and to add any general rule to the scan.

## Method

- [`CodeDump`](CodeDump/Program.cs) decodes the same 1,370 sources as the batch scan (global scripts, room entry, exit and local scripts, object verbs) and writes every instruction to `code.tsv`, plus every room's object directory to [`objects.tsv`](objects.tsv). `code.tsv` is regenerated rather than committed, because it contains every printed game line: `dotnet Exe/CodeDump/Debug/net10.0/CodeDump.dll Games/loom .verification/fallback-evidence`.
- [`object-refs.py`](object-refs.py) → [`object-refs.txt`](object-refs.txt) lists, for each target room, every instruction that names one of its objects in an object position: the object operand of `setState`, `drawObject`, `setClass`, `ifClassOfIs` and `startObject`, the object of `putActorAtObject` and `walkActorToObject`, and literal `startScript` arguments.
- Variable-operand forms were listed separately. There are 8 `drawObject local[..]` calls (Rooms 04, 06, 62 and 70) and 14 `setState local[..]` calls (all in Room 28). No `drawObject` or `setState` uses a global variable.
- `setClass` and ownership do not affect drawing. ScummVM's `drawRoomObjects` and `drawRoomObject` only test the object state and the parent chain. The class and `startScript` hits in `object-refs.txt` therefore explain behaviour, not visibility.

## Findings

### 1. Verb-area images were painted as room artwork (general rule, added)

The user confirmed the finding: these images belong in the UI below the room.

- `verbOps … image, <object>` copies an object's image into a verb (ScummVM `setVerbObject`). The image is shown in the verb area, not in the room.
- Literal operands: Room 01 local script 211 (+0x000C onwards) and script 018 make objects 100–151 the distaff pieces.
- Variable operand: script 008 +0x002D shows `var[0x068]` as verb 0x36 at (248,152), the close-up box. Script 008 runs the selected object's verb 0x39, and 96 of the 101 images at (0,0) 48×40 are named by a `move var[0x068] = <id>` in some object's verb 0x39. For example, Room 03 object 165 verb 0x39 +0x0000 sets `var[0x068] = 0x00A6` (166).
- None of these objects is ever the target of `drawObject`.

`LoomSceneDrawBatches.InterfaceImages` collects both forms, each with its instruction. Literal operands are taken as they are. For a global-variable operand, every literal assigned to that variable in decoded code counts. `local[..]` operands are not followed; the only one, script 031 +0x02B1, is computed inside the script. `RoomObjectSelection.ExcludeInterfaceImages` removes these objects from the nothing-visible fallback, gap filling, gap detection and the empty-background hit test. Reached state still paints them, as SCUMM's room redraw would.

### 2. Reached `setState` was overridden by the stored-image guess (general rule, added)

The session executes entry code, and several target rooms hide their objects there. The fallback then showed those objects anyway. Examples:

- Room 42 entry +0x0000 and +0x0004 set 637 and 638 to 0.
- Room 64 entry +0x0019 sets 826 to 0 when `bit[0xDA5]` is 0.
- Room 08 entry +0x0073/+0x0089/+0x0099/+0x00A9 set 259, 263, 264 and 265 to 0 when bits 0xD7C–0xD7F are 0.

The session now records which objects a reached `setState` set since the room was entered (`LoomSessionSnapshot.StatesSetByCode`). Those positions no longer get the first-stored-image guess. A draw batch can still show them, because such a draw can run after the entry script. Script 106 sets Room 02's 157 to 0 on entry, then draws it in the room's opening frame. This is a **state preview**: the session starts every bit at 0, as at boot, so a state that depends on a bit is the boot-state branch, not a proven first visit.

### 3. Unavoidable entry `setState` (general rule, added)

Room 18's entry stops at an unmodelled `ifClassOfIs` (+0x0070), yet every path through it reaches `setState 0x0175, 0x00` at +0x00DA. `LoomSceneDrawBatches.FindUnavoidableStates` finds literal `setState` calls that no path from the start of an entry script to its end can avoid. A path ends at `stopObjectCode`, `stopScript 0x00`, `chainScript`, the end of the code or an unreadable branch target. The object must be set exactly once in that script, and a variable `setState` in the same entry disqualifies the whole script.

The fallback applies these states after draw batches and before the first-stored-image guess. A nonzero state shows that image with its source; a zero state leaves the position empty. On this release, 29 such states exist, all zero: Rooms 10, 18, 19, 22, 23, 37, 42, 69 and 86. This is static evidence for the state right after the entry script; later scripts may still change it.

### Priority order in the nothing-visible fallback

1. Script draw batches: opening frame 3, entry script 2, other scripts 1. Ties are broken by a seeded pick. (Unchanged.)
2. An unavoidable entry state.
3. The first stored image, only when neither applies and reached code did not set the position's state.

Interface images are never used at any step.

## Per-room result

[Comparison](compare.txt) of [before](room-audit-before.txt) and [after](room-audit-after.txt):

| Room | Before | After | Evidence |
| --- | --- | --- | --- |
| 01 | 24 distaff pieces | 152 (LOOM logo), first stored image | 100–151 are literal verb images (Room 01 local 211, script 018). 152 is only ever set to 0 (script 044 +0x00C7, script 106 +0x036C, Room 02 object 153 verb 0x38 +0x0015) and never drawn. The entry starts local 211 when `bit[0xD60]` is 0. No evidence sets 152 visible; it keeps its label. |
| 03 | 166 | background only | 166 is object 165's close-up (verb 0x39 +0x0000). Room 03 has no other image, and no gap. |
| 04 | 190, 178, 181, 184, 187 | 178, 181, 184, 187, first stored images | 190–198 are close-ups. The only code that draws 178–189 is Room 04 local 201 +0x0082 `drawObject local[0x004]`: a 4-frame animation over a `setVarRange` table chosen by the clicked hotspot 171–174 (startScript 0xC9 from local 200 +0x019C/+0x01CC), gated on the timer `var[0x002]`. It is click-driven, not first-visit evidence. Resolving it would need loop-bound data flow; not added. |
| 08 | 259, 260, 264, 265 | background only | 260–262 and 266 are close-ups. The entry sets 259 and 263–265 to 0 when bits 0xD7C–0xD7F are 0 (reached; state preview). |
| 09, 16, 25, 41, 44 | one close-up each | background only | Every image in these rooms is a verb-0x39 close-up. |
| 18 | 375, 373 | background only | 375–378 are close-ups. 373 is set to 0 on every path: entry +0x00DA (rule 3). |
| 20 | 397, 392–396 | 392–396, first stored images | 397–399 are close-ups. No decoded instruction names 392–396 in an object position; the only hits are walk coordinates. They keep their label. |
| 23 | 428, 426 | background only | 428 is a close-up. The entry sets 426 and 427 to 0 at +0x0038/+0x0034 (reached, and unavoidable). |
| 36 | 600 | background only | The entry starts local 201 at +0x0043, and local 201 +0x0000 sets 600 to 0 (reached). The PNG is unchanged: image 600 matches the background there. |
| 37 | 605, 606, 608 | background only | 606 and 607 are close-ups. Entry +0x0000 sets 608 to 0 (unavoidable). Entry +0x0025 sets 605 to 0 when `bit[0xD95]` is 0 (reached; state preview). |
| 42 | 637, 638 | background only | Entry +0x0000/+0x0004 set both to 0 on every path. |
| 64 | 826 | background only | Entry +0x0019 sets 826 to 0 when `bit[0xDA5]` is 0 (reached; state preview). |
| 58 | random pick 790, 795 | unchanged | No decoded instruction names 790–795 in an object position. The entry (+0x0000–0x000D) only sets a palette colour. No literal `loadRoom`/`loadRoomWithEgo` targets Room 58. The only variable room loads (script 068 +0x037C/+0x070B) return to the caller's room. It keeps its labelled seeded pick. |

Rooms outside the 16 also changed where they showed a close-up or an overridden reached state:

- 02: 158 dropped; 157 kept from script 106's opening frame.
- 38: 619 dropped.
- 43: 647 dropped. 644 is set to 0 by reached code; 645 still comes from script 91 +0x009C.
- 46: 691 dropped.
- 51: 762 dropped.
- 65: 832 dropped.
- 69: 898 is set to 0 on every path by entry +0x0048.

**First-stored-image rooms: 24 → 4** (01, 04, 20 and 30; 30 keeps 504 beside its entry-script batch). The before count of 24 includes rooms where close-ups filled otherwise empty positions next to a batch.

## Checks

- Build: 0 warnings. Tests: **111/111**. New tests:
  - `LoomSceneEvidenceTests`, 4 unit tests: interface images through a literal and through a global variable; unavoidable states with branches that rejoin; a stop before the state; repeated and variable states.
  - `RoomObjectSelectionTests`, 3 tests: interface images are excluded; a reached state blocks only the stored-image guess; an entry state replaces that guess.
  - Real data: `VerbAreaImagesAreFoundAndKeptOffTheRoom` (Rooms 01 and 03), and `EntryStatesSetOnEveryPathKeepTheirObjectsHidden` for Rooms 18 and 42.
- `run-verify.cmd`: BUILD_EXIT=0, TEST_EXIT=0, AUDIT_EXIT=0. The RoomAudit still reports **0 gaps in all 76 rooms** (8 before filling). The audit report now also lists interface images, states set by reached code, unavoidable entry states and the entry script's stop reason.
- WPF ([captures and text](window-check/window-check.txt)): the real `MainWindow` shows Room 01 → 152 only, with the first-stored label in the note; Room 02 → 157 (script 106 opening frame); Rooms 03, 18 and 42 → background with no estimate; Room 58 → the labelled random pick.
- The committed `room-audit/room-NN.png` files were refreshed for the 22 rooms whose render changed.

## Limits

- Rooms 01 (152), 04 (178–189), 20 (392–396) and 30 (504) keep the labelled first stored image. Room 58 keeps its seeded random pick.
- Reached states that depend on bits use the boot value 0 (state preview).
- Unavoidable entry states hold right after the entry script only.
- `local[..]` verb-image operands are not followed, and neither are indexed local-array draws (Room 04 local 201, Room 70 local 205, Room 62 local 206, Room 06 local 200).
- Icon-shaped images 307–309 (Room 10) and 444–445 (Room 24) are not named by any decoded verb 0x39, so they are not classed as interface images.
