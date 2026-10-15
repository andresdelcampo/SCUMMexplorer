# Checkpoint plan: S5 for MonkeyFloppy (15 October 2026)

Run request: "keep going with the scheduled work, update the README support matrix as progress occurs, minimise my intervention" (user, 15 October), after the user confirmed R-3.15, R-5.3a, R-6.12a (widened to every game with a first-visit state), R-15.7 and R-15.8 (both scoped to the first-visit state) in commit 5ee7e52, and accepted the Indy3 EGA rooms of the S5 checkpoint (the mostly empty rooms show the initial map state, as expected).

## Plan

| Item | Agent | Scope | Shared files |
|---|---|---|---|
| S5 MonkeyFloppy: first-visit object state of every room from the game's own entry scripts, rooms without gaps; the room view and cards of the floppy release show it (R-6.12a) | one agent, own worktree, cap about 200 tool calls | `ScummGameSession.VisitRoom/Settle` on a v4 multi-disk game, the App wiring of 1ab9a9d extended by profile/data (no game test), hotspot rules (class bits from the v4 source) | `Scumm/Session`, `Scumm/Rendering`, App room view path |

Run alone: the next candidates (S4b Indy3Vga, S6 Indy3Ega) touch the same session files.

## Result

**Not done.** At 17:48 the orchestrator's clean-up of six merged worktrees (`git worktree remove`) followed their `Games` junctions and deleted the whole game data folder. The agent found no data, committed unverified code (3b2bcf6 on `worktree-agent-ad93884c1135334c7`: v4 first visit in `ScummRoomFirstVisit`, `ScummProfile.HasFirstVisitScenes` by format and dialect, App stored-object filter, an unrun Monkey room audit) and stopped under hard stop 4.4.4. The user restored the data from backup to `D:\Juegos\Lucas`; tests now use `TestGames.Root` (275788e, full suite 985 passed, 5 skipped, 0 failed). The user then stopped the agent. **Do not merge 3b2bcf6 before a verification run.** Next run: resume S5 MonkeyFloppy from 3b2bcf6 (merge master first; no junctions).
