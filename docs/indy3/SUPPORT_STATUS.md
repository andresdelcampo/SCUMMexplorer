# Indiana Jones and the Last Crusade (EGA) support status

Family: Indy3Ega | Fixture: `Games/indy3 ega en` | Last updated: 2026-10-14 | Owner: S4b agent

| Stage | State (not started / in progress / done / blocked) | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (earlier; no tracker then) | Rooms | – | |
| S2 Reading | done (earlier) | Costumes, Scripts, Texts | – | |
| S3 Sound | done (earlier) | Sound | – | AdLib tempo unchecked by ear; see Open uncertainties |
| S4b Session | **done** | – (unchanged) | `.verification/indy3-session-20261014` | boot trace: 1341 frames, rooms 76, 79, 0, 6, 1, 92, 1, 73, nothing unsupported |
| S5 Objects | not started | Objects (partial until S6) | | |
| S6 Play | not started | Play, Objects | | Play requirement for a verb-based game still to be written as a Proposed row |
| S7 Cutscenes | not started | Cutscenes | | |
| S8 State | not started | Status | | requested in a run: no |
| S9 Editions | not started | – | | editions checked: none |

Blocked on: nothing.
Next: S5 (first-visit object state from the session). It needs: (1) a decision on the copy-protection answer of Room 92 (the boot answers wrongly three rounds, so `bit[0x5E4]` is 1; the correct answers are in `var[0x063..0x066]` after Room 92's local script 205 runs); (2) the check of Script 73 (still pending after the boot, see the script map); (3) the display-only operations (`roomOps`, `oldRoomEffect`, `resourceRoutines`, `stringOps`, `lights`) applied where they change what a room shows; (4) the input constants of S6 (click gaps and `runInputScript` mode) as profile data.
Open uncertainties: Script 73 stays pending; the AdLib playback of the sound player uses LOOM's driver constants for Indy3 (R-13.9 Proposed); the PC-speaker music timer is not modelled; the boot's device is AdLib.

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
