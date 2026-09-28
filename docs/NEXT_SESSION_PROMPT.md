Continue the SCUMMexplorer work. LOOM EGA is the main focus. Read CLAUDE.md first, then docs/LOOM_EGA_PLAN.md: "What the app shows today", milestone M2 and "Known issues". Then read docs/LOOM_SCRIPT_MAP.md, especially "Messages, charset and actor commands" (the `actorOps`, `animateActor`, Costumes and "The ego's costume" bullets), "Actors", "How the session maps ScummVM behaviour" and "Not modelled yet". docs/CHANGELOG.md lists the finished work.

State at the start:

* Git: master. The latest commits:
  * 9615e52 "Model actor costume state in Core (M2b)";
  * 1694d38 "Model costume animation in Core (M2a)";
  * 739adc8 "Refine room pan controls and center music playback" (another agent's);
  * 21259ba, the object-name fix.
* Another agent may be working on the room music player (`UpdateRoomMusicActions`, the audio code, `.verification/room-thumbnails/`, the music checks, the room pan controls). Don't edit those. If `git status` shows their uncommitted changes, commit only your own files and check `git diff` hunk by hunk.
* `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 216/216 with SCUMM_SOUND_TEST_ROOT=Games\loom and SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA.
* RoomAudit: 0 gaps in all 76 rooms; `compare-png.ps1`: 0 changed renders.
* Blockers (.verification/actor-state/blockers-after.txt): the only real stops left are `expression` (3).
* WPF checks were not rerun in the last session (no App code changed). Their last results: MessageAppCheck, ClickInputCheck, CameraPanCheck, VerbAreaCheck 0 failures; DoorwayClickCheck 75/75.

Done last session:

* M2a (.verification/costume-anim/validation.md): `LoomCostume` and `LoomCostumeAnimationState` in `src/SCUMMexplorer.Core/Rendering/LoomCostumeAnimation.cs`. They follow ScummVM's `ClassicCostumeLoader`:
  * `DecodeAnimation`: animation = direction + frame * 4; per limb a start command and an extra byte (length & 0x7F; 0x80 holds at the end). A start command of 0x79 hides the limb and 0x7A shows it.
  * `IncreaseAnimation(s)`: each limb loops over its commands or holds at the end.
  * `Draw`: moves start at (−72, −100) and add up over limbs 0–15. Left is drawn mirrored unless the costume's header has bit 0x80. `Compose` draws into an indexed buffer; the costume's palette byte draws the actor's palette entry 0, and colour 0 is transparent.
  * `LoomCostume.Load(game, id)` reads a costume.
  * The render tool `.verification/costume-anim/CostumeAnimRender` writes PNG strips and a trace.
* M2b (.verification/actor-state/validation.md): `LoomActorTable` keeps each actor's costume, elevation, walk speed, frames, 16-entry palette, facing, frame and limb state. `LoomActorSnapshot.Look` (`LoomActorLook`) exposes a copy.
  * actorOps subcommands, `animateActor` (chores 2/3/4, frames 56–60 redirect), `faceActor`, `setDirection`, the talk frames (`LoomMessageTable.TalkAnimation` from `displayDialog`/`stopTalk`) and `showActor`'s init frame change that state.
  * `AdvanceFrame` steps the limbs of the actors in the current room first (ScummVM draws, then animates).
  * The executor answers `getActorCostume`, `getActorElevation` and `getActorFacing`.
  * Labelled estimates: turns end at once, and a walk still ends at once. At its end the actor faces along the whole walk and starts its stand frame.
* Findings:
  * The ego's costume is 1 (script 001 sets `var[0x077]` = 1; script 017 applies it).
  * After the boot `bit[0xD61]` is set, so the ego uses frames 8/9/10 and talks with 14/15. Those frames are Bobbin without the distaff (reading: the bit means "no distaff yet").
  * The ego's frame after the boot is 15 (talk stop): the body stays in stand animation 40 + direction, and the head was restarted.
  * Costume 6 (script 044, the opening) has only frames 0 and 1.
* What the user sees: nothing new yet. The session now knows how each actor looks on each frame.

Tasks, in order:

1. M2c: draw actors in the room in the app, sorted by y (ScummVM `processActors`: actors in the current room, sorted by y; check ScummVM's tie rule), on the playback clock's frames.
   * Use `session.Actors[i].Look`: `LoomCostume.Draw(look.Animation, look.Direction)`, then `Compose` at (x, y − elevation) with `look.Palette`, into the room's indexed pixels, or as an overlay layer above the room image. Keep the costume cache in the App, or use `LoomActorTable.CostumeData`.
   * Draw only visible actors (`Visible == true`) whose costume is not 0. Clip to the room.
   * Priority masks (actors behind foreground objects) come later: decode them only if a checked room shows the problem.
   * Talk text then appears over a visible actor.
   * Verification: after the boot, the ego stands in Room 02 at (828, 60) without the distaff. Compare screenshots with the ScummVM build in scummvm/ by eye and record each comparison. Also check that the walk animation plays during a click's playback frames (the walk itself still ends at once: say so).
   * A WPF harness (extend MessageAppCheck or add ActorDrawCheck) captures the room with the actor.
2. M3a: walking in steps (ScummVM `Actor::walkActor`, `calcMovementFactor`, `actorWalkStep`) with the walk frame, the camera following, and `waitFor actor` waiting for the real arrival. This makes the walk animation visible.
3. Optional, small: crop the view to the 128 room rows the game shows, once enough rooms have been checked to confirm the last 16 rows are unused.

Rules:

* Judge every task by what it changes on screen. Say in the validation what the user now sees.
* Follow the evidence rules in CLAUDE.md and the plan. Messages and actors driven by reached events are game behaviour. Substitutes (a UI font, skipped waits, estimated poses) are labelled presentation and are optional. Label estimates and assumed input.
* Record new findings in docs/LOOM_SCRIPT_MAP.md and mark each as decoded, ScummVM or reading.
* For each task, record verification under .verification/<topic>/, update docs/LOOM_EGA_PLAN.md (status and next task) and add a row to docs/CHANGELOG.md.
* Rerun the audit when Core rendering or session state changes. If renders change, refresh the PNGs with `compare-png.ps1 -Refresh` from PowerShell.
* The WPF harnesses are slow (the user asked, 28 Sep). Don't run the full `wpf-checks.ps1`, or DoorwayClickCheck on all 75 doorways, unless the change touches what they test: the room-click path, doorway navigation or room loading. Otherwise run the unit tests and only the one or two related harnesses, and say in the validation which ones were skipped. DoorwayClickCheck takes a room list as extra arguments (for example `… out.txt 2 3 4 8 10`) for a quick targeted run; `var[0x071]` can differ from a full run because the route differs.
* Commit each task separately once it builds and the tests pass.
* The user runs the app from the main checkout's exe. When a session works in a worktree, fast-forward master to the finished commits (`git -C <main checkout> merge --ff-only <branch>` when the main checkout is clean) and rebuild the Release app there, so the user sees the changes.
* Once the session has used more than 250K tokens, stop at the next reasonable point (a committed task) instead of starting a new task. Check with the session usage tool (`get_usage`).
* At the end of the session, replace docs/NEXT_SESSION_PROMPT.md with a prompt for the next session in this same format (state at start, what was done, ordered tasks, rules, tooling notes). End it with this same request, including the 250K-token stopping rule. Then create a task chip (spawn_task) that starts the next session with that prompt, because a session cannot restart itself.

Tooling notes:

* A worktree has no `Games/` or `scummvm/`. Link them with junctions to the main checkout's folders (`New-Item -ItemType Junction -Path Games -Target <main>\Games`); both are git-ignored. `.verification/fallback-evidence/code.tsv` is not in git either: copy it from the main checkout.
* The Bash sandbox blocks some writes into the repo, and RTK filters grep/awk/head output. Use the Grep tool, or python over .verification/fallback-evidence/code.tsv (columns: owner, offset, operation, operands) and objects.tsv. For longer patches, write a python script to the scratchpad with the Write tool and run it; a long bash heredoc with python inside failed once.
* RoomAudit cannot write `room-audit.txt` into `.verification/room-audit` (access denied). The BMPs it writes there are enough for `compare-png.ps1`. For the gap count, run it again into a scratchpad folder and read "Rooms with remaining gaps".
* Blockers: `.verification/random-picks/Blockers` `<game> <output file>`. Compare against .verification/actor-state/blockers-after.txt.
* CostumeAnimRender: `<game> <output folder> <costume> <steps> <frame>...`.
* In PowerShell, .NET file APIs (`[IO.File]::ReadAllText`) resolve relative paths against the process directory, not `Set-Location`. Use absolute paths.
* Line endings: git normalizes (autocrlf); most working files are CRLF. Python edits should keep each file's line endings and BOM (MainWindow.xaml has a BOM, and so do LoomActors.cs and LoomBoundedScriptExecutor.cs).
* MainWindow already has an audio `StopPlayback()`. The scene clock's stop is `StopScenePlayback()`; its frame step is `PlaybackStep`/`ShowPlaybackFrame`.
* Use the PowerShell tool for harness runs, with `Set-Location` to the repo root first. Rebuild a harness before running it after changing src, because each has its own copy of Core.
* If `SCUMMexplorer.App.exe` is running (the user's or another agent's), building fails to copy the App exe (MSB3026). Build harnesses into the scratchpad (`dotnet build <harness>.csproj -o <scratch dir>`) and run the exe from there, with the repo root as the working directory. Don't stop the user's app.
* The harness output argument is a file path for DoorwayClickCheck and a folder for the others. Always write to a fresh path and read only that file.
* To capture one element, draw it through a `VisualBrush` into a `DrawingVisual` (as MessageAppCheck's Save does); `RenderTargetBitmap.Render(element)` keeps the element's offset in its parent and crops.
* Default parameter values compile into the caller: after changing one in Core, rebuild the App and the harnesses.
* `.verification/**/*.txt` and `*.png` are git-ignored; evidence files are committed with `git add -f`.
* MessageRender (.verification/messages/MessageRender) renders a session step to BMP, with the 16-row room offset. Its steps are `v:obj:verb`, `c:x,y`, `d:x,y`, `f:n` and `t:<message source>`. It is a good base for an actor render check.
* VisitProbe (.verification/opcodes/VisitProbe) steps: `R`, `R/O/V`, `varNNN=`, `+N`, `sounds`, `?052`, `@x,y`, `!x,y`, `!!x,y`, `v:x,y`, `k:0x1B`, `s:34:a,b`.
* Branch semantics: the disassembly's `branch` is taken when the condition is false.
* ScummVM sources: curl raw files from github.com/scummvm/scummvm engines/scumm (costume.cpp, base-costume.cpp, actor.cpp, actor.h, util.cpp, string.cpp, charset.cpp, gfx.cpp, script_v5.cpp, scumm.cpp) into the scratchpad.
* Commit messages: write them to a file and use `git commit -F`.
* Harnesses call private methods through reflection with every argument, because `MethodInfo.Invoke` does not fill optional parameters. `PlaybackRunning` is a private property.
* The auto-mode permission check sometimes fails for minutes at a time. After a few failures in a row, stop and tell the user rather than burn the 10-failure limit.
