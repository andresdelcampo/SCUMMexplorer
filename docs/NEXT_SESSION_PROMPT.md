# Next session

Continue SCUMMexplorer in this chat or a fresh session. Read CLAUDE.md, [CURRENT_WORK.md](CURRENT_WORK.md) (rules), [LOOM_EGA_PLAN.md](LOOM_EGA_PLAN.md) (state, open items, next phase) and [HARNESS_REVIEW_2026-10-04.md](archive/HARNESS_REVIEW_2026-10-04.md) (what was kept, removed and why). Finished work is in [CHANGELOG.md](CHANGELOG.md); older hand-offs are in [HANDOFF_HISTORY_2026-10.md](archive/HANDOFF_HISTORY_2026-10.md) and [PLAN_HISTORY_2026-10.md](archive/PLAN_HISTORY_2026-10.md).

## State (5 October, evening): LOOM CD (VGA) is open for backgrounds, scripts and texts

Read [.verification/loom-vga/README.md](../.verification/loom-vga/README.md) first (the probe, formats and findings, steps 1 to 4). Done and committed (not pushed): the editions pass (record `.verification/editions-20261005/`, README "Verified editions"); the game file source and disc reader (`Core/Storage`), `LoomVgaArchive` (rooms, 256-colour backgrounds, scripts, object records), `ScummScriptDialect.LoomCd` (all 793 scripts read), the text extraction (1,196 texts; the Game Texts window opens, Advanced only), the scanner (`GameId.LoomVga`, a folder, `.cue`, `.iso` or `.bin`) and the app (room cards and scene view, background only). Tests: `LoomVgaTests` (12; they do nothing without `Games/loom vga en`).

**Next, in this order (user priorities: what is on screen, interactions, cutscenes, texts):**
1. **First-visit room state for the CD game.** The `0O` index gives every object state 0, so what a room shows comes from its entry script (EN) like on EGA. The EGA session (`LoomExplorationSession`, about 2,500 lines, and `LoomBoundedScriptExecutor`, about 2,800) reads rooms and scripts through `GameResource.FilePath` and `LoomScriptReader.Decode*`; it needs a data-source seam (rooms, global scripts, local scripts, entry and exit scripts, object records and images, costumes) before it can run the CD data. **Propose the seam to the user before changing the EGA session** (they asked to be consulted about the `GameId.Loom` gates). Global scripts: the index `0S` gives (room, offset) per script number; the `SC` blocks have no number of their own. Object images (`OI`): id, then a `BM`-like body, width from the object, height from the `OC` block; decode them once the states are known.
2. Costumes (`CO` blocks, 166) and Bobbin; walk boxes (`BX`, same idea as EGA); the charsets (`901`–`904.LFL`) for drawing text.
3. CD audio last (`Loom.cue`'s audio track starts at sector 22335 of the image; `soundKludge` and `print` sub 8 (`sayVoice`) carry the positions; ScummVM `o5_*` handles them).

Reference used: ScummVM's `engines/scumm` `script_v5.cpp`, `script_v4.cpp`, `script_v3.cpp` (downloaded to scratch from GitHub; read as reference, the code is our own). No ScummVM source is in the repository.

## State (5 October): MT-32 built, M5 shelved

MT-32 is done and committed (not pushed): [.verification/mt32/README.md](../.verification/mt32/README.md), changelog row 5 October. The user accepted the Mt32emu.net dependency and confirmed the device belongs in the sound dropdown and that the Roland card adds the new overture to the opening (other cards skip it). Rebuild before running: an older Release build lacks the device (rebuilt on 5 October). **M5 is shelved** by the user: it would mostly repeat what the cutscene picker and inspections reach; build it only for a real value they lack, never as a shortcut. **Uncommitted at the end of the 5 October planning session, not mine:** edits to `MainWindow.xaml`, `MainWindow.xaml.cs`, `SceneSoundPlayer.cs`, `.verification/Mt32Probe/Program.cs` and `.verification/RolandCheck/Program.cs` (MT-32 follow-up work from another session); check `git diff` and ask the user before committing or discarding them. **Next:** the user's listening check, then LOOM VGA (`Games/loom vga en` is in `Games/`: feasibility probe first) or what the user chooses.

## State (end of the 4 October late session)

LOOM EGA English (`Games/loom ega mt32 en`) is the reference edition. Build clean (0 warnings); the filtered tests of each change pass. Not pushed.

Done in the demos session (records: [.verification/demos-20261004/README.md](../.verification/demos-20261004/README.md), [.verification/room02-mountain-20261004/README.md](../.verification/room02-mountain-20261004/README.md)):

1. **The two LOOM demos open** (`Games/loom demo short ega en`, `loom demo long ega en`): another build, not a subset. The scanner keeps the index entries that start a resource and read as their kind (`GameLibrary.IsDemo`); the glossary leaves out script, bit, actor and sound names for a demo; rooms are explorable in their first-visit state; the self-running preview (script 045 there) is "The preview" in the Play Cutscene launchers; blank carrier rooms (no objects, one flat colour) are no scenes; a catalog visit dresses the ego as the demo's own preview does (`LoomDemoLooks`). Tools: `DemoProbe` (`index`, `texts`, `play`, `idle`, `ego`, `walk … mask`, `adjust`, `objects`, `verbs`, `shot`, `findload`), `DemoPreviewCheck`, tests `LoomDemoTests`.
2. **Idle frame clock** counts on from the due time (15 ms tick): room animations no longer run at about half the playback clock's speed.
3. **Sound panel:** "Preparing…" until the wave exists, the length from the rendered wave, "No playable audio" or "Muted" where there is none (`SoundPanelCheck`).
4. **Depth mask:** images shown only by the artwork estimate paint no mask (Room 02's object 157 cut Bobbin 12 pixels early, along its edge at x = 560). The earlier "matches ScummVM" note was wrong for the app and is corrected. The user confirmed the fix on screen.
5. **Task 3, the English harness run, triaged: no app failure.** `DoorwayClickCheck` passes on its six rooms (2, 3, 4, 8, 10, 37; with the whole game it freezes the window for many minutes, because every click's frames run at once on the UI thread). The others fail for harness-only reasons: tools that take other arguments (`CostumeAnimRender`, `MessageRender`, `VisitProbe`, `SceneShots`, `Blockers` wants an output file, `ReplayAudit`, `MembershipAudit`, `CacheCheck`), and `TextGalleryCheck` searches for the Spanish words "acantilado" and "cielo" and Spanish object 165 (one check fails on English). Not fixed.

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

> Ready-to-copy prompt: Continue SCUMMexplorer from the latest commit using docs/NEXT_SESSION_PROMPT.md, docs/CURRENT_WORK.md, docs/LOOM_EGA_PLAN.md and .verification/README.md. MT-32 is built and M5 is shelved. Task: the plan section "LOOM EGA editions, before VGA": first rename the Games folders as written there and update everything that names them (tests, scripts, docs, CLAUDE.md; `loom ega mt32 en` is the reference edition and the tests and harnesses are renamed to it), as its own commit; then try the plain editions and the new languages one by one. LOOM VGA comes after. Standing rules: technical views stay Advanced-only; verify with filtered tests and one targeted harness, not the whole suite; commit each step, no push; stage files by explicit path; never key code on byte offsets, bit numbers or object ids from Spanish notes.
