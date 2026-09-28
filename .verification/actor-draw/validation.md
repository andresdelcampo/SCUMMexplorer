# M2c: actors drawn in the room (app), 28 September

## What changed

- `LoomActorRenderer` (`src/SCUMMexplorer.Core/Rendering/LoomActorRenderer.cs`) draws the actors of the current room into an indexed layer the size of the room, as ScummVM `processActors` does for v3 (`actor.cpp`, fetched 28 Sep):
  - The actors in the room are sorted with the engine's own exchange sort: for each j, for each i, swap when `y[j] < y[i]` (layer 0). ScummVM says this unstable order is the original's and must not be "optimized". The result is ascending y, so a lower actor is drawn later (on top). Ties keep the order the exchange sort leaves, starting from actor-number order.
  - Each visible actor with a costume other than 0 is drawn (`drawActorCostume`) at (x, y − elevation) with `LoomCostume.Draw` (its current limb frames and old direction) and `Compose` with the actor's palette. Pixels are clipped to the room. Hidden actors and costume 0 are skipped. An actor whose costume, position, elevation or facing is unknown is skipped and listed.
  - Priority masks (actors behind foreground objects) are not applied.
- The app (`MainWindow`) has a new `RoomActorOverlay` image between the room image and the exit/walk-box/message overlays. `UpdateActorLayer` redraws it whenever the room preview is refreshed and on every playback frame (`ShowPlaybackFrame`), so the limb animation plays on the playback clock (10 frames a second). It shows only in the explore mode, while the session is in the shown room. The App keeps a costume cache per game.
- Two session fixes found by comparing with ScummVM:
  - **`setClass` class 22 on an actor** (ScummVM `putClass` → `Actor::classChanged` for v4 and older): `kObjectClassIgnoreBoxes` sets or clears the actor's `ignoreBoxes`. Before, only class 0 cleared it. Room 02's entry (+0x0019 `setClass 4, [0, 0x96, 0x94]`) now leaves actor 4 at (433, 129) as `putActor` gives it, instead of moving it into box 14. Script 044's fairy (actor 3, +0x053B) and script 042's swan (actor 2, +0x0000) also ignore boxes now.
  - **`remapDirection` for a standing actor** (ScummVM `turnToDirection` → `updateActorDirection` → `remapDirection`, and `adjustActorPos`, which turns an actor to its own facing when its box has `flags & 7`): box flags 0x08/0x10 (or the actor's XFlip/YFlip class, v3 classes 19/18) flip the direction, and the low 3 bits lock it: 1 is left or right (90 stays, anything else becomes 270), 2 is up or down, 3–6 are fixed. The turn is still taken at once (labelled). `_extraBoxFlags` are not modelled.

## Comparison with ScummVM (by eye; the user drove `scummvm/scummvm.exe` 2.2.0 through the opening, Spanish LOOM EGA)

Screenshots were taken with the computer-use tool during the session and looked at directly. No image files were kept, because the tool did not return their paths.

| Scene | ScummVM | App (this harness) | Verdict |
| --- | --- | --- | --- |
| Room 01, intro sky | Night sky, islands, a few small orange/magenta points of light at the lower left | Not captured in the app (the boot runs it in the session only) | Not compared (the app does not play the boot) |
| Room 53, the messenger's close-up | "Levántate, Hijo de Cygna." … "El Gran Consejo espera tu presencia en el Santuario." | Not compared | — |
| Room 02 after the opening, player in control | Bobbin under the tree at the cliff's edge (camera at 800), **without the distaff**, in **left profile**; the red leaf in the branches up and to his left; no other actor in the room screen | `room02-after-boot-app.png`: Bobbin at (828, 60) without the distaff, **facing the viewer** (180); the red leaf (actor 5, costume 8, at (807, 25)); the same camera and position | Position, costume, frames, leaf: match. **Facing: differs** (see below) |

The facing difference comes from the walk estimate, not from drawing. In the opening, script 044 +0x0580 turns the ego to 180 (`animateActor 1, 0xF6`), then starts Room 02 local 201 (the fairy, actor 3, flies off in six `walkActorTo`s with `waitFor actor`) and local 202, which runs `faceActor 1, 3` every frame while 201 runs. In ScummVM the fairy takes many frames (walk speed (4, 1)), so Bobbin keeps turning towards it and ends facing left, where it flew off (x 0x27A). In the session every walk ends at once, so local 201 is over and the fairy is out of the room (`putActorInRoom 3, 0`) before local 202's first `faceActor`. The executor then skips the turn, as ScummVM does for an actor in another room, and Bobbin keeps 180 (traced with temporary logging, since removed). M3a (walking in steps) fixes this. Until then the ego's facing after the boot is a consequence of the labelled walk estimate. Script 044 +0x063E skips the +0x0678 `animateActor 1, 0xFA` reset unless the cutscene was aborted (`var[0x005]`, VAR_OVERRIDE), so ScummVM keeps the left profile.

Other actors that the session puts in Room 02 are not visible in ScummVM, and the app agrees for each:
- Actor 4 (costume 141) is at (433, 129), in the 16 rows that the game never shows (screen 128 rows, see the plan's known issue). The app draws it there, in its 144-row view. It shows as 30 pixels at the bottom left of the rock (visible in the app, not in the game: the optional 128-row crop would hide it).
- The swans (actors 10 and 11, costume 5) are at command 14 of their animation, which draws nothing (no cel). 0 pixels.
- Actor 2 (script 042, costume 5) appears later as a growing speck in the sky: frames 6–8, command runs 3..24, drawn one step a frame during the click's playback (see the frame log). This is the costume's own animation playing on the playback clock.

A click comparison in ScummVM was not done. Synthetic clicks and keys from the computer-use tool did not reach the SDL window (the game's cursor did not move), and the user closed ScummVM afterwards.

## Evidence

- Tests: `LoomActorRendererTests` (4): after the boot the layer draws actors 1, 4, 5, 10 and 11 of Room 02, with the ego's pixels standing on (828, 60) (bottom row 55–64, spans x 828) and every drawn pixel inside a drawn actor's bounds; ScummVM's exchange sort ([90, 40, 60, 40] → actors 2, 4, 3, 1); hidden actors and costume 0 draw nothing; a standing actor put on Room 02 box 1 (flags 0x01) turns from 180 to 270, and on box 0 (no flags) keeps 180. Suite 220/220 (`dotnet build SCUMMexplorer.slnx`: 0 warnings).
- RoomAudit: 0 gaps in all 76 rooms; `compare-png.ps1`: 0 changed renders. Room renders do not include actors.
- Blockers (`blockers-after.txt`): identical to `../actor-state/blockers-after.txt` (1,446 lines; `expression` ×3 is the only real stop).
- `ActorDrawCheck` (`ActorDrawCheck/`, real MainWindow, `actor-draw-check.txt`): 0 failures.
  - Room 02 after the boot: the actor layer shows 501 pixels, and 454 of them are the ego's in (810, 15)–(846, 62) (`room02-after-boot-app.png`).
  - A single click on empty floor at (860, 118) (box 4, assumed input): scripts 005–007 walk the ego there, and script 045 (which watches box 4) starts its cutscene: `startScript 17, [1]` switches the ego to costume 2 (var[0x078], the smaller Bobbin down the path) and walks it to (0x2F6, 0x67) = (758, 103), adjusted to (756, 107). The layer was redrawn 15 times during the playback: the ego jumps (walk at once, labelled estimate: "faces 270 along the whole walk"), then the swan speck (actor 2) grows and shrinks frame by frame (`room02-click-frame1..6-app.png`, `room02-after-click-app.png`).
  - The walk animation itself is not seen: the walk ends at once, so the walk frame (9) never has frames to play. That waits for M3a.
  - Room 03 visit: only Room 03's actors are drawn (72 pixels).
- `costume001-frame10-down.png`/`-left.png` (CostumeAnimRender): the ego's stand frame facing the viewer (the app's pose) and in left profile (ScummVM's pose).
- WPF harnesses not rerun: MessageAppCheck, ClickInputCheck, CameraPanCheck, VerbAreaCheck, DoorwayClickCheck. The change adds a non-hit-testable overlay and does not touch the click path, doorway navigation or room loading. ActorDrawCheck ran the game's click and a room change in the real window.

## What the user sees

In the explore mode, rooms now show their actors: after the boot, Bobbin stands under the tree in Room 02 without the distaff, with the last red leaf in the branches. A click plays forward with the actors redrawn every frame. Bobbin jumps to where the walk ends (walks are not stepped yet), and animated actors such as the far-off swan speck move on the game's 10-frames-a-second clock. Talk text now appears over a drawn Bobbin. Known differences: Bobbin faces the viewer after the boot where ScummVM shows him in left profile (walk estimate, M3a); actors are never hidden behind foreground objects (priority masks not applied); actor 4 is visible in the 16 bottom rows the game does not show.
