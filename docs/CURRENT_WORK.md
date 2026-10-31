# Current work

Rewritten 28 October 2026. This is the single plan, known-gaps list and session handoff. Finished work and its validation are in [CHANGELOG](CHANGELOG.md); the long per-checkpoint status log that used to fill this file (14–26 October, S4a to S9, Maniac/Zak checkpoints) is kept verbatim in [archive/CURRENT_WORK_LOG_THROUGH_2026-10-28.md](archive/CURRENT_WORK_LOG_THROUGH_2026-10-28.md) and only needs digging for history. Per-game detail lives in the trackers (`docs/<game>/SUPPORT_STATUS.md`); the support matrix is generated from `GameSupport` (README, REQUIREMENTS).

## Where things stand

| Family | State |
|---|---|
| LOOM EGA English (Roland), other EGA editions, demos, LOOM CD | Reference quality: live interaction, actors, scripts, scene playback, Game Status, audio. Cutscenes ◐ except the English reference's hand-validated catalog. |
| Indy 3 (EGA, 256 colours), Monkey Island 1 (floppy, CD), Monkey Island 2, Indy 4 | Generic session `ScummGameSession`: first-visit rooms, played through their own verbs, sound, script-derived cutscene lists (◐). Staged S4b to S9 work done 14–20 October. |
| Maniac Mansion and Zak McKracken (enhanced, classic) | Played through native verbs, Status, inventory editor, room controls, cutscene lists (◐). Run by a separate orchestrator ([handoff](../NEXT_SESSION_PROMPT_MANIAC_ZAK.md)); Zak classic Costumes ◐. |
| Other folders under `Games` (Spanish/other-language Indy 4 and MI editions, Indy 3 demo, LOOM Kixx) | Open as browse-only or passed only S9 checks; not individually reviewed in the window. |

Architecture (26–28 October): LOOM-specific App code moved to `App/Loom/` behind `IGameModule` (`GameModuleBase`, `GameModules`, `RoomSource`/`RoomLoad`, `IGameVoice`). A new game's module derives from `GameModuleBase` and is added to `GameModules`. Still LOOM-keyed in shared code: `DecodeBrowsedOldV3Room`, the session toggle, the live-session sound/clock code, the gallery's speaker names. They move to module hooks with the second live EGA game. Every such move needs a before/after run of `.verification/game-module-20261026/Check`.

## Known gaps (open points)

Everything here is open. Items marked **user-reported** come from the user's own play on 28 October and are **not yet investigated or reproduced**; a list of more issues is coming from the user's testing.

### All games / unspecified

- **User-reported:** some games showed thin black rooms (rooms with black bands or gaps). Which games and rooms is not yet recorded.
- **User-reported:** a crash with no stack trace captured. Cause unknown. Next time: capture the trace (harness failures log under `Exe/HarnessFailures/`; the shipped app has no crash log).
- Browsed games' object note and status text vary between runs of the same build (race with the background first visit; Zak, MI2 seen). Not new.
- Cutscenes are ◐ for every game except LOOM EGA English: lists are script-derived, not validated against the original game. Open: check the CD's run-based category against the bundled ScummVM (19 of 98 scenes called dialogue).
- Static previews such as MI2's campfire: a hint is needed (R-8.5b vs R-5.3a); the user must decide.
- Requirements R-7.7 to R-7.18, R-3.15, R-15.7, R-15.8, R-8.32, R-8.33 and R-12.7 to R-12.10 are still Proposed, awaiting the user's confirmation.
- Depth masks and stored object images of the browsed games (R-6.16) not yet compared with ScummVM.
- Performance (31 October, [record](../.verification/indy4-rooms-20261030/README.md)): first launch fixed. Indy 4 shows play at 4.5 s (87 s before), MI2 at 4.2 s; the UI is blocked about 2 s in total. Causes: room reads rescanning the game on the UI thread, and the talkie speech walk opening `MONSTER.SOU` thousands of times. The background cutscene derivation still takes about a minute.

### Per game

| Game | Open |
|---|---|
| **Monkey Island 2** | Fixed 30 October, retest pending: in Play Game Intro the floppy copies' recipe screen lost the recipe's name and ingredients (strings inside strings) and the prompt ran off screen (no talk-line wrapping from version 4 on); both are engine fixes ([record](../.verification/mi2-intro-protection-20261030/README.md)). The player still needs the code wheel. |
| **Monkey Island 1 CD** | Done 30 October ([record](../.verification/monkeycd-rooms-20261030/README.md)): Room 88 (empty entry block) and 89 run with nothing unfollowed; Room 99 is an icon-holder room the game never enters (objects past the 1000-object table), refused with that reason; the lookout frame at start no longer occurs. |
| **Monkey Island 1** (all) | Spanish eight-disk and CD language folders passed S9 only; no real-window look at every edition. |
| **Indy 4** | 30 October ([record](../.verification/indy4-rooms-20261030/README.md)): Rooms 76, 77, 87 run with nothing unfollowed (a walk with no box route now ends where it started, as ScummVM's `walkActor`); the play view's first-state images and Room 4's magenta line are no longer seen. CD has no audio tracks (data). |
| **Indy 3** | Gate A closed 31 October (held objects' verbs in any room). Play uses a 16 × 16 cross-hair from ScummVM; the drawn one differs. Offer: count `printEgo` lines as first texts (changes text columns). |
| **Maniac Mansion / Zak** | Retained black rectangles, Zak camera placement, old-actor alignment and shown-lit previews in S5 (original-game visual acceptance open). Native darkness/flashlight, flight timer and bus approaches, and several unproved paths. Zak classic: 24 derived / 11 estimated palettes, Costumes ◐. Preparation to reach states is labelled exploration, not earned play. |
| **v5 audio** | Script-driven hooks beyond those followed; speech and named tracks not yet heard/seen by the user. |
| **LOOM** | Shaping's canonical label, exact reverse draft names and unidentified story roles remain uncertain. Room 34 standalone fortress reveal unresolved (local 200 +0035). Room 2 skipped cutscene could leave Bobbin small (not reproduced). Room 15/18/20 staff notes (fix applied 11 October, still unconfirmed by the user). CD picture/voice/music timing has no automatable check; it belongs to the user's manual run. |

## Release readiness (LOOM and all its variants)

Done: editions recognised from the bytes of specific LFL files (R-3.1, R-3.9), first-launch scan with percentage, Italian review, LICENSE (GPL-3.0-or-later) and player README.

Open before shipping:
1. Look at each edition in the real window (Italian, plain English, the two demos, the Roland copies); Italian accents on screen are unseen.
2. Release build and packaging: no publish settings, version number or installer; decide the distribution (zip, framework-dependent or self-contained).
3. First run on a clean profile (user will try): no last folder, no MT-32 ROM folder, welcome text; Roland must fail gracefully without ROMs.
4. Update the unit tests' per-edition expectations if more editions are added (`known-editions.json`, `dotnet run --project .verification/known-editions/Make`).

## Scope (31 October 2026)

The user narrowed the product (REQUIREMENTS R-1.14, §8.6; [ROADMAP](ROADMAP.md)): pick any room and trigger its interactions and cutscenes at any time without playing to them; teleports and magic costume changes are fine; nothing is saved or loaded; Play stays as it is and comes last. Scene setup of §8.6: Indy 3 done 31 October (all 153 groups begin when picked; [record](../.verification/scene-setup-20261031/README.md)); next Monkey Island 1, 2, Indy 4, then interactions (R-8.38). The roadmap board now follows the user's ten features.

## Next work

Follow [ROADMAP](ROADMAP.md) "The work, in order" (scene setup for every game first), plus the user's reports as they arrive. Older candidates:
1. Capture and fix the crash and the black-rooms report once the user names the game and room.
2. ~~Monkey Island 2 copy-protection / opening-cutscene blocking~~ (fixed 30 October; retest in USER_CHECKS).
3. ~~Known MI CD and Indy 4 room items~~ (done 30 October).
4. Release readiness list.

## Product rules

- Rooms begin in their earliest state; the running session carries changes caused by real scenes. Game Status is under user control. A scene adds only its minimum prerequisites and preserves later state. Single picks keep the session; explicit sequences start fresh; Restart returns to boot.
- Cutscene launches set missing story state, actors, room state and costume (REQUIREMENTS 8.5, R-8.26 to R-8.31); `LoomCutsceneReadiness` implements this partly for LOOM. The approved late-room exception runs reviewed actual source prerequisites when absent (the fingerprinted English Room 46 recipe); do not generalize it by guessing story bits.
- Costumes persist until source changes them. Derive actor look, placement and scene starts from own-edition scripts; no room-specific engine hacks. Rooms' first visits and Play never force costumes, flags or positions; a scene or interaction the user picks may (teleport, dress, give items, set the flags its code tests: R-1.9, §8.6), generically and listed in Advanced. Label estimates and picker assumptions.
- Single click walks/uses through source scripts; double click also looks. Stored-art cycling is inspection only. Source responses and touchability govern navigation.
- Normal mode shows artwork, game text and concise controls. Technical details belong in Advanced. Preserve edition differences, palettes and verified source glitches; keep window subtitles short.
- Source execution, prepared Core/WPF checks and original-game observation are distinct evidence. A scene completing does not prove correct actors, earned prerequisites or timing.
- Compatibility checks cover plausible version differences for the changed feature (detection, formats/scripts, text/fonts, graphics or audio); reuse the [edition analysis](../.verification/editions-20261005/README.md) and pick only relevant checks. English Roland stays the LOOM reference.

## Preservation and workflow

Read [CLAUDE.md](../CLAUDE.md) for layout, engine facts and build instructions; all shell commands go through RTK. Preserve game data, ROMs, saves, running applications and `%LOCALAPPDATA%\SCUMMexplorer`. Use focused checks and isolated output/profile directories; do not repeat broad audits without a relevant failure. Keep failed evidence and uncertainty. [Verification index](../.verification/README.md).

Commit finished tasks separately with explicit paths. Update this file for current state and CHANGELOG for completed results. Create or message another task only with direct human authorization.

## Parked

M5/secrets guide, Indy expansion/later games, demo carrier rooms, broad performance, optional General MIDI/card archaeology, cleanup/worktree proposals ([historical review](archive/CLEANUP_REVIEW.md)). Earlier-session detail on v5 audio, depth masks and browse-only games is in the archived log.
