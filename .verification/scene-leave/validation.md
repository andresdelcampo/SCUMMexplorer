# Leaving a conversation from the catalog (29 September 2026)

## The user's report

"Coming to a room after leaving a conversation in another has the game in bad state: cannot move." Any conversation; they left through the tree/room list, while the conversation was still running. In the new room, clicks did nothing.

## What the user now sees

- If you pick another room in the tree (or a room button) while a cutscene or conversation plays, the scene ends there. In the new room Bobbin answers clicks at once and walks. The session's uncertainties for the visit say that the explorer stopped and closed the scene, not the game.
- The rest of the left scene does not play: its remaining room loads and texts are gone. Replay it from the cutscene menu or Game Texts.

## Why

- Decoded: every LOOM cutscene runs script 002 at `beginCutscene` (the cutscene start script, `var[0x023]`): `cursorSoftOff`, `userInputSoftOff`, `freezeScripts 0x7F`, `bit[0xD6E] = 1`. Script 003 (`var[0x024]`, run by `endCutscene`) undoes that: `cursorSoftOn`, `userInputSoftOn`, `freezeScripts 0x00`, `actorFollowCamera var[0x001]`, `bit[0xD6E] = 0`.
- A catalog visit enters a room without running the route. The scene's global script (for example the trial's script 046) went on pending in the chosen room, with its cutscene open and input off. `Click` then refused every click: "not sent, because user input is off (cursorCommand), and ScummVM's checkExecVerbs reads no click then" (session probe; SceneLeaveCheck at d0933bf, [scene-leave-check-before.txt](scene-leave-check-before.txt)).
- The game never leaves a scene this way, so no ScummVM path applies directly. The session now does what the scene's own end would do. A catalog visit while a cutscene is open (`InCutscene`) pops each open cutscene, innermost first, and stops the script that began it, before the room is entered. After the entry it sets `VAR_OVERRIDE` to 0 and runs the cutscene end script with the cutscene's data, as `endCutscene` does. Each such visit carries the label "an explorer action: the game never leaves a scene this way, and the scene's remaining room loads and texts did not run". Scripts the scene started (other than its own) are not stopped. Room-scoped ones end with the room change, as before.

## The change

`LoomExplorationSession.VisitRoom` (catalog path): `CloseScenesForCatalogVisit` before the entry (`CatalogEnter`, the former body), and `EndClosedScenes` after it. Trace lines: "catalog visit to Room NN: stops script 046, which began a cutscene (data 1)", then "… endCutscene runs script 3 with data 1".

## Evidence

- New unit test `LoomCutsceneTests.ACatalogVisitDuringACutsceneEndsItSoClicksAreRead`: the trial 300 frames in (cutscene open, the click is refused), then a catalog visit to Room 02. The cutscene is closed, script 003 ran, and after 30 frames a click in Room 02 is sent. Tests 235/235; build 0 warnings.
- New SceneLeaveCheck (`.verification/scene-leave/SceneLeaveCheck`, `<Games\loom> <output folder>`, about 20 s). It plays the trial from its known-cutscene entry in the real MainWindow for 8 s, picks Room 02 in the tree, then gives `HandleRoomPoint` a click 80 px left of Bobbin. With the fix: 0 failures ([scene-leave-check.txt](scene-leave-check.txt)). Bobbin walks from (568, 114) to (540, 108), and the status is "Click sent to the game (assumed input)". At d0933bf, without the fix: 2 failures, the cutscene still open and the click not sent ([scene-leave-check-before.txt](scene-leave-check-before.txt)). There the ego moved anyway, because script 046 went on moving him.
- DoorwayClickCheck on Rooms 02, 03, 04, 08, 10 and 37: 16/16, 0 failed ([doorway-click-check.txt](doorway-click-check.txt)).
- Not rerun: RoomAudit and Blockers (no catalog visit there runs during a cutscene), the playback checks (they start from a fresh session with no cutscene open when the room is visited), the other WPF checks.
- Only the trial was tested. Other cutscenes go through the same path (`_cutscenes`, script 003).
