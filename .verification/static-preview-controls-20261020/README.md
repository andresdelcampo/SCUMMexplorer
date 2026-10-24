# Static-preview controls and ambient scripts, 20 October 2026 (resumed 24 October)

| | |
|---|---|
| Item | The user's rule (R-8.5b, extended): a room that cannot be prepared live still has the room's controls, and entering it runs only its ambient scripts. |
| Base | master `71d55e98` (room controls for LOOM and the four Maniac/Zak families, audio lifecycle and overlays already merged by the other orchestrator). The earlier local merge of the `codex/*` branches was dropped; this branch is master plus its own commits. |
| Harness | `ControlsWindowCheck/` (real `MainWindow`, private profile and room cache): per room, whether it is live or static, the play clock, frames in 3 s, whether the picture changed, whether the story moved, and which controls are visible/enabled. `scene` plays the first (or `item=N`) Play Cutscene entry of the static room; `advanced` switches mode. Not a ScummVM comparison. |

## Survey (Normal mode, one live and one or two static rooms per game)

Columns: room | kind | clock | frames in 3 s | picture changed | story moved | bar | Pause | Skip line | Skip | Play Cutscene (room) | Play Cutscene (header, Advanced only: "-" in Normal) | Interact | Sounds | Play Game Intro. "-" = not visible, on/off = visible and enabled/disabled, (n) = number of actions listed under Interact. Each game: first the live row, then the static row(s).

### Before (merged master `71d55e98`)

```
== indy3 ega en (exit 0)
02 | live | on | 28 | no | no | on | on | off | off | on | - | on (62) | on | on
01 | static | off | 0 | no | no | on | off | off | off | on | - | off (22) | on | on
== indy3 vga en (exit 0)
01 | live | on | 28 | yes | no | on | on | off | off | on | - | on (22) | on | on
06 | static | off | 0 | no | no | on | off | off | off | on | - | off (13) | on | on
== monkey1 ega en (exit 0)
07 | live | on | 27 | no | no | on | on | off | off | on | - | on (42) | on | on
01 | static | off | 0 | no | no | on | off | off | off | on | - | off (12) | - | on
02 | static | off | 0 | no | no | - | - | - | - | - | - | off (8) | - | on
== monkey1 vga en (exit 0)
08 | live | on | 26 | yes | no | on | on | off | off | on | - | on (34) | on | on
02 | static | off | 0 | no | no | - | - | - | - | - | - | off (8) | - | on
== mi2 talkie en (exit 0)
07 | live | on | 29 | yes | no | on | on | off | off | on | - | on (52) | on | on
04 | static | off | 0 | no | no | on | off | off | off | on | - | off (22) | - | on
== indy4 vga en (exit 0)
02 | live | on | 27 | no | no | on | on | off | off | on | - | on (18) | on | on
01 | static | off | 0 | no | no | on | off | off | off | on | - | off (47) | - | on
== loom ega mt32 en (exit 0)
01 | live | on | 0 | no | no | on | off | off | off | on | - | off | on | -
02 | static | off | 0 | no | no | - | - | - | - | - | - | - | - | on
== loom vga en (exit 0)
01 | live | on | 0 | no | no | on | off | off | off | on | - | off | - | -
02 | static | off | 0 | no | no | - | - | - | - | - | - | - | - | on
== mmansion ega en (exit 0)
01 | static | off | 0 | no | cutscene | on | off | off | off | on | - | off (58) | on | on
02 | static | off | 0 | no | cutscene | - | - | - | - | - | - | off (13) | - | on
== mmansionE ega es (exit 0)
01 | static | off | 0 | no | cutscene | on | off | off | off | on | - | off (58) | on | on
== zak ega en (exit 0)
01 | live | on | 27 | yes | no | on | on | off | off | off | - | on (74) | on | on
02 | static | off | 0 | no | no | on | off | off | off | on | - | off (56) | on | on
== zakE ega en (exit 0)
01 | live | on | 27 | yes | no | on | on | off | off | off | - | on (70) | on | on
02 | static | off | 0 | no | no | on | off | off | off | on | - | off (56) | on | on
SURVEY-DONE
```

### After (this branch)

```
== indy3 ega en (exit 0)
02 | live | on | 29 | no | no | on | on | off | off | on | - | on (62) | on | on
01 | static | on | 29 | no | no | on | on | off | off | on | - | on (22) | on | on
== indy3 vga en (exit 0)
01 | live | on | 27 | yes | no | on | on | off | off | on | - | on (22) | on | on
06 | static | off | 0 | no | no | on | off | off | off | on | - | on (13) | on | on
== monkey1 ega en (exit 0)
07 | live | on | 28 | no | no | on | on | off | off | on | - | on (42) | on | on
01 | static | on | 30 | yes | no | on | on | off | off | on | - | on (12) | - | on
02 | static | on | 30 | no | no | on | on | off | off | - | - | on (8) | - | on
== monkey1 vga en (exit 0)
08 | live | on | 26 | yes | no | on | on | off | off | on | - | on (34) | on | on
02 | static | on | 30 | no | no | on | on | off | off | - | - | on (8) | on | on
== mi2 talkie en (exit 0)
07 | live | on | 27 | yes | no | on | on | off | off | on | - | on (52) | on | on
04 | static | on | 29 | yes | no | on | on | off | off | on | - | on (22) | on | on
== indy4 vga en (exit 0)
02 | live | on | 28 | no | no | on | on | off | off | on | - | on (18) | on | on
01 | static | off | 0 | no | no | on | off | off | off | on | - | on (47) | - | on
== loom ega mt32 en (exit 0)
01 | live | on | 0 | no | no | on | off | off | off | on | - | off | on | -
02 | static | off | 0 | yes | no | on | off | off | off | on | - | off | on | -
== loom vga en (exit 0)
01 | live | on | 0 | no | no | on | off | off | off | on | - | off | - | -
02 | static | off | 0 | yes | no | on | off | off | off | on | - | off | on | -
== mmansion ega en (exit 0)
01 | static | off | 0 | no | cutscene | on | off | off | off | on | - | off (58) | on | on
02 | static | off | 0 | no | cutscene | on | off | off | off | - | - | off (13) | on | on
== mmansionE ega es (exit 0)
01 | static | off | 0 | no | cutscene | on | off | off | off | on | - | off (58) | on | on
== zak ega en (exit 0)
01 | live | on | 27 | yes | no | on | on | off | off | off | - | on (74) | on | on
02 | static | off | 0 | no | no | on | off | off | off | off | - | on (56) | on | on
== zakE ega en (exit 0)
01 | live | on | 27 | no | no | on | on | off | off | off | - | on (70) | on | on
02 | static | off | 0 | no | no | on | off | off | off | on | - | on (56) | on | on
SURVEY-DONE
```

Reading it. Live rooms are unchanged in every game (bar, Play Cutscene, Interact and Sounds were already there on master for the verb-based games). The gaps on master were in static previews: LOOM showed nothing; the verb-based games kept the bar and Play Cutscene but with Pause, Skip and the Interact actions disabled, and no animation. Now: the bar stays and Pause works; Interact acts; Sounds is offered; permanent animations move (MI2 Room 4, Indy3 EGA Room 1, MI1 and MI1 CD static rows show a running clock and frames; LOOM Room 02's swan moves: picture changed). Rooms with no ambient script (Indy3 256 Room 6, Indy 4 Room 1) stay a still picture, with the bar visible and Pause disabled, Play Cutscene and Interact working. Maniac Mansion classic/enhanced static rooms are unchanged on purpose (they keep their own startup rules, R-8.34); Zak's static rooms gained enabled Interact. Header Play Cutscene is Advanced only (R-8.1), as before.

## How "ambient" is decided

`ScummAmbientScripts.Classify(data, room)` (Core, `Scumm/Analysis`, any game through `IScummDataSource`): for each script the room's entry script starts with a literal number and literal arguments, the code it can reach (through its branches and the scripts it starts, depth 8) must loop back (a backward jump or a script that starts itself again), change only the picture or sound (`setState`, `drawObject`, actor operations, `startSound`/`stopSound`/`soundKludge`, `roomOps`, `setClass`), and contain none of: a cutscene or override, `print`, a room load, a camera move, a cursor/verb/input change, an object verb or sentence, an inventory change, `freezeScripts`, or a wait on a printed line. A branch that is never taken still counts, so the check errs toward "not ambient". No room list. Advanced shows which scripts run and why each other start is rejected (status tooltip).

`ScummSession.PrepareAmbientRoom`: on an independent copy, the room is loaded as `startScene` loads it (`EnterRoom(runEntry: false)`: no global entry hook, no entry script), every other script slot is ended, and only the ambient scripts start with the entry's arguments. The live session is not modified and the ego is not placed. The window plays that copy on its clock; clicks and keys do not reach the game; Play Cutscene, Play Game Intro and the next room visit leave it for the session it was made from (stated start).

Examples (unit test): MI2 Room 4 runs script 200 (through 205: the fire and pirates, random-timed) and rejects 204 and 206 (cutscene); LOOM Room 02 runs the swan (042), the sound player (039) and idle animations and rejects script 016 (`print`) and 045 (`cutscene`); Indy3 Room 4 runs script 200 (animateActor/startSound loop).

## Window checks (real window)

- MI2 Room 4 (talkie): static preview with the bar, a running clock (ambient frames advance, picture changes), Play Cutscene lists 11 scenes (no verb bytes in Normal) and plays `Room 04 · Entry`: a cutscene began, bar/Pause/Skip active. No story entry runs on arrival (unit test: 300 frames, Room 4 stays current, no cutscene, no message).
- Indy3 256 Room 6 (no ambient script): still picture, bar visible, Play Cutscene lists and plays its entry.
- LOOM EGA Room 02: bar, Play Cutscene (6 entries), Sounds; `Room 02 · Look at leaf` plays from the static preview.

## Folder-change music (R-13.12)

Decision (GAME_SUPPORT_INSTRUCTIONS 4.5): the user's question ("keep music from LOOM after changing the folder??") is read as a complaint that the sound goes on; master already stops it (`ResetGameAudio`, tested there with LOOM Sound 13). This branch adds the static preview's ambient state to that reset (`_loomAmbientSource`, `_playAmbient`, `_playLiveSource`); its sounds are the play session's, detached by the same call. My folder-switch harness mode found no LOOM sound playing in the private profile (sound device off), so no independent playing-sound check is claimed beyond master's.

## Tests

`ScummAmbientScriptsTests` (classification on MI2 Room 4, LOOM Room 02, Indy3 Room 4; MI2 campfire preview animates without story; per-game survey), `ScummPlayStartTests`, `ScummManualRoomPolicyTests`, `ScummRoomOverlayTests`, Maniac/Zak, support and architecture tests: 276 passed, 0 failed.

## User checks

1. Open MI2, pick Room 4 (campfire): the picture with the fire and pirates moving, the bar (Pause enabled), **Play Cutscene**, **Interact** (expander under the room), **Sounds** and the header **Play Game Intro**. Clicking in the picture does nothing (no story). Play Cutscene lists the room's scenes; one plays.
2. Open Indy3 256, pick Room 6: still picture with the same controls; an Interact action runs.
3. Open LOOM EGA, pick Room 02: the swan moves; Play Cutscene lists the room's scenes without verb bytes (Advanced adds them); Sounds is there.
4. Start LOOM music, open another game folder: the music stops.

## Files

`ScummAmbientScripts.cs`, `ScummSessionManualRooms.cs` (`PrepareAmbientRoom`), `ScummSessionRooms.cs` (`runEntry`), `MainWindow.Play.cs`, `.PlayCutscenes.cs`, `.GenericRoomActions.cs`, `.ManualRooms.cs`, `.Cutscenes.cs`, `.RoomSounds.cs`, `.xaml.cs`; harness `ControlsWindowCheck/`.
