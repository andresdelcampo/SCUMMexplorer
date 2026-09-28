Continue the SCUMMexplorer work. LOOM EGA is the main focus. Read CLAUDE.md first, then docs/LOOM_EGA_PLAN.md: "What the app shows today" and milestones M1–M3. Then read docs/LOOM_SCRIPT_MAP.md, especially "Messages, charset and actor commands", "How the session maps ScummVM behaviour" and "Not modelled yet". docs/CHANGELOG.md lists the finished work.

State at the start:

* Git: master. The latest commits are a4bb05e "Read charset 0 and draw messages in the game's font (M1b)" and 33bd0d0 "Model reached prints as messages with the game's talk delay (M1a)". Before them is 959e848, the asynchronous room-selector thumbnails (done by another agent; `.verification/room-thumbnails/` is theirs and untracked).
* `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 205/205 with SCUMM_SOUND_TEST_ROOT=Games\loom and SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA.
* RoomAudit: 0 gaps in all 76 rooms, renders unchanged by M1a/M1b.
* WPF checks: DoorwayClickCheck 75/75 (29 through the game's click), the same as M1e line for line. CameraPanCheck (2 FAIL, "Room 02/04 pan off") and VerbAreaCheck (1 FAIL, "off again") still fail as before; the other harnesses were not rerun (they do not touch messages).
* Blockers baseline: .verification/messages/blockers-after.txt (4979).

Done last session (.verification/messages/validation.md):

* M1a in Core. `LoomMessageTable` (Formats/LoomMessages.cs) ports v3 `decodeParseString`, `actorTalk`, `displayDialog`, `stopTalk` and `drawString` state:
  * text slots, whose position, height and colour carry over to the next print;
  * the talking actor's `talkColor`, and whether that actor is in the room;
  * the talk delay (60 + `VAR_CHARINC` per character, minus `VAR_TIMER_NEXT` jiffies each frame before the scripts);
  * `VAR_HAVE_MSG`, `waitFor message`, `startScene` stopping the talk, drawn strings (`print 0xFE`), and text codes 1/2/3/4/6 (code 6 = a current-room object's stored name).
  * The session exposes `Messages`, `Snapshot.ScreenMessages` and `FrameJiffies`. Print subop 6 is the line height in v3 (the disassembler now says `height`).
  * `ContinueUntilRoomChanges` defaults to `CutsceneFrameLimit` (20,000 frames), because the Elders' trial (script 046, Room 08's door) now takes 2,120 frames.
* M1b in Core. `LoomCharset` reads 99.LFL. `LoomMessageRenderer` draws messages into a 320×200 layer of palette indices (`LoomMessageLayer`, 0xFF transparent), following `displayDialog`/`newLine`/`drawString` layout and `CharsetRendererV3::printChar`/`drawBits1`, with shadow (0x80) and outline (0x40).
* What Core produces for Room 02 (see the PNGs in .verification/messages/, made by MessageRender):
  * "Amanecer." at (160, 112), centred, colour 0x8F, for 105 jiffies (17 frames on the screen, gone in the 18th);
  * a double click on object 156 also draws "cielo" at (272, 181) in the verb screen (script 007 +0x001E);
  * "Ultima hoja del año." after object 154's walk and cutscene.
* The app does not draw any of this yet.

Tasks, in order:

1. M1c/M1d in the app (MainWindow.xaml.cs; the room selector is no longer being edited by anyone else).
   * Draw `session.Snapshot.ScreenMessages` with `LoomMessageRenderer` over the room view, in screen coordinates relative to the camera view (text at y ≥ 144 goes in the verb area, under the close-up). Use the room's palette for the indices.
   * If `LoomCharset.TryLoad` fails, fall back to a UI font labelled as not the game's.
   * After an interaction, advance the session with a DispatcherTimer at `FrameJiffies`/60 s per frame (10 frames a second), redrawing each frame, until no message is shown and the scripts wait for input (or the user interrupts). This replaces the synchronous `ContinueToReachedRoom` run for the visible part. Keep doorway navigation working: a room load reached while playing enters the room as today.
   * A click on the text calls `Messages.SkipTalkDelay()` (ScummVM's '.' talk-stop key; label it assumed input).
   * Add a WPF check that captures the text layer in Room 02 (a double click on object 156 through `HandleRoomPoint`) and save the screenshots under .verification/messages-app/ (or similar).
   * Rerun DoorwayClickCheck and ClickInputCheck, because this changes the click path.
2. If there is room left: fix CameraPanCheck and VerbAreaCheck, or update them to the refreshed layout.
3. Then M2 (actors on screen), as the plan describes.

Rules:

* Judge every task by what it changes on screen. Say in the validation what the user now sees.
* Follow the evidence rules in CLAUDE.md and the plan. Messages and actors driven by reached events are game behaviour. Substitutes (a UI font, skipped waits) are labelled presentation and are optional. Label estimates and assumed input.
* Record new findings in docs/LOOM_SCRIPT_MAP.md and mark each as decoded, ScummVM or reading.
* For each task, record verification under .verification/<topic>/, update docs/LOOM_EGA_PLAN.md (status and next task) and add a row to docs/CHANGELOG.md.
* Rerun the audit when Core rendering or session state changes. If renders change, refresh the PNGs with `compare-png.ps1 -Refresh` from PowerShell.
* The WPF harnesses are slow (the user asked, 28 Sep). Do not run the full `wpf-checks.ps1`, or DoorwayClickCheck (all 75 doorways), unless the change touches what they test: the room-click path, doorway navigation or room loading. Otherwise run the unit tests and only the one or two related harnesses, and say in the validation which ones were skipped. DoorwayClickCheck takes a room list as extra arguments (for example `… out.txt 8 9 10`) for a quick targeted run.
* Commit each task separately once it builds and the tests pass.
* Once the session has used more than 250K tokens, stop at the next reasonable point (a committed task) instead of starting a new task.
* At the end of the session, replace docs/NEXT_SESSION_PROMPT.md with a prompt for the next session in this same format (state at start, what was done, ordered tasks, rules, tooling notes), and end it with this same request, including the 250K-token stopping rule.

Tooling notes:

* The Bash sandbox blocks some writes into the repo, and RTK filters grep/awk output. Use the Grep tool, or python over .verification/fallback-evidence/code.tsv (columns: owner, offset, operation, operands) and objects.tsv. For longer python patches, write the script to the scratchpad and run it (heredocs with nested quotes can fail).
* Patch files with python: read bytes, normalise to \n, patch with an exact-count check, restore CRLF if the file had it, and keep a BOM only if the file had one. LoomExplorationSession.cs, MainWindow.xaml.cs and LoomVerbs.cs are CRLF without a BOM. MainWindow.xaml is LF with a BOM. Other files are LF.
* Use the PowerShell tool for harness runs, with `Set-Location` to the repo root first. Rebuild a harness before running it after changing src, because each has its own copy of Core.
* The harness output argument is a **file path**, not a folder. When the harness cannot write it ("Report not written … wrote %TEMP%\SCUMMexplorer-harness\… instead"), the fallback file is shared between runs. Always write to a fresh path and read only that file. A stale report cost time last session.
* If `SCUMMexplorer.App.exe` is running (the user's or another agent's), building a harness fails to copy the App output. Build into the scratchpad instead (`dotnet build <harness>.csproj -o <scratch dir>`) and run the exe from there, with the repo root as the working directory. Do not stop the user's app.
* Default parameter values compile into the caller: after changing one in Core, rebuild the App and the harnesses.
* `.verification/**/*.txt` and `*.png` are git-ignored; evidence files are committed with `git add -f`.
* MessageRender (.verification/messages/MessageRender) renders a session step to BMP. Its steps are `v:obj:verb`, `c:x,y`, `d:x,y`, `f:n`, and `t:<message source>` (frames until that message shows). Convert the BMP to PNG with System.Drawing.
* VisitProbe (.verification/opcodes/VisitProbe) steps: `R`, `R/O/V`, `varNNN=`, `+N`, `sounds`, `?052`, `@x,y`, `!x,y`, `!!x,y`, `v:x,y`, `k:0x1B`, `s:34:a,b`. Pass only objects in that room and indexed rooms.
* Branch semantics: the disassembly's `branch` is taken when the condition is false.
* ScummVM sources: curl raw files from github.com/scummvm/scummvm engines/scumm (string.cpp, charset.cpp, actor.cpp, costume.cpp, script_v5.cpp, scumm.cpp, gfx.cpp) into the scratchpad.
* Commit messages: write them to a file and use `git commit -F`.
* Harnesses call `HandleRoomPoint` through reflection with three arguments (x, y, doubleClick). `MethodInfo.Invoke` does not fill optional parameters.
