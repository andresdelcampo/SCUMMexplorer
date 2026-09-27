# Validation: global scripts across room loads, beginOverride, isScriptRunning (27 September 2026)

Status: **done.** It builds with 0 warnings and 154/154 tests pass (was 144). Audit: 0 gaps in 76 rooms. DoorwayClickCheck: **75/75** (was 74/75). Room03ActionCheck `162 4 167 38`: 46 PASS. Renders changed in Rooms 08 and 58 (PNGs refreshed).

## Decision (task 1a)

ScummVM `startScene` ends room, object and local scripts, but a **global** script returns from `loadRoom` and carries on (`o5_loadRoom`, `killScriptsAndResources`). The session now follows that rule instead of listing both rooms on the box:

- When a global script reaches a room load inside an action or a frame, the executor pauses right after the load (`LoomBoundedScriptExecutor.PauseAfterEvent`). The session enters the room and resumes the script there in the same frame (`DispatchRequestedLoads`). A room, object or local script that loads a room still ends at the load, as before.
- A catalog visit and the boot do not dispatch loads, so the boot's script 001 still stops at the intro (task 2).
- A later load by the same script is recorded as the same action's outcome; the action lists the rooms it passed through ("Script 52 passed through Room 11 (loadRoom at +0x0045) before this load.").
- `InCutscene` is true while a cutscene begun on this route is open (its script still runs and has not reached `endCutscene`). `ContinueUntilRoomChanges` keeps going while a cutscene is open: frames outside a cutscene count against the 32-frame budget, frames inside it against a 1000-frame cap (labelled if reached). The player has no control during a cutscene, so the click's outcome is where it ends.

## Needed on the way

Script 052 could not reach Room 13 without two more pieces (pulled forward from task 3):

- **`beginOverride`** (`ScummEngine::beginOverride`): records the jump that follows and steps over it. The jump runs only when the player skips the cutscene; the preview never skips. The **disassembler** now also decodes the code after that jump. Before, it followed only the jump, so every cutscene body behind an override was missing (script 052 +0x00CB to +0x0156, for example).
- **`isScriptRunning`** (`ScummEngine::isScriptRunning`): true when a global or local script with that number is executing, waiting for a later frame, or frozen. When the script's last run ended at a step the preview does not model, the answer is labelled as an estimate (it might still run in the game).

Script 052's route ([probe](probe-room10-295.txt)): `cutscene`, 15 animation frames, a delay, `loadRoom 0x0B` at +0x0045 (frame 19), the override body (40 frames, local scripts 210 and 213 in Room 11), `isScriptRunning 0xD2` until local 210 ends, `endCutscene`, then `loadRoomWithEgo` Room 13 at +0x017D (frame 65).

## Effects of decoding the override bodies

- **Game Texts:** 719 → 1225 entries; spoken lines 580 → 1086. Object names (133) and stored lines (6) are unchanged. The added lines are cutscene dialogue that lay behind override jumps.
- **Room 58** has script evidence at last: script 86 +0x0185 loads it and draws 792 (+0x018A). The labelled random pick (790, 795) is replaced by that opening frame.
- **Room 20** images 392–396 are drawn by script 99 +0x02BB; they were the "first stored image" guess. The render is unchanged. First-stored-image rooms: 2 → 1 (Room 01).
- **Room 56** has a second opening frame (script 108 +0x0188), with the same face and mouth (779, 782) as script 68. The render is unchanged.
- **Room 08** now shows 259 from script 46 +0x0DD0 (nothing was visible before). Its state was set to 0 by reached entry code, but a batch can still show it, as the fallback rules state (script 106 draws Room 02's 157 after its entry sets it to 0).
- **Room 42:** script 90 flashes 637 and 638 (`drawObject`, `breakHere`, `setState 0`). The batch builder now drops a draw that the same script undoes in straight-line code before any branch, jump, room load, stop or new draw of that object, so Room 42 stays empty, as its entry sets it.

Audit: [before](room-audit-before.txt), [after](room-audit-after.txt).

## Blockers ([before](blockers-before.txt), [after](blockers-after.txt))

- `beginOverride` stops: 39 → 0. `isScriptRunning` stops: 60 → 0; 23 labelled estimates remain.
- "Room transition awaits bounded session dispatch": 157 → 141 (global scripts now carry on).
- Scripts run further, so later stops show more: `getActorWalkBox` 198 → 215, `getActorX` 70 → 72, `verbOps` 26 → 27.

## Tests (task 1b)

`LoomCutsceneTests` (9 new):

- one-frame waits (`delay`, `waitFor message`, `waitFor actor`) continue on the next frame with a timing label;
- `isSoundRunning`: a started sound reads as playing (estimate), others as silent;
- `isScriptRunning`: needs a session; an unmodelled end is an estimate;
- `beginOverride`: the disassembler decodes past the skip jump, and the executor steps over it;
- freeze: with scripts 39 (freeze-resistant start, opcode 0x2A) and 40 pending, `freezeScripts 0x7F` freezes only 40, `0x00` unfreezes, and `0x80` freezes both;
- Room 10 object 295 carries script 052 through Room 11 into Room 13;
- with the hooks the boot would set (`var[0x023] = 2`, `var[0x024] = 3`, through an internal test seam), the cutscene runs script 002 (freezes the verb and local 200, sets `bit[0xD6E]`) and `endCutscene` runs script 003 (unfreezes, clears `bit[0xD6E]`). See the [probe with hooks](probe-room10-295-with-hooks.txt).

`LoomFirstVisitRenderingTests` was updated for Rooms 56 and 58 and gained a test for undone draws (Room 42) and Room 20's script 99 batch.

## WPF captures (task 1c)

[WindowCheck](window-check/window-check.txt) of Rooms [03](window-check/room-03.png), [10](window-check/room-10.png), [15](window-check/room-15.png) and [43](window-check/room-43.png). The doorway click itself is covered by [DoorwayClickCheck](doorway-clicks.txt) (Room 10 object 295 → Room 13) and the Room 03 [action check](room03-action/). No physical input.

## Open

- The boot still stops at script 001 +0x0160 (task 2), so the cutscene hooks are 0 outside tests.
- When a global script started from a room script loads a room at once (in the same run), the parent finishes its run before the session enters the room; ScummVM would end the parent at the load. No route found so far depends on this (script 052 loads on a later frame).
