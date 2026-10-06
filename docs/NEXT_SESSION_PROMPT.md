# Next session

Continue SCUMMexplorer in this chat or a fresh session. Read CLAUDE.md, [CURRENT_WORK.md](CURRENT_WORK.md) (rules), [LOOM_EGA_PLAN.md](LOOM_EGA_PLAN.md) (state, open items, next phase) and [HARNESS_REVIEW_2026-10-04.md](archive/HARNESS_REVIEW_2026-10-04.md) (what was kept, removed and why). Finished work is in [CHANGELOG.md](CHANGELOG.md); older hand-offs are in [HANDOFF_HISTORY_2026-10.md](archive/HANDOFF_HISTORY_2026-10.md) and [PLAN_HISTORY_2026-10.md](archive/PLAN_HISTORY_2026-10.md).

> The prompt itself is not kept here: the session that ends starts the next one as a task (`spawn_task`). This file holds the details of the project state the prompt points to.

## State (6 October, later): the room button's lists are saved between launches

Committed (not pushed), record `.verification/loom-vga/README.md` step 22, changelog row. `CdRoomScenesCache` keeps the per-room scene lists between launches (reopen of the last game uses it; the Open button re-derives); a first launch shows the button disabled with a tooltip. Local `master` was fast-forwarded to the branch so the user's main checkout builds it (the user's earlier "no button" was an old exe). To look at by hand: the first-launch placeholder and the instant reopen. 18 rooms show no button (kept hidden). 203 of 204 (99.5 %) enabled. Not heard by ear.

## State at a glance (6 October)

LOOM CD (`Games/loom vga en`) opens with rooms, a live session, voice, Game Status, "CD audio" and "Sounds & music" (never remove), "The intro" and 204 cutscenes derived from the CD scripts; 203 of 204 (99.5 %) are enabled (Room 34's entry is unreachable). The room's "Play Cutscene" button is in 60 of 78 rooms; the 18 without a scene stay hidden. Per-step detail: [.verification/loom-vga/README.md](../.verification/loom-vga/README.md) and [CHANGELOG.md](CHANGELOG.md); the state sections of earlier sessions are in [HANDOFF_HISTORY_2026-10.md](archive/HANDOFF_HISTORY_2026-10.md).

## Decided with the user

- Order (5 October): MT-32 is built, M5 is shelved. The game folders are renamed (done: `loom ega mt32 en`/`es` are the Roland copies and the reference; `loom ega en`/`es` the plain game; demos `loom demo short|long ega en`; `loom ega v1.2 es`/`de`). **The editions pass is done** (record `.verification/editions-20261005/`; no replay catalog for editions other than `loom ega mt32 en`, decided by the user; Hebrew dropped), **LOOM VGA is under way** (room backgrounds open in the app, step 3a of the plan; next: object images and records, then the v4 scripts; see the plan's "Status (5 October)" paragraph) (`Games/loom vga en` and `loom vga en kixx`, `.cue`/`.bin` images; decided: a folder or a disc image behind one game file source; CD audio last; the app auto-identifies the edition). **Indy3 is on hold** until the user moves its data from `Games/FUTURE/` to `Games/`.
- A demo plays both ways: rooms explorable, the preview as a cutscene, no new button.
- Harness review: obsolete harnesses removed, three repaired; archive folders are records, not harnesses. Room17Check's interrupted walk is expected.

## On hold: Indy3 EGA (`Games/indy3 ega en`)

Found earlier: the generic session opens all 40 Indy3 rooms tried without errors; the app creates a session only for LOOM, so Indy3 shows plain backgrounds; about 40 `GameId.Loom` gates in `MainWindow.xaml.cs` decide what Indy3 gets. Indy3 needs its own dialect checks (`ScummScriptDialects.For(game.Id)`), dialogue choices (verbs 120–125) and texts; backgrounds and texts are the stated scope.

## Open items (details in the plan)

- User-only checks: Room 43 staff scenes, El Muerto and Room 17 sizes, actor 4 placement, draft verbs on screen; Room 34 local200 +0035 needs a real ScummVM save. (Room 02 mountain mask: closed.)
- Evidence-bound: Silence/Shaping/Sharpening draft names, glossary growth, "Used by" literal-only, 17 costume leftovers, Room 70 truncated stored text, engine gaps only if a route needs them. Glossary notes: Room 18 entry, Room 69 local 203 and Room 35 entry differ between the editions; English has scripts 051 and 116 the Spanish data does not decode.
- Demos, not examined: the demo-only rooms (72–84) draw black and are hidden as carriers; object names and verbs of the demo rooms against the full game; the long demo's rooms the director never enters.
- Not verified: placers other than Room 09, 23 and 51 were not checked for costume against the game's own assignment.
- Housekeeping: `.claude/worktrees` (17 historical worktrees), `CLEANUP_REVIEW.md` proposals (docs were regrouped on 4 October: see docs/README.md), untracked `.verification/english-catalog-20261004/*.json` and `.md` (pack into `Exe/_evidence-archive/` before committing). Evidence packets from earlier sessions are in `Exe/_evidence-archive/` (`tar -xzf` to read one).

## Standing rules

- Name only what the script map, the game's text or play supports, and show the evidence level. Estimates are labelled; no invented story flags or one-off teleports. Estimates of artwork must never change gameplay state (the depth mask above).
- State rules, Advanced-only technical views and one short subtitle per window: see CURRENT_WORK.md.
- Test sparingly: filtered unit tests and one targeted harness (UiShots, SceneShots, StatusShots, DemoPreviewCheck) per change. Commit each finished task separately, no push. CLAUDE.md may be edited (user allowed it on 4 October).
- Another session may work in the same tree: stage files by explicit path, never `git add -A`; do not run `git stash`.
- Tooling: in this environment backslash escapes in bash heredocs are altered; write scripts and C# with backslashes using the Write tool; for commit messages use `git commit -F <file>`. `.verification/**/*.txt`, `*.png` and `*.tsv` are git-ignored (use `git add -f` for golden files). Harness executables are `Exe/<Name>/Debug/net10.0-windows/<Name>.exe`; most take `<Games\loom> <fresh output folder>`; a WPF check that opens a modal window will wait for ever, so run them with a time limit. The bundled ScummVM (`scummvm/`) can be driven for comparisons with `.verification/room02-mountain-20261004/scummvm-window.ps1` (screenshots and clicks; refuse to send keys when the window is not found); its debugger (Ctrl+Alt+D) has `actors`, `box`, `objects`, `room`.
