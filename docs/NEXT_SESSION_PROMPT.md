# Next session

Continue SCUMMexplorer in this chat or a fresh session. Read CLAUDE.md, [CURRENT_WORK.md](CURRENT_WORK.md) (rules), [LOOM_EGA_PLAN.md](LOOM_EGA_PLAN.md) (state, open items, next phase) and [HARNESS_REVIEW_2026-10-04.md](archive/HARNESS_REVIEW_2026-10-04.md) (what was kept, removed and why). Finished work is in [CHANGELOG.md](CHANGELOG.md); older hand-offs are in [HANDOFF_HISTORY_2026-10.md](archive/HANDOFF_HISTORY_2026-10.md) and [PLAN_HISTORY_2026-10.md](archive/PLAN_HISTORY_2026-10.md).

> The prompt itself is not kept here: the session that ends starts the next one as a task (`spawn_task`). This file holds the details of the project state the prompt points to.

## State (7 October)

Latest task: the [individual VGA Advanced Scripts review](../.verification/advanced-scripts/README.md) compares all 122 CD globals with EGA; each now has a source-reviewed title. `loom-cd-glossary.json` guards labels/scoped actors/flags with the reviewed CD instruction fingerprint. Script 7's clicked-object role and Script 16's layout are readable. CD Used by / Explore links now use CD data and offsets, preserving room-local namespaces and unresolved current-room targets. Focused tests and real WPF Script 5/7/16 comparisons/link navigation passed; the EGA readable goldens remain unchanged. Remaining unestablished story roles stay numeric.

Everything is committed on `master` (not pushed); the latest feedback record is [.verification/loom-parity/README.md](../.verification/loom-parity/README.md), earlier CD work is `.verification/loom-vga/README.md` (steps 25 and 26), and finished work is in [CHANGELOG.md](CHANGELOG.md). LOOM CD (`Games/loom vga en`) opens with rooms, a live session, voice, Game Status, "CD audio", "Sounds & music" (never remove), "The Intro" and the cutscenes derived from the CD scripts.

The user's 7 October issue list and follow-up clarifications are addressed; waiting for their viewing check or next changes:

- Room control buttons stay in one row (card width 880); Room 66 EGA Sounds button no longer blinks.
- Staff: note letters refresh before a catalog visit returns, independently of entry walks/camera motion; the costume refresh waits until entry settles. Reconciliation counts 32 editable frames; real input stops it. Both editions apply their source-established three-note browsing floor when held, preserving higher choices (CD Room 01 local 211 initializes its limit). CD Game Status also updates the normal large-costume default while Bobbin wears a small look. Room 69 suppresses staff/letters while retaining possession.
- Advanced sound rows share resource / kind / initiating source / minutes:seconds duration. EGA call chains and other references, and VGA precise disc timing/text, are in tooltips. Shared popup rows wrap long labels.
- Room 68's EGA thumbnail follows the script shadow palette in both initial rendering and selected/live/background replacement paths. The thumbnail stamp includes the App build too.
- Cutscene doorway titles are concise Reached Room labels; VGA reuses the room-hotspot finder, EGA strips redundant response wording. Both pickers add verb IDs in Advanced. Both room lists add `(played from Room NN)` for scenes started elsewhere. Both editions call the introductory sequence The Intro; the Opening spell keeps its name.
- VGA Room entry links open its global/local scripts with correct container/code offsets. Both editions share the Readable presenter and Raw toggle. CD variable names now cover source-verified input, notes/drafts, difficulty, costumes/animations, room and sound roles wherever used, including indexed arrays. Every decoded opcode family in all 1,470 supplied CD blocks (globals, room entry/exit, locals, object verbs) has a readable phrase. Names retain their evidence level; unestablished story roles remain numeric. EGA story/script numbers are not imported, and local IDs remain scoped by room.
- Game Status has Apply and close / Cancel: one click applies and exits or discards and exits. Applying an unchanged dialog also closes; stale/busy guards remain.

Latest app build: `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`, clean solution build. Latest Readable checks: all 1,470 CD blocks formatted without raw-operation fallbacks in a subsecond decoding-only test; named-role/operation checks and three unchanged EGA script goldens passed. A bounded Script 28 UI check for both editions passed in `ui-readable-final`. Earlier staff/popup checks remain in `ui-followup-02` and `ui-followup-vga`. No full suite or long replay harness. Physical-input/listening confirmation remains the user's check. Keep EGA/VGA behavior shared at the proper generic SCUMM / generic LOOM / edition-specific level; preserve working behavior when refactoring.

Open, not done:

- Compare the CD's starting-state planner with the EGA catalog (the script scan alone already finds all 328 catalogued EGA replays; `.verification/cutscene-scan-ega`, whose modes `verbs`, `script`, `grep`, `boxes`, `masks`, `maskmap` also read the CD).
- Why EGA's room cards and start take longer than the CD's (read from the code, not timed: EGA renders cards through the scene logic and starts two background indexes).
- Scenes that do not use the `cutscene` opcode (the Room 66 ending, script 106) are only Interact actions, on both games.
- The CD gap list in step 24; note-head height (now strip-aligned, not compared with the original game).

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
