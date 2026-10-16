# Next session: orchestrator brief

You are the orchestrator of the game-support work. Read, in this order: `docs/GAME_SUPPORT_INSTRUCTIONS.md` (the procedure, the autonomy rules and the checkpoint format), `docs/CURRENT_WORK.md` (state) and the latest files in `docs/checkpoints/` (`20261016-S6-Indy3Ega-S4b-S5-Indy3Vga.md` is the last run). Everything below is only what those do not say.

## State (16 October)
- Played through their own verbs (Objects ✔, Play ✔): Indy3 EGA, Indy3 256 colours, Monkey Island 1 floppy, besides LOOM. Depth masks for every browsed game are in. Suite on master: 1014 (1009 passed, 5 skipped, 0 failed).
- **R-7.7 to R-7.18** (Play for verb-based games) are still `Proposed`; S6 for all three games was built on them.

## Ask the user first (one short question each, only if still open)
1. **Right click** in verb-based games: keep the app's Interact menu (current) or send it to the game as ScummVM does? It matters now: Monkey Island's two-button default verb needs it.
2. Restart and save/load keys (Indy3 F5, `VAR_MAINMENU_KEY`): refused (current) or working?
3. R-1.5 met through the game's default verb; ScummVM's 16×16 cross-hair cursor: acceptable?
4. What did they see in the user checks of the last checkpoint (play in the three games, mouse mapping, timing, cursor; Monkey/Indy rooms)?
When answered, change those rows' Basis to `Rule (confirmed by the user <date>)` or adjust them.

## How this user wants the work run
- Delegate: one worktree agent per item, review, merge, write the checkpoint. No approval gates inside a run; minimise the user's intervention; run independent items in parallel and tell each agent which shared files the other owns.
- Budget is limited: stop at a clean checkpoint. A rate limit kills agents mid-run: resume the same agent with SendMessage (it keeps its worktree).
- Merge: `git merge master` inside the agent's worktree, resolve (nearly always keep both; the README/REQUIREMENTS matrix is regenerated with `SCUMM_UPDATE_SUPPORT_MATRIX=1`), build, full suite (about 3 min, expect 0 failed, with `SCUMM_SOUND_TEST_ROOT`, `SCUMM_INDY3_EGA_TEST_ROOT`, `SCUMM_MT32_ROM_ROOT` under `D:\Juegos\Lucas`), then `git merge --ff-only` into master. Update the README matrix notes as cells change.
- Documentation only in the fixed places (instructions section 7). Raise a matrix cell only with its test; the user may raise one on their own judgement.
- **Game data is in `D:\Juegos\Lucas`, ScummVM in `D:\Juegos\scummvm`, both outside the repo.** Tests use `TestGames.Root`. **Never create a `Games` junction**; before removing any worktree, list its reparse points (`cmd /c dir /AL <worktree>`) and delete junctions on their own first (on 15 October `git worktree remove` followed one and wiped all game data; restored from the user's Macrium backup).
- The agents cannot hear or see the real window and cannot drive ScummVM: collect those checks for the user in the checkpoint.

## Suggested next runs (pick with the user, or take the first if they say "keep going")
- **Sound during play** for the three played games (the S3 sound players exist; the session's sound opcodes must reach them): the most visible gap.
- `S7 Cutscenes` for Indy3Ega, Indy3Vga, MonkeyFloppy (the shared scan `LoomCutsceneIndex` moves to `Scumm/` when it first serves another game).
- `S4b MonkeyCd` / `Monkey2` / `Indy4` (v5 needs an `IScummDataSource` first), then Maniac/Zak (v1/v2).
- `S9` edition checks: `indy3 ega es`, `indy3 vga es`, `monkey1 ega es` (8-disk EGA), `monkey1 vga es`.
- Polish with no cell effect: Indy3Vga rooms 26, 36, 38, 55, 61 differ from EGA by one object; `actorFollowCamera` of a hero in another room; a verb's click area is its text box; Zak's estimated costume colours.

## Housekeeping
Merged agent worktrees remain under `.claude/worktrees/` (none had junctions when merged; still check before removing, and only with the user's OK). Commit with the attribution line from the system reminder; end the session with a fresh `NEXT_SESSION_PROMPT.md`.
