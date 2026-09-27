# Validation: actor positions and actor queries (27 September 2026)

Status: **done.** Builds with 0 warnings; **168/168 tests** pass (4 new in `LoomActorTests`). Room audit: 0 gaps in 76 rooms, **0 changed renders**. DoorwayClickCheck: **75/75** ([report](doorway-clicks.txt)). Room03ActionCheck `162 4 167 38`: **46 PASS** ([captures](room03-action/)). WalkBoxCheck with actor markers: 4/4 PASS in Rooms 02 and 03 ([report](overlay/walk-box-check.txt), [Room 02](overlay/room-02-walk-boxes.png)).

## What is modelled (ScummVM `actor.cpp`, `script_v5.cpp`, `room.cpp`, v3 small-header paths)

`Formats/LoomActors.cs` (`LoomActorTable`) holds actors 1–12 (`_numActors` = 13) for the session; every field can be unknown.

- **Start:** `initActor(-1)`: room 0, (0, 0), walk box 0, hidden, last valid point (0, 0).
- **`putActor x, y`** keeps the room. In the current room the actor is shown at `adjustXYToBeInBox` of the point (box and destination box set); elsewhere it is hidden and keeps the raw point. Putting the ego sets `_egoPositioned`.
- **`putActorAtObject`**: the object's walk point when `whereIsObject` finds it in the room, (240, 120) when the object is in no room and no inventory; an object held in an inventory is unknown (inventory not modelled).
- **`putActorInRoom`**: sets the room only; room 0 runs `putActor(0, 0, 0)`.
- **`walkActorTo`/`walkActorToObject`/`walkActorToActor`** (`startWalkActor` v3): outside the current room the actor is put at the destination exactly. In the current room, `ignoreBoxes` ends in box 255; otherwise the destination stays in the destination box or is adjusted into the nearest box. **The walk is taken to end there at once and labelled as an estimate** (timing and path are not simulated). If the box matrix (`getNextBox`) has no route from the actor's box, the end is unknown.
- **`actorOps`**: `init` (`initActor(0)`: last valid point, `ignoreBoxes` off, actor classes cleared), `ignoreBoxes`/`followBoxes` (then `putActor()` in the current room), `costume` on a visible actor (hide/show re-adjusts). `setClass` 0 on an actor clears `ignoreBoxes`.
- **`setBoxFlags`** changes the current room's copy of the boxes; an unknown box makes box flags unknown for placements until the next room entry.
- **Room entry** (`startScene`): every actor is hidden, the boxes are read, then actors whose room is the new room are shown and adjusted (`showActors`), and `_egoPositioned` is cleared.
- **`loadRoomWithEgo obj, room, walkTo=(x, y)`**: the executor attaches the resolved object and destination to the event. Before the entry the ego's room becomes the target (hidden, coordinates kept); after the entry hooks, if the entry code did not put the ego, it stands at the object's walk point; then, if x ≠ −1, it walks (estimate).
- **Catalog visits** do not execute the route, so the ego becomes unknown: "Room NN was selected from the catalog, so where the ego would arrive was not executed".
- **Queries:** `getActorWalkBox` (any room), `getActorRoom`, `getActorX`/`getActorY` (`getObjX`/`getObjY`: an actor number gives its position; a higher number gives the object's walk point, or −1 when not found), and the conditional `isActorInBox` (`checkXYInBoxBounds` on the current room's boxes). An unknown value stops the script with the reason. A value that comes from an estimate is used, marked as an estimate, and the step lists why.
- **Unknowns:** an operand the executor cannot read makes what it sets unknown; an unknown actor number makes every actor unknown; an actor command on a branch the preview could not decide makes that actor unknown. Values that follow a seeded pick are estimates.

Not modelled: the camera following the ego after `loadRoomWithEgo` (`VAR_CAMERA_POS_X` stays at the room-entry value), `getActorCostume`, `getActorElevation`, `getActorMoving`, `getDist`, walk timing and paths.

## Results

**Boot** (`VisitProbe Games\loom +3`): the ego (actor 1) stands in Room 02 at (828, 60), box 0, visible, not an estimate: the cliff top under the tree. Script 045 (Room 02's walk-box watcher) now reads box 0, which is not 4, and waits for the next box change; it no longer stops. Actors 2, 10 and 11 were put after seeded picks (estimates); actor 3's position is the end of a walk (estimate).

**Room 03 → 04** (object 162): local 200 +0x0008 `loadRoomWithEgo` brings the ego into Room 04 and walks it to (580, 132), box 0, labelled as an estimate.

**Blockers** ([before](blockers-before.txt), [after](blockers-after.txt); counts are visits and actions that show each stop):

| Stop | Before | After |
| --- | --- | --- |
| `getActorWalkBox` not modelled | 184 | 0 |
| `getActorWalkBox` reads an actor whose walk box is unknown | – | 115 |
| `getActorX` not modelled | 71 | 0 |
| `getActorX` reads an actor whose position is unknown | – | 25 |
| `getActorX` answered from a walk end (estimate) | – | 13 |
| `getActorX` answered from a position after a seeded pick (estimate) | – | 8 |

The 115 remaining walk-box stops are mostly ego watchers in rooms the harness enters from the catalog, where the ego's arrival was not executed. Routes that arrive through a reached `loadRoomWithEgo` answer them.

## Tests

`LoomActorTests`: `putActor` in and out of the current room (Room 02 boxes), a walk ending in box 4 as an estimate and an unknown destination, the boot's ego on the Room 02 cliff with script 045 running, and `loadRoomWithEgo` into Room 04 after a catalog visit to Room 03.
