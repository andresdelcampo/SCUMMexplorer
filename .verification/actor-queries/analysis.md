# Analysis: actor queries (`getActorWalkBox`, `getActorX`, `getActorY`), deferred (27 September 2026)

Task 3 of the 27 September session asked for `getActorWalkBox`/`getActorX` "only if actor state can be followed without inventing it". It cannot yet, so no code was changed. This note records why and what would be needed.

## Where they occur

180 decoded sites (from `code.tsv`). The most common:

| Instruction | Sites | Reading |
| --- | --- | --- |
| `getActorWalkBox local[0x000], 0x01` | 21 | the ego's current walk box |
| `getActorWalkBox local[0x001], 0x01` | 17 | the same, compared with the previous frame |
| `getActorX local[..], 0x0005` / `0x0001` / `0x0004` | 13 / 23 / 4 | actor x (5 and 4 are cutscene actors) |
| `getActorY …` | about 20 | actor y |

By source: Room 24 local 216 (8), Room 23 local 230 (8), Room 09 locals 224/228/229 (14), script 106 (6), Room 20 locals 204/207 (7), Room 10 local 200 (4), Room 34 local 202 (4), script 006 (4), script 068 (4). Blockers counts after the boot work: `getActorWalkBox` 184 visits/actions, `getActorX` 71.

The typical pattern is a **walk-box watcher**, for example script 045 (Room 02):

```
+0x0000 breakHere
+0x0001 getActorWalkBox local[0x000], 0x01
+0x0005 breakHere
+0x0006 getActorWalkBox local[0x001], 0x01
+0x000A isNotEqual local[0x001], local[0x000] -> 0x0005   ; wait until the ego changes box
+0x0016 isEqual local[0x000], 0x0004 ...                   ; box 4 → a cutscene
```

It reacts to the player walking the ego from box to box. That is player input.

## What an honest model needs

1. **Walk boxes.** A BOXD reader for old-v3 rooms. The format must be checked against ScummVM (`ScummEngine::getBoxCoordinates`, `Box` in `boxes.cpp`): the small-header layout is a count byte, then boxes of `int16 ulx, uly, urx, ury, lrx, lry, llx, lly; byte mask; byte flags` (18 bytes). Loom EGA may use a different old layout (`GF_OLD_BUNDLE`); verify before relying on it, for example by drawing the boxes over rooms in the audit and comparing with ScummVM's debugger (`boxes` command).
2. **Actor positions.** `putActor x, y` and `putActorInRoom` give exact positions. `putActorAtObject` and `loadRoomWithEgo obj, room, walkTo=(x,y)` use the object's walk point (`LoomRoomObjectRecord.WalkX/WalkY`, already decoded), then start a walk.
3. **Walking.** `walkActorTo` and the `walkTo` of `loadRoomWithEgo` end at the destination only when it lies in a reachable box; ScummVM adjusts it to the closest box point (`adjustXYToBeInBox`) and paths through box matrices. Without boxes, a walk's end point is an estimate.
4. **Input.** The watchers only change their answer when the player walks the ego. With no input, the honest answer is "the ego stays in the box it stands in", which needs 1–3.

## Proposed next step

Build the BOXD reader first, as its own verified milestone. Draw the boxes in the "Show depth areas" overlay, or a new "walk boxes" overlay, and check a few rooms against ScummVM. Then track actor positions only from `putActor`/`putActorAtObject`/`loadRoomWithEgo` (exact or walk-point), and answer `getActorWalkBox` and `getActorX/Y` from them. Answers after a walk should be labelled estimates.
