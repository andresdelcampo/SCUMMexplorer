# Validation: findObject and the input loop with no pointer (27 September 2026)

Status: **done.** Builds with 0 warnings; **174/174 tests** pass (2 new in `LoomInputLoopTests`, 1 updated in `LoomCutsceneTests`). Room audit: 0 gaps, **0 changed renders**. DoorwayClickCheck **75/75** ([report](doorway-clicks.txt)); Room03ActionCheck `162 4 167 38` **46 PASS** ([captures](room03-action/)).

## Decoded: script 006 (the input loop)

- +0x0005–0x0077: tracks how far the virtual mouse (`var[0x014]`, `var[0x015]`) moved from the last position (`var[0x06E]`, `var[0x06F]`) and counts idle time in `var[0x00C]`.
- +0x007A: waits while `bit[0xD66]` is set (the boot clears it at +0x0236).
- +0x0082–0x0096: copies the mouse to `var[0x06C]`/`var[0x06D]` and calls `findObject local[2], var[0x06C], var[0x06D]`.
- With an object under the pointer (+0x009D–0x0121): sets `var[0x067]` (hovered object) and starts script 008 (the close-up). With a click (`local[0]`), it runs verb 0x37, walks the ego to the object, and starts script 007 with verb 0x38 or 0x36.
- With nothing under the pointer and no click (+0x0124 → +0x01AA): clears the hover if needed, then `breakHere` and loop.

## ScummVM behaviour (`object.cpp`, `script_v5.cpp`, `vars.cpp`)

- `o5_findObject` → `ScummEngine::findObject(x, y)`: the room's objects in directory order. It skips untouchable objects (class 24 in small-header games) and follows each parent chain while the parent's state matches the required state. It returns the first object whose box contains the point (x ≤ px < x + w, y ≤ py < y + h), or 0.
- The main loop writes the pointer's room position to `VAR_VIRT_MOUSE_X/Y` (vars 20 and 21) every frame. Before this change the session left them at the engine's initial 0, so the loop would have tested the point (0, 0), which lies inside Room 03's object 162, for example.

## What the session does

- **Assumed input, stated:** each frame, and on room entry, the session puts the virtual mouse just below the room picture, under the camera's centre: (camera x, room height), for example (800, 144) in Room 02 after the boot. This means there is no pointer over the room. Each `findObject` that reads this point is labelled: "findObject at +0x0096 reads the virtual mouse: assumed input: no pointer over the room, …".
- **`findObject`** is ported as above, using the session's object states, classes and moved positions. If an object's box contains the point but a parent's state is unknown, the script stops with that reason.
- `NoRoomObjectReachesTheRowBelowThePicture` checks all 76 rooms: every object box ends at or above y 144, so the assumed pointer never hits an object.

## Results

- After the boot, script 006 runs past `findObject`, finds nothing, and idles: it is one more pending script in every room, as in the game. Cutscenes now freeze it too. Script 002's `freezeScripts 0x7F` lists "Script 06", as ScummVM would, so `LoomCutsceneTests` was updated.
- **Blockers** ([before](blockers-before.txt), [after](blockers-after.txt)): `findObject` not modelled **76 → 0**; no other stop changed. 369 visits and actions carry the assumed-input label.

## Not modelled

A real pointer. The app could pass its hover position as the virtual mouse, so the loop would run verb 0x37 (hover) and script 008 (the close-up), but that needs `verbOps`, which is still a stop.
