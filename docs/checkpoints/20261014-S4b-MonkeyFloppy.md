# Checkpoint plan: S4b for MonkeyFloppy (14 October 2026)

Run request: "S4b MonkeyFloppy in parallel" with the S5 Indy3Ega run ([plan](20261014-S5-Indy3Ega.md)). No budget given.

## Plan

| Item | Agent | Scope | Shared files |
|---|---|---|---|
| S4b MonkeyFloppy: the generic `ScummGameSession` boots The Secret of Monkey Island (floppy, v4, several disks) to its first playable room | one agent, own worktree | v4 data source over every disk of the game, boot sequence and engine variables for v4, executor opcodes the boot needs, the code-wheel/copy-protection screen answered as labelled assumed input (R-15.5), headless boot test | `Scumm/Session`, `ScummVars`, `ScummProfile`, `ScummBoundedScriptExecutor`, `Scumm/V4`: **shared with the running S5 Indy3Ega agent**, so changes there stay small and additive, in separate commits, and the branch is rebased on master before merging |

Merge order: the S5 branch first, then this one rebased on it.

## Result

(filled in at the checkpoint)
