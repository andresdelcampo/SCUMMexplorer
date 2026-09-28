# Validation: the pointer as game input, verbOps and startObject (28 September 2026)

Status: **done.** Builds with 0 warnings; **180/180 tests** pass (5 new in `LoomPointerInputTests`). Room audit: 0 gaps in 76 rooms, **0 changed renders**, report unchanged. [PointerInputCheck](PointerInputCheck/Program.cs) **13/13** ([report](pointer-check/pointer-input-check.txt)). DoorwayClickCheck **75/75** ([report](doorway-clicks.txt)); Room03ActionCheck `162 4 167 38` **46 PASS** ([report](room03-action/window-check.txt)); MusicRowCheck **5/5** ([report](music-row/)); CameraPanCheck **11/11** ([report](pan-check/camera-pan-check.txt)); ReverseHoverCheck **22/22** ([report](reverse-hover/reverse-hover-check.txt)); HighlightCheck **19/19** ([report](highlight/highlight-check.txt)); PresentationCheck **39/39** ([report](presentation-check/geometry-check.txt)). No physical input.

## What stopped the hover path (decoded)

Script 006 with the pointer over an object and no click (`local[0]` = 0):

- +0x0096 `findObject local[2]` at the virtual mouse; +0x009D `ifClassOfIs local[2], [0x81]`: the object has class 1.
- +0x00A6 → +0x0116: no click. When `var[0x067]` is 0, +0x011B `startScript 0x08, [local[2]]`.
- Without class 1, or with nothing under the pointer: +0x01AA starts script 008 with 0 when a close-up is shown (`var[0x066]` ≠ 0).

Script 008: `var[0x066]` = the object; +0x0023 `startObject local[0], 0x39`; each object's verb 0x39 only sets `var[0x068]` to its close-up image object (for example Room 02 object 156 → 159, Room 03 object 165 → 166). +0x002D `verbOps 0x36, [at, 248, 152; image, var[0x068]; on]`. With 0 it turns verb 0x36 off and clears the area (`drawBox`).

Three things stopped it: `verbOps` was not modelled (127 Blockers stops), started scripts could not receive a `local[…]` argument (`startScript 0x08, [local[0x002]]`), and `startObject` never ran. A fourth bug turned up in Room 03: a resumed global script kept the object list of the room it was pended in, so script 006 searched Room 02's objects in Room 03.

## What changed

- **`verbOps`** (`Formats/LoomVerbs.cs`, `LoomVerbTable`), after ScummVM `o5_verbOps`, `getVerbSlot`, `killVerb` and `setVerbObject` (old bundle): 100 slots; `getVerbSlot` returns 0 (a scratch slot, never drawn) for a verb without one; `new` takes the first free slot and resets it; `image`/`assignObject` apply only on a real slot and only when the object is one of the current room's objects (otherwise the old image stays); `name`, `at`, `on`, `off`, `dim`, `delete`. Colours, key and centring are not kept. An unreadable verb number makes every slot unknown. The session exposes `Verbs`.
- **Arguments:** known, non-estimated `local[…]` arguments of `startScript`, `chainScript` and `startObject` are passed as values. The trailing argument list is found by matching brackets (`ArgumentListStart`), so a `local[…]` operand before it is not mistaken for it (this also fixed `verbOps local[0x000], [off]`).
- **`startObject`** (ScummVM `o5_startObject` → `runObjectScript`): object 0 or an object not in the room does nothing (a held object is labelled: inventory code is not modelled); no entry and no fallback does nothing (`getVerbEntrypoint` 0); otherwise the verb's code runs at once with the arguments as locals. `stopObjectScript` is not modelled.
- **Resumed scripts** use the current room's objects (ScummVM `_objs`).
- **Pointer:** `LoomExplorationSession.SetPointer(point)` puts the virtual mouse at a room point from the next frame, labelled "assumed input: the pointer rests over the room at (x, y), placed by the explorer's hover, with no click". `null`, a point outside the picture, or entering another room restores the "no pointer over the room" assumption.
- **App:** "Pointer as game input" (off by default; the tooltip says it is assumed input and that no click is sent). In interactive mode, hovering the room gives the session the pointer; when the object under it changes, one frame runs, as Continue does. A small verb-area panel under the status shows verb 0x36's close-up (image, verb, the hovered object, "no click was sent") while it is on. Leaving the room or turning the setting off takes the pointer away.

## Results

`VisitProbe Games\loom verbs`: the boot's Room 01 local 211 builds the verb bar (verbs 0x01–0x35 as image verbs from Room 01 objects 100–151, off) and verb 0x36 (the close-up slot, empty).

| Probe | Result |
| --- | --- |
| `@700,40 +1` (Room 02) | script 008 for object 156; `var[0x068]` = 159; verb 0x36: object 159's image (Room 02) at (248, 152), on |
| then `@off +1` | `var[0x066]` = 0; verb 0x36 off; `drawBox` clears the area (presentation) |
| `3 @128,16 +1` | object 165 (the sky); verb 0x36: object 166 (Room 03), on |

PointerInputCheck (real MainWindow): off by default and inert; on, Room 02 object 156 shows a 48×40 close-up; moving within the object runs no new frame; leaving turns it off and restores the "no pointer" label; Room 03 object 165 shows object 166; hovering the Room 03 doorway (object 162, class 2) sends no click and the room stays; turning it off hides the panel. Captures: [Room 02](pointer-check/room-02-hover-156.png), [Room 03](pointer-check/room-03-hover-165.png).

**Blockers** ([before](blockers-before.txt), [after](blockers-after.txt)):

| Stop or label | Before | After |
| --- | --- | --- |
| `verbOps` not modelled | 127 | **0** |
| Started script with arguments from unknown variables | 68 | **13** |
| Started object script with an unresolved entrypoint | 1 | 0 |
| Sound started (reached) | 298 | 343 |
| `isSoundRunning` reads a sound as silent | 214 | 260 |

Scripts now run further: the extra sound commands come from started scripts that receive local arguments (script 039, the sound player). The audit's renders and report did not change.

## Not modelled

A click (`local[0]` of script 006, verb 0x37 and script 007's sentence), the verb area's drawing in the room view (only the close-up is shown, in the panel), inventory object code, `stopObjectScript`.
