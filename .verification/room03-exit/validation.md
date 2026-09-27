# Validation: Room 03 exit to Room 04 and back (27 September 2026)

## Summary

On the bounded session's boot state, clicking Room 03's upper-left hotspot (object 162, verb 0x36) now **reaches** `loadRoomWithEgo 0xA7, 0x04` in Room 03 local script 200 after one frame, and the view enters Room 04. In Room 04, object 167 verb 0x38 reaches `loadRoomWithEgo 0xA2, 0x03` after one frame and returns to Room 03. Room 03's other local exits reach their loads the same way: object 161 → Room 05, and objects 163 and 164 → Room 10.

Evidence labels:

- Clicking a hotspot activates its verb directly, so each route is an **exploration action**.
- The session starts from its boot state (script 1 run, all other bits 0) with Room 03 opened from the catalog, so each route runs on a **state preview**.
- The room load itself is **reached** by bounded execution, not inferred from static bytecode.
- Walking, input dispatch and class-based hit selection are still not simulated.

## Trace

All of this comes from decoded LOOM EGA code ([CodeDump](../fallback-evidence/CodeDump/Program.cs)). SCUMM's `jumpRelative` skips to its target when the condition is **false**. `o5_equalZero` runs `jumpRelative(a == 0)`, so `equalZero x → back` loops while `x` is nonzero.

### Room 03 entry (03.LFL)

- +0x0024 `equalZero bit[0xD62] → 0x0053`, and +0x00F8/+0x00FD `equalZero bit[0xD6E] / bit[0xD62] → 0x012F / 0x011A`: script 42 is started (+0x0114 `startScript 0x2A, [0x0005]`) only when both bits 0xD6E and 0xD62 are 0.
- Script 001 +0x015B sets bit 0xD62 = 1 at boot; script 005 clears it (+0x0347) and sets it again (+0x0382). So on a boot-state visit, **the entry does not start script 42**, and the session confirms it.

### Indexed script 42, for when it does run

- +0x0021 `equalZero var[0x003] → 0x0020` waits while `var[0x003]` (VAR_HAVE_MSG) is nonzero, that is, while a message is on screen.
- Then, with no `breakHere` in between: +0x0026 `getRandomNr` picks a position, +0x0045/+0x0049 place actor 2, and +0x0058 sets bit 0xD7A = 1 whatever the random value.
- The bit stays 1 for 5, 7 or 11 frames (+0x0084 countdown). Then +0x008D removes actor 2 and +0x0091 clears the bit.

So bit 0xD7A marks "actor 2 is on screen".

### Room 03 local script 200 (started by object 162 verb 0x36 +0x0000)

- +0x0000 `breakHere`; +0x0001 `equalZero bit[0xD7A] → 0x0000` waits while the bit is **set**.
- +0x0006 `stopScript 0x2A`, then +0x0008 `loadRoomWithEgo 0xA7, 0x04, walkTo=(580,130)`.
- Local scripts 201 (Room 05) and 202 (Room 10) are identical apart from the load.

Other writers of bit 0xD7A are script 015 +0x0005 (clears it), Room 09 local 207, Room 11 local 201, and Room 54's exit and local 200. None runs on the boot-state route to Room 03, so the bit is 0.

### Room 04 object 167 verb 0x38 (04.LFL code 0x66E7)

The same wait (+0x0001), then +0x0006 `loadRoomWithEgo 0xA2, 0x03, walkTo=(130,64)`. It places ego at object 162, the hotspot that leads to Room 04.

### Correction to the 26 September notes

The plan said local script 200 "loops while bit 0xD7A is zero", and that script 42 "waits on `var[0x003]`" before a `0xD7A` gate. Both waits run the other way. No path has to *set* 0xD7A: the exit waits only while actor 2 is on screen. The synthetic test that encoded the old reading (`StartedRoomLocalScriptRemainsPendingWhenItsRecordedGateIsZero`) was replaced; see Checks.

## What blocked the route, and the general changes

The session stopped at local 200 +0x0006: "operation 'stopScript' is not modeled". Nothing else was missing.

1. **`stopScript`**, after ScummVM `o5_stopScript` and `ScummEngine::stopScript`:
   - `stopScript 0` ends the current script, like `stopObjectCode`.
   - A literal ID raises a new `ScriptStopped` event. The session ends pending runs of that indexed or room-local script, which now carry their script number. If that script is not running, nothing happens, as in ScummVM.
   - Without a session, the executor reports that the stop needs one.
   - Each stop is listed in `LoomExplorationSession.SessionTrace`.
2. **Frame continuation.** When a script continued by `AdvanceFrame` reaches a literal room load, the session enters that room, as `ActivateObjectVerb` already did for a load reached at once. `LastAction` gets the reached load and destination. `ContinueUntilRoomChanges(maxFrames)` advances frames while scripts are pending and the room is unchanged.
3. **WPF:**
   - Clicking a code-only exit hotspot runs its verb and then continues pending scripts for up to 8 frames. Only when no load is reached does it fall back to the labelled static destination (browsing), as before.
   - The Continue button now follows a reached load into the destination.
   - The action note reads "Entered Room NN through the reached room load. This is an exploration action on a state preview; exploration state remains incomplete."

## Checks

- Build: 0 warnings. Tests: **118/118**. New and changed tests:
  - `LoomBoundedScriptExecutorTests`: `stopScript 0` stops the script; another script's stop needs a session and raises `ScriptStopped`.
  - `LoomRoomLocalScriptTests`, on local 200's exact bytecode: with the bit clear, the session reaches Room 04 after one frame and records "script 42 was not running". With the bit set by the script, it stays pending in Room 03.
  - `LoomRoom03ExitTests` (real data): the boot-state entry does not start script 42; object 162 → Room 04 through local 200 +0x0008; object 167 → Room 03.
- `run-verify.cmd`: build, tests and audit all exit 0. RoomAudit is unchanged (0 gaps), so no room render moved.
- Session probe ([program](Probe/Program.cs), [output](probe.txt)) for objects 162, 161, 163 and 164, and the reverse route.
- WPF ([harness](../Room03ActionCheck/Program.cs), [report](window-check/window-check.txt)), run as `Room03ActionCheck.dll Games\loom <out> 162 4 167 38` on the real `MainWindow`, all PASS:
  - Coordinate click at (24,24) enters Room 04 through the reached load ([capture](window-check/coordinate-action.png)).
  - The hotspot button and interaction choice, with a synthetic Enter, leave local 200 pending ([capture](window-check/keyboard-action-pending.png)). One Continue press then enters Room 04 ([capture](window-check/keyboard-action.png)).
  - In Room 04, object 167 verb 0x38 plus Continue returns to Room 03 ([capture](window-check/reverse-room-03.png)).
  - Inspection mode at the same point no longer paints close-up 166 onto the room (the 27 September verb-area rule).
  - These are in-process calls and synthetic routed events. Physical mouse and keyboard input was not tested.

## Limits

- The route runs from the session's boot state. On a real first visit, earlier rooms may have changed bits 0xD62, 0xD6E and 0xD7A. If script 42 is running, the exit waits until actor 2 leaves (at most 11 frames, then a random 1–61 tick delay). The session cannot model that because `getRandomNr` (+0x0026) is not modelled.
- Room 03 object 160's direct Room 02 load, and the other doorways, are unchanged.
- The reported town/cemetery labels for the Room 03 exits are still not identified from the source.
