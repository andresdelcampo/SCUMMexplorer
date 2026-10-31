# Current work

The current state only: how ready each game is, what is open, the rules. Finished work leaves this file as soon as it is done: its result goes to [CHANGELOG](CHANGELOG.md) (with its record under `.verification/`), and its history to `docs/archive/`. Earlier versions of this file: [archive/CURRENT_WORK_BEFORE_TIDY_2026-10-31.md](archive/CURRENT_WORK_BEFORE_TIDY_2026-10-31.md), [archive/CURRENT_WORK_LOG_THROUGH_2026-10-28.md](archive/CURRENT_WORK_LOG_THROUGH_2026-10-28.md). The plan and the per-game board are in [ROADMAP](ROADMAP.md).

## Scope

REQUIREMENTS R-1.14 and §8.6 (user, 31 October 2026): an explorer. Pick any room and trigger any of its interactions and cutscenes at any time, without playing to them; teleports and magic costume changes are fine; nothing is saved or loaded; the game's rules never forbid a pick. Playing through the game's own interface stays as it is and comes last.

## Readiness

| Family | State |
|---|---|
| LOOM EGA English (Roland), other EGA editions, demos, LOOM CD | Reference quality: live interaction, actors, scripts, scene playback, Game Status, audio. Interact lists every object of the room. Cutscenes ◐ except the English reference's hand-validated catalog. |
| Indy 3, Monkey Island 1 (floppy, CD), Monkey Island 2, Indy 4 | Every room, costume and script; every listed scene begins when picked (its state set up, or started at its cutscene); Interact offers every object of the room with its verbs. Some scenes started at their cutscene show nothing; sound not yet listened to by the user. |
| Maniac Mansion and Zak McKracken (enhanced, classic) | Played through native verbs, Status, inventory editor, room controls, cutscene lists (◐). Separate track ([handoff](../NEXT_SESSION_PROMPT_MANIAC_ZAK.md)); the 31 October scope is not yet applied there. |
| Other folders under `Games` | Browse-only or passed only S9 checks; not reviewed in the window. |

Architecture: LOOM-specific App code lives in `App/Loom/` behind `IGameModule`. Still LOOM-keyed in shared code: `DecodeBrowsedOldV3Room`, the session toggle, the live-session sound/clock code, the gallery's speaker names; every such move needs a before/after run of `.verification/game-module-20261026/Check`.

## Open

### All games

- **User-reported, not yet reproduced:** thin black rooms (game and room not named); a crash (the crash log in `%LOCALAPPDATA%\SCUMMexplorer\crash-logs` records the next one).
- Scenes started directly at their cutscene that show nothing (Indy 3 10, Monkey Island 1 10, Monkey Island 2 24, Indy 4 35): they need the locals or state the skipped tests set ([record](../.verification/scene-setup-20261031/README.md)).
- The background scene derivation takes about a minute on the large games (it now also works out each scene's setup).
- Browsed games' object note and status text vary between runs of the same build.
- Static previews such as MI2's campfire: a hint is needed (decision 2 in [USER_CHECKS](USER_CHECKS.md)).
- Depth masks and stored object images of the non-LOOM games (R-6.16) not compared with ScummVM.

### Per game

| Game | Open |
|---|---|
| **Indy 3** | 16 × 16 cross-hair from ScummVM; the drawn one differs (decision 4). `printEgo` lines as first texts (decision 5). Rooms 47, 60, 66 start dark and are shown lit. |
| **Monkey Island 1** | Spanish eight-disk and CD language folders passed S9 only. |
| **Monkey Island 2** | The user's retest of Play Game Intro. iMUSE hooks are labelled estimates. |
| **Indy 4** | Talkie speech not yet heard by the user. |
| **Maniac Mansion / Zak** | Black rectangles, Zak camera placement, old-actor alignment, shown-lit previews; native darkness/flashlight, flight timer and bus approaches; Zak classic 11 estimated palettes. |
| **LOOM** | Shaping's canonical label, reverse draft names and story roles uncertain. Room 34 standalone fortress reveal (local 200 +0035). Room 2 skipped cutscene could leave Bobbin small (not reproduced). Room 15/18/20 staff notes unconfirmed by the user. CD picture/voice/music timing is the user's manual run. |

## Release readiness (LOOM first)

Ready: editions recognised from the bytes of specific LFL files (R-3.1, R-3.9), first-launch scan with percentage, LICENSE (GPL-3.0-or-later), player README.

Open:
1. Each edition seen once in the real window (Italian accents, plain English, the two demos, the Roland copies).
2. Release build and packaging: publish settings, version number, distribution (zip, framework-dependent or self-contained).
3. First run on a clean profile; Roland without ROMs fails gracefully.
4. Per-edition test expectations if editions are added (`known-editions.json`, `.verification/known-editions/Make`).

## Product rules

- Rooms begin in their earliest state; the running session carries changes caused by real scenes. Game Status is under user control. A scene adds only its minimum prerequisites and preserves later state.
- A scene or interaction the user picks may get the state its own code tests (teleport, costume, items, flags, an object brought into the room), derived generically from its scripts and listed in Advanced (R-1.9, §8.6). Rooms' first visits and Play never force anything. No room-specific engine hacks; label estimates and picker assumptions.
- Normal mode shows artwork, game text and concise controls; technical details belong in Advanced. Preserve edition differences, palettes and verified source glitches.
- Source execution, prepared checks and original-game observation are distinct evidence. A scene completing does not prove correct actors, prerequisites or timing.
- Compatibility checks cover plausible version differences for the changed feature; reuse the [edition analysis](../.verification/editions-20261005/README.md). English Roland stays the LOOM reference.

## Keeping the documents tidy

Rule (user, 31 October 2026): current-state documents (this file, [ROADMAP](ROADMAP.md), [USER_CHECKS](USER_CHECKS.md), `NEXT_SESSION_PROMPT.md`, the per-game trackers) hold only the readiness state and what is open. When a piece of work is done, in the same commit: its result goes to [CHANGELOG](CHANGELOG.md) (one entry, linking its `.verification/<topic>/` record), its line leaves the current-state documents (no "done", "fixed", "closed" or struck-through items left behind), and anything worth keeping that is not in the CHANGELOG or the record goes to `docs/archive/`. The CHANGELOG keeps about two days; older entries move to `docs/archive/CHANGELOG_THROUGH_<date>.md`.

## Preservation and workflow

Read [CLAUDE.md](../CLAUDE.md) for layout, engine facts and build instructions; all shell commands go through RTK. Preserve game data, ROMs, saves, running applications and `%LOCALAPPDATA%\SCUMMexplorer`. Use focused checks and isolated output/profile directories. Commit finished tasks separately with explicit paths. [Verification index](../.verification/README.md).

## Parked

M5/secrets guide, Indy expansion/later games, demo carrier rooms, optional General MIDI/card archaeology, Status windows for Indy 3/MI1/MI2/Indy 4, new Play features, session save/load ([historical review](archive/CLEANUP_REVIEW.md)).
