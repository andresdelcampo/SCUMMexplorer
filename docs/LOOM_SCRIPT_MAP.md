# LOOM EGA script map

A working reference for the Spanish DOS LOOM EGA data in `Games/loom`: what the decoded scripts do, which variables they use, and how the session models them. Offsets are into each script's code, as `CodeDump` prints them (`.verification/fallback-evidence/code.tsv`, regenerated with `dotnet Exe/CodeDump/.../CodeDump.dll Games\loom .verification\fallback-evidence`). Each entry says whether a claim is **decoded** (read straight from instructions), **ScummVM** (engine behaviour checked against `engines/scumm`), or **reading** (an interpretation that has not been verified in a run).

Last updated 27 September 2026. See also [the plan](LOOM_EGA_PLAN.md), the [boot validation](../.verification/boot/validation.md) and the [cutscene continuation validation](../.verification/cutscene-continuation/validation.md).

## Engine variables (ScummVM v3 numbering)

| Variable | Meaning | Source |
| --- | --- | --- |
| `var[0x001]` | Ego actor (`VAR_EGO`); 1 after the boot | ScummVM; script 001 +0x0184 |
| `var[0x002]` | Camera x (`VAR_CAMERA_POS_X`). The session keeps it clamped to [160, room width − 160] and sets it to 160 on room entry. | ScummVM; modelled 27 Sep |
| `var[0x003]` | A message is on screen (`VAR_HAVE_MSG`). Many scripts wait while it is nonzero. | ScummVM |
| `var[0x004]` | Current room (`VAR_ROOM`) | ScummVM |
| `var[0x005]` | `VAR_OVERRIDE`: 1 when the player skipped a cutscene; `endCutscene` clears it | ScummVM |
| `var[0x014]`, `var[0x015]` | Virtual mouse x/y. Script 006 reads them each frame. | ScummVM (reading) |
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
| `var[0x071]` | The object the player arrived through (Room 22's entry reads it) |
| `var[0x07B]` | Difficulty: 0 practice, 1 standard (the boot's value), 2 expert (Room 69 objects 876–878) |
| `bit[0xD62]` | Set by the boot (+0x015B). Room 03's entry starts script 42 only when it and `bit[0xD6E]` are 0. |
| `bit[0xD6B]` | Room 69 → boot handshake (local 205 +0x0048). Scripts 023 and 034 reuse it later in play. |
| `bit[0xD6E]` | "In a cutscene": script 002 sets it and script 003 clears it; the boot sets it around the opening cutscene |
| `bit[0xD66]` | Input-loop flag, cleared at the end of the boot (+0x0236) |
| `bit[0xD7A]` | Script 42's actor 2 on screen; Room 03 locals 200–202 wait while it is set |
| `bit[0xD83]`, `bit[0xD86]`, `bit[0xD89]`, `bit[0xD8A]` | Set by the boot after the intro. They decide Room 38 (612 vs 614), Room 06 (225–228 vs 221–224) and Room 62 (812 vs 814). |

## Global scripts

| Script | Role | Notes |
| --- | --- | --- |
| 001 | Boot | Loads Room 69, waits for `bit[0xD6B]`, sets the hooks, plays script 044, chains to 006. See the [boot validation](../.verification/boot/validation.md). |
| 002 | Cutscene start hook | Cursor and input off, `freezeScripts 0x7F`, `bit[0xD6E] = 1`, `var[0x005] = 0`; with argument 1, starts script 19 |
| 003 | Cutscene end hook | Cursor and input on, unfreeze, camera follows ego, `bit[0xD6E] = 0`; with argument 1, starts script 18 unless `bit[0xD61]` is set |
| 006 | Input loop (runs forever) | Reads the mouse (`var[0x014]`/`var[0x015]`), `findObject`, hover verb 0x37, walking, and starts script 007 (verb 0x38 or 0x36). **Not modelled:** it stops at `findObject` +0x0096. |
| 007 | Runs a sentence | `startObject obj, verb` when ego is close enough (`getDist ≤ 3`) |
| 008 | Verb-area close-up | Shows `var[0x068]` through `verbOps 0x36 image` |
| 014 | After-entry hook | Room-dependent setup; starts script 18 in some rooms |
| 015 | Before-exit hook | Clears `bit[0xD7A]`, **stops script 42**, clears the hover, starts scripts 008 and 028 |
| 016 | Prints a line at a variable y (`print` with an empty text) | |
| 039 (0x27), 040 (0x28) | Sound players: stop the previous sound, start the argument's sound, loop while it plays | The session reads a started sound as playing (estimate), so these stay pending |
| 042 | Room 03's actor 2 (random position and time on screen, `bit[0xD7A]`) | |
| 044 | **Opening cutscene** | Rooms 01, 02, 01, 02, 53, 02; waits on the camera (+0x0361) and on local scripts (`isScriptRunning` 0xD1/0xD2); two overrides |
| 045 | Room 02 walk-box watcher | Starts a cutscene when ego enters box 4. Blocked by `getActorWalkBox`. |
| 052 | Room 10 object 295 → Room 11 → Room 13 cutscene | |
| 086 | Firewood scene; opens Room 58 on 792 (+0x018A) | Body behind `beginOverride` |
| 090 | Room 42 flashes (637, 638) | Transient draws; not batches |
| 099 | Draws Room 20 (392–396, +0x02BB) | |
| 108 | Glassmakers scene; opens Room 56 on 779 and 782 (+0x0188) | |
| 113, 114 | Ambient actors during the intro (random positions) | Seeded picks |

## Room notes

- **Room 69: intro screen.** The entry starts local 202 (difficulty menu). Objects 876–878 pick the difficulty and start local 201 (the random draft and guild symbol), which chains to 204 (the question). Note objects 886–897 start local 203 [note index]. Local 203 checks the draft and starts 205 after a correct draft or the third wrong try. Local 205 sets `bit[0xD6B]`.
- **Room 03: lake.** Objects 161–164 reach Rooms 05, 04, 10 through locals 200–202, which wait while `bit[0xD7A]` is set. Object 160 leads to Room 02 directly.
- **Room 10.** Object 295 starts script 052 (a cutscene through Room 11 into Room 13).
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
| Camera | `var[0x002]`, pans arrive at once | executor, `CameraRange` |
| `getRandomNr` | Seeded pick, labelled; later steps are estimates | executor |

## Not modelled yet (by frequency in Blockers)

1. `getActorWalkBox`, `getActorX`/`getActorY`, `getActorCostume`, `getActorElevation`: need actor positions and walk boxes (ScummVM `Actor`, `BoxMgr`).
2. `verbOps`: verb-bar setup (Room 01 local 211 on the boot route).
3. `findObject`: needs a mouse position; the input loop (script 006) depends on it.
4. `startScript` with a dynamic target (a variable that is not known).
5. `expression` in some forms.
