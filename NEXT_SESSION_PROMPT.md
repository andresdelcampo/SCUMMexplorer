# Next session: orchestrator brief

You are the orchestrator of the game-support work. Read, in this order: `docs/GAME_SUPPORT_INSTRUCTIONS.md` (the procedure, the autonomy rules and the checkpoint format), `docs/CURRENT_WORK.md` (state) and the latest files in `docs/checkpoints/` (`20261024-shared-room-controls.md` is the last run). Everything below is only what those do not say.

## State (20 October)
- Played through their own verbs (Objects ✔, Play ✔), with sound during play and script-derived cutscene lists (Cutscenes ◐): Indy3 EGA, Indy3 256 colours, Monkey Island 1 floppy, besides LOOM. Spanish copies of all three checked (S9). Monkey Island CD (disc music), Monkey Island 2 and Indy 4 (iMUSE music in play with loops, jump hooks and beat; Indy 4 talkie speech) are played too (Objects ✔, Play ✔), and every played game has a script-derived cutscene list (Cutscenes ◐). Spanish and other copies of every played game checked (S9). Cutscene lists derive from one booted snapshot per game. Monkey Island 1 CD offers its five languages (R-3.16), all five checked (S9).
- A separate orchestrator may be running Maniac Mansion and Zak McKracken (v1/v2) on its own (`NEXT_SESSION_PROMPT_MANIAC_ZAK.md`, checkpoints `*-ManiacZak.md`): leave those families to it; merge master before fast-forwarding, and retry if master moved.
- **R-7.16** (right click) is confirmed: right click goes to the game only where its interface uses it (`ScummProfile.RightClickReachesGame`: verb-based and engine version 4 or later, today Monkey Island floppy); elsewhere it is the Interact menu. The rest of R-7.7 to R-7.18 is still `Proposed`.

## Ask the user first (one short question each, only if still open)
0. A picked room whose own entry leaves it (MI2's campfire, Room 4) is a static preview by R-8.5b, with the reason only in Advanced: add a Normal-mode hint pointing to Play Game Intro, or let a boot room play its settled boot state (R-5.3a)? (checkpoint 20 October)
1. Restart and save/load keys (Indy3 F5, `VAR_MAINMENU_KEY`): refused (current) or working?
2. R-1.5 met through the game's default verb; ScummVM's 16×16 cross-hair cursor: acceptable?
3. R-8.32, R-8.33 (cutscene lists and fresh-start runs for the generic session) are `Proposed`.
4. What did they see and hear in the user checks of the last checkpoint (play, sound and cutscenes in the three games, right click in Monkey Island, the Spanish copies, Monkey Island CD rooms; see the last checkpoint)?
When answered, change those rows' Basis to `Rule (confirmed by the user <date>)` or adjust them.

## How this user wants the work run
- Delegate: one worktree agent per item, review, merge, write the checkpoint. No approval gates inside a run; minimise the user's intervention; run independent items in parallel and tell each agent which shared files the other owns.
- Budget is limited: stop at a clean checkpoint. A rate limit kills agents mid-run: resume the same agent with SendMessage (it keeps its worktree).
- Merge: `git merge master` inside the agent's worktree, resolve (nearly always keep both; the README/REQUIREMENTS matrix is regenerated with `SCUMM_UPDATE_SUPPORT_MATRIX=1`), build, full suite (about 3 min, expect 0 failed, with `SCUMM_SOUND_TEST_ROOT`, `SCUMM_INDY3_EGA_TEST_ROOT`, `SCUMM_MT32_ROM_ROOT` under `D:\Juegos\Lucas`), then `git merge --ff-only` into master. Update the README matrix notes as cells change.
- Documentation only in the fixed places (instructions section 7). Raise a matrix cell only with its test; the user may raise one on their own judgement.
- **Game data is in `D:\Juegos\Lucas`, ScummVM in `D:\Juegos\scummvm`, both outside the repo.** Tests use `TestGames.Root`. **Never create a `Games` junction**; before removing any worktree, list its reparse points (`cmd /c dir /AL <worktree>`) and delete junctions on their own first (on 15 October `git worktree remove` followed one and wiped all game data; restored from the user's Macrium backup).
- The agents cannot hear or see the real window and cannot drive ScummVM: collect those checks for the user in the checkpoint.

## Suggested next runs (pick with the user, or take the first if they say "keep going")
- Shared room controls (25 Oct): the five items of checkpoint 20261024 are done (shared key handler, full-screen Esc, `GameIntro()` provider method, LOOM CD Sounds rows, double-click hint), and LOOM's Advanced panel, room view (pan, wide mode), Restart/Interactive/Connections and local-script entries now serve every game (R-6.43, R-6.49 to R-6.51). Maniac/Zak (v1/v2) have no room-local scripts by nature (checked, `.verification/ui-parity-20261025/LocalScan`). Open: Zak room 19 and similar rooms whose entry never settles stay a still picture after about 25 s (R-6.52; the wait could be cut); hand-check the sluggish Advanced scrolling in Maniac Mansion Enhanced; LOOM's last-action panel was moved to the shared data without a click-through check.
- MonkeyCd Rooms 88, 89, 99; cutscene start setups for entries that begin nothing from a fresh start (R-8.33); the `printEgo` first-text offer.
- A fixed-point stop for long cutscene runs (proposal in `.verification/globals-tracking-20261019/`); cheaper operand parsing (regex groups are now the largest allocation); server GC (user's call).
- Open from S6: iMUSE hook classes other than 0, markers, fades; the room change ScummVM's `killScriptsAndResources` ends a calling script with (broke MI2's boot when tried).
- Polish with no cell effect: Indy3Vga rooms 26, 36, 38, 55, 61 differ from EGA by one object; `actorFollowCamera` of a hero in another room; a verb's click area is its text box.

## Housekeeping
Merged agent worktrees remain under `.claude/worktrees/` (none had junctions when merged; still check before removing, and only with the user's OK). Commit with the attribution line from the system reminder; end the session with a fresh `NEXT_SESSION_PROMPT.md`.
