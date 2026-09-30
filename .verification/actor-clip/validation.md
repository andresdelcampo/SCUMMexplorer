# Actor clip classes (user report B, 30 September)

The user's report: in Room 43, "the sheep show partly hidden behind the background".

## Cause (ScummVM and decoded)

- ScummVM `drawActorCostume` (actor.cpp, versions before 7): `if (_forceClip) _zbuf = _forceClip; else if (isInClass(kObjectClassNeverClip)) _zbuf = 0; else _zbuf = getMaskFromBox(_walkbox)`, clamped to `_numZBuffer - 1`.
- `putClass` (object.cpp) calls `Actor::classChanged` for v4 and older when the object number is an actor: class 21 (`kObjectClassAlwaysClip`) sets `_forceClip`, class 22 sets `_ignoreBoxes`. Class 20 (`kObjectClassNeverClip`) is read from the class data. Small-header games do not translate classes 20–22.
- LOOM's scripts set one of the two on most actors they place: 363 `setClass` sites give an actor `0x0094` (set class 20, 228 sites) or `0x0095` (set class 21, 135), usually with `0x0096` (ignore boxes). Room 43's entry: `setClass local[0x000], [0x0000, 0x0096, 0x0094]` for actors 5–9 (costume 116, animated by locals 206–209; reading: the flames in the torches) and the same for actor 11.
- An ignore-boxes actor has box 255, whose mask reads as 1 (`getMaskFromBox`, ScummVM's workaround), so the app drew them behind the room's depth mask: partly hidden. The app ignored the classes.

## Change

`LoomActorRenderer.Render` takes the actors' class bits (the session's live `ObjectClasses`) and applies ScummVM's order: class 21 → behind plane 1 whatever the box; class 20 → in front of every mask; otherwise the walk box's mask. `UpdateActorLayer` passes them.

## What the user now sees

Room 43's five flames (actors 5–9) are drawn whole. Before: 73, 89, 18, 130 and 24 of their pixels were shown; after: 132, 132, 69, 132 and 132 (`room43-actors.txt`, frame 60 after a catalog visit). `room43-before-after.png` shows rows 20–59, x 70–269, before and after. The same applies wherever a script sets class 20 (Room 02's swans and leaves, Room 03's campfires, Room 41's actor 2, ...), and class 21 actors are now masked even in an unmasked box (Room 08's elders, Room 43's actor 4 in the left cage: unchanged there, as box 255 already read as 1).

The report said "sheep"; no sheep costume is in Room 43. If the user meant something else in that room, ask.

Not explained by this: the minor Room 02 note (Bobbin hidden a few pixels too early near the mountain side). The ego has neither class there (probe: classes 0 in Room 02 after the boot and after visits), so its mask still comes from its walk box.

## Verification

- Unit test `NeverClipActorsSuchAsRoom43sSheepAreDrawnInFrontOfTheDepthMask` (Room 43 after a catalog visit: at least 2 never-clip actors, drawn at z 0 with the classes and fully hidden by a full mask without them; class 21 wins over class 20 and over box 0). `LoomActorRendererTests`: 6/6.
- ActorDrawCheck: 0 failures (`actor-draw-check.txt`).
- WindowCheck Rooms 43 and 02: `room43-window.png`.
- Skipped: RoomAudit (room images unchanged: only the actor layer), DoorwayClickCheck (no click or room-load change), the playback checks.
