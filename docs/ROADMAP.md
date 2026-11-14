# Roadmap

The one current-state document: the scope, how ready each game is, the ten features and their board, the way to a release, what is open, the checks only the user can do, and the rules. Only the current state and open work are here. Finished work is in [CHANGELOG](archive/CHANGELOG.md) and its records under `.verification/`; history is in `docs/archive/` (the former ROADMAP, "Your checks" and earlier roadmaps). What the app must do is in [REQUIREMENTS](REQUIREMENTS.md); how a game is added, in [GAME_SUPPORT_INSTRUCTIONS](GAME_SUPPORT_INSTRUCTIONS.md).

## Scope

REQUIREMENTS R-1.14 and §7 (user, 31 October 2026): an explorer. Pick any room and trigger any of its interactions and cutscenes at any time, without playing to them; teleports and magic costume changes are fine; nothing is saved or loaded; the game's rules never forbid a pick. Playing through the game's own interface stays as it is and comes last.

## Readiness

| Family | State |
|---|---|
| LOOM EGA English (Roland), other EGA editions, demos, LOOM CD | Reference quality: live interaction, actors, scripts, scene playback, Game Status, audio. Interact lists every object of the room. Cutscenes ◐ except the English reference's hand-validated catalog. |
| Indy 3, Monkey Island 1 (floppy, CD), Monkey Island 2, Indy 4 | Every room, costume and script; every listed scene begins when picked (its state set up, or started at its cutscene); Interact offers every object of the room with its verbs. Some scenes started at their cutscene show nothing. |
| Maniac Mansion and Zak McKracken (enhanced, classic) | Played through native verbs, Status, inventory editor, room controls, cutscene lists (◐) on the shared scene setup since 1 November. Maniac Mansion's 39 scenes wait for the user's check. |
| Other folders under `Games` | Browse-only or passed only S9 checks; not reviewed in the window. |

Architecture ([audit, 10 November](archive/AUDIT_2026-11-10.md)): the engine (`Core/Scumm`) names a game only in `ScummProfile.For`; Game Status is one inventory window with a per-game schema, plus LOOM's own. LOOM-specific App code lives in `App/Loom/` behind `IGameModule`. Still LOOM-keyed in shared code: `DecodeBrowsedOldV3Room`, the session toggle, the live-session sound/clock code, the gallery's speaker names; every such move needs a before/after run of `.verification/game-module-20261026/Check`.

## The ten features

| # | Feature | Done when |
|---|---|---|
| 1 | **Rooms** | Every room is in the room selector and opens in its first-visit state, no gaps. |
| 2 | **Costumes** | Every costume is read and shown in the game's palette. |
| 3 | **Sounds** | Music, effects and CD tracks play on the game's sound devices; the user has listened. |
| 4 | **Scripts available** | Every script can be opened. |
| 5 | **Cutscenes** | Every cutscene of a room plays from that room when picked, and the game's intro through Play Game Intro (R-8.35). |
| 6 | **Interactions** | Interact offers every object of the room, present or not yet, with every verb that has code, and plays it (R-8.38). |
| 7 | **Room sounds** | The room's Sounds button plays the sounds and music its scripts use (R-6.40). |
| 8 | **Hotspots, doorways, paths, depth** | Overlays, walk boxes and depth masks right for the room. |
| 9 | **Game state** | A Status window (REQUIREMENTS §8). Parked for Indy 3, Monkey Island 1 and 2, Indy 4: a picked scene sets what it needs. |
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
- **5**: every scene begins when picked; started directly at their cutscene (not the game's own way): Indy 3 4.5%, Monkey Island 1 7.9% (floppy) and 7.5% (CD), Monkey Island 2 5.3%, Indy 4 3.1% (5.0% of all five copies), some of them showing nothing, no line and no other room (Indy 3 2, Monkey Island 1 2 and 2, Monkey Island 2 1, Indy 4 3); LOOM's other editions and CD (on the shared engine) start 0.9% (EGA) and 0.4% (CD) of their scenes at the cutscene; Maniac Mansion and Zak McKracken (on the shared scene setup since 1 November) begin every scene, 15% (Maniac, 6 of 39 in each copy) and 15% and 16% (Zak, 12 of 79 and 13 of 82) started at the cutscene; since 8 November their scenes that take control by hiding the interface are listed too (Maniac 27 → 39, Zak 54 → 79 and 82).
- **6**: LOOM's other editions and CD share the reference's code but were not looked at; Maniac/Zak keep their own Interact. Verbs a game never names show as "Action".
- **8**: depth masks of the non-LOOM games never compared with ScummVM (R-6.16).
- **10**: the other games name objects, actors and verbs from their own data (80%, 64% and 57% of references); their variables and bits keep numbers (the script maps hold few rows to turn into data; most of what stays unnamed has no name in the game's data: image-only objects, actors no script names).

## The way to a release

From the [10 November audit](archive/AUDIT_2026-11-10.md). In order; each step names what ends it. Steps 1 (a clean slate per game), 2 (the picture checks), 3 (the shared causes) and step 4's (a) and (c) (12 November) are done and in the changelog; the numbers stay. Scene work stays on Maniac Mansion, Indy 3 and LOOM until the user has checked them (user, 8 November); a generic fix that reaches every game for free is fine.

**Which scenes get fixed** (user, 11 November, on the audit's advice): scenes that play but look wrong are fixed, by shared cause, because one wrong picture costs the trust in the right ones. The last scenes that show nothing are not chased: each is its own puzzle and one more setup kind. The scenes the user names as ones that matter are the test set (user, 13 November): played through Play Cutscene and Interact, each failure fixed by its shared cause; the rest leave Normal mode. The percentage of scenes played the game's own way is a measure, not a target.

| # | Step | Ends when | Where |
|---|---|---|---|
| 4 | **The scenes that matter, as a test set.** The user's list (12 November): every game's intro and endings; LOOM the green sheep and the dragon, the dragon and the boy, Death and the bishop, the Elders' trial, the whirlwind, the sky on Loom island; Indy 3 the whip, the Grail trials (passing and failing each, Marcus's call, the false Grail), boarding the zeppelin, leaving it in the plane, going to Berlin, Venice, Donovan; Maniac Mansion the car into space, the mansion blowing up, the demo tape, the microwave, the tentacles. Each launches through Play Cutscene or Interact (user, 13 November: a test set, not hand-made starts); a failure is fixed by its shared cause ([record](../.verification/scenes-that-matter-20261112/README.md)). Left without an entry, Indy 3 (15 of 18 named outcomes play, [record](../.verification/indy3-check-20261114/README.md)): the Word of God passed (six clicks, one per letter), the leap's fall (a walk, then a click), Room 82's Give Grail beside the knight (the ending's state: door 862, Henry in the room, the Grail held; scene order). The true Grail plays since 14 November (the cup's own entry). | The named scenes play from the lists or Interact. | `ScummCutsceneIndex`, `ScummSceneSetupSearch` |
| 5 | **The user's check, one game at a time**: Maniac Mansion (ready since 8 November), Indy 3 (its scenes that matter played in the window on 14 November, [record](../.verification/indy3-check-20261114/README.md)), then LOOM. Reports are fixed by shared cause. | "Your checks" below are filled for the three games. | — |
| 6 | **Release, LOOM first**: packaging, version, clean-profile first run, Roland without ROMs, each LOOM copy opened once. LOOM English ships with its reviewed catalog. | A zip the user has run on a clean profile. | `src/SCUMMexplorer.App` |
| 7 | **The other games to the same bar** (Monkey Island 1 and 2, Indy 4, Zak McKracken): steps 2 to 5 for each, then they join the release. | Per game, as step 5. | — |

After the release, or when the code is next touched:

- **Generic code out of `Core/Loom`**: Readable scripts, names, Game Texts and the usage index serve every game but live under LOOM names (`LoomScriptPresenter`, `LoomGlossary`, `LoomTextExtractor`, `LoomUsageIndex`). Move the game-free halves to `Core/Scumm` before adding names for the other games (variables and bits from their script maps; [record](../.verification/readable-names-20261031/README.md)).
- **LOOM English on the shared scene engine** (user, 31 October; after the other games, 8 November). The audit's advice: keep the 328 reviewed replays as shipped data and as the engine's answer key (238 of 238 begin, 222 within 10% of the length; [record](../.verification/loom-answer-key-20261031/README.md)); replace an entry only where picture and length match; not a release condition. The named sequences (the Elders' trial, the firewood scene, Walk to Room 13, Room 46) stay reviewed data in any case.
- **Scene order** (user, 2 November; paused 8 November for the user's check): a picked scene gets the state of the scenes before it. 160 of 1,413 scenes (11.3%) carry it ([record](../.verification/scene-order-20261108/README.md)). Not reached: rooms with a way in that needs nothing, rooms whose variable is set elsewhere (Zak McKracken's travel, Indy 4's Atlantis).
- **Interaction outcomes by state** (user, 1 November): list each verb's outcomes with their conditions in Advanced; today one outcome is played.
- **Scenes started at their cutscene**, the remainder read and left as one-offs on 7 November ([record](../.verification/user-reports-20261107/README.md)): Indy 4 Room 44's local script 203 (a bit, then the hero's walk box and x), Room 88's refusals listed as scenes, Room 65's closet; Monkey Island 2 Room 93's light switch and Room 19's key. Not worked further: Normal leaves out those that show nothing (12 November); step 4 gives a reviewed start to any the user names.
- Splitting `MainWindow.xaml.cs` further (the open game's state is in `GameContext` since 10 November; background work still writes through the window, so a task that outlives its game should hold its own context), a ScummVM picture capture, profile allow-lists as defaults, pruning `.verification` run files (each game's last two runs), worktree housekeeping.

## Open

### All games
- **User-reported, not yet reproduced:** thin black rooms (game and room not named); a crash (the crash log in `%LOCALAPPDATA%\SCUMMexplorer\crash-logs` records the next one).
- Scenes not offered in Normal (they show nothing or the picture checks name a suspect; Advanced lists them, labelled): 68 of 1,327 scenes in the room lists (5.1%) over eleven copies ([record](../.verification/scene-shown-20261112/README.md)). A reviewed start brings one back (step 4).
- Browsed games' object note and status text vary between runs of the same build.
- Static previews such as MI2's campfire: a hint is needed (decision 2 below).
- Depth masks and stored object images of the non-LOOM games (R-6.16) not compared with ScummVM.

### LOOM EGA English (reference)
- Room 2's "sky" on the shared engine: a picture suspect (actor 3 moves on its first frame; probably right), so not in the Normal lists of the scanned editions (Spanish checked in the window). English's Normal list plays the reviewed catalog.
- Your checks: Room 2 skipped cutscene leaving Bobbin small (not reproduced); Rooms 15/18/20 staff notes; Room 34 standalone fortress reveal (local 200 +0035).
- Shaping's canonical label, reverse draft names and story roles: settle or accept as labelled. Release (step 6).

### LOOM EGA other editions, demos, LOOM CD
- Each copy opened once in the window (Italian accents, plain English, French, German, v1.2, both demos).
- CD: the scene categories against ScummVM (19 of 98 scenes called dialogue); your picture/voice/music timing run.

### Indiana Jones 3 (EGA and 256 colours)
- Not offered in Normal: 2 of 81 listed scenes (2.5%) in each copy: Room 2's package (shows nothing) and Room 33's entry (Henry placed by the setup, in his own costume; probably right). A reviewed start brings either back (step 4).
- Rooms 47, 60, 66 start dark (live since 6 November): compare with the game. The 16 × 16 cross-hair (decision 4); `printEgo` lines as first texts (decision 5).

### Monkey Island 1 (floppy and CD)
- Normal offers all 87 listed scenes in each copy.
- `VAR_VIDEOMODE` is 19 for every v4 game, wrong for the EGA release.
- Spanish eight-disk copy and the CD's five languages in the window (passed S9 headless only).

### Monkey Island 2
- Not offered in Normal: 4 of 176 listed scenes (2.3%), all picture suspects (step 7).
- Your retest of Play Game Intro; decision 2 (the campfire room). iMUSE hooks are labelled estimates.

### Indiana Jones 4
- Not offered in Normal: 15 of 304 listed scenes (4.9%), all picture suspects (step 7).
- After Play Game Intro: Indy walking very fast (user report, not reproduced).
- Spanish talkie and floppy in the window.

### Maniac Mansion and Zak McKracken (classic, enhanced)
- Normal offers every listed scene of Maniac Mansion (42 and 40); Zak McKracken leaves out 23 of 128 (18.0%) and 23 of 132 (17.4%), nearly all scenes over at once with nothing changed (step 7).
The separate track ended on 23 October ([its last handoff](archive/NEXT_SESSION_PROMPT_MANIAC_ZAK_2026-10-23.md)); these games follow this roadmap.
- Scenes started at their cutscene (Maniac 5 of 42: Room 4's keypad door, Room 10's door, the wires, Room 30's ending, Room 01's departure; Zak 12 of 79 and 13 of 82; Maniac's timer loops, Scripts 139 and 141, set a flag, clear it on each failed condition and loop until all hold). Maniac Mansion's 39 scenes were all played in the window on 8 November ([record](../.verification/maniac-ready-20261108/README.md)); a pick there needs a room opened first (the assumed party). Cutscene definition (9 November, R-8.10, [record](../.verification/cutscene-definition-20261109/README.md)): next, make every listed cutscene of LOOM, Maniac Mansion and Indy 3 play the game's own way (left, each its own case: Maniac Mansion 5, the wires waiting about 4 minutes, the keypad waiting for the code, Room 10's chain through Room 7, the Room 30 ending, Room 01's departure; Indy 3 the mallet, which needs a second walk, plus 3 arrivals by design; LOOM 2). Open from the user's 8 November reports: which ending left rooms with nothing to do (Room 30's and the Edsel's do not); an option to show an object change off-screen (Room 4's brick opens a door out of view for three seconds).
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
| **Indy 3's scenes that matter, ready for your check** (14 November): Play Cutscene in Room 83 (Go to the middle, Go to the right), Room 84 (Go to the middle (centre)), Room 85 (Go to the far right, Entry), Room 86 (Use Holy Water; Action Plain, shallow bronze vessel: the true cup), Room 94 (Entry: the credits), Room 77 (Turn on motorcycle), Room 37 (Walk to airfield) and Room 37's Interact · Action button (the plane) | [record](../.verification/indy3-check-20261114/README.md) | 14 Nov: rest good; Rooms 37, 84 and 86 (the true cup) fixed; 82's Give Grail open (needs the ending's state: scene order) |
| Room 73 · Turn off light switch, Room 22 · Entry, Room 80 · Entry: start as in the game? Esc and Space work? | [cutscene record](../.verification/indy3-cutscenes-20261017/README.md) | |
| **Retest (9 Nov):** Indy 3's Game Status (header button, once a room is open): add items, set health, wear a held outfit, give IQ points, mark story flags; do the names read right? After another game, Maniac Mansion's scenes play on their own session (Room 16's Edsel, Room 20's stereo); LOOM English's room lists follow each replay's run (Room 6 lists 5, Room 28 33; no "Other draft response" sparkles); Indy 3's Room 13 entry (change of clothes), Room 21's door and Room 33's meeting with Henry play the game's own way. | [record](../.verification/user-reports-20261109/README.md), [scenes](../.verification/cutscene-definition-20261109/README.md) | |
| **Retest (7 Nov):** Rooms 20, 28, 32 open within seconds (the EGA copy waited over half a minute) and are live: walk the plan; only the sections Indy has been in show, a click on an unjoined, unseen corridor does not move him (as in the game), and a guard who reaches him starts the confrontation (no disguise is assumed). Does the real game look the same? | [record](../.verification/user-reports-20261107/README.md), [plans](../.verification/indy3-castle-20261030/README.md) | |
| **Retest (6 Nov, second list):** long rooms scroll (Rooms 1, 6, 43, 86); Room 62 and other still rooms show hotspots and doorways; only real exits are doorways (Rooms 48, 86, 93: statues and Grails are hotspots); Room 4's door opens and closes from Interact. In the real game, is being caught before meeting Henry (Script 60 chains to Script 85, which brings him in) what you saw? | [record](../.verification/user-reports-20261106b/README.md) | |
| **Retest (6 Nov):** Room 6 · Entry twice, also from Room 9 and after Skip; Rooms 12, 28, 32 Indy's size; Room 15 Indy, and its Entry (Elsa). Room 6's men: do they look right next to the real game? | [record](../.verification/user-reports-20261106/README.md) | |
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
| **Maniac Mansion's scenes, ready for your check** (8 November): open a room first (Room 1), then Play Cutscene: Give contract, Turn on mondo stereo, Give cassette tape, Give badge, Open wall safe (the keypad), Walk to stairs (the delivery), Use piano, Open microwave oven | [record](../.verification/maniac-ready-20261108/README.md) | |
| Maniac classic: Syd, Bernard, Start, skip the intro, front door and doormat route | [play record](../.verification/maniacclassic-play-20261017/README.md) | |
| Classic Maniac Room 18 (six black rectangles), Zak classic Room 44, enhanced 13, 16, 44: black in the real game too? | [S5 checkpoint](archive/checkpoints/20261017-S5-ManiacZak.md) | |
| Zak Rooms 3 and 33: camera position; Room 19: still picture after about 25 s | [S5 checkpoint](archive/checkpoints/20261017-S5-ManiacZak.md), NEXT_SESSION_PROMPT | |
| Zak classic costumes with estimated colours: do they look right? | [palette checkpoint](archive/checkpoints/20261019-Palettes-ManiacZak.md) | |
| Advanced scrolling in Maniac enhanced: is it sluggish? | NEXT_SESSION_PROMPT | |

## Product rules

- Rooms begin in their earliest state; the running session carries changes caused by real scenes. Game Status is under user control. A scene adds only its minimum prerequisites and preserves later state.
- A scene or interaction the user picks may get the state its own code tests (teleport, costume, items, flags, an object brought into the room), derived generically from its scripts and listed in Advanced (R-1.9, R-8.36). Rooms' first visits and Play never force anything. No room-specific engine hacks; label estimates and picker assumptions.
- Normal mode shows artwork, game text and concise controls; technical details belong in Advanced. Preserve edition differences, palettes and verified source glitches.
- Source execution, prepared checks and original-game observation are distinct evidence. A scene completing does not prove correct actors, prerequisites or timing.
- Compatibility checks cover plausible version differences for the changed feature; reuse the [edition analysis](../.verification/editions-20261005/README.md). English Roland stays the LOOM reference.

## Keeping the documents tidy

Rule (user, 31 October 2026): this file and `NEXT_SESSION_PROMPT.md` hold only the readiness state and what is open. When a piece of work is done, in the same commit: its result goes to [the changelog](archive/CHANGELOG.md) (one entry, linking its `.verification/<topic>/` record), its lines leave this file and the handoff (no "done", "fixed" or struck-through items left behind), and anything else worth keeping goes to `docs/archive/`. The `docs/` root keeps four files: README, ROADMAP, REQUIREMENTS, GAME_SUPPORT_INSTRUCTIONS.

## Preservation and workflow

Read [CLAUDE.md](../CLAUDE.md) for layout, engine facts and build instructions; all shell commands go through RTK. Preserve game data, ROMs, saves, running applications and `%LOCALAPPDATA%\SCUMMexplorer`. Use focused checks and isolated output/profile directories. Commit finished tasks separately with explicit paths. [Verification index](../.verification/README.md).

## Parked

M5/secrets guide, Indy expansion/later games, demo carrier rooms, optional General MIDI/card archaeology, Status windows for Indy 3/MI1/MI2/Indy 4, new Play features, session save/load ([historical review](archive/CLEANUP_REVIEW.md)).
