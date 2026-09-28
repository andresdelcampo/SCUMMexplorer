Continue the SCUMMexplorer work. LOOM EGA is the main focus. Read CLAUDE.md first, then docs/LOOM_EGA_PLAN.md: "What the app shows today", milestone M3 and "Known issues". Then read docs/LOOM_SCRIPT_MAP.md, especially "Messages, charset and actor commands" (the Walking, Room 02's changes of scene and Camera bullets), "How the session maps ScummVM behaviour" and "Not modelled yet". docs/CHANGELOG.md lists the finished work.

State at the start:

* Git: master. The latest commits:
  * the next-session prompt after M3a;
  * ba66860 "Walk actors in steps and move the camera per frame (M3a)";
  * 9ca1d2c, the previous next-session prompt;
  * 3d4d1a6 (M2c), aa9ceba (another agent's Indy EGA conversation exporter).
* Another agent may be working on the room music player, the room pan controls or Indy3 conversations (`docs/INDY3_CONVERSATIONS_PLAN.md`, `tools/`, `LoomScriptReader.cs` dialect work). Don't edit that code. If `git status` shows their uncommitted changes, commit only your own files and check `git diff` hunk by hunk.
* `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 223/223 with SCUMM_SOUND_TEST_ROOT=Games\loom and SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA.
* RoomAudit: 0 gaps in all 76 rooms; `compare-png.ps1`: 0 changed renders.
* Blockers (.verification/walk-steps/blockers-after.txt, 1,419 lines): the only real stops left are `expression` (3).
* WPF checks: ActorDrawCheck 0 failures; DoorwayClickCheck on Rooms 02, 03, 04, 08 and 10: 13/13 (10 through the game's click; before M3a it was 11: Room 02 object 153 now stops at script 045's scene change and takes the labelled fallback). Not rerun last session: MessageAppCheck, ClickInputCheck, CameraPanCheck and VerbAreaCheck (0 failures before M3a).
* Worktrees: `.claude/worktrees/focused-pasteur-76af82` and the Codex worktree `C:\Users\adelc\.codex\worktrees\room-controls\SCUMMexplorer` were left for the user to decide. `.claude/worktrees/affectionate-tharp-697cdd` (at 9ca1d2c) belongs to another session. Don't remove worktrees unless the user asks.

Done last session:

* M3a (.verification/walk-steps/validation.md):
  * `LoomActorTable` ports ScummVM `Actor_v3` walks: `startWalkActor`, `walkActor` (box by box with `getNextBox`, `findPathTowardsOld` and `getGates`), `calcMovementFactor` and `actorWalkStep`. Turns take frames (`turnToDirection` sets `MF_TURN`; `updateActorDirection` uses `actorTurnInterpolateTable`, and `remapDirection` has its walking form). `putActor`, `hideActor` and `adjustActorPos` stop a walk. A walk from an unknown start point, box or facing, and a catalog visit's assumed arrival walk, still end at once (`WalkAtOnce`, labelled).
  * `LoomCamera` (new) is `moveCamera`, with follow mode, pans and the 8-pixel strip rounding. `panCameraTo` pans; `actorFollowCamera` jumps only when the actor is more than 160 px away.
  * `AdvanceFrame` ends with `walkActors`, then `moveCamera`. `waitFor actor`/`camera` break until the arrival and go on at once otherwise. `getActorMoving` answers. `walkActorToObject` passes the object's direction.
  * The app's playback plays on while the ego walks or turns or the camera moves (`EgoMoving`, `CameraMoving`). It does not wait for every actor, because ambient walkers (script 042's swan) never stop.
  * Frame counts: boot 421 → 725 (still ends in Room 02 at (828, 60)), script 052 74 → 267, the Elders' trial 2,119 → 2,784.
* What the user sees: Bobbin ends the boot in left profile, as in ScummVM. He walks along the path with his walk animation and turns over frames, and the camera follows him 8 pixels a frame. Room 02's scene changes (script 045, boxes 4/5/10/11) now interrupt a click's walk down the path. He stops at (756, 107) in the smaller costume, and the player clicks again. This follows from the decoded code but **has not been compared with ScummVM**.

Tasks, in order:

1. ScummVM comparison of M3a, short. Ask the user to drive `scummvm.exe` (see the tooling notes) from the end of the opening: click Room 02's exit (the path on the left) and say when to capture. Check by eye that Bobbin walks down the path, that the scene change at box 4 stops him in the smaller costume near (756, 107) and waits for another click, and that the camera follows. Record each comparison in `.verification/walk-steps/validation.md`. If ScummVM goes on walking after the scene change, find out why from the ScummVM source (for example `beginCutscene` or the sentence script) before changing the session.
2. M3b: frame timing. Model `delay` and `delayVariable` in jiffies, as ScummVM `o5_delay`/`o5_delayVariable` do (the slot's delay, counted down by the frame's jiffies before the scripts run). Also model the engine timers (`VAR_TMR_1..4`, and `VAR_TIMER` if v3 has it; check `scummLoop_updateScummVars` and `vars.cpp`). The Blockers labels "a delay of # jiffies continues on the next frame" (83) should go. Recheck the boot, script 052 and the Elders' trial frame counts, and LoomBootTests.
   * Verification: a unit test for a delay's frame count, the Blockers diff, and one WPF harness that shows a delayed cutscene step on the playback clock (extend ActorDrawCheck or MessageAppCheck).
3. Optional, small: crop the view to the 128 room rows the game shows. Room 02's actor 4 at (433, 129) is visible in the app but not in the game.

Rules:

* Judge every task by what it changes on screen. Say in the validation what the user now sees.
* Follow the evidence rules in CLAUDE.md and the plan. Messages and actors driven by reached events are game behaviour. Substitutes (a UI font, skipped waits, estimated poses) are labelled presentation and are optional. Label estimates and assumed input.
* Record new findings in docs/LOOM_SCRIPT_MAP.md and mark each as decoded, ScummVM or reading.
* For each task, record verification under .verification/<topic>/, update docs/LOOM_EGA_PLAN.md (status and next task) and add a row to docs/CHANGELOG.md.
* Rerun the audit when Core rendering or session state changes. If renders change, refresh the PNGs with `compare-png.ps1 -Refresh` from PowerShell.
* The WPF harnesses are slow (the user asked, 28 Sep). Don't run the full `wpf-checks.ps1`, or DoorwayClickCheck on all 75 doorways, unless the change touches what they test: the room-click path, doorway navigation or room loading. Otherwise run the unit tests and only the one or two related harnesses, and say in the validation which ones were skipped. DoorwayClickCheck takes a room list as extra arguments (for example `… out.txt 2 3 4 8 10`) for a quick targeted run; `var[0x071]` can differ from a full run because the route differs.
* Commit each task separately once it builds and the tests pass.
* The user runs the app from the main checkout's exe. When a session works in a worktree, bring the branch onto master (rebase onto master if master moved), fast-forward master (`git -C <main checkout> merge --ff-only <branch>` when the main checkout is clean) and rebuild the Release app there (`dotnet build <main>\src\SCUMMexplorer.App\SCUMMexplorer.App.csproj -c Release`), so the user sees the changes.
* Once the session has used more than 250K tokens, stop at the next reasonable point (a committed task) instead of starting a new task. Check with the session usage tool (`get_usage`).
* The user checks in during long tasks: give a short status line every few steps.
* At the end of the session, replace docs/NEXT_SESSION_PROMPT.md with a prompt for the next session in this same format (state at start, what was done, ordered tasks, rules, tooling notes). End it with this same request, including the 250K-token stopping rule. Then create a task chip (spawn_task) that starts the next session with that prompt, because a session cannot restart itself.

Tooling notes:

* A worktree has no `Games/` or `scummvm/`. Link them with junctions to the main checkout's folders (`New-Item -ItemType Junction -Path Games -Target <main>\Games`); both are git-ignored. `.verification/fallback-evidence/code.tsv` is not in git either: copy it from the main checkout. Before any worktree is removed, delete its junctions first with `cmd /c rmdir <worktree>\Games` (removes the link only), so a recursive delete can never reach the real game data.
* ScummVM comparison: launch `scummvm\scummvm.exe --config=<scratch>\scummvm.ini --savepath=<scratch> -p <repo>\Games\loom --gfx-mode=2x loom` (a scratch config keeps the user's ini untouched). The computer-use app name is "Scummvm". Screenshots and `zoom` work, but synthetic clicks and keys did not reach the SDL window, so ask the user to drive the game and say when to capture. `save_to_disk` did not return file paths: describe what you see in the validation.
* The Bash sandbox blocks some writes into the repo, and RTK filters grep/awk/head output. Use the Grep tool, or python over .verification/fallback-evidence/code.tsv (columns: owner, offset, operation, operands) and objects.tsv. For longer patches, write a python script to the scratchpad with the Write tool and run it; a long bash heredoc with python inside failed once, and C# string escapes inside a heredoc'd python script broke once too.
* To trace session internals, a temporary xunit test in tests/SCUMMexplorer.Tests (delete it afterwards) that builds `new LoomExplorationSession(game, snapshot, completeBoot: true)` and prints through `Assert.Fail` or a file in %TEMP% is the quickest probe. `SessionTrace` holds only session-level lines, not per-instruction traces. For per-instruction detail, add temporary `File.AppendAllText` logging and restore the file from a backup copy afterwards.
* RoomAudit cannot write `room-audit.txt` into `.verification/room-audit` (access denied). The BMPs it writes there are enough for `compare-png.ps1`. For the gap count, run it again into a scratchpad folder and read "Rooms with remaining gaps".
* Blockers: `.verification/random-picks/Blockers` `<game> <output file>`. Compare against .verification/actor-draw/blockers-after.txt.
* CostumeAnimRender: `<game> <output folder> <costume> <steps> <frame>...`.
* ActorDrawCheck: `<Games\loom> <output folder>`. It writes actor-draw-check.txt and PNGs.
* In PowerShell, .NET file APIs (`[IO.File]::ReadAllText`) resolve relative paths against the process directory, not `Set-Location`. Use absolute paths.
* Line endings: git normalizes (autocrlf); most working files are CRLF. Python edits should keep each file's line endings and BOM (MainWindow.xaml has a BOM, and so do LoomActors.cs and LoomBoundedScriptExecutor.cs).
* MainWindow already has an audio `StopPlayback()`. The scene clock's stop is `StopScenePlayback()`; its frame step is `PlaybackStep`/`ShowPlaybackFrame`. The actor layer is `UpdateActorLayer()`.
* Use the PowerShell tool for harness runs, with `Set-Location` to the repo root first. Rebuild a harness before running it after changing src, because each has its own copy of Core.
* If `SCUMMexplorer.App.exe` is running (the user's or another agent's), building fails to copy the App exe (MSB3026). Build harnesses into the scratchpad (`dotnet build <harness>.csproj -o <scratch dir>`) and run the exe from there, with the repo root as the working directory. Don't stop the user's app.
* The harness output argument is a file path for DoorwayClickCheck and a folder for the others. Always write to a fresh path and read only that file.
* To capture one element, draw it through a `VisualBrush` into a `DrawingVisual` (as MessageAppCheck's Save does); `RenderTargetBitmap.Render(element)` keeps the element's offset in its parent and crops.
* Default parameter values compile into the caller: after changing one in Core, rebuild the App and the harnesses.
* `.verification/**/*.txt` and `*.png` are git-ignored; evidence files are committed with `git add -f`.
* MessageRender (.verification/messages/MessageRender) renders a session step to BMP, with the 16-row room offset. Its steps are `v:obj:verb`, `c:x,y`, `d:x,y`, `f:n` and `t:<message source>`.
* VisitProbe (.verification/opcodes/VisitProbe) steps: `R`, `R/O/V`, `varNNN=`, `+N`, `sounds`, `?052`, `@x,y`, `!x,y`, `!!x,y`, `v:x,y`, `k:0x1B`, `s:34:a,b`.
* Branch semantics: the disassembly's `branch` is taken when the condition is false.
* ScummVM sources: curl raw files from github.com/scummvm/scummvm engines/scumm (actor.cpp, boxes.cpp, camera.cpp, costume.cpp, base-costume.cpp, object.cpp, script_v5.cpp, scumm.cpp, util.cpp) into the scratchpad. The walk code is in actor.cpp (`startWalkActor`, `walkActor`, `calcMovementFactor`, `actorWalkStep`, `updateActorDirection`, `remapDirection`); `findPathTowards` and `getNextBox` are in boxes.cpp; `moveCamera` is in camera.cpp.
* Commit messages: write them to a file and use `git commit -F`.
* Harnesses call private methods through reflection with every argument, because `MethodInfo.Invoke` does not fill optional parameters. `PlaybackRunning` is a private property.
* Blockers takes about 15 minutes since M3a: each of its ~600 sessions boots, and the boot now runs 725 frames. Run it in the background.
* To measure a change against the previous commit, add a temporary worktree at HEAD in the scratchpad (`git worktree add --detach <scratch>\head HEAD`), junction `Games` into it, build and run the probe there, then remove the junction with `cmd /c rmdir` before `git worktree remove --force`.
* `LoomActors.cs` is LF in the working tree; most other files are CRLF. Python patches should detect the line endings per file.
* DoorwayClickCheck hangs if playback never ends. Playback waits only for the ego (`EgoMoving`) and the camera, never for every actor, because ambient walkers never stop.
* The auto-mode permission check sometimes fails for minutes at a time. After a few failures in a row, stop and tell the user rather than burn the 10-failure limit.
