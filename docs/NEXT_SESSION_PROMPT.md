# Next session

Continue SCUMMexplorer in this chat or a fresh session. Read CLAUDE.md, [CURRENT_WORK.md](CURRENT_WORK.md) (rules), [LOOM_EGA_PLAN.md](LOOM_EGA_PLAN.md) (state, open items, next phase) and [HARNESS_REVIEW_2026-10-04.md](HARNESS_REVIEW_2026-10-04.md) (what was kept, removed and why). Finished work is in [CHANGELOG.md](CHANGELOG.md); older hand-offs are in [HANDOFF_HISTORY_2026-10.md](HANDOFF_HISTORY_2026-10.md) and [PLAN_HISTORY_2026-10.md](PLAN_HISTORY_2026-10.md).

## State (end of the 4 October night session)

LOOM EGA English (`Games/loom ega en`) is the reference edition. Build clean (0 warnings); the relevant filtered tests pass (planner, replay, actor, glossary, first-visit, cutscene: 117 passed). Not pushed.

Done this session (details in [CHANGELOG.md](CHANGELOG.md) and [ENGLISH_COMPAT_2026-10-04.md](ENGLISH_COMPAT_2026-10-04.md)):

1. Room animation runs between inputs (idle frame clock in `MainWindow`); the Options expander always starts collapsed; the three build warnings are fixed.
2. One rule for actors (the user's words: an actor appearing as an object, any interaction should show him if he was not in the scene; the same for cutscenes): the planner brings in every actor the scene or interaction uses (lines, walks, turns, animations) without placing him, plus the actor that *is* the named object (the game's own `putActorAtObject`, or a room-local `putActor` inside the object's rectangle), and dresses him as the game does (`LoomActorPlacer.Dress`). Room 09: Dead One 275 = actor 4, Hetchel 273 = actor 5, egg 270 = actor 9, distaff 271 = actor 11. Used by the Advanced interaction menu and the picker.
3. Task 1 of the English check: glossary compared with `.verification/GlossaryCheck` (report in `.verification/glossary-english-20261004/`): bits, variables, scripts, rooms and sounds hold; actor names corrected and scoped (Mandible, Cob, Rusty, Mr. Stoke, the Dead One); Game Texts uses scene-aware names; story flags: 88 English bits, 23 named, each matching the Spanish flag one bit higher. Presenter goldens are per edition.
4. Task 2: 25 catalog starts played in the real window (`.verification/english-catalog-20261004/sample-notes.md`): no failure.

## Decided with the user (4 October)

- Order of the other versions: the LOOM demos first (next), then Indy3 EGA as the second explorable game. **Do not start Indy3 before the demos work.** M5 (secrets guide) is parked.
- Harness review: obsolete harnesses removed, three repaired; archive folders are records, not harnesses. Room17Check's interrupted walk is expected.

## Start here: the two LOOM demos (`Games/loom ega short demo en`, `Games/loom ega long demo en`)

The user added the self-running LOOM previews (`LOOMDEMO.EXE`; 18 and 34 `.LFL` files; 16 and 32 rooms: a subset of the full game's rooms, same room numbers) and says they "should be a subset of what we have, but not quite opening as expected". Done so far: `GameFolderScanner` identifies `LOOMDEMO.EXE` as LOOM (title "LOOM (demo)"; before, the folder was an unknown game). Tool `.verification/DemoProbe` (`DemoProbe <folder>`) prints what the code makes of a folder. First probe:

- Both open as LOOM old-v3 (index parsed; 16 / 32 rooms; 249 / 263 indexed resources). Every room opens in a session without an exception.
- **00.LFL still lists the full game's resources.** 30 of 68 scripts (short) and 35 of 68 (long) fail to decode: the entries point at container files the demo lacks (`20.LFL` missing), past the end of a file (`Script 14 declares 51968 bytes at 0x51F2, past the end of 01.LFL`), or at zero-length entries. Costumes (122) and sounds (41 / 39) are listed the same way. The scanner should keep only resources that exist and are valid in the folder (a demo is a subset; its index is not), and the tree, the counts and the text extractor must not claim the missing ones.
- A session starts with `BootRunning` false and no frames: the demo's script 001 may be another boot (a self-running preview). Read the demo's boot and opening scripts with `ScriptDump <folder> <script id>` before assuming the full game's boot.
- Unchecked: the app itself (room tree, thumbnails, text gallery, texts), rooms whose code differs from the full game, and which of the replay catalog, glossary and presets apply (the catalog's hash check rejects the demos, so the picker has only the established starts; glossary names are per script number: run `GlossaryCheck` with a demo as the first folder and the full English game as the reference).

Suggested order: (1) the scanner keeps only present, valid resources and says it is a demo; (2) open each demo in the real window with UiShots (rooms, objects, texts) and note what looks wrong; (3) read the demos' boot and opening scripts and decide how a self-running preview is shown (play it with the existing playback clock from its own entry); (4) compare with the full game's rooms (`ScriptDump`, `GlossaryCheck`) and label what differs; (5) test each change with filtered tests and one targeted harness. Ask the user before choosing between "play the preview" and "explore its rooms" if both are needed.

## Still open from the four tasks

- Task 3, the WPF harness run on the English folder: `.verification/harness-review-20261004/run-all.ps1 -Tag eng2` (the driver now skips `archive` and the argument-taking tools) was stopped after 6 results; `out/results-eng2.tsv` resumes it. Triage the failures into harness-only and app issues (see the end of [HARNESS_REVIEW_2026-10-04.md](HARNESS_REVIEW_2026-10-04.md)); SceneSoundCheck and KeyInputCheck first. Do not build other projects while it runs (builds collide).
- Task 4, Indy3 EGA as the second explorable game (`Games/indy3 ega en`), waits for the demos. Found: the generic session opens all 40 Indy3 rooms tried without errors; the app creates a session only for LOOM, so Indy3 shows plain backgrounds; about 40 `GameId.Loom` gates in `MainWindow.xaml.cs` decide what Indy3 gets.
- Glossary notes: Room 18 entry, Room 69 local 203 and Room 35 entry differ between the editions; English has scripts 051 and 116 the Spanish data does not decode.
- Not verified: placers other than Room 09, 23 and 51 were not checked for costume against the game's own assignment; an actor already in the room with a stale costume is left alone.

## Open items (details in the plan)

- User-only checks: Room 43 staff scenes, El Muerto and Room 17 sizes, actor 4 placement, draft verbs on screen (the Room 02 mountain mask matches ScummVM: closed); Room 34 local200 +0035 needs a real ScummVM save.
- Evidence-bound: Silence/Shaping/Sharpening draft names, glossary growth, "Used by" literal-only, 17 costume leftovers, Room 70 truncated stored text, engine gaps only if a route needs them.
- Housekeeping: 2 nullable warnings (`MainWindow.xaml.cs:3691-3692`), README refresh, `.claude/worktrees` (17 historical worktrees) and `CLEANUP_REVIEW.md` proposals. Evidence packets from earlier sessions are packed in `Exe/_evidence-archive/` (`tar -xzf` to read one).

## Standing rules

- Name only what the script map, the game's text or play supports, and show the evidence level. Estimates are labelled; no invented story flags or one-off teleports.
- State rules, Advanced-only technical views and one short subtitle per window: see CURRENT_WORK.md.
- Test sparingly: filtered unit tests and one targeted harness (UiShots, SceneShots, StatusShots) per change. Commit each finished task separately, no push. Ask before editing CLAUDE.md.
- Another session may work in the same tree: stage files by explicit path, never `git add -A`.
- Tooling: in this environment backslash escapes in bash heredocs are altered; write scripts and C# with backslashes using the Write tool. `.verification/**/*.txt` and `*.tsv` are git-ignored (use `git add -f` for golden files). Harness executables are `Exe/<Name>/Debug/net10.0-windows/<Name>.exe`; most take `<Games\loom> <fresh output folder>`; a WPF check that opens a modal window will wait for ever, so run them with a time limit (the driver in `.verification/harness-review-20261004/run-all.ps1` kills a harness whose extra window stays open 30 s).

> Ready-to-copy prompt: Continue SCUMMexplorer from the latest commit using docs/NEXT_SESSION_PROMPT.md (start with "Start here: the two LOOM demos"), docs/CURRENT_WORK.md, docs/LOOM_EGA_PLAN.md, docs/ENGLISH_COMPAT_2026-10-04.md and .verification/README.md. English LOOM (Games/loom ega en) is the reference edition. Task: make the two LOOM demos I added (Games/loom ega short demo en and Games/loom ega long demo en, self-running previews that are subsets of the full game's rooms) open properly. The scanner now recognises LOOMDEMO.EXE, but 00.LFL still lists the full game's resources, so about half the scripts, and the costumes and sounds that are missing, fail or point past the end of the files. Keep only the resources the folder really has, open each demo in the real window and note what looks wrong (rooms, objects, texts), read the demos' boot and opening scripts (ScriptDump) and decide with me how a self-running preview is played, and label what differs from the full game. Use .verification/DemoProbe first. Do not start Indy3 yet. Standing rules: technical views stay Advanced-only; verify with filtered tests and one targeted harness, not the whole suite; commit each step, no push; stage files by explicit path; never key code on byte offsets, bit numbers or object ids from Spanish notes; ask before editing CLAUDE.md. Afterwards, finish the English harness run (task 3) and triage its failures.
