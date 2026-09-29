Continue the SCUMMexplorer work. LOOM EGA is the main focus. Read CLAUDE.md first, then docs/LOOM_EGA_PLAN.md: "What the app shows today", milestone M3 and "Known issues". Then read docs/LOOM_SCRIPT_MAP.md, especially "Messages, charset and actor commands" (the Walking, Room 02's changes of scene and Camera bullets), "How the session maps ScummVM behaviour" (the `scummLoop` and `o5_delay` rows) and "Not modelled yet". docs/CHANGELOG.md lists the finished work.

State at the start:

* Git: master. The latest commits:
  * the next-session prompt after M3b;
  * 6f231a2 "Pause scripts for their delay and run the engine timers (M3b)";
  * 07c0da0 "Compare M3a with ScummVM: Room 02 path walk and scene change match";
  * 6637cda, the previous next-session prompt; ba66860 (M3a).
* Another agent may be working on the room music player, the room pan controls or Indy3 conversations (`docs/INDY3_CONVERSATIONS_PLAN.md`, `tools/`, `LoomScriptReader.cs` dialect work). Don't edit that code. If `git status` shows their uncommitted changes, commit only your own files and check `git diff` hunk by hunk.
* `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 227/227 with SCUMM_SOUND_TEST_ROOT=Games\loom and SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA.
* RoomAudit: 0 gaps in all 76 rooms; `compare-png.ps1`: 0 changed renders.
* Blockers (.verification/frame-timing/blockers-after.txt, 1,381 lines): the only real stops left are `expression` (3).
* WPF checks: ActorDrawCheck 0 failures (since M3b it clicks the floor to box 4, the floor to box 10 at (627, 124), then the exit, and times the exit cutscene's delay); DoorwayClickCheck on Rooms 02, 03, 04, 08 and 10: 13/13 (10 through the game's click). Not rerun since M3a: MessageAppCheck, ClickInputCheck, CameraPanCheck and VerbAreaCheck (0 failures before M3a).
* Worktrees: `.claude/worktrees/focused-pasteur-76af82`, `.claude/worktrees/jovial-sutherland-74247e`, the Codex worktree `C:\Users\adelc\.codex\worktrees\room-controls\SCUMMexplorer` and `.claude/worktrees/affectionate-tharp-697cdd` belong to the user or other sessions. `.claude/worktrees/sweet-faraday-0c1780` was this session's (it has junctions to Games/ and scummvm/). Don't remove worktrees unless the user asks.

Done last session:

* M3a ScummVM comparison (.verification/walk-steps/validation.md): the user drove ScummVM. One click on Room 02's path exit walks Bobbin right along the rock. Script 045's box-4 scene change puts him on the path in the smaller costume, he waits for another click, and the camera follows. This matches the session. Skipping the opening with Escape leaves him facing the screen, as script 044 +0x063E/+0x0678 says.
* M3b frame timing (.verification/frame-timing/validation.md):
  * `delay`/`delayVariable` pause their slot for their jiffies (`ScriptFrame.Delay`), counted down at the start of each frame by min(`VAR_TIMER_NEXT`, 15). The slot runs again once its delay is below 0, as ScummVM `decreaseScriptDelay` does: 60 jiffies = 11 frames.
  * `VAR_TIMER` (`var[0x02E]`), `VAR_TIMER_TOTAL` (`var[0x02F]`) and `VAR_TMR_1..3` (`var[0x00B..0x00D]`) count every frame from 0 (`UpdateTimersAndDelays`). A click still states `VAR_TMR_2` as assumed input.
  * Frame counts: boot 725 → 775, script 052 267 → 277, the Elders' trial 2,784 → 2,824.
  * New trace lines: "… waits N jiffies (delay) before +0x…" and "…'s delay ends; it runs again at +0x…".
* What the user sees: a cutscene's delay holds the scene for its real time. Room 02's exit cutscene pans over the valley and holds 1.1 s before Room 03. The swan speck (script 042) pauses between moves.

Tasks, in order:

1. Room 02's exit click in the app. The game (and the session) stop Bobbin at script 045's box-4 change of scene and wait for another click. But the app's doorway fallback runs when a click's playback ends without a room load, so a click on the exit from the cliff opens Room 03 at once (labelled fallback). The user sees Bobbin turn small, then the scene jumps. Find a rule that follows the game without room-specific hacks. For example, do not run the fallback when the game's click reached a sentence that did something (a cutscene, a walk that a script's `putActor` ended). Or run it only when the walk arrived and verb 0x38 was refused. Keep DoorwayClickCheck on 02, 03, 04, 08 and 10 passing (a doorway may then need more than one click; say so in its report). Verify in the app (ActorDrawCheck or a new small harness), and record what the user sees.
2. Optional, small: crop the view to the 128 room rows the game shows (Known issues; check other rooms first: .verification/view-checks). Room 02's actor 4 at (433, 129) is visible in the app but not in the game.
3. M3c playback controls: play, pause and Escape (skip; `abortCutscene` is modelled, `PressKey(0x1B)`). A "play this cutscene" entry reaches known cutscenes (script 052 from Room 10 object 295; the Elders' trial from Room 08 object 255) from a stated, labelled state. Verification: Room 10 object 295 plays script 052 through Room 11 into Room 13 in the app, with actors, text and the delay; Escape skips to Room 13.

Rules:

* Judge every task by what it changes on screen. Say in the validation what the user now sees.
* Follow the evidence rules in CLAUDE.md and the plan. Messages and actors driven by reached events are game behaviour. Substitutes (a UI font, skipped waits, estimated poses) are labelled presentation and are optional. Label estimates and assumed input.
* Record new findings in docs/LOOM_SCRIPT_MAP.md and mark each as decoded, ScummVM or reading.
* For each task, record verification under .verification/<topic>/, update docs/LOOM_EGA_PLAN.md (status and next task) and add a row to docs/CHANGELOG.md.
* Rerun the audit when Core rendering or session state changes. If renders change, refresh the PNGs with `compare-png.ps1 -Refresh` from PowerShell.
* The WPF harnesses are slow (the user asked, 28 Sep). Don't run the full `wpf-checks.ps1`, or DoorwayClickCheck on all 75 doorways, unless the change touches what they test: the room-click path, doorway navigation or room loading. Task 1 touches doorway navigation, so run DoorwayClickCheck at least on Rooms 02, 03, 04, 08 and 10, and consider a full run. Otherwise run the unit tests and only the one or two related harnesses, and say in the validation which ones were skipped. DoorwayClickCheck takes a room list as extra arguments (for example `… out.txt 2 3 4 8 10`); `var[0x071]` can differ from a full run because the route differs.
* Commit each task separately once it builds and the tests pass.
* The user runs the app from the main checkout's exe. When a session works in a worktree, bring the branch onto master (rebase onto master if master moved), fast-forward master (`git -C <main checkout> merge --ff-only <branch>` when the main checkout is clean) and rebuild the Release app there (`dotnet build <main>\src\SCUMMexplorer.App\SCUMMexplorer.App.csproj -c Release`), so the user sees the changes.
* Once the session has used more than 250K tokens, stop at the next reasonable point (a committed task) instead of starting a new task. Check with the session usage tool (`get_usage`, the context's tokensUsed).
* The user checks in during long tasks: give a short status line every few steps.
* At the end of the session, replace docs/NEXT_SESSION_PROMPT.md with a prompt for the next session in this same format (state at start, what was done, ordered tasks, rules, tooling notes). End it with this same request, including the 250K-token stopping rule. Then create a task chip (spawn_task) that starts the next session with that prompt, because a session cannot restart itself.

Tooling notes:

* A worktree has no `Games/` or `scummvm/`. Link them with junctions to the main checkout's folders (`New-Item -ItemType Junction -Path Games -Target <main>\Games`); both are git-ignored. `.verification/fallback-evidence/code.tsv` is not in git either: copy it from the main checkout. Before any worktree is removed, delete its junctions first with `cmd /c rmdir <worktree>\Games` (removes the link only), so a recursive delete can never reach the real game data.
* ScummVM comparison: when started from the session's shell (Start-Process, in or out of the sandbox, with or without audio and the joystick), `scummvm.exe` hung on a blank white window before it wrote its config (29 Sep). Ask the user to start it from Explorer (`<main>\scummvm\scummvm.exe`) and drive it. The computer-use grant works with the exe's full path as the app name (`D:\Documents\Personal\Code Projects\SCUMMexplorer\scummvm\scummvm.exe`; the names "Scummvm" and "scummvm" are not found). Screenshots and `zoom` work; synthetic input does not reach the SDL window. Take `zoom` captures every few seconds while the user plays, and ask the user what they did between captures (for example whether they clicked again).
* The Bash sandbox blocks some writes into the repo, and RTK filters grep/awk/head output. Use the Grep tool, or python over .verification/fallback-evidence/code.tsv (columns: owner, offset, operation, operands) and objects.tsv. For patches, write a python script to the scratchpad with the Write tool (a helper `edit(path, [(old, new)])` that keeps the file's line endings and BOM is handy) and run it from PowerShell. Don't write patch scripts through PowerShell here-strings or `Set-Content` (they mangle non-ASCII such as en dashes), and don't put C# escapes such as `\n` inside heredoc'd python (they became real newlines; use `Environment.NewLine`).
* To trace session internals, a temporary xunit test in tests/SCUMMexplorer.Tests (delete it afterwards) that builds `new LoomExplorationSession(game, snapshot)` and writes a file in %TEMP% is the quickest probe. `SessionTrace` holds session-level lines (room loads, cutscenes, freezes, delays), not per-instruction traces. For per-instruction detail, add temporary `File.AppendAllText` logging and restore the file from a backup copy afterwards. `session.Actors` (not the snapshot) lists actors; facing is `actor.Look.Facing`.
* RoomAudit cannot write `room-audit.txt` into `.verification/room-audit` (access denied). The BMPs it writes there are enough for `compare-png.ps1`. For the gap count, run it again into a scratchpad folder and read "Rooms with remaining gaps".
* Blockers: `.verification/random-picks/Blockers` `<game> <output file>`, about 15 minutes; run it in the background. Compare against .verification/frame-timing/blockers-after.txt. The file's first part is a normalized summary (counts per label with numbers folded to #), so compare label counts, not raw lines.
* CostumeAnimRender: `<game> <output folder> <costume> <steps> <frame>...`.
* ActorDrawCheck: `<Games\loom> <output folder>`. It writes actor-draw-check.txt and PNGs.
* WPF harness exes return at once when started directly from PowerShell (GUI subsystem). Use `Start-Process -FilePath <exe> -ArgumentList … -WorkingDirectory <repo root> -Wait -PassThru` and then read the output file.
* In PowerShell, .NET file APIs (`[IO.File]::ReadAllText`) resolve relative paths against the process directory, not `Set-Location`. Use absolute paths.
* Line endings: git normalizes (autocrlf); most working files are CRLF, `LoomActors.cs` is LF. MainWindow.xaml, LoomActors.cs and LoomBoundedScriptExecutor.cs have a BOM. Python edits should keep each file's line endings and BOM.
* MainWindow already has an audio `StopPlayback()`. The scene clock's stop is `StopScenePlayback()`; its frame step is `PlaybackStep`/`ShowPlaybackFrame`; `StartPlayback` starts it. The doorway fallback is `FollowDoorway` → `ActivateObjectVerb` → `ContinueToReachedRoom`, which runs its frames at once (not on the clock). The actor layer is `UpdateActorLayer()`.
* Use the PowerShell tool for harness runs, from the repo root. Rebuild a harness before running it after changing src, because each has its own copy of Core.
* If `SCUMMexplorer.App.exe` is running (the user's or another agent's), building fails to copy the App exe (MSB3026). Build harnesses into the scratchpad (`dotnet build <harness>.csproj -o <scratch dir>`) and run the exe from there, with the repo root as the working directory. Don't stop the user's app.
* The harness output argument is a file path for DoorwayClickCheck and a folder for the others. Always write to a fresh path and read only that file.
* To capture one element, draw it through a `VisualBrush` into a `DrawingVisual` (as MessageAppCheck's Save does); `RenderTargetBitmap.Render(element)` keeps the element's offset in its parent and crops.
* Default parameter values compile into the caller: after changing one in Core, rebuild the App and the harnesses.
* `.verification/**/*.txt` and `*.png` are git-ignored; evidence files are committed with `git add -f`.
* MessageRender (.verification/messages/MessageRender) renders a session step to BMP, with the 16-row room offset. Its steps are `v:obj:verb`, `c:x,y`, `d:x,y`, `f:n` and `t:<message source>`.
* VisitProbe (.verification/opcodes/VisitProbe) steps: `R`, `R/O/V`, `varNNN=`, `+N`, `sounds`, `?052`, `@x,y`, `!x,y`, `!!x,y`, `v:x,y`, `k:0x1B`, `s:34:a,b`.
* Branch semantics: the disassembly's `branch` is taken when the condition is false.
* ScummVM sources: curl raw files from github.com/scummvm/scummvm engines/scumm (actor.cpp, boxes.cpp, camera.cpp, costume.cpp, object.cpp, script.cpp, script_v5.cpp, scumm.cpp, vars.cpp, verbs.cpp) into the scratchpad. `scummLoop` and the main loop's delta are in scumm.cpp; `decreaseScriptDelay` and `runAllScripts` in script.cpp; `checkExecVerbs` and the sentence handling in verbs.cpp/script.cpp.
* Commit messages: write them to a file and use `git commit -F`.
* Harnesses call private methods through reflection with every argument, because `MethodInfo.Invoke` does not fill optional parameters. `PlaybackRunning` is a private property.
* To measure a change against the previous commit, add a temporary worktree at HEAD in the scratchpad (`git worktree add --detach <scratch>\head HEAD`), junction `Games` into it, copy the probe test in, build and run it there, then remove the junction with `cmd /c rmdir` before `git worktree remove --force`.
* DoorwayClickCheck hangs if playback never ends. Playback waits only for the ego (`EgoMoving`) and the camera, never for every actor, because ambient walkers never stop.
* The auto-mode permission check sometimes fails for minutes at a time, and it refuses `dangerouslyDisableSandbox`. After a few failures in a row, stop and tell the user rather than burn the 10-failure limit.
