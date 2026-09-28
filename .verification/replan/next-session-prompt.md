Continue the SCUMMexplorer work. LOOM EGA is the main focus. Read CLAUDE.md first, then docs/LOOM_EGA_PLAN.md: "What the app shows today" and milestones M1–M3. Then read docs/LOOM_SCRIPT_MAP.md, especially "Object verbs", "Messages, charset and actor commands", "How the session maps ScummVM behaviour" and "Not modelled yet". docs/CHANGELOG.md lists the finished work.

State at the start:

* Git: master. The latest commit is the replan (docs only), on top of 92c2e3c "Visual-first room explorer UI refresh".
* `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 198/198 with SCUMM_SOUND_TEST_ROOT=Games\loom and SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA.
* RoomAudit: 0 gaps in all 76 rooms.
* WPF checks (`.verification\wpf-checks.ps1 -OutRoot <fresh folder>`): everything passes except CameraPanCheck (2 FAIL, "Room 02/04 pan off") and VerbAreaCheck (1 FAIL, "off again"). Both date from the UI refresh.
* Blockers baseline: .verification/room-owned-loads/blockers-after.txt (5839).

Done last session (replan, no code): the user's priorities are now what they see. First, object interactions do something. Second, cutscenes and animations play. Third, text appears on screen. The session already runs `print` (1,170 sites), `animateActor` (1,853) and cutscenes, but the app draws none of it: no message layer, no charset, no actor drawing, no timed playback. Image-object clicks cycle artwork instead of running the game's click. The old small tasks (label wording, KeyInputCheck, the `expression`/`getActorCostume`/`getActorElevation` stops) are dropped or folded into M2.

Tasks, in order (M1 from the plan):

1. Decision first: ask the user whether a click on an object in the explore mode should go through the game's input (scripts 005–007: walk, verb 0x38, and verb 0x36 "look" on a double click), with artwork cycling moved to the inspection mode or Alt+click. This changes the CLAUDE.md product rule "Objects are clickable and cycle through their alternatives". Do not change that rule without the user's answer. Tasks 2–5 do not depend on it.
2. M1a, the message layer (Core). A reached `print` becomes a message: actor, position, center/left, colour, and text with control codes (`0xFF 0x01` new line; others from ScummVM string.cpp). Keep the source room, script and offset. Keep `VAR_HAVE_MSG` (`var[0x003]`) and the talk delay: `VAR_DEFAULT_TALK_DELAY` (or 60) plus `VAR_CHARINC` (`var[0x025]`) per character, minus the frame's jiffies each frame. `waitFor message` then waits for that many frames. The snapshot exposes the messages on screen. Add tests with Room 02 object 156 verb 0x36 ("Amanecer.") and object 154 verb 0x36 ("Ultima hoja del año.", after a walk and a cutscene).
3. M1b, the game's font (Core). Read charset 0 from Games/loom/99.LFL (ScummVM `ScummEngine_v3::loadCharset`: file `99 - n`, not XOR-encoded) and port `CharsetRendererV3` drawing. Render a message to a bitmap at its screen position with its colour and centring. If the charset cannot be read, use a UI font that is labelled as not the game's.
4. M1c/M1d in the app: draw the messages over the room at the game's position (talk text above the actor). After an interaction, advance the session on a timer at the game's frame rate (`VAR_TIMER_NEXT` 6 jiffies, 10 frames/s) until the scripts wait for input or the user interrupts, redrawing each frame. A click on the text dismisses it (presentation). Add a WPF check that captures the text layer in Room 02, and save the screenshots under .verification/messages/.
5. If there is room left: fix CameraPanCheck and VerbAreaCheck, or update them to the refreshed layout, since M1 touches the room view.
6. Then M2 (actors on screen), as the plan describes.

Rules:

* Judge every task by what it changes on screen. Say in the validation what the user now sees.
* Follow the evidence rules in CLAUDE.md and the plan. Messages and actors driven by reached events are game behaviour. Substitutes (a UI font, skipped waits) are labelled presentation and are optional. Label estimates and assumed input.
* Record new findings in docs/LOOM_SCRIPT_MAP.md and mark each as decoded, ScummVM or reading.
* For each task, record verification under .verification/<topic>/, update docs/LOOM_EGA_PLAN.md (status and next task) and add a row to docs/CHANGELOG.md.
* Rerun the audit and the WPF checks when src changes. If renders change, refresh the PNGs with `compare-png.ps1 -Refresh` from PowerShell.
* Commit each task separately once it builds and the tests pass.
* Once the session has used more than 250K tokens, stop at the next reasonable point (a committed task) instead of starting a new task.
* At the end of the session, write a prompt for the next session in this same format (state at start, what was done, ordered tasks, rules, tooling notes), and end it with this same request, including the 250K-token stopping rule.

Tooling notes:

* The Bash sandbox blocks some writes into the repo, and RTK filters grep/awk output. Use the Grep tool, or python over .verification/fallback-evidence/code.tsv (columns: owner, offset, operation, operands) and objects.tsv.
* Patch files with python: read bytes, normalise to \n, patch with an exact-count check, restore CRLF if the file had it, and keep a BOM only if the file had one. LoomExplorationSession.cs, MainWindow.xaml.cs and LoomVerbs.cs are CRLF without a BOM. MainWindow.xaml is LF with a BOM. Other files are LF.
* Use the PowerShell tool for harness runs, with `Set-Location` to the repo root first. Rebuild every harness after changing src, because each has its own copy of Core. wpf-checks.ps1 builds them all. Do not edit files while it runs, always write to a fresh output folder, and count PASS/FAIL with -cmatch.
* VisitProbe (.verification/opcodes/VisitProbe) steps: `R`, `R/O/V`, `varNNN=`, `+N`, `sounds`, `?052`, `@x,y`, `!x,y`, `!!x,y`, `v:x,y`, `k:0x1B`, `s:34:a,b`. Pass only objects in that room and indexed rooms.
* Branch semantics: the disassembly's `branch` is taken when the condition is false.
* ScummVM sources: curl raw files from github.com/scummvm/scummvm engines/scumm (string.cpp, charset.cpp, actor.cpp, costume.cpp, resource_v3.cpp) into the scratchpad.
* Commit messages: write them to a file and use `git commit -F`.
