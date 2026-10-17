# Maniac Mansion / Zak McKracken handoff

Scope: ManiacClassic, ManiacEnhanced, ZakClassic, ZakEnhanced only. Another orchestrator owns LOOM, Indy3 and Monkey Island. Do not edit `NEXT_SESSION_PROMPT.md`.

Read `docs/GAME_SUPPORT_INSTRUCTIONS.md` in full, `CLAUDE.md`, `docs/CURRENT_WORK.md`, `docs/checkpoints/20261017-S5-ManiacZak.md`, and the Maniac/Zak trackers before resuming. Follow the checkpoint's verified result and next item; do not infer visual acceptance from zero detector findings.

Roadmap: S4b boot and stable first playable room; S5 first-visit objects; S6 verb-based play per REQUIREMENTS §7.1; S7 derived cutscenes; S9 edition checks. S8 is excluded unless requested. Preserve the current classic costume distinction and labelled estimates.

One family/stage per worktree agent. Parallelize independent analysis, tests and records; assign one owner per shared engine file. v1/v2 opcodes belong in their own `ScummBoundedScriptExecutor` partial. Shared profile/variable/data-source/session/UI edits stay small and additive.

Integration: `git merge master` in the agent worktree; resolve keeping both orchestrators' work; regenerate README/REQUIREMENTS matrix using `SCUMM_UPDATE_SUPPORT_MATRIX=1`; build and run the full suite with `SCUMM_SOUND_TEST_ROOT=D:\Juegos\Lucas\loom ega mt32 en`, `SCUMM_INDY3_EGA_TEST_ROOT=D:\Juegos\Lucas\indy3 ega en`, `SCUMM_MT32_ROM_ROOT=D:\Juegos\Lucas\mt32`. Require zero failures, then `git merge --ff-only` into master. If master moved, repeat. Never force or stash.

All shell commands use RTK. Data root is `D:\Juegos\Lucas` (`TestGames.Root`); never create a Games junction or commit data/ROMs/saves. Do not launch ScummVM (`D:\Juegos\scummvm`); source evidence and original-game observation are separate. Before any worktree removal inspect reparse points (`cmd /c dir /AL`) and remove junctions on their own first.

S4b is complete for all four checked fixtures. ManiacClassic and ManiacEnhanced use three labelled room clicks to choose Syd/Bernard/Start, then reach stable Room 44 control. ZakClassic and ZakEnhanced reach stable Room 1 control after their full openings without supplied player input. Read the checkpoint and per-family records for exact frames, tests and remaining estimates. No support cells were raised by S4b; no original ScummVM execution or visual/timing comparison is claimed.

S5 source previews now cover all four families: 221/221 rooms open, 216 without detector findings, sixteen real-window checks pass. Objects remains Partial. Original-game visual acceptance remains open: ScummVM execution was prohibited. Retain classic Maniac Room 18's six native black rectangles; classic Zak Room 44 and enhanced Zak rooms 13/16/44 also retain investigated black regions. Zak rooms 3/33 still have camera uncertainty; dark rooms use the labelled shown-lit preview. Review native old actor movement drawing offsets (+8/+16) before claiming exact placement. Frame bounds and unplayed-route assumptions are recorded, not hidden.

Start by resolving the remaining S5 findings with source-backed evidence, then S6 verb-based play per REQUIREMENTS section 7.1, parallel by family with exclusive shared-file ownership. Preserve ManiacClassic's accepted costume status and ZakClassic's 24 derived / 11 estimated breakdown. Follow with S7 and S9; do not infer those stages from successful boots or catalog previews. Worktrees remain available; do not remove them without the reparse-point checks above.
