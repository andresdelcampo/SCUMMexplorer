# Next session

Continue SCUMMexplorer in this chat or a fresh session. Read CLAUDE.md, [CURRENT_WORK.md](CURRENT_WORK.md) (rules), [LOOM_EGA_PLAN.md](LOOM_EGA_PLAN.md) (state, open items, next phase) and [HARNESS_REVIEW_2026-10-04.md](HARNESS_REVIEW_2026-10-04.md). Finished work is in [CHANGELOG.md](CHANGELOG.md); older hand-offs are in [HANDOFF_HISTORY_2026-10.md](HANDOFF_HISTORY_2026-10.md) and [PLAN_HISTORY_2026-10.md](PLAN_HISTORY_2026-10.md).

## State

LOOM EGA (Spanish DOS) is functionally complete for the user's priorities; build clean, 703 unit tests pass, doorway check 16/16, RoomAudit zero gaps, 234/235 cutscene sites audited. The 4 October review found no game-behaviour regression: the failing harnesses are stale or one-off (listed with a suggested fix or removal).

## First, with the user

1. Decide on the harness review: which stale checks to repair, which probes to remove, whether to keep the 5 skipped diagnostic unit tests (nothing was deleted).
2. Decide whether M5 (secrets guide) comes before or after other-version support.
3. Agree the first step of the other-versions phase in the plan ("Next phase", proposal): separating LOOM-specific from generic code, then an English LOOM/Indy3 EGA, then Indy3 as the second explorable game.

## Open items (details in the plan)

- User-only checks: Room 43 staff scenes, El Muerto and Room 17 sizes, actor 4 placement, draft verbs on screen, Room 02 mountain mask; Room 34 local200 +0035 needs a real ScummVM save.
- Evidence-bound: Silence/Shaping/Sharpening draft names, glossary growth, "Used by" literal-only, 17 costume leftovers, Room 70 truncated stored text, engine gaps only if a route needs them.
- Housekeeping: 2 nullable warnings (`MainWindow.xaml.cs:3691-3692`), README refresh, `.claude/worktrees` (17 historical worktrees) and `CLEANUP_REVIEW.md` proposals.

## Standing rules

- Name only what the script map, the game's text or play supports, and show the evidence level. Estimates are labelled; no invented story flags or one-off teleports.
- State rules, Advanced-only technical views and one short subtitle per window: see CURRENT_WORK.md.
- Test sparingly: filtered unit tests and one targeted harness (UiShots, SceneShots, StatusShots) per change. Commit each finished task separately, no push. Ask before editing CLAUDE.md.
- Another session may work in the same tree: stage files by explicit path, never `git add -A`.
- Tooling: in this environment backslash escapes in bash heredocs are altered; write scripts and C# with backslashes using the Write tool. `.verification/**/*.txt` and `*.tsv` are git-ignored (use `git add -f` for golden files). Harness executables are `Exe/<Name>/Debug/net10.0-windows/<Name>.exe`; most take `<Games\loom> <fresh output folder>`; a WPF check that opens a modal window will wait for ever, so run them with a time limit (the driver in `.verification/harness-review-20261004/run-all.ps1` kills a harness whose extra window stays open 30 s).

> Ready-to-copy prompt: Continue SCUMMexplorer from the latest commit using docs/NEXT_SESSION_PROMPT.md, docs/CURRENT_WORK.md and docs/HARNESS_REVIEW_2026-10-04.md. LOOM EGA is functionally complete; begin by settling with me the harness review decisions and the first step of the other-versions phase (docs/LOOM_EGA_PLAN.md "Next phase"). Keep the standing rules: name only what the script map, the game's text or play supports and show the evidence level; technical views stay Advanced-only; verify with the UiShots harness and filtered tests, not the whole suite; commit each step, no push; stage files by explicit path because another session may work in the same tree; ask before editing CLAUDE.md.
