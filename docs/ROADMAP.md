# Roadmap: what is left to finish each game

Rewritten 31 October 2026 after the user's scope decision (REQUIREMENTS R-1.14, §8.6); first written 30 October from an audit of the code, the tests, the per-game trackers and the user-reported gaps. This file answers one question: **what has to happen, per game, before we can call it finished?** The procedure for a step stays in [GAME_SUPPORT_INSTRUCTIONS](GAME_SUPPORT_INSTRUCTIONS.md); what the app must do stays in [REQUIREMENTS](REQUIREMENTS.md).

## The scope (31 October 2026)

The product is an **explorer**: pick any room, trigger its interactions and cutscenes at any time, without playing to them. Teleports and magic costume changes are fine; nothing is saved or loaded. Playing a game through its own interface stays as it works today and comes last. Work is incremental: one step and one game group at a time.

What this removes from the 30 October plan: the Play sign-off (R-7.7 to R-7.21 stay Proposed and are not a gate), Status beyond v1/v2 (old E5), the ten-room ScummVM comparison per game (spot checks instead), and session snapshots. What it adds: the scene setup of §8.6 for every game, and interactions in a picked room (R-8.38).

## The finish line: six gates per game, in priority order

| Gate | Done when | Who closes it |
|---|---|---|
| **A. Rooms** | Every room opens in its first-visit state with no gaps and never hangs; black areas explained (source) or fixed. | Agent fixes, user glances |
| **B. Interactions** | In a room, every object that belongs to it (present or not yet) and each of its verbs with code can be picked and plays, the state set to allow it (R-8.38). | Agent |
| **C. Cutscenes** | Every scene of the room is offered and plays when picked: its state set up (§8.6), or its code started directly where no setup makes it begin. Nothing hidden (R-8.35). | Agent |
| **D. Looks and sounds** | The user spot-checked a few rooms, scenes and sounds against the game (ScummVM). | User |
| **E. Editions** | Every local copy opened in the window once (language, accents, music device). | User |
| **F. Signed off** | The user tried the game as an explorer and the decisions are made. | User |

Symbols: ✅ done · 🟡 works but unchecked or partial · 🔴 known gap · ⬜ not started · — not applicable. Play (§7.1) is not a gate.

## The board

| Game | A Rooms | B Interactions | C Cutscenes | D Looks/sounds | E Editions | F Signed off |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| LOOM EGA English (reference) | 🟡 | ✅ | ✅ | ✅ | — | 🟡 |
| LOOM EGA other editions, demos | ✅ | ✅ | 🟡 | 🟡 | 🔴 | 🟡 |
| LOOM CD | ✅ | ✅ | 🟡 | 🟡 | — | 🟡 |
| Indiana Jones 3 EGA | ✅ | 🟡 | 🔴 | 🟡 | ✅ | 🟡 |
| Indiana Jones 3 256 colours | ✅ | 🟡 | 🔴 | 🟡 | ✅ | 🟡 |
| Monkey Island 1 floppy | ✅ | 🟡 | 🔴 | 🟡 | 🟡 | 🟡 |
| Monkey Island 1 CD | ✅ | 🟡 | 🔴 | 🟡 | 🟡 | 🟡 |
| Monkey Island 2 | 🟡 | 🟡 | 🔴 | 🟡 | 🟡 | 🟡 |
| Indiana Jones 4 | ✅ | 🟡 | 🔴 | 🟡 | 🟡 | 🟡 |
| Maniac Mansion (classic, enhanced) | 🟡 | 🟡 | 🔴 | 🔴 | 🟡 | 🟡 |
| Zak McKracken (classic, enhanced) | 🟡 | 🟡 | 🔴 | 🔴 | 🟡 | 🟡 |

B is 🟡 for the non-LOOM games because what a picked room offers today has not been compared with each object's verbs: the room lists hold the responses that contain a cutscene, and the rest is reached only through Play. C is 🔴 where entries begin nothing from a fresh start: Indy 3 58 of 153, Monkey Island 1 60 of 164 (CD 59 of 160), Monkey Island 2 95 of 260, Indy 4 216 of 426 groups, Maniac 20 or 21 of 27 groups (enhanced, classic), Zak 41 of 54.

## The work, in order

| # | Work | Unlocks | Where in the code |
|---|---|---|---|
| 1 | **Scene setup for every game (§8.6).** Move LOOM's readiness search (`LoomCutsceneReadiness`) to the engine layer and widen what it may set: object states and owners (items given), actors placed and dressed. First Indy 3 (58 groups), measure, then Monkey Island 1, Monkey Island 2, Indy 4. | C for six game groups | `Loom/Scenes/LoomCutsceneReadiness.cs` → `Scumm/Session/`, `ScummCutscenePlanner.cs` |
| 2 | **Interactions in a picked room (R-8.38).** Compare what each room offers with its objects' verbs that have code; offer the missing ones as exploration actions (hero put next to the object, second object given). | B for every non-LOOM game | room lists (`ScummCutsceneIndex`), `ScummSession.StartObjectScript` |
| 3 | **Derivation time.** The setup search adds trial runs to the background derivation (MI2 already 140–174 s). Share one booted copy between trials; cache plans per build+data (as R-8.21). | Keeps C usable | `ScummCutscenePlanner`, `ScummBoundedScriptExecutor` |
| 4 | **The user's reports** as they arrive (the crash log records game and room). | A | — |
| 5 | **One user pass per game** with [USER_CHECKS](USER_CHECKS.md): a few rooms, scenes and sounds next to ScummVM; each copy opened once. | D, E, F | — |
| 6 | **Release** (LOOM first): packaging, version, clean-profile first run, Roland without ROMs. | — | `src/SCUMMexplorer.App` |

Optional, when the code is next touched: a ScummVM picture capture for comparisons (the bundled build showed a white window for Indy 3), app structure (`MainWindow.xaml.cs` split, LOOM-keyed code into the LOOM module), profile allow-lists as defaults, worktree housekeeping. Dropped by the scope decision: Status beyond v1/v2, new Play features, session snapshots.

## Per game: the steps left

Steps marked **(shared)** are one piece of work in the list above. Earlier records stay linked for the facts they hold.

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
1. A — Copy protection in Play Game Intro fixed 30 October, retest pending ([record](../.verification/mi2-intro-protection-20261030/README.md)). Decide the static-preview question (Room 4 campfire, decision 2).
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
