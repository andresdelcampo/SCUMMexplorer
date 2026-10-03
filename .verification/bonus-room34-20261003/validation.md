# Bonus scene: Room 34 ledge lookout (local200 +0035) — 3 October 2026

The user asked to expose the otherwise unreachable Room 34 local200 +0035 scene (see the global-scripts validation: the watcher needs previous walk box 6 and new box 8, but the walker and box matrix always route 6→7→8) as a bonus/hidden scene in the cutscene menu.

Implementation (no change to execution of any existing route):
- `LoomExplorationSession.RunLocalScriptAt(scriptId, offset)`: starts a room-local script at an instruction offset as an exploration action (any running copy ends first, as runScript does; uncertainty text states the guard was not tested).
- `LoomReplaySequence` step kind `script` (ObjectId = script number, X = offset).
- Catalog entry `bonus-room34-local200` (category "Bonus scenes", steps script 200 @ 0x35 then scene end `endCutscene at Room 34 local script 200 +0x0056`), membership entry, and the coverage note for "Room 34 local 200". The picker gets a "Bonus scenes" filter (indexes after it shift by one; the room picker's "All playable" index is now 3).
- Test `ABonusScriptStartRunsRoom34sLookoutSceneToItsEnd`; probe `BonusProbe` (132 frames, balanced scopes, camera pans to ~376 then back).

Labels: exploration action / state preview, not audited coverage (audited stays 234/235; fresh single-action 169/235). What the scene shows is not established (camera pan to x 464, ego animation 0xF9, 300-jiffy wait, scroll back). No App launch, no push.
