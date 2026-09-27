# Validation: cutscene, freezeScripts, waits and isSoundRunning (27 September 2026, partial)

Status: **partial, parked.** It builds with 0 warnings, 144/144 tests pass, the audit shows 0 gaps, no PNG changed, and Room03ActionCheck gives 46 PASS. DoorwayClickCheck gives 74/75; the one difference is explained below and still open. No new unit tests were written for these opcodes yet.

## Changes (ScummVM semantics)

- **`cutscene`** (`beginCutscene`): the session pushes the first argument and runs the script in `VAR_CUTSCENE_START_SCRIPT` (`var[0x023]`) with the arguments. The calling script is exempt from a freeze while that script runs.
- **`endCutscene`**: sets `VAR_OVERRIDE` (`var[0x005]`) to 0, pops the data and runs `VAR_CUTSCENE_END_SCRIPT` (`var[0x024]`) with it.
- **`freezeScripts`**: a nonzero flag freezes every other running script (the executing callers and the pending scripts), except freeze-resistant ones when the flag is below 0x80, and the cutscene owner. 0 undoes one freeze. Frozen pending scripts keep their place and skip frames. `startScript` opcode bit 0x20 marks a script freeze-resistant (`o5_startScript`). Each script slot now carries a `ScriptFrame` (freeze count, freeze-resistant flag).
- **`delay`, `delayVariable`, and `waitFor` actor/message/camera**: the script continues on the next frame. This is labelled "timing not simulated"; no state is set. `waitFor sentence` is still not modelled.
- **`isSoundRunning`**: sound 0 gives 0. A sound this route started reads as playing; that value is an estimate, because sound length is not simulated. A sound the route did not start reads as silent. Both cases are labelled.
- App: a doorway click now continues up to **32** frames (was 8), since waits now take a frame each.

## Findings

- **The boot never sets the cutscene hooks.** Script 001 loads Room 69 (the intro) at +0x0160, waits for `bit[0xD6B]`, and only then sets `var[0x023] = 2`, `var[0x024] = 3`, the room hooks `var[0x01D]`/`var[0x01E]`, ego `var[0x001] = 1`, and `bit[0xD6E]`. The session stops script 001 at that load ("room transition awaits"), so the cutscene variables are 0 and `cutscene` runs no script. That is correct for the session's state, but scripts 002/003 (which set and clear `bit[0xD6E]` and freeze and unfreeze scripts) will only run once the boot is completed. In ScummVM, a global script keeps running after `loadRoom`.
- **Blockers** ([before](blockers-before.txt), [after](blockers-after.txt)):
  - `cutscene` stops: 333 → 0. `isSoundRunning`: 75 → 0 (195 estimate labels and 11 silent labels). `waitFor`: 10 → 0. `delay`: 5 → 0. `delayVariable`: 1 → 0. `freezeScripts`: 1 → 0.
  - Scripts now run further, so later stops show more often: `getActorWalkBox` 206 → 198, `getActorX` 26 → 70, `isScriptRunning` 13 → 60, `beginOverride` 0 → 39, `verbOps` 7 → 26.
  - Reached loads after a frame advance: 5 → 10.
  - Scripts 037/039/040 start a sound and loop while it plays, so they stay pending with an estimate label (before, they stopped).
- **Audit** ([before](room-audit-before.txt), [after](room-audit-after.txt)): the same renders. Rooms 26, 29, 35, 41, 43, 47 and 68 keep one more pending script.
- **DoorwayClickCheck** ([report](doorway-clicks.txt)): 74/75. Room 10 object 295 starts script 052, a cutscene that loads Room 11 (+0x0045) and later Room 13 (`loadRoomWithEgo` +0x017D). The click now reaches the first load and enters Room 11, whereas the box's label lists only 13. With 8 frames the click fell short and left script 052 pending; it then fired Room 11 during the Room 15 click. Both of these are open.

## Open

Items 1, 3 and 4 were closed on 27 September by the [cutscene continuation work](../cutscene-continuation/validation.md): global scripts carry on after a room load (Room 10 object 295 ends in Room 13; DoorwayClickCheck 75/75), `LoomCutsceneTests` covers these opcodes, and WindowCheck captures were taken.

1. Decide how a cutscene room load (Room 11 here) relates to the exit's destination (13). Options: keep continuing a script that loads a room and carries on (ScummVM keeps a global script running), or list both rooms on the box.
2. Complete the boot (script 001 after the Room 69 load), so the hooks and the cutscene scripts are known.
3. Add unit tests: `cutscene`/`endCutscene` with known hooks, freeze and unfreeze, one-frame waits, `isSoundRunning`.
4. Record the WPF captures (none were taken for this task).
