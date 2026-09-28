# LOOM EGA script map

A working reference for the Spanish DOS LOOM EGA data in `Games/loom`: what the decoded scripts do, which variables they use, and how the session models them. Offsets are into each script's code, as `CodeDump` prints them (`.verification/fallback-evidence/code.tsv`, regenerated with `dotnet Exe/CodeDump/.../CodeDump.dll Games\loom .verification\fallback-evidence`). Each entry says whether a claim is **decoded** (read straight from instructions), **ScummVM** (engine behaviour checked against `engines/scumm`), or **reading** (an interpretation that has not been verified in a run).

Last updated 28 September 2026. See also [the plan](LOOM_EGA_PLAN.md), the [boot validation](../.verification/boot/validation.md) and the [cutscene continuation validation](../.verification/cutscene-continuation/validation.md).

## Engine variables (ScummVM v3 numbering)

| Variable | Meaning | Source |
| --- | --- | --- |
| `var[0x001]` | Ego actor (`VAR_EGO`); 1 after the boot | ScummVM; script 001 +0x0184 |
| `var[0x002]` | Camera x (`VAR_CAMERA_POS_X`). The session keeps it clamped to [160, room width − 160] and sets it to 160 on room entry; `loadRoomWithEgo` puts it on the ego, and `actorFollowCamera` moves it onto the actor when the actor is outside the triggers (strips 10–30). After the boot it is 800 (Room 02, the ego at 828). Readers: script 044 +0x0361, Room 02 object 153, Room 04 local 201 and objects 169/170, Room 24 local 204, Room 34 local 200. | ScummVM; modelled 27 Sep |
| `var[0x011]`, `var[0x012]` | Camera limits (`VAR_CAMERA_MIN_X`/`MAX_X`, v3 numbering 17/18). Room entry sets them to [160, width − 160]; `roomOps roomScroll` narrows them (Room 04 locals 204/205). | ScummVM; modelled 27 Sep |
| `var[0x003]` | A message is on screen (`VAR_HAVE_MSG`). Many scripts wait while it is nonzero. | ScummVM |
| `var[0x004]` | Current room (`VAR_ROOM`) | ScummVM |
| `var[0x005]` | `VAR_OVERRIDE`: 1 when the player skipped a cutscene; `endCutscene` clears it | ScummVM |
| `var[0x014]`, `var[0x015]` | Virtual mouse x/y (`VAR_VIRT_MOUSE_X/Y`, vars 20/21), written by the engine every frame. Script 006 reads them. The session has no pointer and states one: just below the room picture, under the camera (for example (800, 144)), labelled as assumed input. The optional hover or click puts it at the explorer's point; after a click it stays there. | ScummVM; modelled 27 Sep, click 28 Sep |
| `var[0x00C]` | `VAR_TMR_2` (v3 var 12): jiffies the engine adds every frame (`scummLoop`). Script 006 resets it on each click (+0x0068) and reads it for a double click (+0x000A, at most 0x28). The session does not run the timers; a click states it (60 for a single click, 10 between the clicks of a double click), labelled. | ScummVM; decoded |
| `var[0x013]` | `VAR_TIMER_NEXT` (v3 var 19): jiffies per frame; the boot sets 6 (script 001 +0x0017), script 044 sets 0 and 6 around the intro | ScummVM; decoded |
| `var[0x020]` | `VAR_VERB_SCRIPT` (v3 var 32): the script `runInputScript` runs for every click or key. **Script 001 +0x016B sets it to 5.** Script 004 saves and restores it around setting 200 (+0x008F); Room 02's entry and script 106 set 0x73, Room 69's entry 0xC8, script 115 sets 5 again. | ScummVM; decoded 28 Sep |
| `var[0x01C]`–`var[0x01F]` | Room hooks: before entry, after entry, before exit, after exit (`VAR_ENTRY_SCRIPT`, `…2`, `VAR_EXIT_SCRIPT`, `…2`). The boot sets `var[0x01D] = 14` and `var[0x01E] = 15`. | ScummVM; script 001 |
| `var[0x023]`, `var[0x024]` | Cutscene start and end scripts (`VAR_CUTSCENE_START_SCRIPT`/`END`): 2 and 3 | ScummVM; script 001 +0x017A |
| `var[0x030]` | Sound card (`VAR_SOUNDCARD`). 0 in the session; scripts branch on 3 and 4 (AdLib, Roland) when choosing sounds. | ScummVM (reading) |
| `var[0x031]` | Video mode; `== 4` selects alternative actor palettes | reading (script 044 +0x0297) |

## Game variables and bits (decoded)

| Name | Use |
| --- | --- |
| `var[0x052..0x055]` | Room 69's random draft (four notes) for the draft check |
| `var[0x062]`, `var[0x063]` | Room 69: which note is expected next; the number of wrong tries |
| `var[0x066]`, `var[0x067]`, `var[0x068]` | The object whose close-up is shown (script 008 +0x0019); the clicked object (script 006 +0x00AB, cleared by many scripts); the close-up image object, set by the object's verb 0x39 and shown by `verbOps 0x36` (script 008) |
| `var[0x069]` | A click is recorded (script 006 +0x0077); the next click within the timer window at nearly the same point is a double click. The boot clears it (+0x023B). |
| `var[0x06A]`, `var[0x06B]` | The clicked object's walk point (script 006 `getActorX/Y` on it), or the clicked point when nothing is under it |
| `var[0x06C]`, `var[0x06D]` | The pointer point `findObject` is asked about (script 006 +0x0082–0x0091) |
| `var[0x06E]`, `var[0x06F]` | Where the last click was (script 006 +0x006D); the double-click test allows 6 px across and 3 down |
| `bit[0xD63]`, `bit[0xD64]` | Script 006 sets `bit[0xD63]` while an object's verb 0x37 runs; a verb 0x37 that sets `bit[0xD64]` stops the walk and the sentence (+0x00E4) (decoded; reading: 0x37 is the "on click" verb, for example Room 04's pans) |
| `var[0x070]` | The previous room: script 015 (before-exit hook) +0x001A stores `var[0x004]` there. Room 04's entry reads it to pick a screen half. |
| `var[0x071]` | The object the player arrived through (Room 22's entry reads it). Script 006 sets it when the clicked object has class 2 (+0x00C7, +0x0143), the class of the exit hotspots (Room 03 objects 160–163). |
| `var[0x0A2]` | Script 039 plays nothing when it is nonzero (+0x001A); reading: a music-off setting |
| `var[0x0A3]`, `var[0x0A4]` | The sound script 039 last started; the next call stops it first (+0x0000–0x0015) |
| `var[0x07B]` | Difficulty: 0 practice, 1 standard (the boot's value), 2 expert (Room 69 objects 876–878) |
| `bit[0xD62]` | Set by the boot (+0x015B). Room 03's entry starts script 42 only when it and `bit[0xD6E]` are 0. |
| `bit[0xD6B]` | Room 69 → boot handshake (local 205 +0x0048). Scripts 023 and 034 reuse it later in play. |
| `bit[0xD6E]` | "In a cutscene": script 002 sets it and script 003 clears it; the boot sets it around the opening cutscene |
| `bit[0xD66]` | Input-loop flag, cleared at the end of the boot (+0x0236); script 006 skips `findObject` while it is set |
| Class 1, class 2 | Script 006: an object with class 1 (`ifClassOfIs [0x81]`) shows its close-up on hover (script 008); class 2 marks exits (sets `var[0x071]` on a click). Decoded; for example Room 02 objects 154/156 and Room 03 object 165 have class 1, Room 03 objects 160–163 class 2. |
| `bit[0xD7A]` | Script 42's actor 2 on screen; Room 03 locals 200–202 wait while it is set |
| `bit[0xDBE]` | Room 16's first visit: its entry sets the bit and starts sound 30 through script 039 only while it is 0 (+0x0090–0x009A) |
| `bit[0xD83]`, `bit[0xD86]`, `bit[0xD89]`, `bit[0xD8A]` | Set by the boot after the intro. They decide Room 38 (612 vs 614), Room 06 (225–228 vs 221–224) and Room 62 (812 vs 814). |

## Global scripts

| Script | Role | Notes |
| --- | --- | --- |
| 001 | Boot | Loads Room 69, waits for `bit[0xD6B]`, sets the hooks, plays script 044, chains to 006. See the [boot validation](../.verification/boot/validation.md). |
| 002 | Cutscene start hook | Cursor and input off, `freezeScripts 0x7F`, `bit[0xD6E] = 1`, `var[0x005] = 0`; with argument 1, starts script 19 |
| 003 | Cutscene end hook | Cursor and input on, unfreeze, camera follows ego (+0x0018 `actorFollowCamera var[0x001]`), `bit[0xD6E] = 0`; with argument 1, starts script 18 unless `bit[0xD61]` is set |
| 005 | Input script (`VAR_VERB_SCRIPT`, set by the boot) | Runs for every click or key with `local[0]` = area, `local[1]` = verb or key, `local[2]` = button (ScummVM `runInputScript`). Area 2 (room click): +0x000C `chainScript 0x06, [1]`. Area 1 (verb area): verb 0x36 → script 007 `[var[0x066], 0x36]` after the ego's walk; 0x35 → script 009; 0x31–0x33 and the distaff notes → script 010. Area 4 (keys): notes by letter; Escape (0x1B), only outside a cutscene (`bit[0xD6E]` = 0) and when `bit[0xD66]` is 0, turns the close-up off and runs scripts 028 and 021 inside a cutscene of its own (reading: an options or pause screen, not checked); text speed; F5–F8 (0x13F–0x142): scripts 043/004, music and effects on/off, full or fast animation (`bit[0xD62]`), restart; the version line (0x16). Decoded 28 Sep. The session sends room clicks (`Click`) and verb-area clicks (`ClickVerbArea`, [validation](../.verification/verb-click/validation.md)); keys are not sent yet. |
| 006 | Input loop (runs forever) | Reads the mouse (`var[0x014]`/`var[0x015]`), `findObject` +0x0096. Hover, no click (+0x0116): a class-1 object starts script 008 with it while `var[0x067]` is 0. A click restarts it from +0x0000 with `local[0]` = 1 (script 005's `chainScript`; ScummVM `runScript` stops the running copy): +0x0005–0x005E is the double-click test (`var[0x069]`, timer `var[0x00C]` ≤ 0x28, movement against `var[0x06E]`/`var[0x06F]`), which sets `local[1]`. Then for the object under the pointer: `var[0x067]` = it (class 1) or 0, `var[0x071]` = it (class 2), verb 0x37, `walkActorToObject`, `waitFor actor`, script 007 with 0x38, and with 0x36 too on a double click. With nothing under it, the ego walks to the point; a shown close-up is turned off (+0x01AA). The session sends a click only through `Click` (the app's optional "Click as game input", 28 Sep). |
| 007 | Runs a sentence | `getDist obj, ego`: within 3, `startObject obj, verb` (and the name printed for a class-1 object that was clicked); farther, a shown close-up of it is turned off (chain to 008 with 0). Decoded; run by the session since 28 Sep. |
| 008 | Verb-area close-up | With an object: `var[0x066]` = it, `startObject obj, 0x39` (sets `var[0x068]`), `verbOps 0x36 [at 248, 152; image var[0x068]; on]`; prints the object's name when it is the clicked one and the ego is near. With 0: `verbOps 0x36 off`, `drawBox` over the area. Decoded; run by the session since 28 Sep. |
| 009 | Verb 0x35 (script 005, area 1) | Like 010's checks (`var[0x064]` = 2, an aimed object `var[0x067]`, near it), then, in practice mode (`var[0x07B]` = 0) and after a wrong try (`var[0x063]` ≠ 0), plays `var[0x063]` − 1 notes from `var[0x05A..]` through script 027 as a hint, inside a cutscene. Decoded; reading for the role. Reached by a click on verb 0x35 (`ClickVerbArea`) once script 027 has turned it on. |
| 010 | A distaff note (script 005, area 1 and keys) | `local[0]` = the note (1–8). Only when `var[0x064]` = 2 (script 018's argument). With no aimed object (`var[0x067]` = 0) it chains to script 013 ("I should aim at something first"); too far from it (`getDist` against 2), to script 012 ("I should get closer"); a note at or above `var[0x07A]` (the notes the player knows), to script 011 ("that thread is too high for me"). Otherwise, while `bit[0xD65]` is set and fewer than 5 notes are recorded, it stores the note in `var[0x056 + var[0x062]]`, starts script 020 on the first note (which plays the draft back and times it with `var[0x00B]`), and chains to script 027 with the note (marks it in the verb area; script 027 turns verb 0x35 on at (96, 184) in practice mode). Decoded 28 Sep; reading for the script roles. Reached (session, 28 Sep): after script 018 [2], a note click with nothing aimed at chains to 013; after a room click on Room 02 object 156 (class 1), note 3 is stored in `var[0x056]`, `var[0x062]` = 1, and it chains to 027. |
| 018 | Turns the distaff on | `var[0x064]` = `local[0]`; verbs 0x01–0x08 on at y 0x90 (0x98 in expert mode, `var[0x07B]` = 2), images from Room 01 objects 100–142; outside expert mode also 0x31 at (48, 168) and 0x32 at (24, 168); prints the note letters with `print 0xFE` (colours by `var[0x07A]`). Script 003 starts it with 1 unless `bit[0xD61]` is set. Decoded 28 Sep. |
| 014 | After-entry hook | Room-dependent setup; starts script 18 in some rooms |
| 015 | Before-exit hook | Clears `bit[0xD7A]`, **stops script 42**, stores the room being left in `var[0x070]` (+0x001A), clears the hover, starts scripts 008 and 028 |
| 016 | Prints a line at a variable y (`print` with an empty text) | |
| 039 (0x27), 040 (0x28) | Sound players: stop the previous sound, start the argument's sound, loop while it plays | The session reads a started sound as playing (estimate), so these stay pending. Script 039 starts the sound after one `breakHere` (+0x0021, +0x0038). The intro's sound 25 comes from script 044 +0x02C3. |
| 042 | Room 03's actor 2 (random position and time on screen, `bit[0xD7A]`) | |
| 044 | **Opening cutscene** | Rooms 01, 02, 01, 02, 53, 02; waits on the camera (+0x0361) and on local scripts (`isScriptRunning` 0xD1/0xD2); two overrides |
| 045 | Room 02 walk-box watcher | Starts a cutscene when ego enters box 4. After the boot the ego is in box 0 (the cliff top, (828, 60)), so it waits for a box change (decoded; actor model 27 Sep). |
| 052 | Room 10 object 295 → Room 11 → Room 13 cutscene | |
| 086 | Firewood scene; opens Room 58 on 792 (+0x018A) | Body behind `beginOverride` |
| 090 | Room 42 flashes (637, 638) | Transient draws; not batches |
| 099 | Draws Room 20 (392–396, +0x02BB) | |
| 108 | Glassmakers scene; opens Room 56 on 779 and 782 (+0x0188) | |
| 113, 114 | Ambient actors during the intro (random positions) | Seeded picks |

## Verbs (decoded; ScummVM `o5_verbOps`, `verbs.cpp`; modelled 28 September)

- Room 01 local 211 (the intro, on the boot route) creates the verb bar: `verbOps N, [new; at x, 144; image obj; off]` for verbs 0x01–0x35 (images from Room 01 objects 100–151; verbs 0x01–0x08 at x 16–192 are the distaff notes, the others share those positions) and `new` for verb 0x36, the close-up slot, later put at (248, 152).
- Scripts 018 (turns verbs 0x01–0x08 on at `local[4]`'s y, and 0x31/0x32), 019 (turns them off), 027/028 (verb 0x35), 031 and 008 change them later.
- ScummVM facts the session follows: `getVerbSlot` returns 0 for a verb with no slot, and `image` is ignored there; `setVerbObject` takes the image only when the object is one of the current room's objects.
- The verb area is screen rows 144–199 (ScummVM `initScreens(16, 144)` for v3). `drawVerbBitmap` draws an image verb that is on or dim at `x / 8` strips, `height / 8` strips down, and sets its `curRect` to the verb's x, y and that size; `findVerbAtPos` takes the last slot in mode 1 whose `curRect` holds the point. After the boot every verb is off. The app's optional "Show verb area" draws the slots (presentation, [validation](../.verification/verb-area/validation.md)).

## Walk boxes (decoded, ScummVM `room.cpp`/`boxes.cpp`)

- Table at the word at room + 0x15: a count byte, 18-byte boxes, then the matrix up to the image (+0x0A). All 76 rooms fit exactly.
- LOOM's walkable areas are mostly line segments. Room 02 is a chain of 15 (box 4 is what script 045 watches). Room 17 has one quadrangle (box 8). Close-ups have one box outside the image.
- Flags: 0x80 invisible (Room 05, 0x81); 0xA0 invisible and player-only (Rooms 09 and 34).

## Actors (ScummVM `actor.cpp`; modelled 27 September)

- 12 actors (1–12). Actor 1 is the ego (`var[0x001]`). After the boot (opening cutscene) the ego stands in Room 02 at (828, 60), box 0, under the tree (decoded run). Actors 10 and 11 are put over the sea at (137, 42) with `ignoreBoxes` (box 255) after seeded picks (reading: the swans).
- `loadRoomWithEgo obj, room, walkTo=(x, y)` (v3): the ego stands at the object's walk point unless the entry code put it, then walks to (x, y) when x ≠ −1 (ScummVM). Room 03 local 200 +0x0008 brings the ego into Room 04 and walks it to (580, 132), box 0 (decoded; the walk end is an estimate).
- Script 001 +0x0233 `actorFollowCamera var[0x001]` ends the boot with the camera on the ego: x 800 in Room 02 (decoded run; ScummVM `setCameraFollows`).
- `getActorX`/`getActorY` with a number of 13 or more read an object's walk point (ScummVM `getObjX`).
- **Arrivals (decoded, 28 Sep):** 80 `loadRoomWithEgo` sites have literal object and room operands; they bring the ego into 35 rooms. Most are object verbs 0x38 in the neighbouring room (for example Room 02 object 153 → Room 03 at object 160, walk to (250, 68)); a few are local scripts (Room 03 locals 200–202, Room 25 local 208, Room 35 local 210) and global scripts (052 → Room 13, 089 → Room 07, 096 → Room 46). No `loadRoomWithEgo` has a variable or local operand. A catalog visit takes one of them as a labelled assumed arrival: the previous room's own first, a seeded pick among several ([validation](../.verification/catalog-arrivals/validation.md)).
- **`loadRoom` arrivals (decoded, 28 Sep):** Rooms 12, 28, 31, 32, 43, 49, 65 and 66 have no `loadRoomWithEgo` into them. Global scripts enter them with `loadRoom R`, then (after the entry code) `putActorInRoom 1, R` and a literal `putActor` or `putActorAtObject`, often a walk, with a `setCameraAt` just after the load: Room 65 from script 072 +0x0024 (object 827, walk to (228, 44)); Room 32 from scripts 079 +0x001C and 080 +0x0026; Room 43 from scripts 091 +0x0A50 and 092 +0x0005 ((142, 93)); Room 12 from 053, 28 from 077, 31 from 082/084, 49 from 110, 66 from 102. Scripts 090, 093 and 094 load Room 43 as dialogue returns without putting the ego in straight-line code. Many rooms with doorways have such cutscene arrivals too (for example Rooms 08, 09, 30, 38). A catalog visit uses a `loadRoom` arrival only when the room has no `loadRoomWithEgo` one; the camera stays at the script's `setCameraAt` (ScummVM `startScene` leaves the camera in normal mode) ([validation](../.verification/loadroom-arrivals/validation.md)).

## Room notes

- **Room 69: intro screen.** The entry starts local 202 (difficulty menu). Objects 876–878 pick the difficulty and start local 201 (the random draft and guild symbol), which chains to 204 (the question). Note objects 886–897 start local 203 [note index]. Local 203 checks the draft and starts 205 after a correct draft or the third wrong try. Local 205 sets `bit[0xD6B]`.
- **Room 03: lake.** Objects 161–164 reach Rooms 05, 04, 10 through locals 200–202, which wait while `bit[0xD7A]` is set. Object 160 leads to Room 02 directly.
- **Room 10.** Object 295 starts script 052 (a cutscene through Room 11 into Room 13).
- **Room 04.** Two screens. Outside a cutscene, the entry (+0x0013–0x003F) picks a half by the previous room (`var[0x070]`; 0x46 also reads `var[0x0AC]`, 0x40 is Room 64). Local 204 does `roomScroll 152, 160` and `setCameraAt 160` (left half); local 205 does `roomScroll 480, 488` and `setCameraAt 480` (right half). The camera is locked to that half (decoded; `roomScroll` modelled 27 Sep). Objects 169/170 (verb 0x37) pan between the halves.
- **Room 16.** Its entry starts script 039 with sound 30 on the first visit (`bit[0xDBE]`, decoded); the sound starts one frame later.
- **Room 22.** Its entry picks local 210/211/212 by `var[0x071]`, which makes exit 400 or 402 untouchable (class 24).

## How the session maps ScummVM behaviour

| ScummVM | Session | Where |
| --- | --- | --- |
| `startScene` ends room, object and local scripts; global scripts return from `loadRoom` | A global script pauses after the load (`PauseAfterEvent`), the session enters the room, and the script resumes there | `LoomExplorationSession.DispatchRequestedLoads` |
| `beginCutscene`/`endCutscene`, cutscene stack | `BeginCutscene`/`EndCutscene`, `_cutscenes`, `InCutscene` | session |
| `beginOverride` records the skip jump and steps over it | The disassembler decodes past the jump; the executor steps over it; the preview never skips | `LoomScriptReader.Disassemble`, executor |
| `freezeScripts` | Per-slot `ScriptFrame.FreezeCount`, freeze-resistant starts (opcode bit 0x20) | session |
| `chainScript` | `StopAfterEvent`, then the target starts in the same place | session `RunScript` |
| `isScriptRunning` | Executing, pending or frozen global/local scripts; estimate if the script ended at an unmodelled step | `IsScriptRunning` |
| `isSoundRunning` | Sounds started on the route read as playing (estimate) | executor |
| `delay`, `waitFor` actor/message/camera | Continue on the next frame (labelled timing) | executor |
| Camera | `var[0x002]`, pans arrive at once; `loadRoomWithEgo` and `actorFollowCamera` follow `setCameraFollows`'s triggers; the camera follows a walk's end at once (estimate) | executor, session `PlaceEgoAfterEntry`, `CameraRange` |
| The route into a room | A catalog visit does not run it; a decoded literal `loadRoomWithEgo` into the room (the previous room's first, else a seeded pick) is applied as an assumed arrival, an estimate; with none, a `loadRoom` followed by putting the ego in the room | `LoomEgoArrivalIndex`, session `PickCatalogArrival`, `PlaceEgoAfterLoadRoom` |
| `getRandomNr` | Seeded pick, labelled; later steps are estimates | executor |
| `verbOps` | Verb slots as state (`LoomVerbTable`); the app shows the close-up (verb 0x36) when the pointer or click is on, and draws all slots in the optional verb strip (presentation) | executor, `LoomVerbs.cs`, `LoomVerbArea` |
| `startObject` (`runObjectScript`) | Runs the object's verb (or fallback) at once with the arguments; held objects are labelled | session `StartObjectScript` |
| The pointer | No pointer over the room (assumed), or the app's hover when "Pointer as game input" is on | session `SetPointer`, `PlaceVirtualMouse` |
| `checkExecVerbs` → `runInputScript(2, 0, 0)` for a room click (v3 passes 0 as the third argument: `_game.version > 3 ? code : 0`, ScummVM) | `Click(x, y, doubleClick)` runs `var[0x020]`'s script with `[2, 0, 0]`, with the point and the timer (`var[0x00C]`) stated as assumed input; refused while `_userPut` ≤ 0 | session `Click`, app "Click as game input" |
| `checkExecVerbs` → `findVerbAtPos`, `runInputScript(1, verb, 0)` for a verb-area click (`[1, 0, 0]` off every verb) | `ClickVerbArea(x, y)` (screen point, rows 144–199) finds the verb over the session's verb slots (`LoomVerbArea.FindVerbAt`), sets `VAR_MOUSE_X/Y` (`var[0x02C]`/`var[0x02D]`, ScummVM v3 vars 44/45) as assumed input and runs `var[0x020]`'s script; refused while `_userPut` ≤ 0. Verbs it cannot draw cannot be found | session `ClickVerbArea`, `VerbArea`; app verb strip with "Click as game input" |
| `runScript` stops a running copy unless recursive | A started script (opcode bit 0x40 clear), a chained one (the slot's flag) or a hook ends the pending copies of the same number first | session `RunStartedScript` |
| `cursorCommand` user input (`_userPut`) | Kept as a number; clicks are refused at 0 or below | executor, `LoomScriptRuntimeState.UserPut` |
| `getObjActToObjActDist` (`getDist`) | Actor and object positions, 0xFF outside the room, the object's point adjusted into the actor's boxes | executor `TryMeasureDistance` |
| Walks | End at once (estimate); after `waitFor actor` the end is no longer an estimate; when every box routes to the end box, an estimate about the start does not carry to the end | `LoomActorTable.Walk`, `WalkWaited` |
| Sound commands (music row) | Each command keeps the room whose code began its script chain (`ScriptFrame.OriginRoom`). The room view shows cues started since entry, or started by the room itself and not stopped. | session `SoundEvents`, `LoomSoundCueResolver.ResolveForRoom` |

## Not modelled yet (by frequency in Blockers)

1. Actor queries are answered from the actor table ([validation](../.verification/actor-positions/validation.md)); they stop only when the actor is unknown. After a catalog visit the ego takes a source-backed assumed arrival (`loadRoomWithEgo`, else `loadRoom` and a put), so no visit stops on an unknown ego ([validation](../.verification/catalog-arrivals/validation.md), [loadRoom arrivals](../.verification/loadroom-arrivals/validation.md)). `getActorCostume`, `getActorElevation`, walk timing and paths, and step-by-step camera movement are not modelled.
2. Input beyond a room click: room clicks go through scripts 005, 006 and 007 when sent ([validation](../.verification/click-input/validation.md)), and verb-area clicks (area 1: the distaff, the close-up icon) through scripts 005 and 010 ([validation](../.verification/verb-click/validation.md)), but keys (area 4) are not sent, and the engine timers are not run.
3. Inventory object code and `stopObjectScript`.
4. `startScript` with a dynamic target (a variable that is not known).
5. `expression` in some forms.
