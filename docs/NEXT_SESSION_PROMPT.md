Continue the SCUMMexplorer work. LOOM EGA is the main focus. Read CLAUDE.md first, then docs/LOOM_EGA_PLAN.md: "What the app shows today", milestone M3 and "Known issues". Then read docs/LOOM_SCRIPT_MAP.md, especially "Screen rows" and "The room screen is 144 rows", the script 044, 046, 052 and 086 rows, "How the session maps ScummVM behaviour" (the boot, `scummLoop`, `o5_delay`, `beginOverride`/`abortCutscene` rows and the new catalog-visit-during-a-cutscene row) and "Not modelled yet". docs/CHANGELOG.md lists the finished work.

State at the start:

* Git: master. The latest commits:
  * the plan update with the user's new issue list (29 Sep);
  * e343ba7 the next-session prompt after the scene-leave fix;
  * 5d4b5b8 "End a cutscene when a catalog visit leaves it";
  * d0933bf "Show all 144 rows of LOOM rooms and draw text over the room".
* Another agent may be working on the room music player, the room pan controls or Indy3 conversations (`docs/INDY3_CONVERSATIONS_PLAN.md`, `tools/`, `LoomScriptReader.cs` dialect work). Don't edit that code (the room music code in MainWindow: `UpdateRoomMusicActions`, `PlayRoomCueAsync`, `_audioPlayer` and the room cue handlers). If `git status` shows their uncommitted changes (on 29 Sep the main checkout had untracked `.verification/object-click-room17/`, `.verification/room-object-interaction/` and `tests/SCUMMexplorer.Tests/LoomObjectLookAuditTests.cs`, not ours), commit only your own files and check `git diff` hunk by hunk.
* `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 235/235 (29 Sep).
* RoomAudit: 0 gaps in all 76 rooms (not rerun: room images unchanged). Blockers (after the actorOps fix): identical to .verification/frame-timing/blockers-after.txt; the only real stops are `expression` (3). Not rerun after the scene-leave fix (it only changes catalog visits made while a cutscene is open).
* WPF checks (29 Sep evening): TextLineCheck 0 (rewritten for the 144-row screen), VerbAreaCheck 0, PresentationCheck 0, SceneLeaveCheck 0 (new), DoorwayClickCheck 16/16 on Rooms 02 03 04 08 10 37. Earlier: ActorDrawCheck 0, SceneSoundCheck 0, CutsceneLayoutCheck 0, CameraPanCheck 0, FirewoodPlaybackCheck 0, GameTextsCutsceneCheck 0, OpeningPlaybackCheck 0, CutscenePlaybackCheck 0. Not rerun since the 144-row change: CutsceneLayoutCheck and CameraPanCheck (they measure the view; run them if a task touches the layout), ActorDrawCheck, the playback checks. Not run since M3a: MessageAppCheck, ClickInputCheck.
* Worktrees: `.claude/worktrees/cool-lamarr-0ae6eb` was last session's (junctions to Games/ and scummvm/). The others under `.claude/worktrees/` and `C:\Users\adelc\.codex\worktrees\room-controls\SCUMMexplorer` belong to the user or other sessions. Don't remove worktrees unless the user asks.

Done last session:

* The user's answers (29 Sep): the trial's flight shows swans; Hetchel flies as a human nowhere else. The minor depth-mask issue (left for later, in Known issues): walking to the village in Room 02, Bobbin disappears a few pixels before reaching the mountain side.
* LOOM's 144 room rows (user report: the room's bottom was cut, Room 09's egg hidden; .verification/room-rows). LOOM's boot runs `roomOps setScreen 0, 144` (script 001 +0x0000), ScummVM `initScreens(0, 144)`: the room screen is rows 0–143 from screen row 0, and there is no text screen. The app now shows all 144 rows and draws text over the room at its screen y. The 16-row text line is removed, and so are the 29 Sep 128-row crop and the 28 Sep 16-row text offset. Indy3 keeps 128 rows (not checked).
* Leaving a scene from the catalog (user report: after leaving a conversation, clicks did nothing; .verification/scene-leave). A catalog visit during a cutscene now stops the script that began it and runs the cutscene end script (script 003: input on, scripts unfrozen), labelled as an explorer action. It fails at d0933bf and passes now (SceneLeaveCheck).
* ScummVM comparison (the user's screenshot, 29 Sep): Room 02's "Amanecer." (y 112) is at screen row about 113 over the room, and the room's art reaches row about 137, directly above the verb screen: the 144-row screen and text over the room match the game.

The user's issue list (29 Sep, evening; they confirmed the 144-row and scene-leave fixes and sent a ScummVM screenshot, .verification/room-rows/scummvm-room02-amanecer.png):

* (done) screen size changes in cutscenes; scroll arrows in cutscenes; white disabled buttons; no music or sound in cutscenes; text position; the forge's first lines; depth masks; Hetchel flying as a human; the room's bottom rows; stuck after leaving a conversation.
* A. Switching rooms to Room 02 can show big Bobbin instead of the small one, or actor 12 big instead of the boat.
* B. Room 43: the sheep show partly hidden behind the background.
* C. Room 41 can scroll left into black; it should not.
* D. Remove the verb screen's explanatory text row under the verb screen.
* E. After the distaff shows the open spell, the notes keep their shine and colour in the verb area.
* F. Spells cannot be cast with keys.
* G. A key to skip to the next text during a cutscene.
* H. "Play a cutscene" should list only the cutscenes relevant to the current room.
* I. Consider whether the room media player still makes sense now that cutscenes play the rooms' background music. This is another agent's code (the room music player): give the user an analysis and a recommendation, and change it only if they agree.
* (minor, later) Room 02: Bobbin hidden a few pixels too early near the mountain side on the way to the village.

Tasks, in order (the user asked for the plan to cover these; they will start the session):

1. A: Room 02's wrong-size actors after a room switch. Reproduce with a session probe: catalog visits into Room 02 from several rooms, and after cutscenes. Log `session.Actors` (`Look.Costume`, `Room`, `Visible`) and the costume each one draws with. Likely suspects are a costume or actor state left over from the room just left (Room 02's entry script, or a catalog arrival that skips the room's own `actorOps`), or `Actors.Forget`/`ForgetAll` leaving a stale costume. Compare with ScummVM's `startScene` (it hides all actors, and `putActor`/`actorOps` in the entry set them again). The boat is actor 12 in Room 02. Verify with ActorDrawCheck and a probe for each route.
2. B: Room 43's sheep behind the background. Check the actors' walk boxes and masks (`LoomWalkBoxReader.Read(roomBytes).Boxes[i].Mask`) and `ComposeDepthMask` against ScummVM `drawActorCostume` (`getMaskFromBox`). A wrong or unknown walk box, or an actor placed with `putActor` outside any box, is the likely cause. Fix it generically; no room hacks. Check whether the same fix explains the minor Room 02 mountain-side issue.
3. C: Room 41's left scroll. Compare the view's pan limits with the room's camera limits (`roomOps roomScroll`: Room 41's entry +0x0003 sets 0x00B0, 0x00B8) and ScummVM `VAR_CAMERA_MIN_X`/`MAX_X`. The manual pan should not go past what the game's camera can show. Rerun CameraPanCheck.
4. D: remove the verb screen's explanatory note row (`VerbStripNote`, `VerbStripNoteHeight` in `UpdateRoomViewportLayout`). Keep the explanation in a tooltip. Update VerbAreaCheck (it reads the note's text) and PresentationCheck if needed.
5. E, then F, then G: the distaff shine (likely the verb area's drawn strings or the notes' colours not being reset; check ScummVM's verb redraw and `drawString` erasing first), keys for spells (ScummVM `processKeyboard` and LOOM's input script for note keys c d e f g a b C: decode script 005/006's key handling), and a key to skip the text in cutscenes (ScummVM's talk-stop key '.', already used for a click on the text: `SkipTalkAt`). Ask the user about the keys before binding them.
6. H: the cutscene menu (`CutsceneMenu_Click`) lists only the entries whose rooms include the current room (`KnownCutscene.RoomId` and the rooms the script passes through). Say what happens when none match.
7. I: the media player analysis (see above).
8. Room 36's exit click: why the game's click on Room 36's exit (object 599, box (464, 0) 16x144, walk point (494, 126)) does not reach Room 37 after a catalog visit. Trace script 006's walk and sentence (session probe); if the click can work, use it in the 086 entry.
9. More known cutscenes with text, each verified by a harness like FirewoodPlaybackCheck.

Rules:

* Judge every task by what it changes on screen. Say in the validation what the user now sees.
* Follow the evidence rules in CLAUDE.md and the plan. Messages and actors driven by reached events are game behaviour. Substitutes (a UI font, skipped waits, estimated poses) are labelled presentation and are optional. Label estimates and assumed input.
* Record new findings in docs/LOOM_SCRIPT_MAP.md and mark each as decoded, ScummVM or reading.
* For each task, record verification under .verification/<topic>/, update docs/LOOM_EGA_PLAN.md (status and next task) and add a row to docs/CHANGELOG.md.
* Rerun the audit when Core rendering or session state changes. If renders change, refresh the PNGs with `compare-png.ps1 -Refresh` from PowerShell.
* The WPF harnesses are slow (the user asked, 28 Sep). Don't run the full `wpf-checks.ps1`, or DoorwayClickCheck on all 75 doorways, unless the change touches what they test: the room-click path, doorway navigation or room loading. Otherwise run the unit tests and only the one or two related harnesses, and say in the validation which ones were skipped. DoorwayClickCheck takes a room list as extra arguments (for example `… out.txt 2 3 4 8 10`); `var[0x071]` can differ from a full run because the route differs. A task that touches clicks or room loads: run DoorwayClickCheck on Rooms 02, 03, 04, 08, 10 and 37.
* Commit each task separately once it builds and the tests pass.
* The user runs the app from the main checkout's exe. When a session works in a worktree, bring the branch onto master (rebase onto master if master moved), fast-forward master (`git -C <main checkout> merge --ff-only <branch>` when the main checkout is clean) and rebuild the Release app there (`dotnet build <main>\src\SCUMMexplorer.App\SCUMMexplorer.App.csproj -c Release`), so the user sees the changes.
* Once the session has used more than 250K tokens, stop at the next reasonable point (a committed task) instead of starting a new task. Check with the session usage tool (`get_usage`, the context's tokensUsed).
* The user checks in during long tasks: give a short status line every few steps.
* At the end of the session, replace docs/NEXT_SESSION_PROMPT.md with a prompt for the next session in this same format (state at start, what was done, ordered tasks, rules, tooling notes). End it with this same request, including the 250K-token stopping rule. Then create a task chip (spawn_task) that starts the next session with that prompt, because a session cannot restart itself.

Tooling notes:

* A worktree has no `Games/` or `scummvm/`. Link them with junctions to the main checkout's folders (`New-Item -ItemType Junction -Path Games -Target <main>\Games`); both are git-ignored. `.verification/fallback-evidence/code.tsv` is not in git either: copy it from the main checkout. Before any worktree is removed, delete its junctions first with `cmd /c rmdir <worktree>\Games` (removes the link only), so a recursive delete can never reach the real game data.
* FirewoodPlaybackCheck (.verification/firewood-scene): `<Games\loom> <output folder>`, about 100 s; it takes the 086 entry from the private static `KnownCutscenes` through reflection. GameTextsCutsceneCheck (.verification/game-texts-cutscene): same arguments, about 30 s; it builds the gallery modeless with `GalleryCutscenes()`.
* The status line is collapsed and reset to "Scene ready" by every room load: a harness reads it right after the call, before awaiting.
* A catalog visit's assumed arrival makes the ego's position an estimate; branches on it (such as `getActorWalkBox`) put later room loads on the estimated path, which the session refuses to enter. A route through a doorway verb gives a reached position.
* ScummVM comparison: when started from the session's shell, `scummvm.exe` hung on a blank white window (29 Sep). Ask the user to start it from Explorer (`<main>\scummvm\scummvm.exe`) and drive it. The computer-use grant works with the exe's full path as the app name (`D:\Documents\Personal\Code Projects\SCUMMexplorer\scummvm\scummvm.exe`). Screenshots and `zoom` work; synthetic input does not reach the SDL window. Take `zoom` captures every few seconds while the user plays, and ask the user what they did between captures.
* The Bash sandbox blocks some writes into the repo, and RTK filters grep/awk/head output. Use the Grep tool, or python over .verification/fallback-evidence/code.tsv (columns: owner, offset, operation, operands; owners like `script 044`, `Room 37 local 200`, `Room 37 entry`) and objects.tsv. For doc patches with non-ASCII, a python helper `edit(path, [(old, new)])` that keeps line endings and BOM works (write it to the scratchpad with the Write tool). The Edit tool is fine for ASCII code edits. Don't write patch scripts through PowerShell here-strings or `Set-Content` (they mangle non-ASCII), and don't put C# escapes such as `\n` inside heredoc'd python.
* `git add -f .verification/<topic>/*.txt` also picks up `obj/…/*.FileListAbsolute.txt` of a harness built there: add evidence files by name.
* To trace session internals, a temporary xunit test in tests/SCUMMexplorer.Tests (delete it afterwards) that builds `new LoomExplorationSession(game, snapshot)` and writes a file in %TEMP% is the quickest probe. `SessionTrace` holds session-level lines (room loads, cutscenes, freezes, delays, the boot's stop). `session.Actors` (not the snapshot) lists actors (`Room`, `X`, `Y`, `Visible` are nullable); facing is `actor.Look.Facing` (nullable).
* RoomAudit cannot write `room-audit.txt` into `.verification/room-audit` (access denied). The BMPs it writes there are enough for `compare-png.ps1`. For the gap count, run it again into a scratchpad folder and read "Rooms with remaining gaps".
* Blockers: `.verification/random-picks/Blockers` `<game> <output file>`, about 15 minutes; run it in the background. Compare label counts against .verification/frame-timing/blockers-after.txt.
* ActorDrawCheck: `<Games\loom> <output folder>`. OpeningPlaybackCheck (.verification/opening-playback): `<Games\loom> <output folder>`, about 80 s on the real clock; it calls `PlayOpening` and `SkipCutscene` through reflection and compares with a fresh booted session. CutscenePlaybackCheck: same arguments, about 70 s; `KnownCutscenes` is a private static array. Room02ExitClickCheck: same arguments, about 30 s. VerbAreaCheck: same arguments.
* WPF harness exes return at once when started directly from PowerShell (GUI subsystem). Use `Start-Process -FilePath <exe> -ArgumentList … -WorkingDirectory <repo root> -Wait -PassThru` and then read the output file. Build harnesses into the scratchpad (`dotnet build <harness>.csproj -o <scratch dir>`), and rebuild after changing src (each has its own copy of Core).
* In PowerShell, .NET file APIs (`[IO.File]::ReadAllText`) resolve relative paths against the process directory. Use absolute paths.
* Line endings: git normalizes (autocrlf); most working files are CRLF, `LoomActors.cs` is LF. LoomActors.cs and LoomBoundedScriptExecutor.cs have a BOM; MainWindow.xaml and MainWindow.xaml.cs do not (checked 29 Sep). PowerShell's WriteAllText with UTF8Encoding(true) adds one: write with UTF8Encoding(false) for those.
* MainWindow: the audio stop is `StopPlayback()`; the scene clock's stop is `StopScenePlayback()`; `StartPlayback` starts it; `PlaybackShouldContinue`/`PlaybackStep`/`ShowPlaybackFrame` run it; `SessionInScene` is "no player control" (cutscene or boot running). The doorway fallback is `FollowDoorway` → `ActivateObjectVerb` → `ContinueToReachedRoom`. The actor layer is `UpdateActorLayer()`.
* If `SCUMMexplorer.App.exe` is running, building the App fails to copy the exe (MSB3026). Build harnesses into the scratchpad. Don't stop the user's app.
* The harness output argument is a file path for DoorwayClickCheck and a folder for the others. Always write to a fresh path.
* To capture one element, draw it through a `VisualBrush` into a `DrawingVisual` (as the harnesses' Save does).
* Default parameter values compile into the caller: after changing one in Core, rebuild the App and the harnesses.
* `.verification/**/*.txt` and `*.png` are git-ignored; evidence files are committed with `git add -f`.
* MessageRender (.verification/messages/MessageRender) renders a session step to BMP; steps `v:obj:verb`, `c:x,y`, `d:x,y`, `f:n`, `t:<message source>`. VisitProbe (.verification/opcodes/VisitProbe) steps: `R`, `R/O/V`, `varNNN=`, `+N`, `sounds`, `?052`, `@x,y`, `!x,y`, `!!x,y`, `v:x,y`, `k:0x1B`, `s:34:a,b`.
* Branch semantics: the disassembly's `branch` is taken when the condition is false.
* ScummVM sources: curl raw files from github.com/scummvm/scummvm engines/scumm into the scratchpad (`vars.cpp` has `VAR_VIDEOMODE` and `setVideoModeVarToCurrentConfig`).
* Commit messages: write them to a file and use `git commit -F`.
* Harnesses call private methods through reflection with every argument (`MethodInfo.Invoke` does not fill optional parameters). `PlaybackRunning` is a private property.
* To measure a change against the previous commit, add a temporary worktree at HEAD in the scratchpad, junction `Games` into it, build and run there, then remove the junction with `cmd /c rmdir` before `git worktree remove --force`.
* DoorwayClickCheck hangs if playback never ends. Playback waits only for the ego (`EgoMoving`) and the camera, never for every actor.
* The auto-mode permission check sometimes fails for minutes at a time. After a few failures in a row, stop and tell the user rather than burn the 10-failure limit.
* A full DoorwayClickCheck run browses Room 01 first, so Room 02 is then a catalog visit; run `… 2 3 4 8 10` to test the boot's Room 02.
* Before blaming a WPF check failure on a change, run it twice and compare with HEAD.
* Python has no PIL here. To read PNG pixels, use `System.Drawing.Bitmap` from PowerShell.
* The `mcp__ccd_session_mgmt__get_usage` tool (load it with ToolSearch) reports the context's tokensUsed for the 250K rule. The weekly limit was at 93% on 29 Sep (evening).
* CutsceneLayoutCheck (.verification/cutscene-layout): `<Games\loom> <output folder>`, about 2 minutes; plays the opening and 40 s of script 046 and samples the room view's size, the verb strip, the arrows and disabled-button brightness.
* App-wide WPF styles go in MainWindow.xaml's resources, not App.xaml: the harnesses create `new Application()`, so App.xaml's resources never load there.
* SceneSoundCheck (.verification/cutscene-sound): `<Games\loom> <output folder>`, about 70 s; the opening on the real clock for 20 s, then script 046 stepped through `PlaybackTimer_Tick` (with the timer stopped) for 2,500 frames, compared with a separate session's reached sound events. `SceneSoundPlayer.Log` lists what the player did (frame, action, sound, kind).
* TextLineCheck (.verification/text-line): `<Games\loom> <output folder>`, about 20 s; Room 41: the view is 144 rows, and the sword's (634) and object 632's look lines (verb 0x36 run directly) are drawn over the room's top rows.
* SceneLeaveCheck (.verification/scene-leave): `<Games\loom> <output folder>`, about 20 s; plays the trial from `KnownCutscenes` for 8 s, picks Room 02 in the tree, clicks 80 px left of Bobbin through `HandleRoomPoint`.
* The playback clock plays sounds from `PlaybackStep`; `_playbackBatch` is true while an instant playback runs all its frames.
* The layout fits 144 room rows (LOOM; `RoomScreenRows`, 128 for other games) plus 56 verb rows when the verb strip is shown (`UpdateRoomViewportLayout`); there is no text line any more (`TextLinePanel` is gone, `LoomMessageRenderer.RoomScreenTop` is 0).
* ScummVM text placement: `displayDialog` (string.cpp) sets `_charset->_top = ypos`; `CharsetRendererV3::printChar` (charset.cpp) draws into `findVirtScreen(_top)`, clipped at that screen's bottom for the one-buffer screens (text, verb).
* Python edits of docs: a helper `edit(path, [(old, new)])` in %TEMP%\edit.py keeps line endings and BOM; put non-ASCII or backslash-heavy text in a file written with the Write tool and read it from python, rather than in a heredoc.
* ActorDrawCheck (.verification/actor-draw): `<Games\loom> <output folder>`, about 60 s; its Room 02 click into box 4 shows the depth mask (Bobbin behind the foliage). The mask composition runs on every actor-layer update (`UpdateActorLayer` calls `_objectSelection.ComposeDepthMask`).
* Box masks per room: `LoomWalkBoxReader.Read(roomBytes).Boxes[i].Mask`. Close-up rooms (53–87) have one box with mask 0.
* The session tokens were at about 230K when the last session stopped (5-hour limit 10%, weekly limit 93%).
* `actorOps` operands are `<actor>, [item; item; ...]`: split the list after `, [`, never at the first `[` (a variable actor such as `local[0x000]` has its own brackets). LoomDoorwayFinder.ReadArguments and LoomRoomSoundCandidates.BindLiteralArguments still cut at the first `[` for startScript argument lists; that is only wrong if the script number is a variable (not checked).
* A quick probe of a cutscene: `session.VisitRoom(Room 08)`, `session.ActivateObjectVerb(255, 0x38)` (the trial), then `AdvanceFrame()` in a loop and log `session.Actors` (`Look.Costume`, `Look.Frame`). For instruction-level detail, a temporary `File.AppendAllText` in `ApplyActorPlacement` filtered by offset is the fastest (revert it afterwards). The executor's `trace` is not exposed by the session. To look at an actor layer, `LoomActorRenderer.Render(...).Pixels` written as a 24-bit BMP with the EGA palette, then scaled to PNG with System.Drawing, works.
* The trial reaches Room 11 about 2,470 frames after the verb; the flight runs to about frame 2,700.
