# M2b: actor costume state (Core), 28 September

## What changed

- `LoomActorTable` (`src/SCUMMexplorer.Core/Formats/LoomActors.cs`) keeps each actor's costume, elevation, walk speed, init/walk/stand/talk frames, 16-entry palette, facing, last started frame and limb state (`LoomCostumeAnimationState`). `LoomActorSnapshot.Look` (`LoomActorLook`) exposes a copy. ScummVM `actor.cpp` (fetched 28 Sep):
  - `setActorCostume` (old costumes): resets the limbs and the palette (0–15) and marks the costume for init; a visible actor is re-shown, which starts the init frame (`showActor`). `putActor` into the current room and `showActors` at a room entry do the same.
  - `initActor(0)` (actorOps `init`): elevation 0, walk speed (8, 2), frames 1–5; facing and costume stay.
  - `startAnimActor`: frames 56–60 redirect to init/walk/stand/talk-start/talk-stop; only for an actor in the current room with a costume; the init frame resets the limbs; then `costumeDecodeData(frame)` for the facing.
  - `animateActor` (v3): `chore = 0x3F − (n >> 2) + 2`, `dir = oldDirToNewDir(n & 3)`: 2 stand frame, 3 `setDirection`, 4 turn, anything else starts frame n.
  - `setDirection`: each limb whose animation's direction differs restarts that animation's frame for the new direction (use mask of one limb).
  - `faceActor` (`faceToObject`, v3): left or right of the target, or up or down when the x distance is under half the target's width (an actor's is 24, an object's its width).
  - Talk frames (`runActorTalkScript` with no talk script): `displayDialog` starts the talk-start frame when text follows (LOOM DOS: not for an empty string); `stopTalk` starts the talk-stop frame.
  - `processActors` → `animateCostume` (animation speed 0): every frame the session steps the limbs of the actors in the current room (`AdvanceFrame`, at the start of the next frame, which is the same order as draw-then-animate).
- The executor applies actorOps `costume`, `elevation`, `walkSpeed`, `palette`, `initAnimation`, `walkAnimation`, `standAnimation`, `talkAnimation`, plus `animateActor` and `faceActor`, and answers `getActorCostume`, `getActorElevation` and `getActorFacing` (`newDirToOldDir(facing)`).

## Labelled estimates

- A turn (`animateActor` 0xF4–0xF7, `faceActor`) takes several frames in ScummVM (`MF_TURN`); the table sets the direction at once and says so in `Look.Estimate`.
- A walk in the current room still ends at once (M3). At its end the actor faces along the whole walk (ScummVM: the last leg's direction) and starts its stand frame; `Look.Estimate` says so.
- An unreadable `animateActor`/`faceActor` operand changes nothing and is traced; it no longer forgets positions.

## Findings (decoded)

- After the boot `bit[0xD61]` is set, so script 017 gives the ego frames 8/9/10 and talk 14/15: costume 1's animations 32–63, Bobbin without the distaff (`../costume-anim/renders/costume001-frame9-right.png`: walking, empty hands). Frames 1–5 are the same with the distaff. So `bit[0xD61]` reads as "has no distaff yet" (reading).
- The ego's last frame after the boot is 15 (talk stop): the boot's last talk line ended with `stopTalk`, which restarts only the head (limb 1); the body stays in the stand animation (40 + direction).

## Evidence

- Tests: `LoomActorLookTests` (4): the ego after the boot (Room 02, (828, 60), costume 1, elevation 0, walk speed (4, 2), frames 8/9/10/14/15, frame 15, body in animation 40 + direction); `animateActor` stop/face/turn/frame and the 57/58 redirects with the walk loop 23..28; an actor outside the room keeps its frame; palette, elevation and init. `LoomBootTests.OpeningCutsceneStartsRoom01LocalsThroughALocalTarget` now expects Room 01 local 208 to have no limitation (its only one was the unapplied `animateActor`). Suite 216/216.
- Blockers (`blockers-after.txt`, same tool as `../room-owned-loads/`): the `getActorCostume` (1) and `getActorElevation` (1) stops are gone; only `expression` (3) is left. "presentation operation not applied: animateActor/faceActor" no longer appears. (The older baseline predates M1, so its other counts differ for other reasons.)
- RoomAudit: 0 gaps in all 76 rooms; `compare-png.ps1`: 0 changed renders (actors are not drawn yet).
- WPF harnesses: not run; nothing in the App changed (M2c draws the actors).

## What the user sees

Still nothing new on screen: the session now knows what each actor looks like frame by frame, which M2c draws.
