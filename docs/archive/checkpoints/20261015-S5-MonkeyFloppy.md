# Checkpoint plan: S5 for MonkeyFloppy (15 October 2026)

Run request: "keep going with the scheduled work, update the README support matrix as progress occurs, minimise my intervention" (user, 15 October), after the user confirmed R-3.15, R-5.3a, R-6.12a (widened to every game with a first-visit state), R-15.7 and R-15.8 (both scoped to the first-visit state) in commit 5ee7e52, and accepted the Indy3 EGA rooms of the S5 checkpoint (the mostly empty rooms show the initial map state, as expected).

## Plan

| Item | Agent | Scope | Shared files |
|---|---|---|---|
| S5 MonkeyFloppy: first-visit object state of every room from the game's own entry scripts, rooms without gaps; the room view and cards of the floppy release show it (R-6.12a) | one agent, own worktree, cap about 200 tool calls | `ScummGameSession.VisitRoom/Settle` on a v4 multi-disk game, the App wiring of 1ab9a9d extended by profile/data (no game test), hotspot rules (class bits from the v4 source) | `Scumm/Session`, `Scumm/Rendering`, App room view path |

Run alone: the next candidates (S4b Indy3Vga, S6 Indy3Ega) touch the same session files.

## Result

**Not done.** At 17:48 the orchestrator's clean-up of six merged worktrees (`git worktree remove`) followed their `Games` junctions and deleted the whole game data folder. The agent found no data, committed unverified code (3b2bcf6 on `worktree-agent-ad93884c1135334c7`: v4 first visit in `ScummRoomFirstVisit`, `ScummProfile.HasFirstVisitScenes` by format and dialect, App stored-object filter, an unrun Monkey room audit) and stopped under hard stop 4.4.4. The user restored the data from backup to `D:\Juegos\Lucas`; tests now use `TestGames.Root` (275788e, full suite 985 passed, 5 skipped, 0 failed). The user then stopped the agent. **Do not merge 3b2bcf6 before a verification run.** Next run: resume S5 MonkeyFloppy from 3b2bcf6 (merge master first; no junctions).

### Resumed (15 October, later)

**Done.** A second agent verified 3b2bcf6 on the restored data and fixed on top of it (81d962e code, tests and harness; d831a67 record and docs; merged into master): v4 bit variables kept apart from word variables (`ScummProfile.SeparateBitVariables`, ScummVM `readVar`), pseudo rooms (`o5_pseudoRoom`: a pseudo number loads its real room, `VAR_ROOM` keeps the number), arrivals from other rooms preferred when choosing how a room is entered. Monkey room audit: **81 of 81 rooms without a gap**; objects in 30 rooms, actors in 56; 70 settle, 9 reach the frame limit, 2 leave. Source-level comparison of 14 script-heavy rooms. Registry unchanged (Objects partial until S6). Record: [monkey1-objects-20261015](../../../.verification/monkey1-objects-20261015/README.md). Whole suite on master after the merge: 994 (989 passed, 5 skipped, 0 failed); LOOM room audit unchanged.

**Side effect:** Indy3 EGA also uses pseudo rooms; rooms 17, 18, 21, 23, 24, 33, 72 now show Indy and more objects.

**In parallel:** the Play requirement for a verb-based game, R-7.7 to R-7.18 as `Proposed` rows (763882e, merged). Open questions for the user are in the S6 section of the [Indy3 tracker](../indy3/SUPPORT_STATUS.md) and listed in the final report of this session: right click (game or Interact menu), the restart and save/load keys, reading R-1.5 through the game's default verb, the ScummVM crosshair cursor, the order of updating R-1.3/§17.

**User checks:** in Normal mode open `Games/monkey1 ega en` rooms 33, 58, 1, 30, 28, 35, 53, 70, 39, and `Games/indy3 ega en` rooms 17, 18, 21, 23, 24, 33, 72; say which look wrong. Room 1's pseudo number 130 is a labelled pick. **Cost:** S5 resume about 155 tool calls; requirements about 56.
