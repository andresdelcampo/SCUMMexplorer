# M3b: frame timing (29 September 2026)

## What the user now sees

- A cutscene's `delay` holds the scene for its real time. In Room 02, after Bobbin reaches the end of the path (clicks on the floor to box 4, then box 10, then on the exit), the exit's cutscene pans the camera left over the valley and the town. The view then **holds for about 1.1 s** (60 jiffies: 11 frames at 10 frames a second) before Room 03 opens. Before M3b the view went on to Room 03 on the next frame, 0.1 s later. ([room02-exit-delay-app.png](room02-exit-delay-app.png) is the frame where the hold starts.)
- The same applies to every reached `delay` and `delayVariable` (117 and 12 sites). The opening cutscene (script 044) pauses 1.1 s at each of its five reached delays. Script 052 pauses in Room 10 before it loads Room 11. Script 045's scene changes on the path wait 20 jiffies. Script 042's swan speck waits its random delays (for example 7, 26 or 53 jiffies) between moves; before, each wait lasted one frame.
- A double click is still told apart by the click's stated timer value (below), so clicks behave as before.

## What changed (ScummVM `scummLoop`, `decreaseScriptDelay`, `o5_delay`, `o5_delayVariable`; `vars.cpp` v3 numbering)

- Executor: `delay` (a 24-bit literal) and `delayVariable` (a variable's value) set the slot's delay in jiffies and break (`ssPaused`). The result carries the delay (`LoomBoundedScriptResult.Delay`). A `delayVariable` whose variable holds an estimate is labelled: "when it runs again is an estimate". A delay whose variable is unknown still continues on the next frame, labelled as before. No reached delay hit that case in the Blockers run.
- Session: each slot keeps its delay (`ScriptFrame.Delay`). At the start of every `AdvanceFrame`, before the talk delay and the scripts, `UpdateTimersAndDelays(delta)` does what ScummVM's `scummLoop(delta)` does for v3, where delta = `VAR_TIMER_NEXT` (at least 1):
  - `VAR_TIMER` (var 46, `var[0x02E]`) = delta; `VAR_TIMER_TOTAL` (var 47, `var[0x02F]`) and `VAR_TMR_1..3` (vars 11–13, `var[0x00B..0x00D]`) += delta. v3 has no `VAR_TMR_4`.
  - `decreaseScriptDelay(min(delta, 15))`: every paused slot's delay goes down, frozen or not. A slot whose delay falls **below 0** runs again in this frame. So `delay 60` at 6 jiffies a frame runs again on the 11th frame (60 − 66 < 0), 66 jiffies later.
  - Paused slots are skipped by the frame's script loop, as frozen ones are.
- The engine starts with every variable at 0 (ScummVM), so the session sets the timers to 0 when it starts script 001; they count from there. Script 020 (the draft playback) resets and reads `VAR_TMR_1` (≤ 0xB4), script 006 `VAR_TMR_2` (≤ 0x28), and Room 69 local 201 reads `VAR_TMR_1` in an expression.
- A click still states `VAR_TMR_2` (60 for a single click, 10 between the clicks of a double click) as assumed input, because the player's time before the click is not known. The label now says the frame loop counts on from there.
- New session trace lines: "… waits N jiffies (delay) before +0x…" and "…'s delay ends; it runs again at +0x…".

## Frame counts (same seed; the probe runs the same route at HEAD and after)

| Route | Before (M3a) | After (M3b) |
| --- | --- | --- |
| Boot, frames after script 001's first room load | 725 | 775 (still ends in Room 02 at (828, 60), facing 270) |
| Room 10 object 295 → script 052 → Room 13, cutscene frames | 267 | 277 |
| Room 08 object 255 → the Elders' trial, cutscene frames | 2,784 | 2,824 (ends in Room 09) |

- The boot passes five of script 044's `delay 0x3C` (+0x023B, +0x0244, +0x0258, +0x02B3, +0x02CF): 5 × 10 more frames. +0x00BE is jumped over (+0x0046/+0x005C jump to +0x00C2), and +0x0190 lies behind the `var[0x030]`/`local[9]` test.
- Script 052 has one `delay 0x3C` (+0x0041, before `loadRoom 0x0B`): 10 more frames.
- `LoomBootTests` still passes (the boot ends well within `BootFrameLimit`, 5,000).

## Evidence

- Unit tests: 227/227 (223 before; `SCUMM_SOUND_TEST_ROOT=Games\loom`, `SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA`).
  - `LoomFrameTimingTests.ADelayOf60JiffiesRunsAgainOnTheEleventhFrame`: a booted session, a script with `delay 0x3C` then a variable write; the write happens on the 11th frame.
  - `LoomFrameTimingTests.ADelayVariableReadsItsVariable`: `delayVariable` of 13 jiffies runs again on the 3rd frame (7, 1, −5).
  - `LoomFrameTimingTests.TheEngineTimersCountTheFramesJiffies`: 5 frames add 30 to `VAR_TMR_3` and `VAR_TIMER_TOTAL`; `VAR_TIMER` is 6.
  - `LoomCutsceneTests.ADelayPausesTheScriptWithItsJiffies` replaces the test of the old "continues on the next frame" label.
- ActorDrawCheck, extended ([actor-draw-check.txt](actor-draw-check.txt)): **0 failures**. The second click now goes on the path's floor at (627, 124) instead of the exit, so the app's doorway fallback does not step in. The camera still follows 8 pixels a frame (14 moves, 792 → 680), and the ego stops at (609, 110) at box 10's change of scene. A third click on the exit runs object 153's verb 0x38 through script 007. Its cutscene pans the camera to 440, and the delay starts at `VAR_TIMER_TOTAL` 5427 and ends at 5493 (**66 jiffies**). On the app's playback clock that was **1,252 ms** (the check accepts 0.9–3 s; the WPF timer runs a little slow). Then `loadRoomWithEgo` enters Room 03 and the view shows it.
- Blockers ([blockers-after.txt](blockers-after.txt), about 15 minutes): 1,381 lines, before 1,419 ([../walk-steps/blockers-after.txt](../walk-steps/blockers-after.txt)). Every "a delay of … continues on the next frame" label is gone: 9 lines in the old file, which folded the handover's 83 occurrences into per-site lines. `expression` (3) is still the only real stop. One new label, 3 times in the summary: a room's local script whose `delayVariable` reads a local holding a seeded pick is labelled, because when it runs again is an estimate. (The summary folds room numbers. The `delayVariable` sites are script 042 and eleven room locals, for example Room 43 local 205 +0x0039.) The `picked` rows under Room 43's object verbs (29) are gone. The likely reason (a reading) is that Room 43 local 205 now really waits on its delay, so it does not reach its `getRandomNr` in a verb's few frames.
- DoorwayClickCheck on Rooms 02, 03, 04, 08 and 10 ([doorway-clicks.txt](doorway-clicks.txt)): 13/13, 10 through the game's click, the same as after M3a.
- RoomAudit: 0 gaps in all 76 rooms; `compare-png.ps1`: 0 changed renders.
- Not rerun: MessageAppCheck, ClickInputCheck, CameraPanCheck and VerbAreaCheck (slow WPF harnesses). Only the timing of script continuations changed; the message timing (`LoomMessageTable`) is untouched, and ActorDrawCheck covers the click, walk and camera path.

## Not modelled

- `_scrollDeltaAdjust` and the other platform delta adjustments in ScummVM's main loop (not used by DOS EGA LOOM at 6 jiffies a frame).
- `VAR_MUSIC_TIMER` (var 14, `var[0x00E]`): script 044 +0x00B2 waits on it, but that code is jumped over on the route the session takes.
