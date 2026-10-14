# Indy3 S5: every room shows the first visit of the generic session

| | |
|---|---|
| Family / stage | Indy3Ega / S5 |
| Fixture and edition | `Games/indy3 ega en` (EGA, English, DOS floppy) |
| Date and author | 2026-10-14, S5 agent |
| Commits | 019cb07 (display operations), 0386f59 (Room 92 route), e75d290 (visit, settle, scene, isScriptRunning fix), 1c3ed31 and the harness commits (audit), 1ab9a9d (app wiring, tests), and the documentation commit after them |
| Registry change | unchanged, because the Objects cell stays partial until S6 (REQUIREMENTS section 3.1: full needs click responses too). The tracker records "S5 done". |

## What was done

- **Display operations** (the five the S4b boot reported as not applied), from ScummVM `script_v5.cpp` (read 14 October 2026): `roomOps roomColorLegacy` (`_roomPalette[b] = a`, kept in `ScummScriptRuntimeState.RoomColors`, reset at room load), `lights` (`VAR_CURRENT_LIGHTS`, flashlight), `stringOps` (string resources, `getStringChar` gives real values); transition effects, resource routines, shake and screen split change nothing in a room picture and are traced instead of reported. `ScummDisplayOperationTests` (4 tests).
- **Room 92 copy protection:** the boot now follows a real player: one click on the full-screen object 929 (its code starts local script 201, which starts 205, the script that computes `var[0x063..0x066]`), then each question is answered with the value the answering script's own check accepts (`isEqual var[0x06A], n` followed by `isNotEqual var[K], local[0x000]`), derived by `ScummSession.FindAnswerPreparation` and `DeriveAcceptedAnswer`. Result: no failed round (`var[0x06B]` = 0), `bit[0x5E4]` = 0, `var[0x039]` = 1; the boot takes 1145 frames (S4b: 1341). Labelled "Player input assumed" and "Player answer assumed" (Advanced).
- **Script 73 (item c): not pending in the real game.** S4b's session answered `isScriptRunning 1` with "not running" while script 1 was loading Room 6, so Room 6's entry started the Donovan scene. ScummVM's `startScene` runs inside the loading script's opcode, so the script is running (S5 fix: `_loadingFrames`). Script 73 is no longer started; Room 6's entry also no longer changes the ego's costume during the boot, so the ego reaches Room 73 in costume 1 (S4b: 39, a consequence of the same bug). After the boot 900 more frames leave only Room 73's ambient scripts and Script 17 pending. ScummVM source: `object.cpp` `getObjectOrActorXY` (an actor outside the current room gives 0xFF distance, so the loop at Script 73 +0x00A3 would also have waited forever); the decisive fact is the `isScriptRunning` guard in Room 6's entry.
- **First visit of every room:** `ScummGameSession.VisitRoom` (a visit that does not play the route: entry hooks, entry script, child scripts; the ego arrives as one decoded `loadRoomWithEgo` would bring it, a labelled seeded pick) and `Settle` (frames until nothing changed for 90 frames; ambient animators are ignored for resting; a script leaving the room keeps the last state in the room; limit 2400 frames). `ScummRoomFirstVisit.Run/Cached` builds the `ScummRoomScene` (picture, actors, shadow palette and colour table, lights, hotspot rule `IsHotspot`). Also fixed: `setVarRange` into a bit (Script 61).
- **App:** for Indy3 EGA the room view, the actors, the palettes and the room cards come from the scene (`MainWindow`: `_browsedScene`, `DisplayPalette`, `UpdateActorLayer`, `RenderRoomCardThumbnail`). The stored-object list (Advanced) keeps only objects the game's own search can select: owned by the room, not untouchable (class 24, R-6.15), parent states matching, not at 0, 0 (R-6.14 equivalent). The plain background is the fallback, with a note in Advanced only. LOOM and the other games are unchanged (`ScummProfile.HasFirstVisitScenes`).

## Commands run (through RTK)

```powershell
rtk.exe proxy dotnet build SCUMMexplorer.slnx
rtk.exe proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build            # whole project
rtk.exe proxy dotnet test ... --filter "FullyQualifiedName~Loom"                                       # LOOM regression
rtk.exe proxy dotnet Indy3RoomAudit.dll "Games\indy3 ega en" <out>                                     # this record's audit
rtk.exe proxy dotnet RoomAudit.dll "Games\loom ega mt32 en" <out>                                      # LOOM room audit (regression)
rtk.exe proxy dotnet Indy3WindowCheck.dll "<abs>\Games\indy3 ega en" <out> 73 28                       # real window, private profile
```

## Results (numbers)

- **Room audit** (`room-audit.txt`, `room-audit.tsv`, contact sheets `rooms-sheet0..3.png`): 88 rooms, **88 without a gap** (0 rooms with a gap), 0 rooms that could not run, about 45 s for all rooms (4 threads). Before S5 (the view painted the index states only): 1 room with a gap by the same stored-image test, but only 7 rooms showed any object and no room showed an actor; after: 35 rooms show objects and 62 show actors. The gap test only sees colour-0 holes under stored images, so "no gap" is not "complete"; see the uncertainties.
- **How the runs ended:** 68 settled, 16 reached the frame limit (1, 6, 15, 20, 28, 32, 39, 44, 47, 60, 66, 79, 80, 83, 84, 94), 4 left the room (37, 41, 43, 82). 9 things the run could not follow remain (Script 131 starting 132 with unknown arguments in Rooms 12, 13, 77; Room 03 entry +0x006F; Room 33 depends on `bit[0x1517]`; Script 23 following the ego into another room in Rooms 18, 78, 80; Script 73 in Room 6). Rooms 47, 60 and 66 start dark (room lights off); the explorer shows them lit.
- **Unit tests:** `ScummRoomFirstVisitTests` (3): Room 73 settled (active images 826, 828, 830, 832; ego costume 1; actor 9 costume 38), Rooms 45 (615, 618, 621) and 90 (913, 915, 917, 920, 921, 922) pinned, and nine sample rooms without a gap with hotspots obeying the object-search rule. Boot test updated (two assumed inputs, four answers, `var[0x06B]` 0, `bit[0x5E4]` 0, no Script 73). Whole project 983 total, 978 passed, 5 skipped, 0 failed; `Loom*` 841 total, 836 passed, 5 skipped, 0 failed (baseline after S4b: 835 passed, 5 skipped); `SupportMatrixTests`, `SupportConformanceTests`, `GameSupportTests`, `ScummSessionArchitectureTests` pass.
- **LOOM regression:** LOOM EGA (English Roland) room audit: 0 rooms with gaps, 3 gaps before filling, 0 after: identical to the S4a/S4b baseline. LOOM CD tests are among the `Loom*` tests.
- **Real window** (`window-check.txt`, `window-room-73.png`): Rooms 73, 28 and 15 open with the first-visit scene and actors; Normal mode shows no note; Advanced mode shows how the visit was made; the room cards fill with first-visit pictures.

## Comparison with ScummVM (source files and date; runtime comparison if any)

- **Source** (`engines/scumm`, `master`, read 14 October 2026): `script_v5.cpp` (`o5_roomOps`, `o5_lights`, `o5_stringOps`, `o5_loadRoomWithEgo`), `room.cpp` (`startScene`), `object.cpp` (`getObjActToObjActDist`, `getObjectOrActorXY`, `findObject`), `camera.cpp` (`setCameraFollows`), `debugger.cpp` (`Cmd_Room`), `gfx.cpp` (`LIGHTMODE_*`, `_roomPalette`), `vars.cpp`. The facts are in [SCUMM_BOOT_SEQUENCE.md](../../docs/engine/SCUMM_BOOT_SEQUENCE.md), [SCUMM_SCRIPT_MAP.md](../../docs/engine/SCUMM_SCRIPT_MAP.md) and [SCUMM_OBJECT_STATES.md](../../docs/engine/SCUMM_OBJECT_STATES.md).
- **Runtime comparison with the bundled ScummVM: not made.** The bundled `scummvm.exe` was started with a private configuration (never the user's) and the English EGA folder (it detected `indy3-ega`); its default renderer showed only a white window that could not be captured, and its software renderer stopped with "SDL_BlitSurface failed: SDL_UpperBlit: passed a NULL surface" (a dialog on the user's desktop; the process was ended and the temporary files removed). Driving it by synthetic keys without a capture was not reliable enough to compare pictures, so no ScummVM picture of any room was compared.
- **What was compared instead** (source-level and static, 15 sampled rooms chosen to include the most script-heavy; table below): the rules the pictures depend on were checked against the ScummVM source above, and the objects the room's own entry and local scripts name (`setState` with a state above 0 or `drawObject`) were compared with the objects the run shows. Differences are listed.

| Room | Ending | Frames | Active images / stored | Actors | Static cross-check (objects named by the room's scripts vs shown) |
|---|---|---|---|---|---|
| 73 gym (boot room, script-heavy) | settled | 587 | 4 / 9 | 2 | 6 named; 825, 829, 833 not shown (ambient crowd in another phase), 830 shown by the index |
| 01 Marcus's room (14 pending scripts) | frame limit | 2400 | 11 / 38 | 2 | 59 named, not shown; others shown from index states and global scripts |
| 02 | settled | 90 | 2 / 6 | 1 | agrees |
| 15 Venice square | frame limit | 2400 | 8 / 27 | 2 | 190, 192 named, not shown |
| 20, 28, 32 (castle, scripts that never rest) | frame limit | 2400 | 8 / 16, 10 / 20, 9 / 18 | 3 | no object named by the room's own scripts; shown from index states; mostly black pictures |
| 35 | settled | 304 | 5 / 12 | 4 | agrees (nothing named) |
| 36 | settled | 102 | 3 / 13 | 3 | agrees |
| 38 restaurant | settled | 118 | 3 / 6 | 2 | 577, 578, 581 named, not shown |
| 45 stage | settled | 96 | 3 / 9 | 0 | nothing named; shown from index states |
| 52 | settled | 90 | 0 / 2 | 1 | agrees |
| 71 | settled | 202 | 7 / 19 | 2 | 789, 796, 806, 807 named, not shown |
| 86 | settled | 96 | 2 / 16 | 2 | agrees |
| 90 | settled | 92 | 6 / 12 | 0 | nothing named; shown from index states |

## Differences and uncertainties kept

- No ScummVM picture was compared (above). Everything in the table is source-level or static; "named, not shown" can be a branch not taken, an ambient phase or a missing behaviour, and was not resolved room by room.
- Rooms 20, 28 and 32 show mostly black pictures (the backgrounds are mostly colour 0 and the objects the run shows cover little); their ambient scripts never rest. Not verified against the game.
- The visit does not play the route: the story variables are those the boot leaves (copy protection passed, lecture seen). A room whose entry depends on later story state shows the state it has at that point (labelled "state preview"). Nothing was invented (R-1.9); the ego's arrival is a seeded, labelled pick (R-1.4); where no decoded arrival exists the ego is not placed (Rooms 8, 9 and others; Advanced note).
- Ambient animators are shown in the phase the room was in when it rested; this is a labelled pick, not a game state.
- Rooms 47, 60 and 66 start dark (room lights off); they are shown lit.
- `ScummRoomFirstVisit.Cached` runs each room once per folder in the app; opening a room takes about two seconds the first time.
- The input constants of S6 (click gaps, camera 160) were not changed.

## User checks (exact steps and where; none if none)

Open `Games/indy3 ega en` in the app (Normal mode) and look at the room cards and the room view of: **73** (gym: Indy at the bench in his first clothes, the boxers and the crowd), **6** (the college outside; the two men of the Donovan scene are not there), **92** (the notebook), **15** (Venice), **28, 20 and 32** (mostly black pictures: is that what the game shows?), **1** and **38**. Say which rooms look wrong or incomplete compared with the real game; those rooms are the next S5 work.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Indy3 script map](../../docs/indy3/INDY3_SCRIPT_MAP.md) (Room 6, 73, 92 rows, first-visit facts, open questions), [boot sequence v3](../../docs/engine/SCUMM_BOOT_SEQUENCE.md), [display operations](../../docs/engine/SCUMM_SCRIPT_MAP.md), [object states](../../docs/engine/SCUMM_OBJECT_STATES.md), [tracker and decision log](../../docs/indy3/SUPPORT_STATUS.md).
