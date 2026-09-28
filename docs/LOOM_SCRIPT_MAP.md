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
| `var[0x014]`, `var[0x015]` | Virtual mouse x/y (`VAR_VIRT_MOUSE_X/Y`, vars 20/21), written by the engine every frame. Script 006 reads them. The session has no pointer and states one: just below the room picture, under the camera (for example (800, 144)), labelled as assumed input. | ScummVM; modelled 27 Sep |
| `var[0x01C]`–`var[0x01F]` | Room hooks: before entry, after entry, before exit, after exit (`VAR_ENTRY_SCRIPT`, `…2`, `VAR_EXIT_SCRIPT`, `…2`). The boot sets `var[0x01D] = 14` and `var[0x01E] = 15`. | ScummVM; script 001 |
| `var[0x023]`, `var[0x024]` | Cutscene start and end scripts (`VAR_CUTSCENE_START_SCRIPT`/`END`): 2 and 3 | ScummVM; script 001 +0x017A |
| `var[0x030]` | Sound card (`VAR_SOUNDCARD`). 0 in the session; scripts branch on 3 and 4 (AdLib, Roland) when choosing sounds. | ScummVM (reading) |
| `var[0x031]` | Video mode; `== 4` selects alternative actor palettes | reading (script 044 +0x0297) |

## Game variables and bits (decoded)

| Name | Use |
| --- | --- |
| `var[0x052..0x055]` | Room 69's random draft (four notes) for the draft check |
| `var[0x062]`, `var[0x063]` | Room 69: which note is expected next; the number of wrong tries |
| `var[0x066]`, `var[0x067]`, `var[0x068]` | Selected object; hovered object; the close-up image `verbOps` shows in the verb area (script 008) |
| `var[0x069]` | Input-loop state (script 006) |
| `var[0x070]` | The previous room: script 015 (before-exit hook) +0x001A stores `var[0x004]` there. Room 04's entry reads it to pick a screen half. |
| `var[0x071]` | The object the player arrived through (Room 22's entry reads it) |
| `var[0x0A2]` | Script 039 plays nothing when it is nonzero (+0x001A); reading: a music-off setting |
| `var[0x0A3]`, `var[0x0A4]` | The sound script 039 last started; the next call stops it first (+0x0000–0x0015) |
| `var[0x07B]` | Difficulty: 0 practice, 1 standard (the boot's value), 2 expert (Room 69 objects 876–878) |
| `bit[0xD62]` | Set by the boot (+0x015B). Room 03's entry starts script 42 only when it and `bit[0xD6E]` are 0. |
| `bit[0xD6B]` | Room 69 → boot handshake (local 205 +0x0048). Scripts 023 and 034 reuse it later in play. |
| `bit[0xD6E]` | "In a cutscene": script 002 sets it and script 003 clears it; the boot sets it around the opening cutscene |
| `bit[0xD66]` | Input-loop flag, cleared at the end of the boot (+0x0236) |
| `bit[0xD7A]` | Script 42's actor 2 on screen; Room 03 locals 200–202 wait while it is set |
| `bit[0xDBE]` | Room 16's first visit: its entry sets the bit and starts sound 30 through script 039 only while it is 0 (+0x0090–0x009A) |
| `bit[0xD83]`, `bit[0xD86]`, `bit[0xD89]`, `bit[0xD8A]` | Set by the boot after the intro. They decide Room 38 (612 vs 614), Room 06 (225–228 vs 221–224) and Room 62 (812 vs 814). |

## Global scripts

| Script | Role | Notes |
| --- | --- | --- |
| 001 | Boot | Loads Room 69, waits for `bit[0xD6B]`, sets the hooks, plays script 044, chains to 006. See the [boot validation](../.verification/boot/validation.md). |
| 002 | Cutscene start hook | Cursor and input off, `freezeScripts 0x7F`, `bit[0xD6E] = 1`, `var[0x005] = 0`; with argument 1, starts script 19 |
| 003 | Cutscene end hook | Cursor and input on, unfreeze, camera follows ego (+0x0018 `actorFollowCamera var[0x001]`), `bit[0xD6E] = 0`; with argument 1, starts script 18 unless `bit[0xD61]` is set |
| 006 | Input loop (runs forever) | Reads the mouse (`var[0x014]`/`var[0x015]`, movement against `var[0x06E]`/`var[0x06F]`, idle count `var[0x00C]`), `findObject` +0x0096. When an object is found, it sets the hover (`var[0x067]`) and starts script 008; on a click (`local[0]`), it runs verb 0x37, walks the ego and starts script 007 (verb 0x38 or 0x36). With nothing under the pointer it idles (+0x01AA, `breakHere`). The session runs it with the assumed pointer below the room, so it idles (27 Sep). |
| 007 | Runs a sentence | `startObject obj, verb` when ego is close enough (`getDist ≤ 3`) |
| 008 | Verb-area close-up | Shows `var[0x068]` through `verbOps 0x36 image` |
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

## Walk boxes (decoded, ScummVM `room.cpp`/`boxes.cpp`)

- Table at the word at room + 0x15: a count byte, 18-byte boxes, then the matrix up to the image (+0x0A). All 76 rooms fit exactly.
- LOOM's walkable areas are mostly line segments. Room 02 is a chain of 15 (box 4 is what script 045 watches). Room 17 has one quadrangle (box 8). Close-ups have one box outside the image.
- Flags: 0x80 invisible (Room 05, 0x81); 0xA0 invisible and player-only (Rooms 09 and 34).

## Actors (ScummVM `actor.cpp`; modelled 27 September)

- 12 actors (1–12). Actor 1 is the ego (`var[0x001]`). After the boot (opening cutscene) the ego stands in Room 02 at (828, 60), box 0, under the tree (decoded run). Actors 10 and 11 are put over the sea at (137, 42) with `ignoreBoxes` (box 255) after seeded picks (reading: the swans).
- `loadRoomWithEgo obj, room, walkTo=(x, y)` (v3): the ego stands at the object's walk point unless the entry code put it, then walks to (x, y) when x ≠ −1 (ScummVM). Room 03 local 200 +0x0008 brings the ego into Room 04 and walks it to (580, 132), box 0 (decoded; the walk end is an estimate).
- Script 001 +0x0233 `actorFollowCamera var[0x001]` ends the boot with the camera on the ego: x 800 in Room 02 (decoded run; ScummVM `setCameraFollows`).
- `getActorX`/`getActorY` with a number of 13 or more read an object's walk point (ScummVM `getObjX`).
- **Arrivals (decoded, 28 Sep):** 80 `loadRoomWithEgo` sites have literal object and room operands; they bring the ego into 35 rooms. Most are object verbs 0x38 in the neighbouring room (for example Room 02 object 153 → Room 03 at object 160, walk to (250, 68)); a few are local scripts (Room 03 locals 200–202, Room 25 local 208, Room 35 local 210) and global scripts (052 → Room 13, 089 → Room 07, 096 → Room 46). Rooms 32, 43 and 65 have none, so a catalog visit cannot place the ego there. A catalog visit takes one of them as a labelled assumed arrival: the previous room's own first, a seeded pick among several ([validation](../.verification/catalog-arrivals/validation.md)).

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
| The route into a room | A catalog visit does not run it; a decoded literal `loadRoomWithEgo` into the room (the previous room's first, else a seeded pick) is applied as an assumed arrival, an estimate | `LoomEgoArrivalIndex`, session `PickCatalogArrival` |
| `getRandomNr` | Seeded pick, labelled; later steps are estimates | executor |
| Sound commands (music row) | Each command keeps the room whose code began its script chain (`ScriptFrame.OriginRoom`). The room view shows cues started since entry, or started by the room itself and not stopped. | session `SoundEvents`, `LoomSoundCueResolver.ResolveForRoom` |

## Not modelled yet (by frequency in Blockers)

1. Actor queries are answered from the actor table ([validation](../.verification/actor-positions/validation.md)); they stop only when the actor is unknown. After a catalog visit the ego takes a source-backed assumed arrival, so only Rooms 32, 43 and 65 (no literal arrival) still stop ([validation](../.verification/catalog-arrivals/validation.md)). `getActorCostume`, `getActorElevation`, walk timing and paths, and step-by-step camera movement are not modelled.
2. `verbOps`: verb-bar setup (Room 01 local 211 on the boot route).
3. A real pointer: `findObject` is modelled, but the session's pointer is an assumed rest position below the room, so hover and click paths in script 006 are not exercised.
4. `startScript` with a dynamic target (a variable that is not known).
5. `expression` in some forms.
