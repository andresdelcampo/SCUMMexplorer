# Next session

Continue SCUMMexplorer in this chat or a fresh session. Read CLAUDE.md, [CURRENT_WORK.md](CURRENT_WORK.md) (rules), [LOOM_EGA_PLAN.md](LOOM_EGA_PLAN.md) (state, open items, next phase) and [HARNESS_REVIEW_2026-10-04.md](HARNESS_REVIEW_2026-10-04.md) (what was kept, removed and why). Finished work is in [CHANGELOG.md](CHANGELOG.md); older hand-offs are in [HANDOFF_HISTORY_2026-10.md](HANDOFF_HISTORY_2026-10.md) and [PLAN_HISTORY_2026-10.md](PLAN_HISTORY_2026-10.md).

## State

LOOM EGA (Spanish DOS; English DOS in progress, see below) is functionally complete for the user's priorities; build clean, 703 unit tests pass, doorway check 16/16, RoomAudit zero gaps, 234/235 cutscene sites audited. The 4 October review found no game-behaviour regression; obsolete harnesses were removed and three repaired.

## Decided with the user (4 October)

- Harness review: obsolete harnesses removed, three repaired, `Exe/` compacted; the rest stays (see the review). Room17Check's interrupted walk is expected.
- M5 (secrets guide) is parked.
- Other versions go ahead in this order: (1) mark LOOM-specific vs generic code (documentation and small renames, no behaviour change), (2) an English LOOM / Indy3 EGA through the existing code, (3) Indy3 EGA as the second explorable game, (4) assess other containers and dialects later.

## Start here (set 4 October, end of session)

The English LOOM EGA release (`Games/loom ega en`) is now the reference: all unit tests pass on it, the replay catalog is regenerated for it (332 starts, 169/235 sites; Spanish stays in git history) and the hand-built scenes carry its offsets and object ids (see the migration section of [ENGLISH_COMPAT_2026-10-04.md](ENGLISH_COMPAT_2026-10-04.md)). The Spanish release is `Games/loom ega es`, kept for comparison. Next: (1) check the glossary's variable, script, room and actor names and `LoomEgaStateFlags` against the English data; (2) play a sample of the 332 catalog starts in the WPF picker (only a few were played); (3) run the WPF harnesses on the English folder with the review driver and look at the failures; (4) then phase 3, Indy3 EGA as the second explorable game. CLAUDE.md still says `Games/loom` and needs the user's approval to change.

Done this session: sphere vision rooms 19 and 86 start as a plain sphere (an entry reset to state 0 now beats a later script's draw when the background has no gap); the Advanced interaction menu places the speakers a direct verb needs (El Muerto, Room 09); the Advanced button rows share one size and a gap; UiShots repaired. Not reproducible by the user any more: Room 17/22 sizes. Needs a ScummVM reference before any change: Room 02, Bobbin vanishes early near the mountain (see `.verification/room02-mountain-mask-20260930`: mask is per pixel at the actual coordinates; costume 3 draws only about 24 pixels there). Room 43 cage scenes are fine when started from the 41 → 42 → 43 transition; other starts lack the scene's earlier state (accepted).

Also open: seven WPF harnesses print FAIL lines while exiting 0 (see the correction at the end of the harness review); SceneSoundCheck (the trial's music from the picker) and KeyInputCheck (note key, Escape during a draft) deserve a look first.

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

> Ready-to-copy prompt: Continue SCUMMexplorer from the latest commit using docs/NEXT_SESSION_PROMPT.md, docs/CURRENT_WORK.md, docs/LOOM_EGA_PLAN.md and docs/ENGLISH_COMPAT_2026-10-04.md. The English LOOM data (Games/loom ega en) is the reference now: tests pass and the catalog is regenerated. Check the glossary names and LoomEgaStateFlags against it, play a sample of the catalog in the picker, run the WPF harnesses on English, then start Indy3. Keep the standing rules: name only what the script map, the game text or play supports and show the evidence level; technical views stay Advanced-only; verify with filtered tests and one targeted harness; commit each step, no push; stage files by explicit path; ask before editing CLAUDE.md (it needs the new folder names).
