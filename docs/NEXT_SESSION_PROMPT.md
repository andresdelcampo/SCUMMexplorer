Continue the SCUMMexplorer work. LOOM EGA is the main focus. Read CLAUDE.md first, then docs/LOOM_EGA_PLAN.md: "What the app shows today", milestones M2 and M3, and "Known issues". Then read docs/LOOM_SCRIPT_MAP.md, especially "Messages, charset and actor commands" (the Drawing actors, `setClass` on an actor, Facing on a direction-locked box, Bobbin's facing after the opening and Walking bullets), "Actors", "How the session maps ScummVM behaviour" and "Not modelled yet". docs/CHANGELOG.md lists the finished work.

State at the start:

* Git: master. The latest commits:
  * 3d4d1a6 "Draw actors in the room (M2c)";
  * aa9ceba "Add offline Indy EGA conversation tree exporter" (another agent's);
  * dc8807f, the previous next-session prompt;
  * 9615e52 (M2b), 1694d38 (M2a).
* Another agent may be working on the room music player, the room pan controls or Indy3 conversations (`docs/INDY3_CONVERSATIONS_PLAN.md`, `tools/`, `LoomScriptReader.cs` dialect work). Don't edit that code. If `git status` shows their uncommitted changes, commit only your own files and check `git diff` hunk by hunk.
* `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 223/223 with SCUMM_SOUND_TEST_ROOT=Games\loom and SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA.
* RoomAudit: 0 gaps in all 76 rooms; `compare-png.ps1`: 0 changed renders.
* Blockers (.verification/actor-draw/blockers-after.txt, 1,446 lines): the only real stops left are `expression` (3).
* WPF checks: ActorDrawCheck 0 failures (new, `.verification/actor-draw/ActorDrawCheck`). Not rerun last session, because the click path, doorways and room loading did not change: MessageAppCheck, ClickInputCheck, CameraPanCheck and VerbAreaCheck (0 failures before), DoorwayClickCheck (75/75 before).
* Worktree clean-up is pending: `.claude/worktrees/focused-pasteur-76af82` (branch `claude/focused-pasteur-76af82`, clean, at dc8807f, already in master; its Games/scummvm junctions were already removed) could not be removed by the agent, because the permission check refused it. The Codex worktree `C:\Users\adelc\.codex\worktrees\room-controls\SCUMMexplorer` (detached at 8878cd9, a pre-rebase copy of master's 739adc8, with untracked evidence folders) was left for the user to decide. Don't remove worktrees unless the user asks again.

Done last session:

* M2c (.verification/actor-draw/validation.md):
  * `LoomActorRenderer` (src/SCUMMexplorer.Core/Rendering/LoomActorRenderer.cs) uses ScummVM's `processActors` exchange sort (for each j, each i: swap when y[j] < y[i]) and draws each visible actor with a costume at (x, y − elevation), clipped to the room, into an indexed layer (0xFF transparent).
  * The app's `RoomActorOverlay` (MainWindow.xaml, between the room image and the other overlays) is redrawn by `UpdateActorLayer()` from `RefreshRoomPreview` and `ShowPlaybackFrame`. It shows only in the explore mode, while the session is in the shown room. The App keeps a costume cache per game (`_actorCostumes`).
  * Session fixes: `setClass` class 22 on an actor sets or clears `ignoreBoxes` (ScummVM `putClass` → `classChanged`); a standing actor's turn is remapped by its box's flags (`remapDirection`), and `adjustActorPos` turns an actor on a box with `flags & 7` to its facing.
  * ScummVM comparison (the user drove scummvm.exe through the opening): Room 02 after the opening matches in position, costume (no distaff), frames and the red leaf (actor 5, costume 8). **Bobbin's facing differs:** ScummVM shows him in left profile, the app facing the viewer. The cause is decoded: Room 02 local 202 runs `faceActor 1, 3` every frame while local 201 flies the fairy off left, but the session's walks end at once, so the fairy has left before the first `faceActor`. M3a fixes this.
  * ActorDrawCheck: after a click on box 4 (860, 118), script 045's cutscene switches the ego to costume 2 and walks it to (756, 107) (at once). Script 042's swan speck (actor 2, costume 5) animates frame by frame on the playback clock.
* What the user sees: actors in the rooms. Bobbin under the tree, the leaf, animated actors moving at 10 frames a second, talk text over Bobbin. Bobbin jumps to a walk's end.

Tasks, in order:

1. M3a: walking in steps. Port ScummVM `Actor::startWalkActor`, `walkActor`, `calcMovementFactor`, `actorWalkStep` and the box path (`findPathTowards`/`getNextBox`, already partly in `LoomWalkBoxSet`) for v3, with `walkSpeed` (x, y), the walk frame (`startAnimActor(_walkFrame)` at the start of each leg, the stand frame at the end, `turnToDirection(destdir)`), and `MF_TURN` turns over frames (`updateActorDirection` with `actorTurnInterpolateTable`, remapped as in M2c).
   * The session's `AdvanceFrame` then steps the walking actors each frame (ScummVM `processActors` order: `walkActor` for every actor, then draw and `animateCostume`). `waitFor actor` waits for the real arrival, and `getActorMoving` (4 sites) can be answered.
   * The camera follows the ego each step (ScummVM `moveCamera` with `setCameraFollows`, the 8-pixel strip rounding) instead of jumping to the walk's end.
   * Remove the walk-at-once estimate where the walk is now stepped. Keep the labels for anything still estimated. Check that the boot still ends in Room 02 at (828, 60), and that the ego now ends in left profile (Room 02 local 202). This changes the boot's frame count; check `LoomBootTests`, the Room 03/04 arrivals, script 052 (Room 10 object 295 → Room 13) and the Elders' trial frame counts.
   * Verification: extend ActorDrawCheck. The ego's walk animation plays during a click's playback (walk frame 9 with the limbs stepping), the ego moves a few pixels each frame, and the camera follows. Compare with ScummVM by eye (ask the user to drive ScummVM; synthetic input does not reach its SDL window) and record each comparison. Rerun the audit, Blockers and DoorwayClickCheck on a few rooms (the walk and room-click path changes).
2. M3b: frame timing (`delay`, `delayVariable` in jiffies; the engine timers). See the plan.
3. Optional, small: crop the view to the 128 room rows the game shows. Room 02's actor 4 at (433, 129) is one more reason: it is visible in the app but not in the game.

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
* The auto-mode permission check sometimes fails for minutes at a time. After a few failures in a row, stop and tell the user rather than burn the 10-failure limit.
