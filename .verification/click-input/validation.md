# Validation: a click as game input (28 September 2026)

Status: **done.** Builds with 0 warnings; **185/185 tests** pass (5 new in `LoomClickInputTests`). Room audit: 0 gaps in 76 rooms, **0 changed renders**; the report changes only in four rooms' pending counts (below). [ClickInputCheck](ClickInputCheck/Program.cs) **11/11** ([report](click-check/click-input-check.txt)). Rerun after the app change ([reports](wpf/)): DoorwayClickCheck **75/75**, Room03ActionCheck `162 4 167 38` **46 PASS**, PointerInputCheck **13/13**, ReverseHoverCheck **22/22**, HighlightCheck **19/19**, MusicRowCheck **5/5**, CameraPanCheck **11/11**, PresentationCheck **39/39**. No physical input.

## How this release gives script 006 a click (decoded and ScummVM)

- ScummVM `checkExecVerbs`: a click in the room calls `runInputScript(kSceneClickArea = 2, 0, code)` (`code` 1 for the left button), which runs the script in `VAR_VERB_SCRIPT` with those three values as `local[0..2]` (`runScript(verbScript, 0, 0, args)`, not recursive). Clicks are read only while `_userPut > 0` (`cursorCommand userInputOn`).
- `VAR_VERB_SCRIPT` is var 32 in v3 (`vars.cpp`). **Script 001 +0x016B sets `var[0x020]` = 5**, and +0x0247 turns user input on (decoded).
- **Script 005** (decoded): +0x0005 `isEqual local[0], 2` → +0x000C `chainScript 0x06, [0x0001]`. A room click chains into the input loop with `local[0]` = 1. Area 1 (verb clicks) handles verbs 0x36 (script 007 with the close-up object), 0x35, 0x31–0x33 and the distaff notes (script 010); area 4 (keys) handles the notes by key, Escape (skip a cutscene), text speed, music, restart and the version line.
- `chainScript` → `runScript(6, …)`, not recursive, so the running copy of script 006 is **stopped** and a fresh one starts at +0x0000 (ScummVM `runScript` calls `stopScript` first).
- **Script 006 from the top** (decoded): +0x0005–0x005E is the double-click test: when `var[0x069]` (a click was recorded) is set, the timer `var[0x00C]` (`VAR_TMR_2`, jiffies the engine adds each frame) is at most 0x28, and the pointer moved at most 6 px across and 3 down from the last click (`var[0x06E]`/`var[0x06F]`), `local[1]` becomes 1 (a double click). Otherwise +0x0068 records this click (timer 0, position, `var[0x069]` = 1). Then `findObject`; for any object under the pointer: class 2 sets `var[0x071]`; `bit[0xD63]` = 1, `bit[0xD64]` = 0; `startObject obj, 0x37` (an object's 0x37 can set `bit[0xD64]` to stop the walk); `walkActorToObject ego, obj`; `waitFor actor`; `startScript 7, [obj, 0x38]`; on a double click also `startScript 7, [obj, 0x36]`. A class-1 object first starts script 008 (its close-up). With nothing under the pointer, the ego walks to the point.
- **Script 007** (decoded): `getDist local[2], ego, obj`; within 3 it runs `startObject obj, verb` (and prints the object's name for a class-1 object that is the clicked one); farther away, it turns a shown close-up off.

## What the session models now

- **`LoomExplorationSession.Click(x, y, doubleClick)`**: refuses (labelled) outside the picture, while `_userPut` ≤ 0, or when `var[0x020]` is 0. Otherwise it puts the pointer at the point, states the timing (`var[0x00C]` = 60 jiffies for a single click, past the 40-jiffy window; 10 between the two clicks of a double click), and runs `var[0x020]`'s script with `[2, 0, 1]`. Every part is labelled as assumed input. The pointer stays at the point afterwards. The result carries `Input` (`IsGameInput`), and a room load it reaches (now or in later frames) is recorded with it.
- **`runScript` stops a running copy** when the start is not recursive (`startScript` without opcode bit 0x40, `chainScript` from a non-recursive slot, cutscene hooks, the click). Before, a restarted script ran twice.
- **`getDist`** after ScummVM `getObjActToObjActDist` (actor/object positions, 0xFF when not in the room, the object's point adjusted into the actor's boxes).
- **`cursorCommand`** keeps `_userPut` (on/off/soft on/soft off).
- **Walks:** after `waitFor actor`, a walk's end is no longer an estimate (only its duration is not simulated). When every box of the room routes to the end box, the end does not depend on where the walk started, so an estimate about the start (an assumed catalog arrival) no longer carries to the end. An arrival's own walk keeps its arrival label.

## Results

`VisitProbe Games\loom <steps>` (new steps: `!x,y` click, `!!x,y` double click, `objects`):

| Probe | Result |
|---|---|
| `!450,60 +8` (Room 02 after the boot, object 153, class 2) | 005 → 006: `var[0x071]` = 153, object 153 has no 0x37, the ego walks to (540, 108); next frame 007: distance 0, verb 0x38: cutscene, pan, `loadRoomWithEgo` → **Room 03 reached** |
| `3 !24,24 +8` (catalog visit to Room 03, object 162) | the ego walks to (41, 63) (every Room 03 box reaches box 0); 007 runs verb 0x38 → local 200 → **Room 04 reached** |
| `!!700,40 +6` (Room 02 object 156, class 1) | first click: close-up (script 008); second click (10 jiffies, same point): `local[1]` = 1; 007: no verb 0x38, then **verb 0x36 runs**; `var[0x067]` = 156, verb 0x36 on with image 159 |

Compared with the doorway click (a direct call of the object's verb, an exploration action): the click goes through the input scripts, so the object's class, its verb 0x37, the walk, the distance test and script 007's choice of sentence all come from the game's code. The destination is the same for these exits.

**Blockers** ([before](blockers-before.txt), [after](blockers-after.txt)): no stop was added. Changes: the "presentation operation not applied: cursorCommand" notes are gone (modelled); `isSoundRunning` reads a sound as silent 260 → 226 (sound-player scripts are no longer started twice); walk-end estimates read by `getActorWalkBox` 6 → 0 and camera-follows-an-estimate 55 → 47 (walks settle at `waitFor actor`).

**Room audit:** 0 gaps, 0 changed renders. Rooms 16, 24, 30 and 36 list one pending script fewer: a script started again no longer leaves its earlier copy running.

## App

"Click as game input" (off by default; its tooltip says it is assumed input). In interactive mode a click on the room is sent through `Click`; a single click waits for the system double-click time, and a second press within it is sent as a double click. Frames then continue as for a doorway click (up to 32, more in a cutscene). The action note says "an assumed click went through the game's input scripts (005, 006, 007)", and the verb-area close-up shows while either input setting is on. Off, clicks keep the direct verb call.

ClickInputCheck (real MainWindow): off by default; a click on Room 02 object 153 reaches Room 03 through 005 → 006 → 007 → verb 0x38, with the note; a queued click waits, then reaches Room 04 through object 162 and local 200; a second press while a click waits is a double click and runs object 156's verb 0x36, with the close-up; off again, the doorway click is the direct verb call. Captures: [Room 03](click-check/room-03-after-click.png), [Room 04](click-check/room-04-after-click.png), [Room 02 double click](click-check/room-02-double-click-156.png).

## Not modelled

Clicks in the verb area (area 1: the distaff notes, the close-up icon) and keys (area 4); the engine timers (`VAR_TMR_*`), so click timing is stated; walk duration and paths; inventory object code; `stopObjectScript`.
