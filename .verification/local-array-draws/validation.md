# Validation: local-array draws, `setVarRange`, and the remaining fallback rooms (27 September 2026)

## Question

Rooms 01 (152), 04 (178–189) and 20 (392–396) showed "first stored image for this position; no decoded script draws it", and Room 58 its labelled random pick. Four decoded scripts draw an object named by a local variable: Room 04 local 201 +0x0082, Room 70 local 205, Room 62 local 206 and Room 06 local 200. The question was whether decoded data flow, including loop bounds, resolves any of them, and whether modelling `setVarRange` changes any room.

## Method

`code.tsv` was regenerated with CodeDump (`dotnet Exe/CodeDump/Debug/net10.0/CodeDump.dll Games/loom .verification/fallback-evidence`, not committed). Every `drawObject` whose object operand is not a literal was listed, along with every `setVarRange` and every `startScript`/`chainScript` that starts one of those local scripts.

## Findings per script and room

| Draw | Where the object comes from | Result |
| --- | --- | --- |
| Room 04 local 201 +0x0082 `drawObject local[0x004]` | `local[0x004] = local[0x006+local[0x003]]` (+0x007B), with `local[0x003]` from 0 (+0x0076) and `isLess local[0x003], 0x0004` looping back (+0x008D). In ScummVM, `o5_isLess` jumps back while the index is at most 4, so entries 0–4 are drawn. The table is a literal `setVarRange local[0x006], count=5` chosen by `local[0x000]`, the hotspot the script is started with (171–174, via `startScript 0xC9` in local 200 +0x019C/+0x01CC): +0x000C [179, 180, 180, 179, 178], +0x0024 [182, 183, 183, 182, 181], +0x003C [185, 186, 186, 185, 184], +0x0054 [188, 189, 189, 188, 187]. | **Resolved from data flow.** Each path ends on 178, 181, 184 or 187. The animation is click-driven, so this is not first-visit evidence. It does show that the images the fallback showed are the ones this script leaves on screen, and that "no decoded script draws it" was wrong. |
| Room 70 local 205 +0x000B/+0x001B `drawObject local[0x001]`, then `local[0x000]` | Arguments. Objects 900, 901, 902, 910 and 911 (fallback verbs) pass literal pairs (first, second): 900/905, 901/906, 902/907, 904/909 and 903/908. The script draws the second, then the first. | Not added. Room 70 already shows 900–902 from local 206's batch (+0x003A), and calls from 900–902 end on those same images. An argument rule would also give candidates for the positions of 903 and 904, which the room leaves empty without a gap. Those draws follow a click, so adding them would put click results into the first view. |
| Room 62 local 206 +0x003E `drawObject local[0x004]` | Argument 5, passed as a literal (812–815) by locals 201–204. | Not needed. Room 62 renders from reached code (draw queue 815, 814). |
| Room 06 local 200 +0x0072…+0x0096 `drawObject local[0x003..0x006]` | Arguments 4–7, passed as literals (212–238) by `chainScript 0xC8` from locals 203–210. | Not needed. Room 06 renders from reached code (12 images in the draw queue). |
| Room 01 (152) | No local draw. 152 is only set to 0 (script 044 +0x00C7, script 106 +0x036C, Room 02 object 153 verb 0x38 +0x0015). | **No evidence;** keeps its label. |
| Room 20 (392–396) | No decoded instruction draws or sets them. The only table in the room (local 202 +0x0020/+0x0028) holds actor coordinates for `putActor`. | **No evidence;** keeps its label. |
| Room 58 (790–795) | No decoded instruction names them, and no decoded room load targets Room 58. | **No evidence;** keeps its labelled random pick. |

## Changes

1. **`setVarRange` is modelled** (`LoomBoundedScriptExecutor`). ScummVM's `o5_setVarRange` writes each listed value to the result variable and the ones after it (`_resultVarNumber++`); word values are stored as their 16-bit pattern. Indexed destinations resolve as other writes do. On an estimated path, the written variables are estimates.
2. **Rest frames of literal local-table loops** (`LoomSceneDrawBatches.FindLocalTableRestFrames`). This is a bounded walk over each decoded script. It tracks the locals the script itself sets from literals: `setVarRange`, literal, copied and indexed `move`, and `increment`/`decrement`. It decides comparisons whose operands are known, with ScummVM's direction (`jumpRelative` skips when the condition is false), and follows any other branch both ways. A local set anywhere else (an argument, a global, a result such as `getRandomNr`) is unknown, and a path that draws an unknown local gives nothing. A path that returns to a branch with the same locals is dropped. A path that draws at least one table entry gives one batch: the last image drawn at each position before the script ends. ScummVM's `drawObject` sets the drawn object to state 1 and clears the others at its position, so that frame is what stays. The batch has "other script" priority. Its source names the draw and the table, for example "Room 04 local script 201 +0x0082, last frame from the table set at +0x000C".

Across all 1,370 decoded sources, the walk gives batches only for Room 04 local 201: four batches, one per table.

## Results

- RoomAudit ([before](room-audit-before.txt), [after](room-audit-after.txt)): **0 gaps in all 76 rooms**, and **0 committed PNGs changed** (`compare-png.ps1`).
  - **Room 04:** the same images (178, 181, 184, 187), now labelled "Room 04 local script 201 +0x0082, last frame from the table set at +0x000C / +0x0024 / +0x003C / +0x0054" instead of "first stored image … no decoded script draws it". First-stored-image rooms: **3 → 2 (01, 20).**
  - **Room 41:** the entry no longer stops at `setVarRange` +0x00ED. It runs on to presentation commands and leaves 2 scripts pending. Nothing visible changes: its only images (635, 636) are close-ups.
  - **Room 69:** the entry no longer stops at +0x003B. It now sets 898 to 0 as reached code (before, this was the unavoidable-state rule), starts local 202 (1 pending), and shows 876–878 as before.
  - **Room 54:** the entry starts local 200 (+0x0019), which stopped at `setVarRange` +0x0005 and now waits at its first frame break (+0x0041), so 1 script is pending. Its tables are actor coordinates; nothing visible changes.
- Blockers ([before](blockers-before.txt), the 27 September run with classes; [after](blockers-after.txt)): `setVarRange` stops **5 → 0**. Room 69 local 202 now runs, and it picks through `getRandomNr` +0x00B0 on the visit and on 15 object actions; those picks are labelled estimates. Because scripts run further, later stops show up more often: `print` 310 → 323, `getActorWalkBox` 193 → 206, `waitFor` 8 → 10, `verbOps` 6 → 7.
- Tests: **144/144**, 0 warnings. New tests:
  - `SetVarRangeWritesConsecutiveVariables`: consecutive locals, an indexed read, and a signed word.
  - `ALoopOverALiteralLocalTableRestsOnItsLastFrame`: Room 04's shape; the rest frame is the table's last entry.
  - `DrawsOfArgumentsOrUnknownLocalsGiveNoRestFrame`: Room 70's shape, and a table entry overwritten by a result.
  - Real data: `EntryScriptsRunPastSetVarRange` (Rooms 41, 69), and `NothingVisibleFallbackOpensOnTheStrongestScriptBatch` with Room 04 178.

## Limits

- Room 04's images are the rest frame of a click-driven animation. They are a static candidate for what that script leaves on screen, not the first-visit state. Before any click, the index states are 0.
- Draws of script arguments are not resolved. Rooms 06 and 62 render from reached code; Room 70 would only gain click-driven candidates (see above).
- Rooms 01 and 20 keep the first stored image, and Room 58 its random pick, because no decoded instruction provides evidence for them.
