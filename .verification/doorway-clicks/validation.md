# Validation: doorways that did not navigate, Room 22, and object classes (27 September 2026)

## Reported problem

Some room hotspots, labelled "Doorway?" or "Room NN?" on the overlay, did nothing when clicked. In Room 22, two overlapping hotspots to Rooms 17 and 18 could not be clicked.

## Measurement

[DoorwayClickCheck](DoorwayClickCheck/Program.cs) opens every room in the real `MainWindow`. For each doorway box, it clicks, through the in-process `HandleRoomPoint`, a point that no other doorway box covers, or the box centre when every point is shared. It then checks that the view entered one of that box's rooms. When a menu of rooms opens, it picks the entry for that box. No physical mouse input is used.

[Before](clicks-before.txt): **68 of 75 boxes navigated.** The 7 failures had two causes:

1. **Several possible rooms at one point** (Room 05 object 203 → 62/63, Room 07 objects 251 → 05/50 and 252 → 08/09, Room 43 object 639 → 44/45/46, and Room 22 objects 400 and 402, which share the rectangle (304,0) 16×144). The click only filled the side panel with "Open Room …" buttons, and the room did not change.
2. **Room 24 object 430**: its verb reached a load of Room 23, not the static Room 25, and the view did not follow.

## Findings for Room 22

- The Room 22 entry chooses one of three local scripts by `var[0x071]`, the object clicked to arrive, which script 006 +0x00D0/+0x014C stores: 0x0161 (Room 17 object 353) → local 210 (+0x00D9); 0x015C (Room 16 object 348) or 0x0170 → local 211 (+0x00E6/+0x00F3); anything else → local 212 (+0x00F9).
- These scripts set or clear class 24 on the room's exits. For example, local 210 +0x001E `setClass 0x0190, [0x0018]` makes 400 (→ Room 17) touchable, and +0x002C `setClass 0x0192, [0x0098]` makes 402 (→ Room 18) untouchable. Local 211 does the reverse, and local 212 (+0x002F/+0x003D) makes both untouchable.
- In small-header games, ScummVM's `getClass` maps kObjectClassUntouchable to class 24. `findObject` walks the room's objects in directory order, skips untouchable ones, and returns the first box that contains the point. So in the game, the right edge of Room 22 leads back the way you came, and exactly one of 400 and 402 is live. The entry also stopped at `debug 0x0001` (+0x009F) before any of this.

## Changes

Core, general rules:

- `LflObjectStateReader` reads each object's three class bytes from `00.LFL`, as ScummVM's small-header `readIndexFile` does, into `LoomIndexedObjectSnapshot.Classes`.
- **`setClass`** follows `putClass`: 0 clears every class, 0x80|n sets class n, and n clears it. It is applied when the object's classes are known.
- **`ifClassOfIs`** follows `o5_ifClassOfIs`: it is true unless a listed 0x80|n is missing or a listed n is present. With unknown classes it stays unresolved.
- **`debug`** is a no-op, as in `o5_debug`.
- Classes set on a path that follows a seeded pick are marked as estimates.
- `LoomSessionSnapshot.ObjectClasses` and `IsUntouchable(id)` are new. When an untouchable object's verb is run directly, the session adds an uncertainty saying the game would not select it.

App (`MainWindow`):

- **Doorways first.** In exploration mode, a click inside a doorway box that the room holds (owner 15, parent state matching) goes to the doorway, before artwork cycling. Among doorways at the point, the first touchable one in directory order takes the click, as `findObject` does.
- **Following a doorway** runs its verb, then continues pending scripts for up to 8 frames. The view enters the room whose load is reached. Only if none is reached does it open the single possible room (labelled browse), or offer the possible rooms in a menu at the cursor.
- **Untouchable doorways** stay reachable as exploration actions. The box is dimmer and its label is in italics. The tooltip says the hotspot is untouchable in the current preview, so the game would not select it. When several untouchable doorways share a point, as on Room 22's catalog visit, a menu at the cursor lists their rooms.
- **Overlay labels:** boxes with the same rectangle share one label ("Room 17 / 18"). Every "?" was removed ("Doorway?" and "Room NN?"). Labels stay inside the room image, so the 16-pixel box at Room 22's right edge is readable.
- Removed the side-panel destination list (`ShowRoomDestinationChoices`) and `FindNavigationCandidatesAt`, which no longer have callers.

## Results

- [After](clicks-after.txt): **75 of 75 boxes navigate.** Room 05 object 203 → Room 63, Room 07 objects 251 → 05 and 252 → 08, and Room 43 object 639 → 45 follow the load their script reaches, with no menu. Room 24 object 430 → Room 25. Room 22 on a catalog visit (`var[0x071]` = 0, local 212): both 400 and 402 are untouchable, and a click at their shared box offers "Room 17" and "Room 18". Each entry navigates.
- [Room 22 and Room 05 captures](window-check/window-check.txt): "Room 17 / 18" on one right-edge box, and "Room 62 / 63" in Room 05.
- Room03ActionCheck `162 4 167 38`: every check passes ([report](room03-action/window-check.txt)).
- RoomAudit ([before](room-audit-before.txt), [after](room-audit-after.txt)): **0 gaps**, and gaps before filling went from 8 to 3. The 50 visits and actions that stopped at `ifClassOfIs` no longer do ([uncertainty shapes](blockers-with-classes.txt)). Three room renders changed, and their committed PNGs were refreshed:
  - **Room 13**: the entry now runs past +0x00C0 and draws the waterspout frame 332 after 327, 330, 334 and 336. Before, a batch estimate from the entry script showed the same objects.
  - **Room 30**: entry +0x002E draws 520 as reached code. The fallback had shown 520 and the first stored image 504; 504 is gone.
  - **Room 46**: the entry sets 686 to 0 and draws 687 and 688 (reached). Before, the fallback showed 686.
  - Rooms 33 and 38 now show the same images from reached code instead of estimates (547–550 and 614).
- First-stored-image rooms: **4 → 3** (01, 04, 20).
- `.verification/room-audit/compare-png.ps1` compares every audit BMP with the committed PNG pixel by pixel and, with `-Refresh`, rewrites only the PNGs that changed.
- Tests: 137/137, 0 warnings. New `LoomObjectClassTests` (10):
  - `putClass` set, clear and clear-all;
  - `ifClassOfIs` truth table, and unknown classes;
  - index class bytes;
  - real data `Room22CatalogVisitMakesBothForestEdgeExitsUntouchable`: 400 and 402 untouchable, 411 touchable, and local 212 started.
- Updated tests: `NothingVisibleFallbackOpensOnTheStrongestScriptBatch` now uses Rooms 65 and 43, because Rooms 30 and 38 no longer need the fallback. New `EntryScriptsThatTestClassesDrawTheirRoomsThemselves` covers Rooms 30, 38 and 46.

## Limits

- Direct verb calls bypass script 006, so `var[0x071]` stays 0 on these routes. Arriving in Room 22 from Room 17 therefore still runs local 212, not local 210. Both exits show as untouchable, and the menu offers both.
- Classes are tracked per object number, and actors share that table as in ScummVM. Actor behaviour that depends on class (box ignoring, flipping) is not modelled.
- These are in-process calls and synthetic events; physical mouse and keyboard input was not tested.
