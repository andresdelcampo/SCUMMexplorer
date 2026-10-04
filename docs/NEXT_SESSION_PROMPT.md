# Next session

Continue SCUMMexplorer in this chat or a fresh session. Read CLAUDE.md, [CURRENT_WORK.md](CURRENT_WORK.md) (rules), [LOOM_EGA_PLAN.md](LOOM_EGA_PLAN.md) (state, open items, next phase) and [HARNESS_REVIEW_2026-10-04.md](HARNESS_REVIEW_2026-10-04.md) (what was kept, removed and why). Finished work is in [CHANGELOG.md](CHANGELOG.md); older hand-offs are in [HANDOFF_HISTORY_2026-10.md](HANDOFF_HISTORY_2026-10.md) and [PLAN_HISTORY_2026-10.md](PLAN_HISTORY_2026-10.md).

## State (end of the 4 October late session)

LOOM EGA English (`Games/loom ega en`) is the reference edition. Build clean (0 warnings); the filtered tests of each change pass. Not pushed.

Done in the demos session (records: [.verification/demos-20261004/README.md](../.verification/demos-20261004/README.md), [.verification/room02-mountain-20261004/README.md](../.verification/room02-mountain-20261004/README.md)):

1. **The two LOOM demos open** (`Games/loom ega short demo en`, `long demo en`): another build, not a subset. The scanner keeps the index entries that start a resource and read as their kind (`GameLibrary.IsDemo`); the glossary leaves out script, bit, actor and sound names for a demo; rooms are explorable in their first-visit state; the self-running preview (script 045 there) is "The preview" in the Play Cutscene launchers; blank carrier rooms (no objects, one flat colour) are no scenes; a catalog visit dresses the ego as the demo's own preview does (`LoomDemoLooks`). Tools: `DemoProbe` (`index`, `texts`, `play`, `idle`, `ego`, `walk … mask`, `adjust`, `objects`, `verbs`, `shot`, `findload`), `DemoPreviewCheck`, tests `LoomDemoTests`.
2. **Idle frame clock** counts on from the due time (15 ms tick): room animations no longer run at about half the playback clock's speed.
3. **Sound panel:** "Preparing…" until the wave exists, the length from the rendered wave, "No playable audio" or "Muted" where there is none (`SoundPanelCheck`).
4. **Depth mask:** images shown only by the artwork estimate paint no mask (Room 02's object 157 cut Bobbin 12 pixels early, along its edge at x = 560). The earlier "matches ScummVM" note was wrong for the app and is corrected. The user confirmed the fix on screen.
5. **Task 3, the English harness run, triaged: no app failure.** `DoorwayClickCheck` passes on its six rooms (2, 3, 4, 8, 10, 37; with the whole game it freezes the window for many minutes, because every click's frames run at once on the UI thread). The others fail for harness-only reasons: tools that take other arguments (`CostumeAnimRender`, `MessageRender`, `VisitProbe`, `SceneShots`, `Blockers` wants an output file, `ReplayAudit`, `MembershipAudit`, `CacheCheck`), and `TextGalleryCheck` searches for the Spanish words "acantilado" and "cielo" and Spanish object 165 (one check fails on English). Not fixed.

## Decided with the user

- Order of the other versions: LOOM demos (done), then **Indy3 EGA as the second explorable game** (next). M5 (secrets guide) is parked.
- A demo plays both ways: rooms explorable, the preview as a cutscene, no new button.
- Harness review: obsolete harnesses removed, three repaired; archive folders are records, not harnesses. Room17Check's interrupted walk is expected.

## Next: Indy3 EGA (`Games/indy3 ega en`)

Found earlier: the generic session opens all 40 Indy3 rooms tried without errors; the app creates a session only for LOOM, so Indy3 shows plain backgrounds; about 40 `GameId.Loom` gates in `MainWindow.xaml.cs` decide what Indy3 gets. Indy3 needs its own dialect checks (`ScummScriptDialects.For(game.Id)`), dialogue choices (verbs 120–125) and texts; backgrounds and texts are the stated scope.

## Open items (details in the plan)

- User-only checks: Room 43 staff scenes, El Muerto and Room 17 sizes, actor 4 placement, draft verbs on screen; Room 34 local200 +0035 needs a real ScummVM save. (Room 02 mountain mask: closed.)
- Evidence-bound: Silence/Shaping/Sharpening draft names, glossary growth, "Used by" literal-only, 17 costume leftovers, Room 70 truncated stored text, engine gaps only if a route needs them. Glossary notes: Room 18 entry, Room 69 local 203 and Room 35 entry differ between the editions; English has scripts 051 and 116 the Spanish data does not decode.
- Demos, not examined: the demo-only rooms (72–84) draw black and are hidden as carriers; object names and verbs of the demo rooms against the full game; the long demo's rooms the director never enters.
- Not verified: placers other than Room 09, 23 and 51 were not checked for costume against the game's own assignment.
- Housekeeping: 2 nullable warnings (`MainWindow.xaml.cs`), README refresh, `.claude/worktrees` (17 historical worktrees), `CLEANUP_REVIEW.md` proposals, untracked `.verification/english-catalog-20261004/*.json` and `.md` (pack into `Exe/_evidence-archive/` before committing). Evidence packets from earlier sessions are in `Exe/_evidence-archive/` (`tar -xzf` to read one).

## Standing rules

- Name only what the script map, the game's text or play supports, and show the evidence level. Estimates are labelled; no invented story flags or one-off teleports. Estimates of artwork must never change gameplay state (the depth mask above).
- State rules, Advanced-only technical views and one short subtitle per window: see CURRENT_WORK.md.
- Test sparingly: filtered unit tests and one targeted harness (UiShots, SceneShots, StatusShots, DemoPreviewCheck) per change. Commit each finished task separately, no push. Ask before editing CLAUDE.md.
- Another session may work in the same tree: stage files by explicit path, never `git add -A`; do not run `git stash`.
- Tooling: in this environment backslash escapes in bash heredocs are altered; write scripts and C# with backslashes using the Write tool; for commit messages use `git commit -F <file>`. `.verification/**/*.txt`, `*.png` and `*.tsv` are git-ignored (use `git add -f` for golden files). Harness executables are `Exe/<Name>/Debug/net10.0-windows/<Name>.exe`; most take `<Games\loom> <fresh output folder>`; a WPF check that opens a modal window will wait for ever, so run them with a time limit. The bundled ScummVM (`scummvm/`) can be driven for comparisons with `.verification/room02-mountain-20261004/scummvm-window.ps1` (screenshots and clicks; refuse to send keys when the window is not found); its debugger (Ctrl+Alt+D) has `actors`, `box`, `objects`, `room`.

> Ready-to-copy prompt: Continue SCUMMexplorer from the latest commit using docs/NEXT_SESSION_PROMPT.md, docs/CURRENT_WORK.md, docs/LOOM_EGA_PLAN.md and .verification/README.md. English LOOM (Games/loom ega en) is the reference edition. The two LOOM demos work (see "State"). Task: start Indy3 EGA (Games/indy3 ega en) as the second explorable game: backgrounds, texts and dialogue choices only; read the ~40 `GameId.Loom` gates in MainWindow.xaml.cs first and propose which become generic before changing them. Standing rules: technical views stay Advanced-only; verify with filtered tests and one targeted harness, not the whole suite; commit each step, no push; stage files by explicit path; never key code on byte offsets, bit numbers or object ids from Spanish notes; ask before editing CLAUDE.md.
