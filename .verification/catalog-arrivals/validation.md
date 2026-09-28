# Validation: source-backed arrivals after catalog visits (28 September 2026)

Status: **done.** Builds with 0 warnings; **175/175 tests** pass (1 new, 1 rewritten in `LoomActorTests`). Room audit: 0 gaps in 76 rooms, **0 changed renders**. DoorwayClickCheck **75/75** ([report](doorway-clicks.txt)); Room03ActionCheck `162 4 167 38` **46 PASS** ([captures](room03-action/)).

## The problem

A room picked from the catalog is entered without running the route into it, so the session left the ego (actor 1) unknown. Local scripts that watch the ego's walk box (`getActorWalkBox`) or position (`getActorX`) then stopped: 115 visits and actions in Blockers, across 19 rooms.

## What the session does now

`LoomEgoArrivalIndex` (`Formats/LoomEgoArrivals.cs`) collects, once per game folder, every decoded `loadRoomWithEgo` whose object and room operands are literals (walk destinations are always literal in the v3 encoding). It scans global scripts, each room's entry and exit scripts, its local scripts and its object verbs. An instruction whose object is not in the target room's directory is left out.

On a catalog visit (`LoomExplorationSession.VisitRoom` with no reached action):

1. The arrivals into the room are collected. Those in the **previous room's own code** are preferred when there are any.
2. Distinct (object, walk destination) pairs are the choices; sources that agree on the pair are listed together.
3. **One choice:** it is taken. **Several:** a seeded pick (`LoomSeededRandom`, site "catalog arrival into Room NN", so it does not shift any `getRandomNr` draw), labelled with its index, the seed and every alternative. **None:** the ego stays unknown, with the reason "no decoded loadRoomWithEgo with literal operands brings the ego into it".
4. The arrival is applied as `o5_loadRoomWithEgo` would after `startScene` (the code path reached loads already use): unless the entry code put the ego, it stands at the object's walk point; the camera is put on it and follows it; then it walks to the destination (the walk ends at once, an estimate). The ego's position is marked as an estimate: "assumed arrival at <source> after a catalog visit". Every value read from it is an estimate, and so is the camera.
5. During the entry scripts the ego's position is unknown (where the route would have left it is not known), as before.

The visit's label says what was assumed, for example: "Room 03 selected from the catalog; the route to it was not executed from game input. Assumed arrival: the ego enters as Room 02 Object 153 Verb 0x38 +0x002B (object 160, walk to (250, 68)) would bring it in (the only one in Room 02's own code). Which route led here was not executed; the arrival is a possible outcome, not a reached one."

Nothing about the source's other effects is applied: a verb that sets variables before its load (for example Room 03 object 160 sets `var[0x07C]`) is not run. Actions that reach a real load are unchanged.

## Results

`VisitProbe Games\loom <room> +8` for the 19 rooms that stopped on an unknown ego (each a fresh session from the boot, so the previous room is Room 02):

| Room | Arrival taken | Unknown-ego stops |
| --- | --- | --- |
| 07 | Room 08 object 253 verb 0x38 +0x0000 | 0 |
| 10 | Room 03 local 202 +0x0008 | 0 |
| 13 | Script 052 +0x017D or Room 12 object 322 verb 0x38 +0x0078 (same object and walk) | 0 |
| 15 | Room 23 object 418 +0x0005 | 0 |
| 16 | Room 23 object 420 +0x0005 | 0 |
| 17 | Room 16 object 345 +0x0005 (seeded pick 1 of 3) | 0 |
| 22 | Room 18 object 368 +0x0017 | 0 |
| 23 | Room 15 object 338 +0x0026 | 0 |
| 24 | Room 23 object 419 +0x0005 | 0 |
| 25 | Room 24 object 430 +0x0000 | 0 |
| 30 | Room 33 object 542 +0x0000 | 0 |
| 34 | Room 33 object 543 +0x004C | 0 |
| 36 | Room 37 object 601 +0x0005 | 0 |
| 37 | Room 38 object 623 +0x0000 | 0 |
| 41 | Room 37 object 603 +0x0000 | 0 |
| 63 | Room 05 object 203 +0x0010 | 0 |
| 32, 43, 65 | none: no literal `loadRoomWithEgo` targets them | 1 each (unchanged, labelled) |

Several arrivals come from seeded picks; their alternatives are listed in the label (the table shows seed 0).

**Blockers** ([before](blockers-before.txt), [after](blockers-after.txt); counts are visits and actions that show each stop):

| Stop or label | Before | After |
| --- | --- | --- |
| `getActorWalkBox` reads an actor whose walk box is unknown | 115 | **14** |
| `getActorWalkBox` answered from an assumed arrival (estimate) | – | 92 |
| `getActorX` reads an actor whose position is unknown | 25 | **6** |
| `getActorX` answered from an assumed arrival (estimate) | – | 19 |
| Camera unknown after `actorFollowCamera` on the ego | 72 | 14 |
| Camera follows an estimated ego position | – | 60 |
| Catalog visit with an assumed arrival | – | 34 |
| Catalog visit with no source-backed arrival | 75 | 41 |

The remaining 14 walk-box and 6 `getActorX` stops are the visits and actions in Rooms 32, 43 and 65. A few counts moved by 2–4 (sound, timing, started-script notes) because scripts now run further in those rooms.

## Tests

- `LoadRoomWithEgoPlacesTheEgoAndACatalogVisitAssumesASourceBackedArrival`: from the boot (Room 02), a catalog visit to Room 03 puts the ego at object 160's walk point and walks it to (250, 68), marked "assumed arrival at Room 02 Object 153 Verb 0x38 +0x002B"; the camera is an estimate; the label says it is the only one in Room 02's code. The Room 03 → 04 action still reaches its real `loadRoomWithEgo`.
- `CatalogArrivalsPreferThePreviousRoomAndPickAmongSeveral`: Room 17 → Room 16 picks among Room 17's own two arrivals (seeded pick, alternatives listed); Room 01 (no arrival) leaves the ego unknown.

## App

Nothing in the app changed. A catalog visit now has an estimated camera on the ego's assumed arrival, so the 320-px view of a wide room opens where the ego is assumed to arrive (the camera is shown as an estimate in "Advanced room details").
