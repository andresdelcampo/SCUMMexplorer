# Indiana Jones and the Last Crusade (EGA) support status

Family: Indy3Ega | Fixture: `Games/indy3 ega en` | Last updated: 2026-10-14 | Owner: S5 agent

| Stage | State (not started / in progress / done / blocked) | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (earlier; no tracker then) | Rooms | – | |
| S2 Reading | done (earlier) | Costumes, Scripts, Texts | – | |
| S3 Sound | done (earlier) | Sound | – | AdLib tempo unchecked by ear; see Open uncertainties |
| S4b Session | **done** | – (unchanged) | `.verification/indy3-session-20261014` | boot trace: 1341 frames, rooms 76, 79, 0, 6, 1, 92, 1, 73, nothing unsupported |
| S5 Objects | **done** (14 Oct; Objects stays partial until S6) | Objects (partial until S6) | `.verification/indy3-objects-20261014` | 88 rooms, 88 without a gap; room view, actors and room cards from the first visit of the generic session; Room 92 answered correctly; Script 73 no longer pending |
| S6 Play | not started | Play, Objects | | Play requirement for a verb-based game still to be written as a Proposed row |
| S7 Cutscenes | not started | Cutscenes | | |
| S8 State | not started | Status | | requested in a run: no |
| S9 Editions | not started | – | | editions checked: none |

Blocked on: nothing.
Next: S6 (Play). It needs the Play requirement for a verb-based game as `Proposed` rows (sentence line, verb pick, inventory, the game's input scripts 4/15/16), the input constants as profile data (click gaps, `runInputScript` mode, camera 160), and clicks delivered to the game's own input script chain. Open S5 follow-ups (user's look at the rooms listed in the record): Rooms 20, 28, 32 show mostly black pictures; rooms 1, 6, 15 and 13 more never rest within 2400 frames; Script 23's `actorFollowCamera` of an ego in another room (ScummVM `setCameraFollows` loads that room) is not modelled; the first visit of the boot's own rooms (76, 79, 6, 1, 92) is the state a visit gives, not the opening scene as the boot plays it.
Open uncertainties: the AdLib playback of the sound player uses LOOM's driver constants for Indy3 (R-13.9 Proposed); the PC-speaker music timer is not modelled; the boot's device is AdLib; no ScummVM runtime picture was compared in S5 (the bundled build showed a white window with the default renderer and failed with the software renderer); Rooms 47, 60, 66 start dark (room lights off) and are shown lit.
## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 2026-10-14 | S4b | One data-driven session or an Indy3 class? | `ScummGameSession` / `Indy3Session` | one class in `Core/Scumm/Session` that takes everything from `ScummProfile`; no game-specific behaviour was needed | delete `ScummGameSession`, add a derived class |
| 2026-10-14 | S4b | Engine-variable table per version? | new table by profile / keep the v3/v4 constants | Indy3 EGA uses the numbers `ScummVars` already has (ScummVM `setupScummVars`, same table for v3 and v4); only the extras (video mode, text speed, three extra timers, sound card) became profile data | none needed |
| 2026-10-14 | S4b | LOOM-calibrated input constants (`SingleClickGap`, `DoubleClickGap`, `InputMode`, camera 160)? | make profile data now / leave | left: S4b does not deliver clicks and Indy3 EGA is 320 wide like LOOM; they become profile data in S6 when its input script chain is read | none |
| 2026-10-14 | S4b | The Room 92 copy-protection screen waits for the player | start the answering script with a wrong answer / compute the correct answer / edit `var[0x039]` | the first (the game's own rule lets play go on after three failed rounds of four); editing the variable is forbidden (R-1.9) and the correct answer has no generic derivation. Labelled *Player input assumed* | change the argument in `ScummSession.AssumeBootAnswer`; S5 may choose the correct answer |
| 2026-10-14 | S4b | `VAR_SOUNDCARD` of the generic session | 0 (PC speaker) / 3 (AdLib) | 3: the session's sound clock reads the AdLib resources | set another card in `ScummGameSession` |
| 2026-10-14 | S4b | Music-timer source | model PC speaker too / AdLib only | AdLib (`Player_AD`) only; the PC-speaker timer is a different driver and is listed as not modelled | add a `Player_V2` clock |
| 2026-10-14 | S4b | Executor ops: new opcode handling shared with LOOM | per-game branch / generic from ScummVM | generic, from ScummVM; LOOM results unchanged (840 LOOM tests, same outcomes; room audit 3 to 0 gaps, 0 rooms with gaps) | revert the commit "ScummGameSession boots Indiana Jones..." |
| 2026-10-14 | S5 | The Room 92 copy-protection answer | keep three wrong rounds / edit variables / answer as a real player | a real player: one first click on the object whose code starts the question scripts, then the answer the answering script's own `isEqual`/`isNotEqual` check accepts, derived from the script. Labelled assumed input; no variable edited | set `answer` to 0 and skip `FindAnswerPreparation` in `ScummSession.AssumeBootAnswer` |
| 2026-10-14 | S5 | Script 73 pending after the boot | accept as the game's / find the cause | cause found: `isScriptRunning` was false for the script loading a room; fixed in the engine (`_loadingFrames`), per ScummVM `startScene` running inside the opcode. Script 73 is not started during the boot; the ego's costume at Room 73 becomes 1 | remove `_loadingFrames` from `IsScriptRunning` |
| 2026-10-14 | S5 | Display operations | leave as "not applied" / apply what changes a room / apply all | apply `roomColorLegacy`, `lights`, `stringOps`; trace (not report) transitions, resource routines, shake and screen split, which leave nothing in a room picture | revert the commit "apply the display operations ..." |
| 2026-10-14 | S5 | Dark rooms (lights off) | show dark / show lit | show lit and say so in Advanced (the explorer is for looking at rooms; the game's flashlight follows the mouse) | draw black outside a flashlight in `ScummRoomScene` |
| 2026-10-14 | S5 | How to reach a room whose route needs unplayed state | invent flags / play the route / visit like ScummVM's `room` | visit from the post-boot state without a route, ego by a labelled `loadRoomWithEgo` pick; no flag written | none needed |
| 2026-10-14 | S5 | When a room has settled | a fixed frame / until quiet | until nothing changed for 90 frames, ambient animators ignored; limit 2400; a script leaving the room keeps the last state in it | change `Settle` arguments |
| 2026-10-14 | S5 | Runtime comparison with ScummVM | drive the bundled build / source only | the bundled build could not be captured (white window; the software renderer failed); source-level comparison only, recorded | retry with another renderer or a saved game |
| 2026-10-14 | S5 | R-6.12 (Indy3 EGA background note) contradicts the S5 playbook | stop the item / propose a superseding row | propose R-6.12a and R-5.3a (own commit); the wiring follows the playbook, the old note remains only as the Advanced fallback note | revert the wiring commit and the proposal commit |
