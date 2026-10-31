# Roadmap: what is left to finish each game

Rewritten 31 October 2026 after the user's scope decision (REQUIREMENTS R-1.14, §8.6); first written 30 October from an audit of the code, the tests, the per-game trackers and the user-reported gaps. This file answers one question: **what has to happen, per game, before we can call it finished?** The procedure for a step stays in [GAME_SUPPORT_INSTRUCTIONS](GAME_SUPPORT_INSTRUCTIONS.md); what the app must do stays in [REQUIREMENTS](REQUIREMENTS.md).

## The scope (31 October 2026)

The product is an **explorer**: pick any room, trigger its interactions and cutscenes at any time, without playing to them. Teleports and magic costume changes are fine; nothing is saved or loaded. Playing a game through its own interface stays as it works today and comes last. Work is incremental: one step and one game group at a time.

What this removes from the 30 October plan: the Play sign-off (R-7.7 to R-7.21 stay Proposed and are not a gate), Status beyond v1/v2 (old E5), the ten-room ScummVM comparison per game (spot checks instead), and session snapshots. What it adds: the scene setup of §8.6 for every game, and interactions in a picked room (R-8.38).

## The ten features (the user's list, 31 October 2026)

The user's own reading of the roadmap: ten features every game should have, in the order an explorer meets them. A game is finished when all ten are ✅ and the user has tried it.

| # | Feature | Done when |
|---|---|---|
| 1 | **Rooms** | Every room is in the room selector and opens in its first-visit state, no gaps. |
| 2 | **Costumes** | Every costume is read and shown in the game's palette. |
| 3 | **Sounds** | Music, effects and CD tracks play on the game's sound devices (PC speaker, AdLib, Roland where the game has it); the user has listened. |
| 4 | **Scripts available** | Every script can be opened (disassembly). |
| 5 | **Room cutscenes** | Every cutscene of a room plays from that room when picked (§8.6, R-8.35): its state set up, or its code started directly. The game's intro counts too: Play Game Intro plays it in every game that has one (R-8.5c, R-6.38). |
| 6 | **Interactions** | The room's Interact button offers every object that belongs to the room, present or not yet, with every verb that has code, and plays it (R-8.38). |
| 7 | **Room sounds** | The room's Sounds button plays the sounds and music its scripts use (R-6.40). |
| 8 | **Hotspots, doorways, paths, depth** | Hotspot and doorway overlays, walk boxes, depth masks right for the room. |
| 9 | **Game state** | A Status window to set the game's state (§12). Optional under the scope: picked scenes set what they need themselves. |
| 10 | **Readable scripts** | Scripts read as plain language with names for the game's variables, verbs and objects (reverse engineered, `docs/<game>/*_SCRIPT_MAP.md`). |

Symbols: ✅ done · 🟡 works but partial or not checked by the user · 🔴 known gap · ⬜ not started · — parked or not applicable. Play through the game's own interface is not a feature here (§7.1, last priority).

## The board

| Game | 1 Rooms | 2 Costumes | 3 Sounds | 4 Scripts | 5 Cutscenes | 6 Interactions | 7 Room sounds | 8 Hotspots… | 9 State | 10 Readable |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| LOOM EGA English (reference) | ✅ | ✅ | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | ✅ |
| LOOM EGA other editions, demos | ✅ | ✅ | ✅ | ✅ | 🟡 | 🟡 | ✅ | ✅ | ✅ | ✅ |
| LOOM CD | ✅ | ✅ | 🟡 | ✅ | 🟡 | 🟡 | ✅ | ✅ | ✅ | ✅ |
| Indiana Jones 3 (EGA, 256 colours) | ✅ | ✅ | 🟡 | ✅ | 🟡 | 🟡 | ✅ | 🟡 | — | 🟡 |
| Monkey Island 1 (floppy, CD) | ✅ | ✅ | 🟡 | ✅ | 🟡 | 🟡 | ✅ | 🟡 | — | 🟡 |
| Monkey Island 2 | ✅ | ✅ | 🟡 | ✅ | 🟡 | 🟡 | ✅ | 🟡 | — | 🟡 |
| Indiana Jones 4 | ✅ | ✅ | 🟡 | ✅ | 🟡 | 🟡 | ✅ | 🟡 | — | 🟡 |
| Maniac Mansion (classic, enhanced) | ✅ | ✅ | 🟡 | ✅ | 🔴 | 🟡 | ✅ | 🟡 | ✅ | 🟡 |
| Zak McKracken (classic, enhanced) | ✅ | 🟡 | 🟡 | ✅ | 🔴 | 🟡 | ✅ | 🟡 | ✅ | 🟡 |

Why the cells are what they are (31 October 2026):
- **3 Sounds** 🟡: every non-LOOM game plays its sounds on its devices, but the user has not listened (USER_CHECKS); LOOM CD's picture/voice/music timing is the user's run. Indy 4's CD image has no audio tracks (data).
- **5 Cutscenes** 🟡 for Indy 3, Monkey Island 1, Monkey Island 2, Indy 4 since 31 October: every scene begins when picked (Indy 3 153, MI1 164 and 160, MI2 190, Indy 4 426), by the game's way, a setup, or started at its cutscene; but some of those started at their cutscene end at once with nothing shown (Indy 3 10, MI1 10, MI2 24, Indy 4 35), and only Indy 3 was looked at in the window (hatch and jar right; the brick shows the wall without Indy) ([record](../.verification/scene-setup-20261031/README.md)). Maniac and Zak still have scenes that begin nothing (their track). LOOM's scanned editions and the CD use LOOM's own readiness search; not every entry is proven to begin. Monkey Island 2's and Monkey Island 1 floppy's intro now pass the copy protection by themselves.
- **6 Interactions** 🟡 everywhere: the Interact button exists in every live game, but offers only objects present and touchable now, and runs the verb directly; R-8.38 (objects not yet present, the hero put at the object, the second object given) is the next step after 5.
- **8** 🟡 for the non-LOOM games: hotspots, doorways and walk boxes work; depth masks are read generically and were never compared with ScummVM (R-6.16).
- **9 Game state** — for Indy 3, Monkey Island 1 and 2, Indy 4: parked; the setup of a picked scene does what Status would be used for. Revisit if the user wants to set state by hand.
- **10 Readable** 🟡: the readable view works for every game, but names come only from LOOM's glossary; the other games show variable numbers. Their script maps (`docs/indy3`, `monkey1`, `monkey2`, `indy4`) hold decoded names to feed it.

## The work, in order

| # | Work | Unlocks | Where in the code |
|---|---|---|---|
| 1 | **Scene setup for every game (§8.6).** Engine-layer search (`ScummSceneSetupSearch`): bits, story variables, object states, items held, the second object of a two-object verb, actors' costume and room; the game's sentence script, then the verb run directly, then the cutscene started directly. **31 October:** every scene of Indy 3, MI1, MI2, Indy 4 begins. Next: the at-cutscene starts that show nothing (start them with the locals and state the skipped tests set), a window look at MI1/MI2/Indy 4, then LOOM's scanned editions and Maniac/Zak. | Feature 5 | `Loom/Scenes/LoomCutsceneReadiness.cs` → `Scumm/Session/`, `ScummCutscenePlanner.cs` |
| 2 | **Interactions in a picked room (R-8.38).** Compare what each room offers with its objects' verbs that have code; offer the missing ones as exploration actions (hero put next to the object, second object given), using the same setup. | Feature 6 | room lists (`ScummCutsceneIndex`), `ScummSession.StartObjectScript` |
| 3 | **Derivation time.** The setup search adds trial runs to the background derivation (MI2 already 140–174 s). Share one booted copy between trials; cache plans per build+data (as R-8.21). | Keeps 5 usable | `ScummCutscenePlanner`, `ScummBoundedScriptExecutor` |
| 4 | **Readable names for the other games** from their script maps. | Feature 10 | `LoomScriptPresenter`, `docs/<game>/*_SCRIPT_MAP.md` |
| 5 | **The user's reports** as they arrive (the crash log records game and room). | 1 | — |
| 6 | **One user pass per game** with [USER_CHECKS](USER_CHECKS.md): a few rooms, scenes and sounds next to ScummVM; each copy opened once. | 3, sign-off | — |
| 7 | **Release** (LOOM first): packaging, version, clean-profile first run, Roland without ROMs. | — | `src/SCUMMexplorer.App` |

Optional, when the code is next touched: a ScummVM picture capture for comparisons (the bundled build showed a white window for Indy 3), app structure (`MainWindow.xaml.cs` split, LOOM-keyed code into the LOOM module), profile allow-lists as defaults, worktree housekeeping. Dropped by the scope decision: Status beyond v1/v2, new Play features, session snapshots.

## Per game: the steps left

Steps marked **(shared)** are one piece of work in the list above. Their letters are the 30 October gates: A rooms (features 1, 8), B interactions (6), C cutscenes (5), D looks and sounds (2, 3), E editions, F sign-off. Earlier records stay linked for the facts they hold.

### LOOM EGA English (reference)
1. A — Confirm or reproduce: Room 2 skipped cutscene leaving Bobbin small; Rooms 15/18/20 staff notes (fix of 11 October); Room 34 standalone fortress reveal (local 200 +0035).
2. F — Settle the open naming uncertainties (Shaping's canonical label, reverse draft names) or accept them as labelled. Release (work item 6).

### LOOM EGA other editions and demos
1. C — Scanned lists already use LOOM's readiness; the shared setup (item 1) widens it to actors and object states.
2. E — Open each copy in the window once: Italian (accents never seen on screen), plain English, French, German, v1.2, both demos.

### LOOM CD
1. C — Check the run-based category against ScummVM (19 of 98 scenes are called dialogue). D — The user's run of picture, voice and music timing.

### Indiana Jones 3 (EGA and 256 colours)
1. A — **Closed 31 October**: castle Rooms 20, 28, 32 explained (decision 7 in USER_CHECKS); all 88 rooms run with nothing unfollowed; Rooms 1 and 15 never rest (crowds, as the game); a held object's verb works in any room ([record](../.verification/indy3-rooms-20261030/README.md)). Possibly the user's "thin black rooms". Rooms 47, 60, 66 start dark and are shown lit.
2. C — 58 of 153 entries begin nothing from a fresh start: the first game for the shared setup (item 1). Their code tests an open door, the students' scene, story bits ([record](../.verification/indy3-cutscenes-20261017/README.md)).
3. B — Interactions in a picked room (shared, item 2).
4. D — Spot checks; AdLib and PC speaker listening.

### Monkey Island 1 floppy and CD
1. C — 60 of 164 (CD 59 of 160) entries begin nothing (shared setup). Example: Script 117, the first SCUMM Bar exit, tests the open door and `bit[0x1BE]` ([record](../.verification/monkey1-cutscenes-20261017/README.md)).
2. B — Interactions (shared). D — Listening (floppy credits timer is an estimate; CD track 21 started by no sound); `VAR_VIDEOMODE` is 19 for every v4 game, wrong for the EGA release.
3. E — Spanish eight-disk copy; the CD's five languages in the window.

### Monkey Island 2
1. A — Copy protection in Play Game Intro answered by the explorer since 31 October (no code wheel; seen in the real window), your retest pending ([record](../.verification/mi2-intro-protection-20261030/README.md)). Decide the static-preview question (Room 4 campfire, decision 2).
2. C — 95 of 260 entries begin nothing (shared setup); derivation takes 140–174 s (item 3).
3. B — Interactions (shared). D — iMUSE hooks are labelled estimates; listening pending. Rooms 104–106 keep colour-0 image holders (accepted).

### Indiana Jones 4
1. C — 216 of 426 groups begin nothing (shared setup): the largest gap.
2. B — Interactions (shared). D — Listening, including talkie speech. E — Spanish talkie and floppy.

### Maniac Mansion and Zak McKracken (classic, enhanced)
Run by their own orchestrator ([handoff](../NEXT_SESSION_PROMPT_MANIAC_ZAK.md)); the 31 October scope applies to them too once the user tells that track.
1. A — Zak Room 19 and similar never settle (a still picture after about 25 s); black regions (Maniac classic Room 18, Zak classic 44, enhanced 13, 16, 44); old-actor offsets.
2. C — 6 or 7 of 27 (Maniac) and 13 of 54 (Zak) groups start: the shared setup replaces "state-gated triggers get no invented prerequisites".
3. D — Zak classic's 11 estimated costume colours; PCjr/Tandy never listened to.
