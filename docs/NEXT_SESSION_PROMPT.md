Continue the SCUMMexplorer work. LOOM EGA is the main focus. Read CLAUDE.md first, then docs/LOOM_EGA_PLAN.md: "What the app shows today", milestone M2 and "Known issues". Then read docs/LOOM_SCRIPT_MAP.md, especially "Messages, charset and actor commands", "Actors", "How the session maps ScummVM behaviour" and "Not modelled yet". docs/CHANGELOG.md lists the finished work.

State at the start:

* Git: master. The latest commits:
  * 4153d47 "Open the first room at the camera; update the verb area check";
  * f90b453 "Draw messages over the room and play clicks forward (M1c/M1d)";
  * before them, 798d094, the room music selection (another agent's).
* Another agent may be working on the room music player (`UpdateRoomMusicActions`, the audio code, `.verification/room-thumbnails/`, the music checks). Don't edit those. If `git status` shows their uncommitted changes, commit only your own files and check `git diff` hunk by hunk.
* `dotnet build SCUMMexplorer.slnx`: 0 compiler warnings. Tests: 206/206 with SCUMM_SOUND_TEST_ROOT=Games\loom and SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA.
* WPF checks:
  * MessageAppCheck (new): 0 failures;
  * DoorwayClickCheck: 75/75 (29 through the game's click), identical line for line to M1e;
  * ClickInputCheck, CameraPanCheck, VerbAreaCheck: 0 failures each.
* RoomAudit: 0 gaps in all 76 rooms; room renders are unchanged since M1b.

Done last session (.verification/messages-app/validation.md, .verification/view-checks/validation.md):

* M1c: the app draws the session's messages in charset 0 over the room (`RoomMessageOverlay`) and in the verb screen (`VerbMessageOverlay`).
  * Placement: screen row y is room row y − 16 (ScummVM `initScreens(16, 144)`), and x is relative to the game's camera left (camera x − 160).
  * The verb screen appears under the room while it holds text, even with the verb strip off. That text is script 018's distaff note letters and a clicked object's name ("cielo").
  * A click on talk text ends its delay (the '.' talk-stop key, assumed input).
  * If charset 0 cannot be read, a labelled UI font is used.
  * The Core renderer now clips each virtual screen at its own bottom.
* M1d: after an input, a DispatcherTimer plays the frames at `FrameJiffies`/60 s (10 frames a second) and redraws each frame (`StartPlayback`, `PlaybackStep`, `ShowPlaybackFrame`, `FinishPlayback`, `StopScenePlayback` in MainWindow.xaml.cs).
  * It continues while a talk message is on, or while scripts are pending, for up to 32 frames outside a cutscene. Cutscene frames don't count (up to 20,000).
  * A room load reached while playing opens that room; the clock waits while the view loads it.
  * The doorway fallback (`FollowDoorwayFallback`) runs when playback ends without a room load.
  * Harnesses set the private `_playInstantly` to run frames at once, as before.
* The first room opened now starts at the game's camera (it used to start at x 0). CameraPanCheck and VerbAreaCheck pass again.
* Finding: LOOM's room screen is 128 rows, but room images are 144, so the last 16 rows never show in the game. The app still shows them (plan, Known issues).
* What the user sees: a double click on Room 02's sky shows "Amanecer." centred in the game's font for about 2 s, and "cielo" in the verb screen under the room.

Tasks, in order:

1. M2a: costume animation in Core, as the plan describes. Extend `LoomCostumeRenderer` from static poses to ScummVM's `ClassicCostumeLoader` for format 0x58:
   * animation = frame * 4 + direction;
   * each limb has a start command and a length, stepped per frame (`increaseAnim`), which loops or stops;
   * the mirror flag for left-facing directions, commands 0x79/0x7A, and the palette mapping.
   * Fetch costume.cpp and actor.cpp from ScummVM into the scratchpad. Add Core tests (the ego's stand and walk frames) and a render tool with PNGs under .verification/costume-anim/.
2. M2b: actor state in Core. Model the rest of `actorOps` (costume, elevation, walkSpeed, init/walk/stand/talk animation, talkColor), plus `animateActor` with ScummVM's mapping, `faceActor` and the current frame. This also answers the `getActorCostume`/`getActorElevation` Blockers stops.
3. M2c: draw actors in the room in the app, sorted by y, on the playback clock's frames. Talk text then appears over a visible actor.
   * Verification: after the boot, the ego stands in Room 02 at (828, 60). Compare screenshots with the ScummVM build in scummvm/ by eye and record each comparison.
4. Optional, small: crop the view to the 128 room rows the game shows, once enough rooms have been checked to confirm the last 16 rows are unused.

Rules:

* Judge every task by what it changes on screen. Say in the validation what the user now sees.
* Follow the evidence rules in CLAUDE.md and the plan. Messages and actors driven by reached events are game behaviour. Substitutes (a UI font, skipped waits, estimated poses) are labelled presentation and are optional. Label estimates and assumed input.
* Record new findings in docs/LOOM_SCRIPT_MAP.md and mark each as decoded, ScummVM or reading.
* For each task, record verification under .verification/<topic>/, update docs/LOOM_EGA_PLAN.md (status and next task) and add a row to docs/CHANGELOG.md.
* Rerun the audit when Core rendering or session state changes. If renders change, refresh the PNGs with `compare-png.ps1 -Refresh` from PowerShell.
* The WPF harnesses are slow (the user asked, 28 Sep). Don't run the full `wpf-checks.ps1`, or DoorwayClickCheck on all 75 doorways, unless the change touches what they test: the room-click path, doorway navigation or room loading. Otherwise run the unit tests and only the one or two related harnesses, and say in the validation which ones were skipped. DoorwayClickCheck takes a room list as extra arguments (for example `… out.txt 2 3 4 8 10`) for a quick targeted run; `var[0x071]` can differ from a full run because the route differs.
* Commit each task separately once it builds and the tests pass.
* Once the session has used more than 250K tokens, stop at the next reasonable point (a committed task) instead of starting a new task. Check with the session usage tool (`get_usage`).
* At the end of the session, replace docs/NEXT_SESSION_PROMPT.md with a prompt for the next session in this same format (state at start, what was done, ordered tasks, rules, tooling notes). End it with this same request, including the 250K-token stopping rule. Then create a task chip (spawn_task) that starts the next session with that prompt, because a session cannot restart itself.

Tooling notes:

* The Bash sandbox blocks some writes into the repo, and RTK filters grep/awk output. Use the Grep tool, or python over .verification/fallback-evidence/code.tsv (columns: owner, offset, operation, operands) and objects.tsv. For longer patches, write the script to the scratchpad and run it.
* In PowerShell, .NET file APIs (`[IO.File]::ReadAllText`) resolve relative paths against the process directory, not `Set-Location`. Use absolute paths.
* Line endings: LoomExplorationSession.cs, MainWindow.xaml.cs and LoomVerbs.cs are CRLF without a BOM. MainWindow.xaml has a BOM. The Edit tool keeps a file's line endings.
* MainWindow already has an audio `StopPlayback()`. The scene clock's stop is `StopScenePlayback()`.
* Use the PowerShell tool for harness runs, with `Set-Location` to the repo root first. Rebuild a harness before running it after changing src, because each has its own copy of Core.
* If `SCUMMexplorer.App.exe` is running (the user's or another agent's), building fails to copy the App exe (MSB3026). Build harnesses into the scratchpad (`dotnet build <harness>.csproj -o <scratch dir>`) and run the exe from there, with the repo root as the working directory. Don't stop the user's app.
* The harness output argument is a file path for DoorwayClickCheck and a folder for the others. Always write to a fresh path and read only that file.
* To capture one element, draw it through a `VisualBrush` into a `DrawingVisual` (as MessageAppCheck's Save does); `RenderTargetBitmap.Render(element)` keeps the element's offset in its parent and crops.
* Default parameter values compile into the caller: after changing one in Core, rebuild the App and the harnesses.
* `.verification/**/*.txt` and `*.png` are git-ignored; evidence files are committed with `git add -f`.
* MessageRender (.verification/messages/MessageRender) renders a session step to BMP, now with the 16-row room offset. Its steps are `v:obj:verb`, `c:x,y`, `d:x,y`, `f:n` and `t:<message source>`.
* VisitProbe (.verification/opcodes/VisitProbe) steps: `R`, `R/O/V`, `varNNN=`, `+N`, `sounds`, `?052`, `@x,y`, `!x,y`, `!!x,y`, `v:x,y`, `k:0x1B`, `s:34:a,b`.
* Branch semantics: the disassembly's `branch` is taken when the condition is false.
* ScummVM sources: curl raw files from github.com/scummvm/scummvm engines/scumm (costume.cpp, actor.cpp, charset.cpp, gfx.cpp, script_v5.cpp, scumm.cpp) into the scratchpad.
* Commit messages: write them to a file and use `git commit -F`.
* Harnesses call private methods through reflection with every argument, because `MethodInfo.Invoke` does not fill optional parameters. `PlaybackRunning` is a private property.
* The auto-mode permission check sometimes fails for minutes at a time. After a few failures in a row, stop and tell the user rather than burn the 10-failure limit.
