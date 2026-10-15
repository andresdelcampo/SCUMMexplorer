# Monkey Island 1 CD support status

Family: MonkeyCd | Fixture: `Games/monkey1 vga en` (`The_Secret_of_Monkey_Island.cue`) | Last updated: 14 October 2026 | Owner: S3-leftovers agent

| Stage | State | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (before this file) | Rooms | | |
| S2 Reading | done (before this file) | Costumes, Scripts, Texts | | polish: ~3% of v5 script blocks unresolved |
| S3 Sound | in progress, **partial** | Sound | [v5-sound-leftovers-20261014](../../.verification/v5-sound-leftovers-20261014/README.md) | listening check listed in the run report: yes; result: pending |
| S4b Session | not started | – | | |
| S5 Objects | not started | Objects (partial) | | |
| S6 Play | not started | Play | | the script-driven sound hooks wait for this |
| S7 Cutscenes | not started | Cutscenes | | |
| S8 State | not started | Status | | requested in a run: no |
| S9 Editions | not started | – | | the disc also carries ENGLISH, FRENCH, GERMAN, ITALIAN, SPANISH data folders |

Blocked on: S6 for the sound hooks (see the record, section 4).
Next: the user listens to the loops (list in the run report); S4b.
Open uncertainties: disc track 21 (16 s) is started by no sound and stays unnamed; six `startSound` operands are variables (script 101 `var[100]`, script 162 `local[4]`, room 75 local script 205 `local[0]`), which sound they start needs running scripts.

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 14 Oct | S3 | The sound's CD track byte against the cue's track numbers | (a) byte = cue number, as written on 12 Oct; (b) byte counts the audio tracks from 1 | (b): the disc has 24 audio tracks (cue 2 to 25) and the sounds name 1 to 24; the data track cannot be played; room names and one-shot lengths fit. Cue-number labels in the list are unchanged, the rooms/sounds shown are now right | revert the `gameTrack` change in `GameFolderScanner` |
| 14 Oct | S3 | Name disc track 21? | name it from a guess / leave unnamed | unnamed: no sound, no direct start (only LOOM CD has one) and no literal script start names it (R-2.1) | none needed |
| 14 Oct | S3 | How long a looping song is rendered | one pass; 3 minutes (the preview limit already used by the other devices); more | 3 minutes, ending at the first jump back at or after it (hard cap 420 s) | `ScummImuseTimeline.MinLoopSeconds` |
| 2026-10-14 | S3 | Raise Sound to Full? | keep partial until S6 hooks / raise now | raised: the user listened and accepted the v5 sounds (loops, hooks with id 0, MI1 CD track list); the script-driven hooks stay a known limit for S6 | revert the one registry line |


---

# Monkey Island 1 floppy support status

Family: MonkeyFloppy | Fixture: `Games/monkey1 ega en` (the four-disk 256-colour floppy release, SCUMM v4; the folder name says EGA, the data is VGA) | Last updated: 15 October 2026 | Owner: S5 MonkeyFloppy agent

| Stage | State | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (before this file) | Rooms | | |
| S2 Reading | done (before this file) | Costumes, Scripts, Texts | | |
| S3 Sound | done (before this file) | Sound | | polish: AdLib music at half tempo, set by ear |
| S4b Session | **done** (14 Oct) | unchanged (no cell changes in S4b) | [monkey1-session-20261014](../../.verification/monkey1-session-20261014/README.md) | boot to Room 33 in 2522 frames, unsupported list empty |
| S5 Objects | **blocked** (15 Oct: fixture missing) | Objects (partial), unchanged | `.verification/monkey1-objects-20261015/RoomAudit` (harness only, not run) | code for the v4 first visit and the profile-driven wiring committed, **not verified**; rooms compared: 0/10 |
| S6 Play | not started | Play | | the interface loop (script 23) and the input script (script 4) already run |
| S7 Cutscenes | not started | Cutscenes | | |
| S8 State | not started | Status | | requested in a run: no |
| S9 Editions | not started | – | | the eight-disk Spanish EGA release boots the same way (record); its video-mode variable is the VGA value |

Blocked on: **missing data** (hard stop 4.4.4): on 15 October at 17:48 the main checkout's `Games` folder became empty (no `Games/monkey1 ega en`, no LOOM or Indy3 folder either; checked inside and outside the sandbox and on drives D, E, X; waited about 10 minutes). Nothing could be run: no room audit, no pinned rooms, no LOOM regression (its tests skip without data).
Next: when `Games` is back, rerun S5 from the committed code: run `Monkey1RoomAudit` on `Games/monkey1 ega en`, pin three settled rooms in a `ScummMonkeyFloppyFirstVisitTests` test (candidates: 33 the dock, the boot room; two rooms entered without their route), check `Indy3` first-visit tests and the LOOM regression unchanged, then the record, script map, engine notes and changelog.
Open uncertainties: the session's AdLib music timer for the credits is an estimate (iMuse in the engine); the profile gives `VAR_VIDEOMODE` 19 to every v4 game, wrong for the EGA release; the wrong-answer path of the protection (a fourth wrong answer quits) was not run.

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 2026-10-14 | S4b | Which disks does the game ask for? | model "insert disk" as a blocking screen; assume every disk inserted | every disk available (a v4 resource sits on the one disk that holds its room, found through the index); the boot never waits for a disk (R-3.15, Proposed) | `ScummV4DiskSetDataSource` could be made to refuse a room until its disk is "inserted" |
| 2026-10-14 | S4b | Answer to the Dial-A-Pirate question (R-15.5 says answer with the answering script, wrong answer allowed) | a wrong answer four times (the game quits); start a local answering script as for Indy3; keys through the game's input script with the right year | the right year, as keys through the game's own input script (script 153 for Enter, 155 for the digits), read from `var[0x10A]` where the game's own script put it (read, never written): the game takes its right branch and the answer is the game's data (R-15.7, Proposed); the failing answer would end the game | `ScummProfile.BootInput` null: the boot then stalls at the protection |
| 2026-10-14 | S4b | When is a boot "waiting for the player"? | detect the awaited variable like the Indy3 answer; any state that does not change for N frames | nothing changes for 400 frames (`ScummGameSession.BootStallFrames`), so no script number or variable is named in shared code | change the constant |
| 2026-10-14 | S4b | Where does the boot end? | when script 1 ends (Room 96, title music, input off); when the player has control | when the player has control (a room, no cutscene, user input on): Room 33 (R-15.8, Proposed); other games keep the script-1 end | `BootEndsWhenPlayerHasControl` false |
| 2026-10-14 | S4b | `actorFollowCamera` of an actor in another room | keep reporting "not modelled"; run `startScene` as ScummVM's `setCameraFollows` does | modelled, but only for the generic session of v4 games (`ScummProfile.CameraFollowStartsScene`), because LOOM's sessions are pinned to identical results; script 1 ends this way | set the property false |
| 2026-10-14 | S4b | Global variables no script has written yet | "unknown" (LOOM's policy); 0 as the engine allocates them (`calloc`) | 0 for the v4 profile (800 variables); the old-v3 games keep their policy | `GlobalVariableCount` 0 |
| 2026-10-14 | S4b | The music timer the credits wait on | leave it at 0 (the boot stalls); skip the credits with Escape (a labelled assumed key); play the song's `AD` block at the browsed half tempo (S3's decision) | the AD block at half tempo, labelled an estimate: the engine's own timer is iMuse's, the credits run to their end without a skip | `ScummGameSession.AdlibLifetime` |
| 2026-10-14 | S4b | A key pressed while no room is current (room 0) | refuse (as before); deliver it | deliver: the engine reads keys in room 0 and the credits' script waits there; `PressKey` takes the null room | restore the exception in `PressKey` |
| 2026-10-15 | S5 | Which games get first-visit room views and cards (R-6.12a, widened 15 Oct), without testing the game in the App | a list of game ids in the App; a profile flag set by game id; a profile flag derived from format and dialect | the profile flag `HasFirstVisitScenes`, derived from the format: old-bundle v3 with the Indy3 dialect, or v4 dialect on several disks (no single archive); LOOM keeps its own session, the 256-colour Indy3 (room files) waits for its S4b. The App asks only the profile | restore `game.Id == GameId.IndianaJones3 && game.Version == ScummVersion.V3` in `ScummProfile.For` |
| 2026-10-15 | S5 | Stored-object list (Advanced) of a browsed v4 room with a first visit | every named stored object; only what the game's object search can select | only selectable ones (`ScummRoomScene.IsHotspot`, as Indy3 EGA, R-6.12a); without a first visit the full stored list stays | `ReadStoredV4Objects` returns the labels unfiltered |
| 2026-10-15 | S5 | The fixture disappeared mid-run | wait; work on without data; stop | committed the data-independent code (builds; Indy3/LOOM paths unchanged by construction) with this tracker marking it unverified, stopped the item (hard stop 4.4.4) | revert the S5 MonkeyFloppy commit |
