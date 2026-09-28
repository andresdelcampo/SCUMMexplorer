# LOOM EGA exploration plan

Replanned 28 September 2026. The history of finished work is in the [changelog](CHANGELOG.md). What the decoded scripts, variables and rooms do is in the [script map](LOOM_SCRIPT_MAP.md).

## Product direction

Make the DOS *LOOM* EGA release a place to explore. Rooms and their original artwork lead. Clicking things does what the game does: Bobbin walks over, looks, speaks and uses things, drafts change the world, and cutscenes play with their actors, text and music. Source details (IDs, bytecode, uncertainty labels) stay available on demand, but they are not the experience. Later, a spoiler-aware guide to secrets is built from verified triggers.

**User priorities, in order (28 September):**

1. Object interactions do something visible.
2. Cutscenes and animations play, mostly as the result of those interactions.
3. Text appears on the screen where the game shows it: descriptions, dialogue and cutscene lines.

## What the app shows today

The Core session already runs a lot of the game: the boot, the hooks, cutscenes, the input scripts (005–010), object verbs, room loads, verb slots, actor positions and camera. **Most of what it runs never reaches the screen.** The app shows room backgrounds with object states, navigates doorways and plays sounds on demand. Everything an actor or a `print` does stays in the trace.

| What the player expects | Today | Why |
| --- | --- | --- |
| Clicking an object does something | Since 1e (28 Sep): a click goes through scripts 005–007 (walk and verb 0x38; a double click adds 0x36). Doorways lead on, with a labelled direct-verb fallback. The only visible effects are a room change and the verb-area close-up. | The verb's output (text, animation) is not drawn. |
| Text on the screen | Since 1c/1d (28 Sep): reached messages appear over the room and in the verb screen in the game's font, for their talk delay, while a click plays forward at 10 frames a second. A click on the text skips it. | Works; since M2c the talking actor is drawn under its text. |
| Actors (Bobbin, the swans, the shepherds…) | Since M2c (28 Sep): drawn over the room in the explore mode, sorted by y, on their current limb frames, and redrawn on every playback frame. After the boot, Bobbin stands under the tree in Room 02 without the distaff, in left profile as in ScummVM. Since M3a (28 Sep) he walks along the box path with his walk animation, turns over frames, and the camera follows him 8 pixels a frame. | Priority masks are not applied. A catalog visit's assumed arrival walk still ends at once (labelled). |
| Cutscenes and animations play | A click's playback shows walks, turns, costume animation and the camera frame by frame at 10 frames a second. | `delay` and `delayVariable` still go on on the next frame (M3b); the boot and catalog cutscenes are not played in the app. |
| Drafts (the distaff) | Optional verb strip plus game input: notes are recorded and a draft reaches the object's verb, which produces text and animation that are not shown. | Same as above. |
| Room music | Plays on demand; reached cues are shown. | Works. |

**Scale of the gap** (decoded code, `.verification/fallback-evidence/code.tsv`): 1,853 `animateActor`, 1,170 `print`, 878 `putActorInRoom`, 712 `actorOps`, 662 `putActor` and 488 `walkActorTo`. Of the 626 object verbs, 156 print text and 394 start scripts (mostly reactions, draft animations and cutscenes). Only 68 load a room, and room loads are the one result the app shows.

**Consequence for the old task list:** the remaining "Blockers" stops (`expression` ×3, `getActorCostume` ×1, `getActorElevation` ×1), the refused-input label wording and the KeyInputCheck extension do not change what the user sees. They are dropped as separate tasks. The actor work below models `actorOps costume/elevation`, which removes two of those stops as a side effect.

## Ordered work

Each milestone ends with something the user can see in the running app, verified by a WPF harness and screenshots under `.verification/<topic>/`.

### M1. Object clicks run the game's interaction, and its text appears on screen

- **1a. Message layer (Core). Done 28 September ([validation](../.verification/messages/validation.md)).** Turn a reached `print` into a message: actor (0xFF is a system line, 0xFE is used by script 018 for the note letters), position, `center`, `left`, colour and text. Decode the control codes: `0xFF 0x01` is a new line, and the others follow ScummVM `string.cpp`. Keep the room, script and offset. Track `VAR_HAVE_MSG` (`var[0x003]`) and the talk delay as ScummVM does: `VAR_DEFAULT_TALK_DELAY` (or 60) plus `VAR_CHARINC` per character, counted down by the frame's jiffies. A `waitFor message` then waits for the right number of frames instead of one. The snapshot exposes the messages on screen now.
  - Actor positions for talk text come from `actorOps talkColor` (85 sites) and the actor's position. Where the actor or position is an estimate, the message says so.
- **1b. The game's font (Core). Done 28 September ([validation](../.verification/messages/validation.md)).** Read charset 0 from `99.LFL` (not XOR-encoded; ScummVM `ScummEngine_v3::loadCharset`, `CharsetRendererV3`). Render a message into a bitmap at its screen position, with the colour and the centring rules of `CharsetRendererV3`/`drawString`. If the charset cannot be read, fall back to a UI font that is labelled as not the game's.
- **1c. The app shows messages over the room. Done 28 September ([validation](../.verification/messages-app/validation.md)).** The session's messages are drawn in charset 0 over the room at their screen position relative to the game's camera: screen row y is room row y − 16 (ScummVM `initScreens(16, 144)`), and rows 144–199 are the verb screen, which appears under the room while it holds text even with the verb strip off. A click on talk text ends its delay (ScummVM's '.' talk-stop key, assumed input). If charset 0 cannot be read, a UI font draws the text, labelled as not the game's.
- **1d. Clicks play forward. Done 28 September (same validation).** After an input, the app advances the session on a timer at `VAR_TIMER_NEXT` jiffies per frame (10 frames a second) and redraws each frame (object states when they change, camera, messages) until no talk is shown and the scripts wait for input (32 frames outside a cutscene, as the exit continuation did; cutscene frames do not count). A room load reached while playing opens that room and playback goes on there. Opening another room, Continue, a session reset or leaving the explore mode stops it. The doorway fallback runs when playback ends without a room load. This is the first version of the playback loop that M3 completes.
- **1e. Default click = the game's click (decided 28 September). Done 28 September ([validation](../.verification/game-click/validation.md)).** Bobbin stays on screen and walks (M2, M3), so clicks follow the game. In the explore mode, a single click on an object goes through scripts 005–007: Bobbin walks to it and verb 0x38 runs (use, or go through an exit), with the close-up and name for class-1 objects. A single click on empty floor walks Bobbin there. A double click adds verb 0x36 (look: the description). The click is labelled assumed input, as `Click` already is. "Click as game input" stops being optional in the explore mode. Artwork cycling stays only in the inspection mode, and that mode's text says so. There is no Alt+click. Until M2 draws Bobbin, a single click on a non-exit object shows only its text, if it has any. The CLAUDE.md product rule was changed to match.
- **Verification:** Room 02, double click on object 156 (the sky) shows "Amanecer." in the game's font over the room for its talk delay. Object 154's verb 0x36 walks the ego, then shows "Ultima hoja del año.". A WPF check captures the text layer, and a Core test checks message text, position and duration.

### M2. Actors on screen

- **2a. Costume animation (Core). Done 28 September ([validation](../.verification/costume-anim/validation.md)).** `LoomCostume` steps each limb as ScummVM does; Bobbin's costume 1 stands, walks (8 poses left/right, 6 down) and talks (head only). Extend `LoomCostumeRenderer` from static poses to ScummVM's `ClassicCostumeLoader` for format 0x58. An animation is `frame * 4 + direction`. Each limb has a start command and a length, so the command index advances per frame and loops or stops. Handle the mirror flag for left-facing directions, the stop and hide commands (0x79 and 0x7A), and the palette mapping (`actorOps palette`, 380 sites).
- **2b. Actor state (Core). Done 28 September ([validation](../.verification/actor-state/validation.md)).** The actor table keeps costume, elevation, walk speed, frames, palette, facing and limb state; `animateActor`, `faceActor` and the talk frames change it, and each frame steps the limbs. Turns and walks still end at once (labelled). The `getActorCostume`/`getActorElevation` stops are gone.
- **2c. Draw actors in the room. Done 28 September ([validation](../.verification/actor-draw/validation.md)).** `LoomActorRenderer` sorts the room's actors with ScummVM's `processActors` exchange sort and draws each visible actor with a costume at (x, y − elevation), clipped to the room; the app's actor layer is redrawn with the room and on each playback frame. Two session fixes came from the ScummVM comparison: `setClass` class 22 sets an actor's `ignoreBoxes`, and a standing actor's turn is remapped by its box's direction flags (`remapDirection`). Priority masks (actors behind foreground objects) are a follow-up: decode them only if a verified room shows the problem.
- **Verification:** after the boot, the ego stands in Room 02 at (828, 60) in its stand frame (done: matches ScummVM except the facing, see the M2c validation). Room 01's intro locals 200–208 show their points of light while they run (not yet: the app does not play the boot). Screenshots are compared by eye with the ScummVM build in `scummvm/`, and each comparison is recorded.

### M3. Cutscenes and animations play

- **3a. Walking in steps. Done 28 September ([validation](../.verification/walk-steps/validation.md)).** Actors move along the walk-box path at their `walkSpeed` (ScummVM `Actor_v3::walkActor`, `calcMovementFactor`, `actorWalkStep`, `findPathTowardsOld`), with the walk animation; turns go one step a frame (`updateActorDirection`); the camera follows 8 pixels a frame (`moveCamera`), and `panCameraTo` pans. `waitFor actor`/`camera` wait for the real arrival and `getActorMoving` answers. Bobbin now ends the boot in left profile, as in ScummVM. Room 02's changes of scene (script 045) now interrupt a click's walk down the path, as the decoded code says; still to compare with ScummVM by eye.
- **3b. Next: frame timing.** Model `delay` and `delayVariable` in jiffies, the talk delay from M1 and `VAR_TIMER_NEXT`. The app's playback clock drives `AdvanceFrame` and redraws the room each frame: object states, actors, messages and camera. Sounds start on the frame they are reached (the audio code exists).
- **3c. Playback controls.** Play, pause and Escape (skip; `abortCutscene` is already modelled). A "play this cutscene" entry reaches known cutscenes from Game Texts or a room. Each one starts from a stated, labelled state.
- **Verification:** Room 10 object 295 plays script 052 through Room 11 into Room 13, with actors and text. Escape skips to Room 13 as today. The boot's opening cutscene (script 044) plays in Rooms 01, 02, 53 and 02.

### M4. Drafts

With M1–M3 in place, playing a draft on an object shows its result. The distaff notes show in the verb area by default when game input is on, the draft's notes sound, and the object's verb (0x3A–0x4D, or 0x56 for "that spell won't work") shows its text and animation. The boot's draft table is a seeded estimate, so which draft is which stays labelled until the drafts are learnt through a route.

### M5. Secrets and guide

Build a discoverable, spoiler-aware index of secrets from verified triggers and outcomes, with source links. This is deferred until M1–M3 are done.

### Engine gaps to close only when a milestone route needs them

- `stopObjectScript`, inventory object code, `killScriptsAndResources` ending the other room-owned scripts at a room load.
- `expression` in some forms (3 sites; find them with `Blockers … "expression"`).
- The Room 69 difficulty and draft answer are assumed, not simulated. The Room 03 town/cemetery labels are unidentified.

### Known issues

- The app shows all 144 rows of a room image, but LOOM's room screen is 128 rows (ScummVM `initScreens(16, 144)`, not resized in v3), so the game never shows the last 16. In Room 02 they are black. Check other rooms before cropping ([view checks](../.verification/view-checks/validation.md)).
- Actors are drawn in all 144 rows, so Room 02's actor 4 (costume 141 at (433, 129)) shows in rows the game never shows. The 128-row crop would hide it.
- Actors are never hidden behind foreground objects: priority masks are not applied (M2c).
- CameraPanCheck and VerbAreaCheck pass again since 28 September ([view checks](../.verification/view-checks/validation.md)).
- Stored-text coverage is incomplete: Room 70 locals 203 and 204 are truncated, and unreachable code is not scanned.

## Evidence rules

- Tie every exit, action, state change, message, cue, animation and scene to the resource, script and instruction behind it.
- A decoded branch or room load is a **possible** outcome. Calling a verb directly is an **exploration action**. A partial session is a **state preview**. Label estimates, assumed input and timing assumptions.
- Messages, actors and playback are game behaviour when reached events drive them. A substitute (a UI font, a placeholder actor, a skipped wait) is labelled as presentation, not the game's behaviour, and is optional.
- No room-specific hacks or invented states. Rooms show no gaps and their first-visit state. When in doubt, pick at random and label the pick. Room hotspots navigate between rooms.

Status words: `verified` means source-backed and demonstrated in the app; `preview` is intentionally partial; `unresolved` means missing evidence or execution; `deferred` is later scope.

## Records

- [Changelog](CHANGELOG.md): every finished step, with its validation.
- [Script map](LOOM_SCRIPT_MAP.md): the reverse-engineering reference.
- [Object states](SCUMM_OBJECT_STATES.md): the rendering and state contract.
- Historical: [research](RESEARCH.md) (23 Sep), [script coverage](LOOM_SCRIPT_COVERAGE.md) (25 Sep), [exploration audit](EXPLORATION_AUDIT.md) (25–26 Sep), [rendering and Indy3 audit](AUDIT_2026-09-27.md) (27 Sep).
