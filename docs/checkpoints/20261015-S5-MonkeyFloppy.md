# Checkpoint plan: S5 for MonkeyFloppy (15 October 2026)

Run request: "keep going with the scheduled work, update the README support matrix as progress occurs, minimise my intervention" (user, 15 October), after the user confirmed R-3.15, R-5.3a, R-6.12a (widened to every game with a first-visit state), R-15.7 and R-15.8 (both scoped to the first-visit state) in commit 5ee7e52, and accepted the Indy3 EGA rooms of the S5 checkpoint (the mostly empty rooms show the initial map state, as expected).

## Plan

| Item | Agent | Scope | Shared files |
|---|---|---|---|
| S5 MonkeyFloppy: first-visit object state of every room from the game's own entry scripts, rooms without gaps; the room view and cards of the floppy release show it (R-6.12a) | one agent, own worktree, cap about 200 tool calls | `ScummGameSession.VisitRoom/Settle` on a v4 multi-disk game, the App wiring of 1ab9a9d extended by profile/data (no game test), hotspot rules (class bits from the v4 source) | `Scumm/Session`, `Scumm/Rendering`, App room view path |

Run alone: the next candidates (S4b Indy3Vga, S6 Indy3Ega) touch the same session files.

## Result

(written when the agent returns)
