# Bonus scene: Room 34 ledge lookout (local200 +0035) — 3 October 2026

The user asked to expose the otherwise unreachable Room 34 local200 +0035 scene (see the global-scripts validation: the watcher needs previous walk box 6 and new box 8, but the walker and box matrix always route 6→7→8) as a bonus/hidden scene in the cutscene menu.

Implementation (no change to execution of any existing route):
- `LoomExplorationSession.RunLocalScriptAt(scriptId, offset)`: starts a room-local script at an instruction offset as an exploration action (any running copy ends first, as runScript does; uncertainty text states the guard was not tested).
- `LoomReplaySequence` step kind `script` (ObjectId = script number, X = offset).
- Catalog entry `bonus-room34-local200` (category "Bonus scenes", steps script 200 @ 0x35 then scene end `endCutscene at Room 34 local script 200 +0x0056`), membership entry, and the coverage note for "Room 34 local 200". The picker gets a "Bonus scenes" filter (indexes after it shift by one; the room picker's "All playable" index is now 3).
- Test `ABonusScriptStartRunsRoom34sLookoutSceneToItsEnd`; probe `BonusProbe` (132 frames, balanced scopes, camera pans to ~376 then back).

Labels: exploration action / state preview, not audited coverage (audited stays 234/235; fresh single-action 169/235). What the scene shows is not established (camera pan to x 464, ego animation 0xF9, 300-jiffy wait, scroll back). No App launch, no push.

## Addendum: Rusty the ghost at the 098 Walk response (user report)

The user saw two cutscenes with the same name in Room 34 (objects 559 and 560 verb 0x37, plus a third from object 586) that all start script 098, and in them Rusty still appeared as the sleeping, living boy instead of a walking ghost. Cause (decoded + Core probe `GhostProbe`): Room 34's entry chooses by bit D9D, which only scene 087 sets (setClass operand 0x98 *sets* class 24, 0x18 clears it). With D9D=0 (the fresh catalog arrival the three replays used) object 561 stays touchable and actor 3 gets costume 0x70 (living Rusty); with D9D set, 561 becomes untouchable, 586 "Rusty" touchable and actor 3 gets costume 0x78 (the ghost). The old replays therefore showed an impossible mix: the 098 dialogue (he is dead) over the early room.

Fix (no state writes): the three duplicate entries are replaced by one sequence `sequence-087-098`: the existing firewood 086 → straw dream 087 sequence, a catalog visit to Room 34 (new `visit` step; explicit exploration assumption that keeps the story state), 40 settle frames, then object 559 verb 0x37 → script 098 `+0x0035/end0618`. 4128 frames; before the verb the ghost (actor 3 costume 120) is placed, 561 untouchable and 586 touchable. Its scriptIds are [98] only, so Game Texts can now offer it for the 098 texts (three entries had made that mapping ambiguous). New `LoomExplorationSession.VisitRoomById` and sequence step `visit`; test `RustysWalkResponseRunsAfterTheDreamSoTheGhostArrives`. Not changed: the draft-response previews at Rusty (586 verbs 0x43/0x44/0x4C/0x4D/0x56) and at chico (561) still start from the fresh state; 586's are only touchable in the ghost state, so they likely deserve the same treatment if wanted. No App launch; the picker was not run.
