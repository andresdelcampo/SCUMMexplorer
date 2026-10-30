# Roadmap: what is left to finish each game

Written 30 October 2026 from an audit of the code, the tests, the per-game trackers, the checkpoints and the user-reported gaps in [CURRENT_WORK](CURRENT_WORK.md). This file answers one question: **what has to happen, per game, before we can call it finished?** The procedure for doing a step stays in [GAME_SUPPORT_INSTRUCTIONS](GAME_SUPPORT_INSTRUCTIONS.md); what the app must do stays in [REQUIREMENTS](REQUIREMENTS.md). Update the board when a gate changes.

## Why the support matrix is not the roadmap

The matrix (`GameSupport.Families`) shows ✔ in nearly every cell, yet the games feel unequal. Three reasons, from the audit:

1. **The cells were raised from headless tests, not from the user's play.** About 115 evidence records and checkpoints end with "user checks" that have never been done. The trackers for Monkey Island CD, Monkey Island 2 and Indy 4 still say *S3 Sound: in progress, partial, listening pending*, while the matrix shows Sound ✔ (its own definition says ✔ means "as listened to by the user").
2. **The original game was never compared.** The S5 acceptance asks for ten rooms compared with ScummVM per game. Done: 0 for Indy 3 VGA, Monkey Island 1 and Indy 4; the Indy 3 attempt failed (the bundled ScummVM showed a white window); the Maniac/Zak runs were not allowed to run ScummVM.
3. **The matrix has no column for "the user tried it and it works".** That is the column where the games differ most: LOOM English is hand-validated; everything else is "passes its scripted route".

So the roadmap below measures each game against the same seven **gates**, each one something the user can see.

## The finish line: seven gates per game

| Gate | Done when | Who closes it |
|---|---|---|
| **A. Plays** | No known blocker: no user-reported bug open, no room that fails to run or never settles, the opening reaches play. | Agent fixes, user confirms |
| **B. Looks right** | Ten first-visit rooms compared side by side with ScummVM; every black area explained (source) or fixed; costumes in the game's palette. | Agent captures, user glances |
| **C. Sounds right** | The user listened to play and cutscenes on each device the game has; unmodelled hooks listed. | User |
| **D. Cutscenes** | Every list entry either starts (with source-derived prerequisites) or says why not; categories checked against the game. | Agent |
| **E. Game Status** | A Status window exists for the game's state (S8). | Agent, on request |
| **F. Editions** | Every local copy opened in the real window once (language, accents, music device). | User |
| **G. Signed off** | The Proposed requirements the game depends on are confirmed; the matrix row is honest; decisions made. | User |

Symbols: ✅ done · 🟡 works but unchecked or partial · 🔴 known broken or never attempted · ⬜ not started · — not applicable.

## The board

| Game | A Plays | B Looks | C Sounds | D Cutscenes | E Status | F Editions | G Signed off |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| LOOM EGA English (reference) | 🟡 | ✅ | ✅ | ✅ | ✅ | — | 🟡 |
| LOOM EGA other editions, demos | ✅ | 🟡 | ✅ | 🟡 | ✅ | 🔴 | 🟡 |
| LOOM CD | ✅ | ✅ | 🟡 | 🟡 | ✅ | — | 🟡 |
| Indiana Jones 3 EGA | 🟡 | 🔴 | 🟡 | 🟡 | ⬜ | ✅ | 🟡 |
| Indiana Jones 3 256 colours | 🟡 | 🔴 | 🟡 | 🟡 | ⬜ | ✅ | 🟡 |
| Monkey Island 1 floppy | ✅ | 🔴 | 🟡 | 🟡 | ⬜ | 🟡 | 🟡 |
| Monkey Island 1 CD | ✅ | 🔴 | 🟡 | 🟡 | ⬜ | 🟡 | 🟡 |
| Monkey Island 2 | 🔴 | 🔴 | 🟡 | 🟡 | ⬜ | 🟡 | 🔴 |
| Indiana Jones 4 | 🟡 | 🔴 | 🟡 | 🟡 | ⬜ | 🟡 | 🟡 |
| Maniac Mansion (classic, enhanced) | 🟡 | 🔴 | 🟡 | 🟡 | ✅ | 🟡 | 🟡 |
| Zak McKracken (classic, enhanced) | 🟡 | 🔴 | 🟡 | 🟡 | ✅ | 🟡 | 🟡 |

Distance to the finish line, roughly: **LOOM** needs only sign-off and release work; **Monkey Island 1 floppy, Indy 3, Indy 4** need a user pass plus the shared engine items; **Monkey Island 2 and Monkey Island 1 CD** have open blockers; **Maniac/Zak** have the most open visual fidelity work and were built on a separate track.

## Per game: the steps left

Each step names its gate. Steps marked **(shared)** are one piece of engine work that closes the gate for several games at once (see the next section).

### LOOM EGA English (reference)
1. A — Confirm or reproduce: Room 2 skipped cutscene leaving Bobbin small; Rooms 15/18/20 staff-note fix (11 October); Room 34 standalone fortress reveal (local 200 +0035).
2. G — Settle the open naming uncertainties (Shaping's canonical label, reverse draft names) or accept them as labelled.
3. Release: packaging and version (no publish settings, version or installer yet), first run on a clean profile, Roland without ROMs fails gracefully.

### LOOM EGA other editions and demos
1. F — Open each copy in the real window once: Italian (accents never seen on screen), plain English, French, German, v1.2, both demos.
2. D — Script-derived lists stay ◐ by design; decide whether that is the finish line for them (G).

### LOOM CD
1. C — The user's manual run of picture/voice/music timing (no automatic check exists).
2. D — Check the run-based category against the bundled ScummVM (19 of 98 scenes are called dialogue).

### Indiana Jones 3 (EGA and 256 colours)
1. A — Rooms 20, 28, 32: **explained 30 October** ([record](../.verification/indy3-castle-20261030/README.md)): the castle's cut-away plan draws only Indy's section (Script 25), and patrolling guards can start a confrontation, so they stay still pictures by R-8.5b (decision 7 in USER_CHECKS). Rooms 1, 6, 13, 15 and more never settle within 2400 frames; `actorFollowCamera` of an ego in another room is not modelled; inventory objects run no verb code of their own. Possibly the user's "thin black rooms".
2. B — Get a working ScummVM capture (shared) and compare ten rooms per edition; Rooms 47, 60, 66 start dark and are shown lit; the 256-colour release differs from EGA by one object in Rooms 26, 36, 38, 55, 61; the drawn cross-hair differs from ScummVM's 16 × 16.
3. C — Listening check of play and cutscenes (AdLib with Indy 3's `Player_AD` constants, PC speaker).
4. D — 58 of 153 entries begin nothing from a fresh start: generic start planner (shared).
5. E — S8 Status (shared: inventory editor beyond v1/v2).
6. G — Confirm R-7.7 to R-7.18 (the Play rules for verb games), the F5 save/load key decision, the `printEgo` first-text offer.

### Monkey Island 1 floppy
1. B — ScummVM comparison (shared); `VAR_VIDEOMODE` is 19 for every v4 game, wrong for the EGA release.
2. C — The user's listening and timing check of play (credits music timer is an estimate).
3. D — 60 of 164 entries begin nothing (shared planner).
4. E — S8 (shared). F — Spanish eight-disk copy seen in play.

### Monkey Island 1 CD
1. A — **Done 30 October** ([record](../.verification/monkeycd-rooms-20261030/README.md)): Room 88's empty entry block, Room 89 clean, Room 99 is an icon-holder room the game never enters (refused with that reason), the lookout frame no longer occurs.
2. B — ScummVM comparison (shared).
3. C — Listening; disc track 21 is started by no sound; six `startSound` operands are variables.
4. D — 59 of 160 entries begin nothing; category check against ScummVM. E — S8 (shared).
5. F — Five languages passed S9 headless only; open each in the window.

### Monkey Island 2
1. A — ~~The copy protection blocks the opening~~ **Fixed 30 October** (retest pending): Play Game Intro answers nothing, and the recipe screen lost its strings inside strings and the prompt was not wrapped ([record](../.verification/mi2-intro-protection-20261030/README.md)).
2. G — Decide the static-preview question (Room 4 campfire: Normal-mode hint vs. letting the boot room play, R-8.5b vs R-5.3a). This decision also shapes A.
3. B — ScummVM comparison (shared); Rooms 104–106 keep colour-0 image holders (accepted, documented).
4. C — iMUSE: hooks of classes 1–5, markers, trigger queue, fades, music timer are labelled estimates; listening check pending.
5. D — 95 of 260 entries begin nothing; derivation takes 140–174 s in the window. E — S8 (shared).

### Indiana Jones 4
1. A — Rooms 76, 77, 87 keep a camera-after-walk item; the play view draws each object's first-state image; a magenta line crosses Room 4 after the boot (not investigated); first launch takes about 68 s (34 s cutscene derivation).
2. B — Ten-room ScummVM comparison not done (shared).
3. C — Listening, including talkie speech (the CD image has no audio tracks: data, not a gap).
4. D — 216 of 426 groups begin nothing (shared planner). E — S8 (shared). F — Spanish talkie and floppy seen in the window.

### Maniac Mansion (classic, enhanced)
1. A — Native darkness/flashlight, several unproved paths; preparation to reach states is labelled exploration, not earned play.
2. B — Six black rectangles in classic Room 18 retained; old-actor drawing offsets (+8/+16) to review; dark rooms shown lit; no original-game picture comparison (ScummVM was not allowed in those runs).
3. C — PCjr/Tandy never listened to.
4. D — 6 (classic) and 7 (enhanced) of 27 groups start; "state-gated triggers get no invented prerequisites" (shared planner).
5. F — One local copy per family; nothing to widen without more copies.
6. G — Confirm R-7.19 to R-7.21 and R-8.34.

### Zak McKracken (classic, enhanced)
1. A — Room 19 and similar rooms never settle (a still picture after about 25 s, R-6.52); five cutscene starts hit the frame bound; flight timer and bus approaches unproved.
2. B — Classic: 11 of 35 costume colours are labelled estimates (Costumes ◐); camera placement in Rooms 3 and 33; black regions in classic Room 44 and enhanced Rooms 13, 16, 44.
3. C, D, F, G — as Maniac (13 of 54 groups start).

## Shared engine and app work (one fix, several games)

Ordered by how many gates each one unlocks.

| # | Work | Unlocks | Where in the code |
|---|---|---|---|
| E1 | **Done 30 Oct:** `CrashLog` writes `crash-logs` under the profile, with a trail of the last actions and an unclean-exit log after a crash no handler sees. **Crash log in the shipped app.** The user's crash came with no trace; only harnesses log (`HarnessFailurePolicy`). | A for all | `App.xaml.cs` (25 lines today) |
| E2 | **Done 30 Oct:** Sound ◐ for the ten non-LOOM rows, §3.1 refreshed, [USER_CHECKS](USER_CHECKS.md). **Honest matrix and one user-check list per game.** Lower Sound to ◐ where listening is pending (or add an "accepted" mark), refresh the stale "Checked by" column and family notes in REQUIREMENTS §3.1, and collect the scattered user checks into one short checklist per game. | G for all | `GameSupport.cs`, REQUIREMENTS §3.1, trackers |
| E3 | **ScummVM picture capture that works** (the bundled build showed a white window for Indy 3; try the OpenGL renderer or a save-state screenshot) plus a side-by-side harness. | B for all non-LOOM | `.verification/` |
| E4 | **Generic cutscene start planner.** `ScummCutscenePlanner` only runs fresh starts; the prerequisite search is LOOM-only (`LoomCdSceneSetupPlanner`, story bits, staff). About 40% of non-LOOM entries begin nothing. | D for 6 game groups | `Scumm/Session/ScummCutscenePlanner.cs` |
| E5 | **Status (S8) beyond v1/v2.** The inventory editor needs a `ScummV2DataSource` and returns "no reviewed inventory edit contract" on any other version. | E for Indy 3, MI1, MI2, Indy 4 | `Scumm/Session/ScummSessionInventory*.cs` |
| E6 | **App structure.** `MainWindow.xaml.cs` is 5,933 lines and 322 methods with 92 LOOM references; only LOOM has a game module, every other game falls to `NoGameModule`; LOOM runs on its own session path (`_session`, `_idleSession`, `_pointerSession`) beside the generic `_play`. Move the remaining LOOM-keyed code (`DecodeBrowsedOldV3Room`, session toggle, live sound/clock, gallery speaker names) to the LOOM module, then split MainWindow by feature. | Fewer regressions across games | `src/SCUMMexplorer.App` |
| E7 | **Profile clean-up.** `ScummProfile.For` grew one allow-list per stage (`HasFirstVisitScenes`, `PlaysThroughVerbs`, `BootEndsWhenPlayerHasControl` list every non-LOOM family by name). Now that every known family is in them, express them as defaults with LOOM as the exception. | Adding a game without editing six lists | `Scumm/ScummProfile.cs` |
| E8 | **Performance.** Indy 4 first launch 68 s; MI2 derivation 140–174 s. Lever: change tracking in the executor's variable sync; share one booted snapshot. | A for Indy 4, MI2 | `ScummBoundedScriptExecutor.cs` |
| E9 | **Housekeeping.** 103 registered worktrees and 112 branches from the agent runs; three hand-off files (`NEXT_SESSION_PROMPT.md`, `NEXT_SESSION_PROMPT_MANIAC_ZAK.md` at 24 KB, `docs/loom/FINAL_GAPS_HANDOFF.md`); trackers exist for four games but not for LOOM, Maniac or Zak, while the instructions say not to create one. Pick one format and one hand-off. Remove worktrees only after listing their junctions. | Clarity | repository root, `docs/` |

What the audit found clean: the full suite passes (1,824 passed, 0 failed, 5 skipped on 30 October, with `SCUMM_SOUND_TEST_ROOT` set to the LOOM reference folder); the engine layer (`Scumm/`) has no `GameId`/`GameFamily` branches outside `ScummProfile`, no room-number hacks, and every game goes through `IScummDataSource`. The mess is in the app layer, the docs and the unverified claims, not in the engine.

## Suggested order

1. **Honest and debuggable** (small): E1 crash log, E2 honest matrix and per-game checklists. After this the board above is the user's to-do list, not a guess.
2. **Gate A everywhere:** the user's bug list as it arrives, MI2 copy protection, MI1 CD Rooms 88/89/99, Indy 3 black and unsettled rooms, Indy 4 camera rooms.
3. **One user pass per game** (C, F, G): the user plays each game for a few minutes with its checklist and confirms the Proposed requirements; each answer flips cells in one-line commits.
4. **Shared engine items** E3 (ScummVM capture, gate B), then E4 (planner, gate D), then E5 (Status, gate E) if the user wants Status for those games.
5. **Release** (LOOM first, then the others as they finish).

E6 and E7 are done when the code they touch is next changed, not as a separate project; E6 must be finished before a second game gets its own live module.
