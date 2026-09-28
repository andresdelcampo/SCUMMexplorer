# M3a: walking in steps, 28 September

## What the user now sees

- After the boot, Bobbin stands under the tree in Room 02 in **left profile**, as in ScummVM. The M2c difference is gone. Room 02 local 202 turns him towards the fairy on every frame while local 201 flies it off to the left, and those walks now take their frames.
- A click makes Bobbin **walk**: he moves a few pixels a frame along the walk-box path and plays his walk animation (frame 9 without the distaff, with the limbs stepping), then stops in his stand frame. He no longer jumps to the walk's end.
- The **camera follows** him 8 pixels a frame once he passes the trigger strips, rounded to 8-pixel strips (ScummVM `moveCamera`). `panCameraTo` pans the same way.
- Turns take frames. A turn to the opposite side goes through a side step (for example, down to up goes by way of right).
- Room 02's changes of scene now show up. Script 045 watches the ego's walk box. When Bobbin walks down the path into box 4 (and later box 10), its cutscene puts him at the next scene's point in the smaller costume and walks him on. Its `putActor` ends the click's walk, so script 007 finds him too far from the exit and does nothing. The player clicks again. This follows from the decoded code (script 045 +0x001D…+0x003E and +0x0075…+0x0092, script 006 +0x0165…+0x016E, script 007 +0x0000). **Not yet compared with ScummVM** (see below).

## What changed

- `LoomActorTable` (`src/SCUMMexplorer.Core/Formats/LoomActors.cs`) ports ScummVM `Actor_v3` (`actor.cpp`, fetched 28 Sep):
  - `startWalkActor` for v3: the destination is kept in `_walkdata.destbox` or adjusted into a box. The walk sets `MF_NEW_LEG`, and an actor already there only turns to the direction. Outside the current room, the actor is put at the destination and takes its direction.
  - `walkActor` for v3: box by box through `getNextBox`, with `findPathTowardsOld` and `getGates` (the gate between two boxes, `boxes.cpp`) and `point3`. With no route, the walk ends where the actor is. At the end the stand frame starts, then the actor turns to `destdir`.
  - `calcMovementFactor` for v3: the x step is the x speed, or y speed + 1 on steep legs. The leg's facing uses `_facingXYratio` 3. A leg within one step is skipped unless it is the last one.
  - `actorWalkStep` for v3: a leg's first step turns the actor and starts the walk frame. Later steps snap to the leg's end within one step and change box when inside the next one. They move by Bresenham counters (`xfrac`/`yfrac` against the step threshold).
  - `turnToDirection` (`_moving = MF_TURN`) and `updateActorDirection` with `actorTurnInterpolateTable`. `remapDirection` gains the walking form (a locked box's direction follows the leg's sign) and the `0x400` interpolation mark.
  - `putActor`, `hideActor` and `adjustActorPos` stop a walk (`stopActorMoving`, and the stand frame for a visible walking actor). `animateActor` 0xFC–0xFF stops a walk, and 0xF8–0xFB clears `MF_TURN`.
  - `StepWalks` is `walkActors`. A walk from an unknown start point, box or facing still ends at once as a labelled estimate (`WalkAtOnce`, the pre-M3a code).
  - The estimate rule from the at-once walk is kept. When every box routes to the end box, arrival at the destination makes the position independent of an estimated start (an assumed catalog arrival), unless a new estimate was marked after the walk started. An actor already at the destination gets the same treatment.
  - A catalog visit's assumed arrival (`loadRoomWithEgo` or `loadRoom` arrival) still ends its walk at once, labelled. A catalog visit shows the state after the arrival and does not play it. Without this, the unplayed arrival walk carried on under the player's first click: in Room 08 the click on object 253 found the ego already at the object's walk point, so it only turned, and script 007 ran after the arrival walk ended 60 px away.
- `LoomCamera` (new) holds the camera mode, followed actor, destination and `_movingToActor`. `Move` is `moveCamera`: round down to a strip, return into `VAR_CAMERA_MIN_X/MAX_X` 8 pixels a frame, start moving to the followed actor outside strips 10–30, step 8 pixels towards the destination, and clamp half a screen from the edges (`cameraMoved`).
  - `setCameraAt` is `setCameraAtEx`: normal mode, placed at once.
  - `panCameraTo` sets panning mode and the destination only.
  - `actorFollowCamera` jumps only when the actor is more than half a screen away (`setCameraAt` in follow mode); otherwise the camera pans.
  - `loadRoomWithEgo` enters follow mode, and `startScene` resets to normal mode.
- The session's `AdvanceFrame` ends each frame as `scummLoop` does after the scripts: `walkActors`, then `moveCamera`. The next frame's `processActors` animates the limbs, as before. `ActorsMoving`, `EgoMoving` and `CameraMoving` are new. `CameraMoving` asks whether the next `moveCamera` would still move the camera. A camera held at a room edge short of its actor does not count, although ScummVM keeps `_movingToActor` set then.
- Executor:
  - `waitFor actor` breaks while the actor's `_moving` is set and goes on **at once** otherwise (before, it always went on on the next frame, labelled).
  - `waitFor camera` breaks while the camera is not on its destination's strip.
  - `getActorMoving` (4 sites) answers `_moving`.
  - `walkActorToObject` passes the object's actor direction (`getObjectXYPos`: `oldDirToNewDir(actordir & 3)`) as the walk's end direction.
- The app's playback clock keeps running while the **ego** walks or turns or the camera moves, and those frames do not count towards the 32 idle frames. So a sentence that waits for a long walk still runs. Waiting for every actor hung DoorwayClickCheck: ambient walkers such as script 042's swan never stop. Counting `_movingToActor` hung it too, because at a room edge that flag never clears.

## Frame counts (before → after, same seed)

| Route | Before (walks at once) | After (stepped) |
| --- | --- | --- |
| Boot, frames after script 001's first room load | 421 | 725 (ends in Room 02 at (828, 60), facing 270) |
| Room 10 object 295 → script 052 → Room 13, cutscene frames | 74 | 267 |
| Room 08 object 255 → the Elders' trial (script 046), cutscene frames | 2,119 | 2,784 (ends in Room 09) |

`LoomBootTests` checks only that the boot ends within `BootFrameLimit` (5,000), and passes.

## ScummVM comparison

- **Room 02 after the opening:** ScummVM shows Bobbin in left profile (seen 28 Sep, M2c). The session now gives 270 as well (`LoomBootTests`, and ActorDrawCheck `room02-after-boot-app.png`). Match.
- **A walk in progress, and Room 02's change of scene:** not compared this session. Synthetic input does not reach ScummVM's SDL window, so the user has to drive it. Still to check by eye: that clicking the exit from the cliff makes Bobbin walk down the path; that the scene change in box 4 stops him at (756, 107) in the smaller costume and waits for a new click; that the camera follows 8 pixels a frame.

## Evidence

- Tests (223/223; `dotnet build SCUMMexplorer.slnx`: 0 warnings):
  - `LoomActorTests.AWalkInTheCurrentRoomStepsAlongTheBoxPath`: Room 02 from (820, 60) to box 4. The actor moves at most (8, 2) a frame, passes more than 20 points and ends at (860, 118) in box 4 with no estimate.
  - `LoomActorLookTests`: turns go one step a frame, and down to up goes through right.
  - `LoomActorRendererTests`: a locked box's turn happens on the next walk step.
  - `LoomCutsceneTests.AWaitForActorBreaksWhileTheActorMovesAndGoesOnOnceItStops`.
  - `LoomActorTests`: the catalog visit's assumed arrival into Room 03 ends at x 250 at once. The reached Room 04 arrival (Room 03 local 200) steps to (580, 132), and the camera follows it in 8-pixel steps (each a multiple of 8) to 480. Room 65's assumed arrival is at (228, 44).
  - `LoomClickInputTests.AClickOnRoom02sExitWalksTheEgoAndRunsTheSentence`: first click, script 045's box-4 scene to (756, 107); second click, box 10's scene to (609, 110); third click reaches Room 03 through script 007 and verb 0x38.
  - The verb-click, key-input and double-click tests now wait for the 5-frame walk to object 156.
- ActorDrawCheck (`.verification/actor-draw/ActorDrawCheck`, real MainWindow, no physical input): **0 failures**.
  - After the boot the ego faces 270.
  - A click at (860, 118): the layer was redrawn 78 times. 61 frames move the ego by at most (8, 2), with 1 jump (script 045's `putActor`). The walk frame 9 shows in 63 frames with 23 limb poses.
  - A second click on the exit: the camera goes 792 → 680 in 14 steps of 8 pixels, and the view scrolls with it (15 offsets). The walk stopped at box 10's scene change, so playback ended without a room load and the app's labelled doorway fallback opened Room 03.
- RoomAudit: 0 gaps in all 76 rooms; `compare-png.ps1`: 0 changed renders. Room renders do not include actors. `room-audit.txt` was refreshed (it still listed `animateActor` as unapplied, from before M2b).
- DoorwayClickCheck on Rooms 02, 03, 04, 08 and 10 ([doorway-clicks.txt](doorway-clicks.txt), 24 s): **13/13**, 10 through the game's click and 3 through the labelled fallback. Before M3a ([../messages-app/doorway-clicks.txt](../messages-app/doorway-clicks.txt)) it was 11 and 2. The one change is Room 02 object 153: script 045's box-4 scene change stops the walk, so the fallback opens Room 03. `var[0x071]` can differ from a full run, because the route differs.
- Blockers ([blockers-after.txt](blockers-after.txt), about 15 minutes): 1,419 lines, before 1,446 ([../actor-draw/blockers-after.txt](../actor-draw/blockers-after.txt)). `expression` (3) is still the only real stop. These labels are gone: "waiting for actor … continues on the next frame" (127), "the camera pan reaches x = # at once" (96) and "walk … taken to end at" from scripts (15).
- Not rerun: MessageAppCheck, ClickInputCheck, CameraPanCheck and VerbAreaCheck (the slow WPF harnesses). The click path changed only through walk timing, which ActorDrawCheck and the unit tests cover.

## Still estimated or not modelled

- `delay` and `delayVariable` still go on on the next frame (M3b).
- A catalog visit's assumed arrival walk ends at once (estimate, as before M3a).
- Walk scripts (`_walkScript`), `animationSpeed` and `_extraBoxFlags` are not modelled.
