Continue the SCUMMexplorer work. LOOM EGA is the main focus. Read CLAUDE.md first, then docs/LOOM_EGA_PLAN.md: "What the app shows today" and milestones M1–M3. Then read docs/LOOM_SCRIPT_MAP.md, especially "Object verbs", "Messages, charset and actor commands", "How the session maps ScummVM behaviour" and "Not modelled yet". docs/CHANGELOG.md lists the finished work.

State at the start:

* Git: master. The latest commit is 4c64b39 "Make the explore-mode click the game's click (M1e)", on top of the replan (docs only).
* `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 198/198 with SCUMM_SOUND_TEST_ROOT=Games\loom and SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA.
* RoomAudit: 0 gaps in all 76 rooms (Core has not changed since).
* WPF checks: everything passes except CameraPanCheck (2 FAIL, "Room 02/04 pan off") and VerbAreaCheck (1 FAIL, "off again"). Both date from the UI refresh. DoorwayClickCheck is 75/75: 29 through the game's click and 46 through the labelled direct-verb fallback.
* Blockers baseline: .verification/room-owned-loads/blockers-after.txt (5839).

Done last session: M1e (.verification/game-click/validation.md). In the explore mode, every room click is the game's click through `Click` (scripts 005–007). A single click walks and runs verb 0x38, and a double click adds 0x36. The "Click as game input" toggle is gone. `HandleRoomPoint(x, y, doubleClick)` sends the click. When it reaches no room load on a doorway, the doorway's verb runs directly and is labelled (`_lastDoorwayFallback` holds why). Of the 46 fallbacks, 33 were refused because user input is off (`cursorCommand`) in that catalog visit's state, and 13 reached no room load. The inspection mode cycles artwork, and its status line says so. ClickInputCheck, DoorwayClickCheck, Room03ActionCheck and VerbAreaCheck were updated. Worth a look later: why catalog visits leave user input off in 33 doorway rooms (an arrival that does not run the route's `cursorCommand`?). Only chase it if a milestone route needs it.

Tasks, in order (M1 from the plan):

1. M1a, the message layer (Core). A reached `print` becomes a message: actor, position, center/left, colour, and text with control codes (`0xFF 0x01` new line; others from ScummVM string.cpp). Keep the source room, script and offset. Keep `VAR_HAVE_MSG` (`var[0x003]`) and the talk delay: `VAR_DEFAULT_TALK_DELAY` (or 60) plus `VAR_CHARINC` (`var[0x025]`) per character, minus the frame's jiffies each frame. `waitFor message` then waits for that many frames. The snapshot exposes the messages on screen. Add tests with Room 02 object 156 verb 0x36 ("Amanecer.") and object 154 verb 0x36 ("Ultima hoja del año.", after a walk and a cutscene). Test through `Click(…, doubleClick: true)` as well as the direct verb.
2. M1b, the game's font (Core). Read charset 0 from Games/loom/99.LFL (ScummVM `ScummEngine_v3::loadCharset`: file `99 - n`, not XOR-encoded) and port `CharsetRendererV3` drawing. Render a message to a bitmap at its screen position with its colour and centring. If the charset cannot be read, use a UI font that is labelled as not the game's.
3. M1c/M1d in the app: draw the messages over the room at the game's position (talk text above the actor). After an interaction, advance the session on a timer at the game's frame rate (`VAR_TIMER_NEXT` 6 jiffies, 10 frames/s) until the scripts wait for input or the user interrupts, redrawing each frame. A click on the text dismisses it (presentation). Add a WPF check that captures the text layer in Room 02 (a double click on object 156 through `HandleRoomPoint`), and save the screenshots under .verification/messages/.
4. If there is room left: fix CameraPanCheck and VerbAreaCheck, or update them to the refreshed layout, since M1 touches the room view.
5. Then M2 (actors on screen), as the plan describes.

Rules:

* Judge every task by what it changes on screen. Say in the validation what the user now sees.
* Follow the evidence rules in CLAUDE.md and the plan. Messages and actors driven by reached events are game behaviour. Substitutes (a UI font, skipped waits) are labelled presentation and are optional. Label estimates and assumed input.
* Record new findings in docs/LOOM_SCRIPT_MAP.md and mark each as decoded, ScummVM or reading.
* For each task, record verification under .verification/<topic>/, update docs/LOOM_EGA_PLAN.md (status and next task) and add a row to docs/CHANGELOG.md.
* Rerun the audit when Core rendering or session state changes. If renders change, refresh the PNGs with `compare-png.ps1 -Refresh` from PowerShell.
* The WPF harnesses are slow (the user asked, 28 Sep). Do not run the full `wpf-checks.ps1`, or DoorwayClickCheck (all 75 doorways), unless the change touches what they test: the room-click path, doorway navigation or room loading. Otherwise run the unit tests and only the one or two related harnesses (build each and start its exe, as wpf-checks.ps1 does), and say in the validation which ones were skipped.
* Commit each task separately once it builds and the tests pass.
* Once the session has used more than 250K tokens, stop at the next reasonable point (a committed task) instead of starting a new task.
* At the end of the session, write a prompt for the next session in this same format (state at start, what was done, ordered tasks, rules, tooling notes), and end it with this same request, including the 250K-token stopping rule.

Tooling notes:

* The Bash sandbox blocks some writes into the repo, and RTK filters grep/awk output. Use the Grep tool, or python over .verification/fallback-evidence/code.tsv (columns: owner, offset, operation, operands) and objects.tsv.
* Patch files with python: read bytes, normalise to \n, patch with an exact-count check, restore CRLF if the file had it, and keep a BOM only if the file had one. LoomExplorationSession.cs, MainWindow.xaml.cs and LoomVerbs.cs are CRLF without a BOM. MainWindow.xaml is LF with a BOM. Other files are LF.
* Use the PowerShell tool for harness runs, with `Set-Location` to the repo root first. Rebuild a harness before running it after changing src, because each has its own copy of Core. wpf-checks.ps1 builds them all. Do not edit files while it runs, always write to a fresh output folder, and count PASS/FAIL with -cmatch.
* VisitProbe (.verification/opcodes/VisitProbe) steps: `R`, `R/O/V`, `varNNN=`, `+N`, `sounds`, `?052`, `@x,y`, `!x,y`, `!!x,y`, `v:x,y`, `k:0x1B`, `s:34:a,b`. Pass only objects in that room and indexed rooms.
* Branch semantics: the disassembly's `branch` is taken when the condition is false.
* ScummVM sources: curl raw files from github.com/scummvm/scummvm engines/scumm (string.cpp, charset.cpp, actor.cpp, costume.cpp, resource_v3.cpp) into the scratchpad.
* Commit messages: write them to a file and use `git commit -F`.
* Harnesses call `HandleRoomPoint` through reflection with three arguments (x, y, doubleClick). `MethodInfo.Invoke` does not fill optional parameters.
