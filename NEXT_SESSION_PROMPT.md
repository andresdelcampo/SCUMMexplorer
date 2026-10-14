# Next session: orchestrator brief

You are the orchestrator of the game-support work. Read, in this order: `docs/GAME_SUPPORT_INSTRUCTIONS.md` (the procedure, the autonomy rules and the checkpoint format), `docs/CURRENT_WORK.md` (state) and the latest files in `docs/checkpoints/`. Everything below is only what those do not say.

## Decisions waiting for the user (ask first, one short question)
1. **R-6.12a** (in `docs/REQUIREMENTS.md`, `Proposed`) supersedes the existing R-6.12 note where the first visit runs. Confirm or reject; rejecting means reverting `ac1f268` and the app-wiring commit `1ab9a9d`.
2. **R-3.15, R-15.7, R-15.8, R-5.3a** are `Proposed`. The user said "yes to the requirements if they are new" for an earlier batch, so confirm them (change the Basis to `Rule (confirmed by the user <date>)`) unless the user objects.
3. The user was asked to look at Indy3 EGA rooms 73, 6, 92, 15, 28, 20, 32, 1, 38 in the real window (rooms 20, 28, 32 come out mostly black) and to listen to the v5 sounds (already accepted). Ask what they saw; wrong rooms are the next S5 follow-up.

## How this user wants the work run
- Delegate: they give a stage and a family ("S5 MonkeyFloppy"), you plan, spawn one worktree agent per item, review, merge, write the checkpoint. No approval gates inside a run; they review at the checkpoint.
- Budget is limited: stop at a clean checkpoint, never leave half-done work. A rate limit kills agents mid-run: check the worktree for uncommitted work and resume the same agent with SendMessage.
- Merge by `git merge master` inside the agent's worktree (rebase hits the same conflict on every commit), resolve (nearly always keep both), build, run the full suite (about 2.5 min, expect 0 failed), then `git merge --ff-only` into master.
- Documentation only in the fixed places (instructions section 7). Remind every agent; it was needed once.
- Games paths: only `Games/<game folder>`, never subfolders of `Games`.
- The agents cannot hear or see the real window: collect those checks for the user in the checkpoint. The bundled ScummVM (`scummvm/`) could not be driven headlessly by an agent (white window, SDL error dialog on the user's desktop): do not tell an agent to run it; use source-level comparison.
- Raise a matrix cell only with its test; the user may raise one on their own judgement (they did for Maniac Mansion costumes and v5 sound). Revert is one registry line.

## Suggested next runs (pick with the user)
- `S5 MonkeyFloppy` (ready; S4b done). Can run in parallel with an Indy3 follow-up only if they touch different engine files.
- `S6 Indy3Ega`: first write the Play requirement for a verb-based game as `Proposed` rows (instructions S6), then build.
- `S4b` for the other families needs an `IScummDataSource` first (v1/v2/v5 have none): Indy3Vga (v4, like MonkeyFloppy) is the cheapest next, then MonkeyCd/Monkey2/Indy4 (v5), then Maniac/Zak.
- Leftover polish with no cell effect: Zak's 10 estimated costume colours (needs a session), v5 script hooks (need S6), 16 Indy3 rooms that never settle.

## Housekeeping
Agent worktrees remain under `.claude/worktrees/` (merged, safe to remove with the clean-up tool when the user agrees). `CLAUDE.md` still lists Indy3 as "backgrounds and texts only"; update its first paragraph when S6 lands. Commit with the attribution line from the system reminder; end the session with a fresh `NEXT_SESSION_PROMPT.md`.
