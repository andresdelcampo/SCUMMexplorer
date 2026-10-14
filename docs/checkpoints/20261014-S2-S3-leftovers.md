# Checkpoint plan: S2 and S3 leftovers (14 October 2026)

Run request: "Run S2 and S3 leftovers in parallel", alongside the S4a run ([plan](20261014-S4a.md)). No budget given.

## Plan

| Item | Agent | Scope | Shared files |
|---|---|---|---|
| S2 ManiacClassic + ZakClassic: real actor colours of the v1 costumes instead of the estimate | agent A (worktree) | derive the colours from the game's own tables/scripts per ScummVM (`actor.cpp`, `costume.cpp`, v1 code); cell Costumes may go to full only if derived and compared with the bundled ScummVM | `Scumm/V1`, costume drawing; must not touch the session, the executor or `Loom/Session` (S4a owns them) |
| S3 MonkeyCd + Monkey2 + Indy4: the part of the v5 Sound cell that needs no running script (song looping, the two unnamed Monkey 1 CD tracks, anything else found), and a precise list of what only S6 can finish | agent B (worktree) | the hooks that wait for a script id are NOT in scope (they need S6); the cell stays partial unless every remaining item is done and the user has listened | `Scumm/Audio`, imuse files, V5 archive reader |

Independent of each other and of S4a (different files). Each merges to master as it returns, `GameSupport.cs` conflicts resolved by re-running the matrix tests with `SCUMM_UPDATE_SUPPORT_MATRIX=1`.

## Result

(filled in at the checkpoint)
