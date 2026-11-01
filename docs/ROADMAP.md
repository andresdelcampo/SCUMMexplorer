# Roadmap

The one current-state document: the scope, how ready each game is, the ten features and their board, what is open, the work in order, the checks only the user can do, and the rules. Only the current state and open work are here. Finished work is in [CHANGELOG](archive/CHANGELOG.md) and its records under `.verification/`; history is in `docs/archive/` (the former ROADMAP, "Your checks" and earlier roadmaps). What the app must do is in [REQUIREMENTS](REQUIREMENTS.md); how a game is added, in [GAME_SUPPORT_INSTRUCTIONS](GAME_SUPPORT_INSTRUCTIONS.md).

## Scope

REQUIREMENTS R-1.14 and §8.6 (user, 31 October 2026): an explorer. Pick any room and trigger any of its interactions and cutscenes at any time, without playing to them; teleports and magic costume changes are fine; nothing is saved or loaded; the game's rules never forbid a pick. Playing through the game's own interface stays as it is and comes last.

## Readiness

| Family | State |
|---|---|
| LOOM EGA English (Roland), other EGA editions, demos, LOOM CD | Reference quality: live interaction, actors, scripts, scene playback, Game Status, audio. Interact lists every object of the room. Cutscenes ◐ except the English reference's hand-validated catalog. |
| Indy 3, Monkey Island 1 (floppy, CD), Monkey Island 2, Indy 4 | Every room, costume and script; every listed scene begins when picked (its state set up, or started at its cutscene); Interact offers every object of the room with its verbs. Some scenes started at their cutscene show nothing. |
| Maniac Mansion and Zak McKracken (enhanced, classic) | Played through native verbs, Status, inventory editor, room controls, cutscene lists (◐). Separate track ([handoff](../NEXT_SESSION_PROMPT_MANIAC_ZAK.md)); the 31 October scope is not yet applied there. |
| Other folders under `Games` | Browse-only or passed only S9 checks; not reviewed in the window. |

Architecture: LOOM-specific App code lives in `App/Loom/` behind `IGameModule`. Still LOOM-keyed in shared code: `DecodeBrowsedOldV3Room`, the session toggle, the live-session sound/clock code, the gallery's speaker names; every such move needs a before/after run of `.verification/game-module-20261026/Check`.

## The ten features

| # | Feature | Done when |
|---|---|---|
| 1 | **Rooms** | Every room is in the room selector and opens in its first-visit state, no gaps. |
| 2 | **Costumes** | Every costume is read and shown in the game's palette. |
| 3 | **Sounds** | Music, effects and CD tracks play on the game's sound devices; the user has listened. |
| 4 | **Scripts available** | Every script can be opened. |
| 5 | **Cutscenes** | Every cutscene of a room plays from that room when picked, and the game's intro through Play Game Intro (§8.6, R-8.35). |
| 6 | **Interactions** | Interact offers every object of the room, present or not yet, with every verb that has code, and plays it (R-8.38). |
| 7 | **Room sounds** | The room's Sounds button plays the sounds and music its scripts use (R-6.40). |
| 8 | **Hotspots, doorways, paths, depth** | Overlays, walk boxes and depth masks right for the room. |
| 9 | **Game state** | A Status window (§12). Parked for Indy 3, Monkey Island 1 and 2, Indy 4: a picked scene sets what it needs. |
| 10 | **Readable scripts** | Scripts read as plain language with names for the game's variables, verbs and objects. |

✅ done · 🟡 works, partial or not checked by the user · 🔴 known gap · — parked.

## The board

| Game | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| LOOM EGA English (reference) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| LOOM EGA other editions, demos | ✅ | ✅ | ✅ | ✅ | 🟡 | 🟡 | ✅ | ✅ | ✅ | ✅ |
| LOOM CD | ✅ | ✅ | ✅ | ✅ | 🟡 | 🟡 | ✅ | ✅ | ✅ | ✅ |
| Indiana Jones 3 (EGA, 256 colours) | ✅ | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | 🟡 | — | 🟡 |
| Monkey Island 1 (floppy, CD) | ✅ | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | 🟡 | — | 🟡 |
| Monkey Island 2 | ✅ | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | 🟡 | — | 🟡 |
| Indiana Jones 4 | ✅ | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | 🟡 | — | 🟡 |
| Maniac Mansion (classic, enhanced) | ✅ | ✅ | ✅ | ✅ | 🟡 | 🟡 | ✅ | 🟡 | ✅ | 🟡 |
| Zak McKracken (classic, enhanced) | ✅ | 🟡 | ✅ | ✅ | 🟡 | 🟡 | ✅ | 🟡 | ✅ | 🟡 |

What keeps a cell from ✅:
- **5**: every scene begins when picked; started directly at their cutscene (not the game's own way): Indy 3 5.9%, Monkey Island 1 8.5% (floppy) and 8.1% (CD), Monkey Island 2 7.9%, Indy 4 4.7% (6.5% of all five copies), some of them showing nothing, no line and no other room (Indy 3 3, Monkey Island 1 1 and 1, Monkey Island 2 1, Indy 4 5); LOOM's other editions and CD (on the shared engine) start 0.9% (EGA) and 0.4% (CD) of their scenes at the cutscene; Maniac Mansion and Zak McKracken (on the shared scene setup since 1 November) begin every scene, 33% (Maniac, both) and 30% (Zak, both) started at the cutscene.
- **6**: LOOM's other editions and CD share the reference's code but were not looked at; Maniac/Zak keep their own Interact. Verbs a game never names show as "Action".
- **8**: depth masks of the non-LOOM games never compared with ScummVM (R-6.16).
- **10**: the other games name objects, actors and verbs from their own data (80%, 64% and 57% of references); their variables and bits keep numbers (the script maps hold few rows to turn into data; most of what stays unnamed has no name in the game's data: image-only objects, actors no script names).

## The work, in order

| # | Work | Feature | Where |
|---|---|---|---|
| 1 | **Scenes started at their cutscene that show nothing**: what is left needs case-by-case reading: scenes that wait on two values at once (Indy 4 Room 44's local script 203: a bit, then the hero's walk box and x), script arguments (Indy 3 Room 13's entry needs Script 52's argument), refusals the scan lists as scenes (Indy 4 Room 88's "I can't reach it", a `printEgo` while Indy is not in the room; its many "lines" are a room script's blank prints); Indy 4 has the most, 5. | 5 | `ScummSession.StartSceneAt`, `ScummSceneSetupSearch` |
| 2b | **One scene engine for every game** (user, 31 October): Maniac/Zak (`ManiacCutsceneSetup`, built on the separate track) onto the shared scene setup (`ScummSceneSetupSearch`), keeping a game-specific setup only where the game needs one (LOOM English's reviewed catalog, Maniac's party selection). LOOM English's 328 reviewed replays are the answer key (first run 31 October: 238 of 238 listed replays begin and reach the catalog's rooms, 219 share its category; the other 90 are shared draft reactions; [record](../.verification/loom-answer-key-20261031/README.md)); scene length within 10% for 217 of 238 (91.2%, on LOOM's own session); LOOM's other editions and the CD play their scanned scenes through the shared engine, and every LOOM copy's Interact offers the draft reactions ([record](../.verification/loom-editions-20261031/README.md)); still to do: the remaining length differences, then retire the catalog to titles, draft names and the named sequences; Maniac/Zak: run the shared engine on the English reference and compare each replay (begins, lines, rooms, Bobbin's look); where it matches, the catalog entry is redundant, where it differs, fix the shared engine; in the end only the named sequences (The Elders' trial, the firewood scene, Walk to Room 13, Room 46) stay as reviewed data, carried to the other LOOM editions by room, object name and verb. Before/after counts per game. | 5 | `Loom/Scenes/`, `Core/Maniac/`, `Scumm/Session/` |
| 3 | **Readable names for the other games**: objects, actors and verbs are named from each game's data ([record](../.verification/readable-names-20261031/README.md)); still to do: variables and bits from their script maps (reviewed rows as data, as LOOM's glossary). | 10 | `LoomGlossary`, `LoomScriptPresenter`, `docs/<game>/*_SCRIPT_MAP.md` |
| 3b | **Interaction outcomes by state** (user, 1 November: revisit after the user's test pass): one verb can answer differently by state (locked/open, conversations had, the second object); today one outcome is played. First step: list each verb's outcomes with their conditions in Advanced (the guard reading already walks the code). | 6 | `ScummSceneSetupSearch.Paths.cs`, Interact |
| 4 | **The user's reports** as they arrive (the crash log records game and room). | 1 | — |
| 5 | **One user pass per game** with "Your checks" below: a few rooms next to ScummVM, each copy opened once. | 3, sign-off | — |
| 6 | **Release**, LOOM first: packaging, version, clean-profile first run, Roland without ROMs. | — | `src/SCUMMexplorer.App` |

Optional, when the code is next touched: a ScummVM picture capture, splitting `MainWindow.xaml.cs`, profile allow-lists as defaults, worktree housekeeping.

## Open

### All games
- **User-reported, not yet reproduced:** thin black rooms (game and room not named); a crash (the crash log in `%LOCALAPPDATA%\SCUMMexplorer\crash-logs` records the next one).
- Scenes started directly at their cutscene that show nothing (work item 1; [record](../.verification/scene-setup-20261031/README.md)).
- Browsed games' object note and status text vary between runs of the same build.
- Static previews such as MI2's campfire: a hint is needed (decision 2 below).
- Depth masks and stored object images of the non-LOOM games (R-6.16) not compared with ScummVM.

### LOOM EGA English (reference)
- Your checks: Room 2 skipped cutscene leaving Bobbin small (not reproduced); Rooms 15/18/20 staff notes; Room 34 standalone fortress reveal (local 200 +0035).
- Shaping's canonical label, reverse draft names and story roles: settle or accept as labelled. Release (work item 6).

### LOOM EGA other editions, demos, LOOM CD
- Each copy opened once in the window (Italian accents, plain English, French, German, v1.2, both demos).
- CD: the scene categories against ScummVM (19 of 98 scenes called dialogue); your picture/voice/music timing run.

### Indiana Jones 3 (EGA and 256 colours)
- 4 scenes started at their cutscene show nothing (work item 1).
- Rooms 47, 60, 66 start dark and are shown lit. The 16 × 16 cross-hair (decision 4); `printEgo` lines as first texts (decision 5).

### Monkey Island 1 (floppy and CD)
- 1 (floppy) and 1 (CD) scene started at its cutscene shows nothing (work item 1).
- `VAR_VIDEOMODE` is 19 for every v4 game, wrong for the EGA release.
- Spanish eight-disk copy and the CD's five languages in the window (passed S9 headless only).

### Monkey Island 2
- 1 scene started at its cutscene shows nothing (work item 1).
- Your retest of Play Game Intro; decision 2 (the campfire room). iMUSE hooks are labelled estimates.

### Indiana Jones 4
- 5 scenes started at their cutscene show nothing (work item 1).
- Spanish talkie and floppy in the window.

### Maniac Mansion and Zak McKracken (classic, enhanced)
Separate track ([handoff](../NEXT_SESSION_PROMPT_MANIAC_ZAK.md)); the 31 October scope applies once the user tells that track.
- Scenes started at their cutscene (Maniac 9 of 27, Zak 16 of 54; work item 1; Maniac's timer loops, Scripts 139 and 141, set a flag, clear it on each failed condition and loop until all hold); a Maniac pick in the window waits for the party selection (the startup rule), so it is checked headless only.
- Zak Room 19 and similar never settle; black regions (Maniac classic Room 18, Zak classic 44, enhanced 13, 16, 44); Zak camera in Rooms 3 and 33; old-actor offsets; shown-lit previews; native darkness/flashlight, flight timer and bus approaches; Zak classic's 11 estimated colours.

## Release readiness (LOOM first)

Ready: editions recognised from the bytes of specific LFL files (R-3.1, R-3.9), first-launch scan with percentage, LICENSE (GPL-3.0-or-later), player README.

Open:
1. Each edition seen once in the real window (Italian accents, plain English, the two demos, the Roland copies).
2. Release build and packaging: publish settings, version number, distribution (zip, framework-dependent or self-contained).
3. First run on a clean profile; Roland without ROMs fails gracefully.
4. Per-edition test expectations if editions are added (`known-editions.json`, `.verification/known-editions/Make`).

## Your checks

The checks only you can do, one short list per game, gathered from about 115 evidence records that each ended with their own "user checks". Each line links the record that has the exact steps. When you do one, write the date and the result in its last column (or tell the session); a failing one becomes an open item in this file.

Game folders are under `D:\Juegos\Lucas`. Roland needs the ROMs in `mt32`. If the app crashes, the log is in `%LOCALAPPDATA%\SCUMMexplorer\crash-logs` (a forced close or a crash no handler saw is logged as `…-unclean-exit.log` at the next start).

### Decisions waiting for you

Decisions 1, 3 and 6 were answered by the scope decision of 31 October (REQUIREMENTS R-1.14); the numbers of the others stay as they were.

| # | Question | Where it comes from | Answer |
|---|---|---|---|
| 2 | A picked room whose entry leaves it (MI2's campfire, Room 4): a Normal-mode hint pointing to Play Game Intro, or let the boot room play its settled boot state? | R-8.5b vs R-5.3a | |
| 4 | Indy 3's cursor: ScummVM's 16 × 16 cross-hair is used; the game draws a different one. Acceptable? | R-1.5, Indy 3 tracker | |
| 5 | Indy 3: count `printEgo` lines as first texts (changes the text columns)? | ROADMAP | |
| 7 | Indy 3 castle Rooms 20, 28, 32: guards can start a confrontation at any moment, so the catalog shows a still picture (R-8.5b). Make such rooms live anyway? | [record](../.verification/indy3-castle-20261030/README.md) | |

### LOOM

| Check | Steps | Result |
|---|---|---|
| Room 2: skip the cutscene, is Bobbin the right size afterwards? | ROADMAP, known gaps | |
| Rooms 15, 18, 20: staff notes after the 11 October fix | ROADMAP | |
| Each other EGA copy once in the window: Italian (accents), plain English, French, German, v1.2, both demos | ROADMAP, release list | |
| LOOM CD: picture, voice and music timing in a few scenes | ROADMAP | |
| LOOM CD intro: "LucasArts Entertainment Company" now shows on two lines (ScummVM's v4 wrapping rule); same as the real game? | [record](../.verification/mi2-intro-protection-20261030/README.md) | |
| First run on a clean profile; Roland with no ROM folder | ROADMAP, release list | |

### Indiana Jones 3 (EGA and 256 colours)

| Check | Steps | Result |
|---|---|---|
| Room 73 · Turn off light switch, Room 22 · Entry, Room 80 · Entry: start as in the game? Esc and Space work? | [cutscene record](../.verification/indy3-cutscenes-20261017/README.md) | |
| Rooms 20, 28, 32: the castle's cut-away plan shows only the section around Indy (decoded, Script 25). Does the real game look the same? | [record](../.verification/indy3-castle-20261030/README.md) | |
| Rooms 6, 18, 78, 80: their pictures now come from the moment their scene leaves them (the game follows Indy into the next room). Do they look right? | [record](../.verification/indy3-rooms-20261030/README.md) | |
| The same in the 256-colour copy, and the Spanish copies' accents | [editions](../.verification/indy3-editions-20261017/README.md) | |

### Monkey Island 1 floppy

| Check | Steps | Result |
|---|---|---|
| Room 33 · Look at poster, Room 37 · Entry, Room 19 · Entry | [cutscene record](../.verification/monkey1-cutscenes-20261017/README.md) | |
| Spanish eight-disk copy in play | [editions](../.verification/monkey1-editions-20261017/README.md) | |

### Monkey Island 1 CD

| Check | Steps | Result |
|---|---|---|
| Room 38 to the dock: CD track 22 loops, each music stops when you leave, door creak plays | [play record](../.verification/monkeycd-play-20261017/README.md) | |
| Verb colours, rubber-chicken icon (Room 29) and cursor next to ScummVM; right click on the poster | [play record](../.verification/monkeycd-play-20261017/README.md) | |
| Room 19 · Entry (spoken text, disc music), Room 37 · Entry | [cutscene record](../.verification/monkeycd-cutscenes-20261017/README.md) | |
| French, German, Italian, Spanish in the window (language picker) | [editions](../.verification/monkeycd-editions-20261019/README.md) | |

### Monkey Island 2

| Check | Steps | Result |
|---|---|---|
| **Retest (31 Oct):** a floppy copy (`mi2 mt32 en`), Play Game Intro: the protection screen passes by itself (no code wheel), you choose Monkey Island 2 or Lite, the opening plays | [record](../.verification/mi2-intro-protection-20261030/README.md) | |
| Long talk lines in LOOM CD, Monkey Island and Indy 4 now wrap as in the game (version 4 and later): any line that looks wrongly broken? | [record](../.verification/mi2-intro-protection-20261030/README.md) | |
| Roland and AdLib: Woodtick music (107) loops, pauses with Space; Largo's theme (151) at the bridge | [play record](../.verification/monkey2-play-20261017/README.md) | |
| Route next to ScummVM: sign, dialogue order, inventory icon, verb colours; right click on the sign | [play record](../.verification/monkey2-play-20261017/README.md) | |
| Room 74 · voodoo key (LeChuck on the beat?), Room 13 · Entry, Room 09 · Talk to barkeeper | [cutscene record](../.verification/monkey2-cutscenes-20261018/README.md) | |
| Spanish floppy: recipe screen passes, Spanish verbs with accents | [editions](../.verification/monkey2-editions-20261018/README.md) | |

### Indiana Jones 4

| Check | Steps | Result |
|---|---|---|
| Speech: skip the credits, Smith's scene, the ticket taker; each line spoken once, text stays until the voice ends | [play record](../.verification/indy4-play-20261017/README.md) | |
| Verb area, dialogue lines and newspaper icon next to ScummVM; statue and fire-escape route side by side | [play record](../.verification/indy4-play-20261017/README.md) | |
| Room 11 · Entry (Sophia's lecture), Room 82 · Entry; titles use the game's verbs | [cutscene record](../.verification/indy4-cutscenes-20261018/README.md) | |
| Room 4 after the boot: is the magenta line there? | [Indy 4 tracker](archive/indy4/SUPPORT_STATUS.md) | |
| Spanish talkie and floppy in the window | [editions](../.verification/indy4-editions-20261018/README.md) | |

### Maniac Mansion and Zak McKracken

| Check | Steps | Result |
|---|---|---|
| Maniac classic: Syd, Bernard, Start, skip the intro, front door and doormat route | [play record](../.verification/maniacclassic-play-20261017/README.md) | |
| Classic Maniac Room 18 (six black rectangles), Zak classic Room 44, enhanced 13, 16, 44: black in the real game too? | [S5 checkpoint](archive/checkpoints/20261017-S5-ManiacZak.md) | |
| Zak Rooms 3 and 33: camera position; Room 19: still picture after about 25 s | [S5 checkpoint](archive/checkpoints/20261017-S5-ManiacZak.md), NEXT_SESSION_PROMPT | |
| Zak classic costumes with estimated colours: do they look right? | [palette checkpoint](archive/checkpoints/20261019-Palettes-ManiacZak.md) | |
| Advanced scrolling in Maniac enhanced: is it sluggish? | NEXT_SESSION_PROMPT | |

## Product rules

- Rooms begin in their earliest state; the running session carries changes caused by real scenes. Game Status is under user control. A scene adds only its minimum prerequisites and preserves later state.
- A scene or interaction the user picks may get the state its own code tests (teleport, costume, items, flags, an object brought into the room), derived generically from its scripts and listed in Advanced (R-1.9, §8.6). Rooms' first visits and Play never force anything. No room-specific engine hacks; label estimates and picker assumptions.
- Normal mode shows artwork, game text and concise controls; technical details belong in Advanced. Preserve edition differences, palettes and verified source glitches.
- Source execution, prepared checks and original-game observation are distinct evidence. A scene completing does not prove correct actors, prerequisites or timing.
- Compatibility checks cover plausible version differences for the changed feature; reuse the [edition analysis](../.verification/editions-20261005/README.md). English Roland stays the LOOM reference.

## Keeping the documents tidy

Rule (user, 31 October 2026): this file and `NEXT_SESSION_PROMPT.md` hold only the readiness state and what is open. When a piece of work is done, in the same commit: its result goes to [the changelog](archive/CHANGELOG.md) (one entry, linking its `.verification/<topic>/` record), its lines leave this file and the handoff (no "done", "fixed" or struck-through items left behind), and anything else worth keeping goes to `docs/archive/`. The `docs/` root keeps four files: README, ROADMAP, REQUIREMENTS, GAME_SUPPORT_INSTRUCTIONS.

## Preservation and workflow

Read [CLAUDE.md](../CLAUDE.md) for layout, engine facts and build instructions; all shell commands go through RTK. Preserve game data, ROMs, saves, running applications and `%LOCALAPPDATA%\SCUMMexplorer`. Use focused checks and isolated output/profile directories. Commit finished tasks separately with explicit paths. [Verification index](../.verification/README.md).

## Parked

M5/secrets guide, Indy expansion/later games, demo carrier rooms, optional General MIDI/card archaeology, Status windows for Indy 3/MI1/MI2/Indy 4, new Play features, session save/load ([historical review](archive/CLEANUP_REVIEW.md)).
