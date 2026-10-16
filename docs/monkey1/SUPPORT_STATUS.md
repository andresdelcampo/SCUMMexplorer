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

Family: MonkeyFloppy | Fixture: `Games/monkey1 ega en` (the four-disk 256-colour floppy release, SCUMM v4; the folder name says EGA, the data is VGA) | Last updated: 16 October 2026 | Owner: S6 MonkeyFloppy agent

| Stage | State | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (before this file) | Rooms | | |
| S2 Reading | done (before this file) | Costumes, Scripts, Texts | | |
| S3 Sound | done (before this file) | Sound | | polish: AdLib music at half tempo, set by ear |
| S4b Session | **done** (14 Oct) | unchanged (no cell changes in S4b) | [monkey1-session-20261014](../../.verification/monkey1-session-20261014/README.md) | boot to Room 33 in 2522 frames, unsupported list empty |
| S5 Objects | **done** (15 Oct) | Objects raised to full with S6 (16 Oct) | [monkey1-objects-20261015](../../.verification/monkey1-objects-20261015/README.md) | 81/81 rooms without a gap; objects in 30 rooms, actors in 56; 14 rooms compared at source level (no ScummVM picture) |
| S6 Play | **done** (16 Oct) | Objects and Play raised to full | [monkey1-play-20261016](../../.verification/monkey1-play-20261016/README.md) | the R-7.18 route from the dock: sentence, cutscene skip, the SCUMM Bar, the pirates' conversation, the rubber chicken; WPF check |
| S7 Cutscenes | not started | Cutscenes | | |
| S8 State | not started | Status | | requested in a run: no |
| S9 Editions | not started | – | | the eight-disk Spanish EGA release boots the same way (record); its video-mode variable is the VGA value |

Blocked on: nothing.
Next: the user checks play in the real window (record, WPF section) and the S5 rooms; then S7 Cutscenes.
Open uncertainties: the session's AdLib music timer for the credits is an estimate (iMuse in the engine); the profile gives `VAR_VIDEOMODE` 19 to every v4 game, wrong for the EGA release; the wrong-answer path of the protection (a fourth wrong answer quits) was not run. S5: no ScummVM picture compared; Room 1's first pseudo number (130) is a labelled pick; Room 39's unshown named objects not resolved one by one. S6: no ScummVM run of the route; a verb's click rectangle is its text width by the font height; the right click of the default two-button interface is delivered from 16 October (R-7.16, Rule: mode 2, the object's default verb); ScummVM's both-buttons Esc is not modelled; Ctrl+W (the game's own "win" key) is delivered.

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
| 2026-10-15 | S5 | The fixture is back (`D:\Juegos\Lucas`); merge 3b2bcf6 unverified? | revert and redo; verify and fix on top | verified on top: it built and ran for every room; fixes in a separate commit | revert the S5 fix commit |
| 2026-10-15 | S5 | Bit variables of a v4 game (the session aliased them into the word variables, so bits 0x040..0x04F rewrote `VAR_ROOM`) | keep LOOM's v3 aliasing; keep them apart as ScummVM `readVar` does from version 4 | apart for every v4 game (`ScummProfile.SeparateBitVariables`), LOOM CD already did so in its own session; v3 games unchanged | make the property false |
| 2026-10-15 | S5 | Rooms the game loads only as pseudo rooms (Room 58 as 201..220, Room 1 as 130..132) showed nothing under their own number | room-specific numbers; follow `startScene` (load the real room, `VAR_ROOM` = the pseudo number) and enter under the number a decoded arrival uses | the engine rule, in shared code (also improves Indy3 rooms 17, 18, 21, 23, 24, 33, 72); a room without a literal arrival takes its first pseudo number (seeded pick when several), labelled "Assumed pseudo room" (R-1.4) | revert `IndexedRoom`, the `VAR_ROOM` line in `EnterRoom`, `EnterWithoutEgo` and the pseudo map of `ScummEgoArrivalIndex` |
| 2026-10-15 | S5 | An arrival found in the room's own code (a maze cell leading to another) as the first visit | allow; prefer arrivals from elsewhere | arrivals from elsewhere first (a first visit comes into the room); the room's own arrivals only when nothing else exists | drop `fromElsewhere` in `VisitRoom` |
| 2026-10-15 | S5 | The ScummVM picture comparison of ten rooms | run the bundled ScummVM; source-level comparison | source level (the run request says ScummVM cannot be driven headlessly; the Indy3 attempt failed): 14 rooms with the entry branches read, listed in the record | none |
| 2026-10-16 | S6 | Which object to take on the route (the SCUMM Bar's mugs are untouchable, class 24, until the trials) | a mug after setting its class; the kitchen's meat while the cook is out; the rubber chicken of the Voodoo shop (two rooms further, free to take) | the rubber chicken: the game's own state allows it, nothing is forced (R-1.9) | change the route in `ScummMonkeyFloppyPlayTests` |
| 2026-10-16 | S6 | `VAR_CURSORSTATE`/`VAR_USERPUT` (Script 23 hovers nothing without them) | leave them 0; copy them after every `cursorCommand` as ScummVM does from version 4 | copied for every engine version 4 game (`ScummProfile.PublishesCursorVariables`); v3 has no such variables | set the property false |
| 2026-10-16 | S6 | F5 (`VAR_MAINMENU_KEY` = 0x13F, Script 1 +0x077F) | deliver it (Script 4 sends it to Script 20, which does nothing); refuse it as the engine's save/load menu | refused (R-7.11: a save/load key is not delivered), from the variable, not a key list | drop the `MainMenuKeyVariable` line of `PressPlayKey` |
| 2026-10-16 | S6 | The play cursor (S6 Indy3Vga found the v2 crosshair drawn for v3) | keep the v2 shape for v3 and draw the bitmap for v4; the bitmap for both | the bitmap cross-hair of `resetCursors` for both, as ScummVM's class tree gives it to v3 and v4; R-7.9 wording corrected | restore `DrawCrosshair` in `ScummPlayScreen` |
| 2026-10-16 | S6 | Ctrl+W (Script 4 key 0x17: "Are you sure you want to win?", then the ending) | refuse; deliver | delivered: it is the game's own key (R-7.11), not a save/load or engine key; the explorer never presses it | add 0x17 to `UndeliveredKeys` |
| 2026-10-16 | S6 | Right click in play. User, 16 Oct: "Right click goes to the game in verb-based games, as in ScummVM; keep the Interact menu elsewhere.", then "why not use right click only in the games that need it? -instead of everywhere?" | deliver in every verb-based game; deliver only where the game's interface reads it, decided by a family test / by engine version data | only where it reads it, by data: `ScummProfile.RightClickReachesGame` = play through verbs and engine version 4 or later (ScummVM master `checkExecVerbs` passes mode 2 only from version 4; up to 3 a right press is Esc outside LOOM, already the app's Skip). Monkey's Script 4 +0x0005 `isEqual local[0x002], 2` reads it (the two-button default verb, +0x0016). The Interact menu (R-6.9) is LOOM-session only and was never offered in Play, so nothing moved; its **Interact ▴** button stays its visible entry point | set `RightClickReachesGame` false |
