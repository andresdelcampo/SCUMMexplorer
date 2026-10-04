# Next session

Continue SCUMMexplorer in this chat or a fresh session. Read CLAUDE.md, [CURRENT_WORK.md](CURRENT_WORK.md) (rules), [LOOM_EGA_PLAN.md](LOOM_EGA_PLAN.md) (state, open items, next phase) and [HARNESS_REVIEW_2026-10-04.md](HARNESS_REVIEW_2026-10-04.md) (what was kept, removed and why). Finished work is in [CHANGELOG.md](CHANGELOG.md); older hand-offs are in [HANDOFF_HISTORY_2026-10.md](HANDOFF_HISTORY_2026-10.md) and [PLAN_HISTORY_2026-10.md](PLAN_HISTORY_2026-10.md).

## State

LOOM EGA (Spanish DOS) is functionally complete for the user's priorities; build clean, 703 unit tests pass, doorway check 16/16, RoomAudit zero gaps, 234/235 cutscene sites audited. The 4 October review found no game-behaviour regression; obsolete harnesses were removed and three repaired.

## Decided with the user (4 October)

- Harness review: obsolete harnesses removed, three repaired, `Exe/` compacted; the rest stays (see the review). Room17Check's interrupted walk is expected.
- M5 (secrets guide) is parked.
- Other versions go ahead in this order: (1) mark LOOM-specific vs generic code (documentation and small renames, no behaviour change), (2) an English LOOM / Indy3 EGA through the existing code, (3) Indy3 EGA as the second explorable game, (4) assess other containers and dialects later.

## Start here

Begin with step 1: list which `Loom*` types, data files (`loom-glossary.json`, `loom-replays.json`, `LoomEgaStateFlags`, `LoomDraftNames`) and tests depend on LOOM data and which are generic SCUMM, write the result into docs/LOOM_EGA_PLAN.md, and propose the smallest renames that make the split visible. Ask before large renames.

## Open items (details in the plan)

- User-only checks: Room 43 staff scenes, El Muerto and Room 17 sizes, actor 4 placement, draft verbs on screen, Room 02 mountain mask; Room 34 local200 +0035 needs a real ScummVM save.
- Evidence-bound: Silence/Shaping/Sharpening draft names, glossary growth, "Used by" literal-only, 17 costume leftovers, Room 70 truncated stored text, engine gaps only if a route needs them.
- Housekeeping: 2 nullable warnings (`MainWindow.xaml.cs:3691-3692`), README refresh, `.claude/worktrees` (17 historical worktrees) and `CLEANUP_REVIEW.md` proposals. Evidence packets from earlier sessions are packed in `Exe/_evidence-archive/` (`tar -xzf` to read one).

## Standing rules

- Name only what the script map, the game's text or play supports, and show the evidence level. Estimates are labelled; no invented story flags or one-off teleports.
- State rules, Advanced-only technical views and one short subtitle per window: see CURRENT_WORK.md.
- Test sparingly: filtered unit tests and one targeted harness (UiShots, SceneShots, StatusShots) per change. Commit each finished task separately, no push. Ask before editing CLAUDE.md.
- Another session may work in the same tree: stage files by explicit path, never `git add -A`.
- Tooling: in this environment backslash escapes in bash heredocs are altered; write scripts and C# with backslashes using the Write tool. `.verification/**/*.txt` and `*.tsv` are git-ignored (use `git add -f` for golden files). Harness executables are `Exe/<Name>/Debug/net10.0-windows/<Name>.exe`; most take `<Games\loom> <fresh output folder>`; a WPF check that opens a modal window will wait for ever, so run them with a time limit (the driver in `.verification/harness-review-20261004/run-all.ps1` kills a harness whose extra window stays open 30 s).

> Ready-to-copy prompt: Continue SCUMMexplorer from the latest commit using docs/NEXT_SESSION_PROMPT.md, docs/CURRENT_WORK.md and docs/LOOM_EGA_PLAN.md. LOOM EGA is functionally complete; start the other-versions phase with step 1 (mark LOOM-specific versus generic code, documentation and small renames only). Keep the standing rules: name only what the script map, the game text or play supports and show the evidence level; technical views stay Advanced-only; verify with the UiShots harness and filtered tests, not the whole suite; commit each step, no push; stage files by explicit path because another session may work in the same tree; ask before editing CLAUDE.md.
