# Estimated locals passed as script arguments (28 September 2026)

## The stop

Blockers counted 13 visits or actions with "Started script # has arguments depending on unknown variables". The message now names the site. With `Blockers.dll <game> <out> "<text>"` (new optional third argument: list every uncertainty containing the text, with its room and action) all 13 are the same shape: an object's verb 0x36 plays a draft on it and starts script 030 (0x1E) with one note.

| Room, action | Site | Operands |
| --- | --- | --- |
| Room 04 objects 171–174, verb 0x36 | Room 04 local 201 +0x006E | `startScript 0x1E, [local[0x005]]` |
| Room 06 object 205, verb 0x36 | Room 06 local 201 +0x0028 | `startScript 0x1E, [local[0x001]]` |
| Room 06 object 214, verb 0x36 | Room 06 local 215 +0x007E | `startScript 0x1E, [local[0x001]]` |
| Room 13 object 325, verb 0x36 | its verb code +0x001B | `startScript 0x1E, [local[0x000]]` |
| Room 20 objects 386, 387, verb 0x36 | Room 20 local 202 +0x0045 | `startScript 0x1E, [local[0x001]]` |
| Room 24 object 432, verb 0x36 | Room 24 local 204 +0x0034 | `startScript 0x1E, [local[0x000]]` |
| Room 31 object 523, verb 0x36 | Room 31 local 200 +0x0077 | `startScript 0x1E, [local[0x001]]` |
| Room 62 objects 810, 811, verb 0x36 | Room 62 local 200 +0x0053 | `startScript 0x1E, [local[0x001]]` |

## Decoded

- Each caller first starts script 023 (0x17) with a draft number (Room 13 object 325: `startScript 0x17, [0x0004]`), then reads the note as `local[n] = var[0x052+local[i]]` and starts script 030 with it.
- Script 023 (load draft N): reads the draft's pattern word `var[0x032+2N]` and its second word `var[0x033+2N]` (into `var[0x065]`). The pattern's four 3-bit fields (bits 0–2, 3–5, 6–8, 9–11) plus one are the notes 1–8, stored in `var[0x052..0x055]`; bits 0x2000, 0x1000 and 0x4000 go to `bit[0xD69]`, `bit[0xD6A]` and `bit[0xD6B]`.
- The draft table `var[0x032..0x051]` (16 drafts, two words each) is set by Room 01 local 211 (+0x02C8–0x04BC) on the boot route. Drafts 0 and 15 are literal; each of the other 14 is one of three patterns chosen by `getRandomNr local[0x001], 0x02`. The session takes these as seeded picks, so the table, and every note read from it, is an estimate.
- Script 030 (play a note): `bit[0xD6C]` = 1; with the distaff in hand (`var[0x064]` = 2) it starts script 027 with the note; then chains to script 031 with it. Script 031 sets `bit[0xD67]` while the note plays; unless `bit[0xD70]` is set it starts script 040 (0x28) with the note, which plays sound N for note N (N + 0x20 when `var[0x030]` = 4); it then picks the note's animation frames. The callers wait while `bit[0xD67]` is set before the next note.

## Why it stopped

The executor passed a local argument to the session by value only when the local was known and not an estimate. These locals are known but estimated (they come from the seeded draft table), so they stayed as `local[…]` text and the session could not read them.

## Model

- The executor now passes every known local by value (ScummVM passes the values, `getWordVararg`) and records which argument positions held an estimate (`LoomScriptEvent.EstimatedArgumentIndexes`). The session marks those locals of the child as estimates, exactly as it already did for arguments read from estimated globals. The same applies to `chainScript`, `startObject` and `cutscene` arguments.
- A sound started from an estimated operand is now a possible outcome (event certainty `Conditional`). The session says "sound N was started from a seeded pick, so it is a possible outcome", and the reached-sound resolver (music row) does not count it.
- The note played, and anything that follows from it, is an estimate: the drafts are random per game in the release, and this session's table comes from seed 0. Nothing about the player's own playing is simulated here; the verb is still an exploration action.

## Evidence

VisitProbe `13/325/36 sounds` (Room 13, object 325, verb 0x36): frame 1 `Script 30 chains to script 31`; frame 2 script 040 starts sound 01; frame 11 the next note chains again and frame 12 starts sound 04 (draft 4 from the seeded table: notes 1, 4, 1). Before the change the verb stopped at +0x001B with the unknown-arguments label.

Test: `LoomRandomPickTests.DraftFromTheSeededTablePlaysAsAnEstimate`.

## Verification

- Tests: 198/198.
- Blockers (`blockers-after.txt` against `../abort-cutscene/blockers-after.txt`): 5912 → 5905. "Started script # has arguments depending on unknown variables" 13 → 0. New: "sound # was started from a seeded pick, so it is a possible outcome" 13 (these 13 actions only; no other sound changed certainty). "Sound # was started" 348 → 360 and "isSoundRunning … silent" 231 → 244 (the notes now run). Camera, `findObject` and print labels drop by 1–9 because the 8-frame budget now ends inside the draft's wait on `bit[0xD67]`.
- Room audit and WPF checks: see below.
- Room audit: 0 gaps in 76 rooms; `compare-png.ps1`: 0 changed renders.
- WPF checks (`wpf/`, log in `wpf-checks.log`): ClickInputCheck 11/11, DoorwayClickCheck 75/75, Room03ActionCheck 46, PointerInputCheck 13/13, ReverseHoverCheck 22/22, HighlightCheck 19/19, MusicRowCheck 5/5, KeyInputCheck 10/10, PresentationCheck 40/40. CameraPanCheck 9 PASS, 2 FAIL ("Room 02 pan off … view starts at source x 0 (expected 640)", same for Room 04) and VerbAreaCheck 14 PASS, 1 FAIL ("off again: the strip is gone and the room has its size back"). Both fail the same way with the previous Core (`wpf-head-core/`, the three changed Core files set aside), so they come from the uncommitted 28 September UI refresh in `src/SCUMMexplorer.App`, whose own validation did not run these two checks.
