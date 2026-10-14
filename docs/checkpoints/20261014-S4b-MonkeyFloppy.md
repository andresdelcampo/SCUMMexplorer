# Checkpoint plan: S4b for MonkeyFloppy (14 October 2026)

Run request: "S4b MonkeyFloppy in parallel" with the S5 Indy3Ega run ([plan](20261014-S5-Indy3Ega.md)). No budget given.

## Plan

| Item | Agent | Scope | Shared files |
|---|---|---|---|
| S4b MonkeyFloppy: the generic `ScummGameSession` boots The Secret of Monkey Island (floppy, v4, several disks) to its first playable room | one agent, own worktree | v4 data source over every disk of the game, boot sequence and engine variables for v4, executor opcodes the boot needs, the code-wheel/copy-protection screen answered as labelled assumed input (R-15.5), headless boot test | `Scumm/Session`, `ScummVars`, `ScummProfile`, `ScummBoundedScriptExecutor`, `Scumm/V4`: **shared with the running S5 Indy3Ega agent**, so changes there stay small and additive, in separate commits, and the branch is rebased on master before merging |

Merge order: the S5 branch first, then this one rebased on it.

## Result

**Done.** MonkeyFloppy, S4b: `ScummGameSession` boots `monkey1 ega en` (the four-disk 256-colour floppy) to Room 33 in 2522 frames, unsupported list empty; no cell changes. Commits 5bad9f2 (disk-set data source), 3e89556 (executor: actorFollowCamera starts the scene, v4 actor scale), 4bd9824 (session, profile, tests), 1dca2a3 (Proposed rows), and the documentation commit. Record: [monkey1-session-20261014](../../.verification/monkey1-session-20261014/README.md).

**Shared engine files touched (small, additive, for the S5 merge):** `ScummBoundedScriptExecutor` (one block after the event is built), `ScummScriptRuntimeState` (one property), `ScummVars` (three constants), `ScummGameSession` (constructor set-up, boot loop, `PressBootKeys`, `PlayerHasControl`, the AdLib lifetime source), `ScummSessionInput` (`PressKey` accepts room 0), `ScummSessionRooms` (`FollowActorAfterEntry`), `ScummProfile` (new properties and `ScummBootInputPlan`), `ScummAdlibAudio` (an overload), `ScummScriptReader` (one line), `ScummDataSources` (one branch). Conflicts, if any, are in `ScummGameSession` (both runs add to the constructor and the boot loop); keep both.

**Not done and why.** Nothing blocked. Not run: a wrong protection answer (the game quits after four), a comparison with the bundled ScummVM (picture, credits length), the EGA video-mode value for the eight-disk release.

**Decisions made.** Full log in [monkey1/SUPPORT_STATUS.md](../monkey1/SUPPORT_STATUS.md): all disks inserted; the protection answered with the game's own derived year as keys through its input script (stall of 400 frames); boot ends when the player has control; `actorFollowCamera` starts the scene only for the generic v4 session; 800 zeroed globals for the v4 profile; the AdLib music timer is an estimate (iMuse in the engine).

**Proposed requirements to confirm or reject.** R-3.15 (disks assumed inserted), R-15.7 (a stalled boot answered with keys through the game's input script, preferring the answer the game derived), R-15.8 (boot ends when the player has control). Rejecting R-15.7 or R-15.8 means reverting the "ScummGameSession boot" part of commit 4bd9824 for MonkeyFloppy only (the data source and the executor fixes stay).

**User checks.** None required. Optional: look at `.verification/monkey1-session-20261014/room-33-after-boot.png` against the real first room.

**Offers.** S8 (a state window) is not offered for this game yet; S5 is the next stage.

**Cost.** One agent, about 130 tool calls; the executor needed two fixes and the session two profile flags: the second v4 game costs far less than the first since the generic session exists.

**Next run:** `Run stage S5 for MonkeyFloppy` (first-visit object state of the rooms from the settled state of the game's own scripts, at least ten rooms compared with the bundled ScummVM).
