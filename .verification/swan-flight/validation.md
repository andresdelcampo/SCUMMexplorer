# Swans in the trial's flight (actorOps on a variable actor), 30 September

The user's issue (29 Sep): "Hetchel flies as a human instead of a swan". The user picked it next on 30 Sep.

## What was wrong

The Elders' trial (script 046) ends with the Guild flying off through Room 11 (the loom's rift). The code (decoded):

- +0x1143 `loadRoom 0x0B`, +0x114E `move local[0x000] = 0x0002`
- +0x1153 `setClass local[0x000], [0x0000, 0x0096, 0x0095]`
- +0x1160 `actorOps local[0x000], [costume, 0x50; elevation, 0x0000; walkSpeed, 0x02, 0x01; initAnimation, 0x01; walkAnimation, 0x02; standAnimation, 0x03]`
- +0x1172 `increment local[0x000]`, +0x1175 `isLess local[0x000], 0x000A` back to +0x1153 (actors 2–10)
- +0x117C actor 2 gets frames 6, 7, 8; +0x1185–0x11D0 put actors 2–10 at the room's rift point.

A temporary session probe showed the loop ran with local[0] = 2 … 10, but the actors kept their earlier costumes: Hetchel (actor 5) in costume 48 and actor 9 in 50 (people), the elders in 53, actor 6 in 34, actor 7 in 35. In the app Hetchel and the others flew up through Room 11 as people.

The cause was generic, in the executor's `actorOps` handler (`LoomBoundedScriptExecutor.ApplyActorPlacement`): it cut the item list at the operands' **first** `[`. When the actor number is a variable (`local[0x000]`), that bracket belongs to the variable, so the list's first item came through as `0x000], [costume, 0x50` and was dropped. The first item is nearly always the costume. The list now starts after `, [`, as the actor number was already read.

ScummVM: `o5_actorOps` reads the actor with `getVarOrDirectByte(PARAM_1)`, so a variable actor is ordinary and every sub-opcode applies to it.

## Reach

In the game's code, 41 `actorOps` sites name the actor through a local (from `.verification/fallback-evidence/code.tsv`), and 30 of them set a costume first. Among them: script 044 +0x00DD (costume 5), 046 +0x1160, 052 +0x0070 (costume 0x52), 065, 070, 085, 100, 104, and the entries of Rooms 03, 05, 13, 43 and 47. It is one fix for all of them. Nothing is done per cutscene.

## What the user now sees

- **The trial's flight (Room 11):** nine small swans (costume 80) fly in a line to the rift, as the script sets them. Before, Hetchel and the elders flew as people. [room11-flight-actors-after.png](room11-flight-actors-after.png) is the actor layer at frame 2530 of the probe (on a plain background).
- **Room 03 on arrival from Room 02:** the entry's loop gives actors 4–7 costume 9: the campfires in front of the tents. Before, the room showed Room 02's leaf (costume 8) and actor 4 (costume 141) as stray marks. Before: [room03-visit-before.png](room03-visit-before.png) (HEAD, same harness), after: [room03-visit-after.png](room03-visit-after.png). Bobbin is at (276, 63) in both, behind the cliff's depth mask as he comes down the path.

## Checks

- New unit test `LoomActorLookTests.ActorOpsOnAVariableActorAppliesItsFirstItem`: plays the trial to Room 11 and expects actors 2–10 in Room 11 with costume 80. Tests 234/234; build 0 warnings.
- ActorDrawCheck: 0 failures (twice, deterministic). It was also run at HEAD in a temporary worktree: the Room 03 line differs only in the costume-9 actors (layer 48 → 119 px). [actor-draw-check.txt](actor-draw-check.txt).
- RoomAudit: 0 gaps in all 76 rooms ([room-audit.txt](room-audit.txt)). Object images are unaffected: the room-audit PNGs were not refreshed.
- Blockers: the output is identical to .verification/frame-timing/blockers-after.txt (the only real stops are `expression` (3)).
- Skipped (not touched: click path, doorways, room loading, text, sound): DoorwayClickCheck, TextLineCheck, SceneSoundCheck, CutsceneLayoutCheck, VerbAreaCheck, PresentationCheck, CameraPanCheck, the playback checks.
- Not compared with the running ScummVM. The fix follows `o5_actorOps` and the decoded script.
