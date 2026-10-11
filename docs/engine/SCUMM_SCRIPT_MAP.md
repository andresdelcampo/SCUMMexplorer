# SCUMM script reference

This reference separates engine-defined variables and script behavior from the meanings a game assigns to scripts, variables, bits and verbs. Its scope is DOS SCUMM v3 and v4, with narrower version and format limits stated below. A shared opcode handler does not make every operand or behavior identical across versions.

For LOOM's script IDs, story state, draft verbs and room examples, see the [LOOM script map](../loom/LOOM_SCRIPT_MAP.md). Its EGA numeric tables are edition-specific evidence; CD/VGA uses SCUMM v4 and must be decoded independently.

## Engine variables (v3/v4)

These slots belong to the engine contract. The game selects values such as the ego actor, hook script IDs, key bindings and text speed. Those selected values belong in a game map.

| Slot (hex) | Engine name | Role |
| --- | --- | --- |
| `0x001` | `VAR_EGO` | Ego actor |
| `0x002` | `VAR_CAMERA_POS_X` | Camera x |
| `0x003` | `VAR_HAVE_MSG` | Message status |
| `0x004` | `VAR_ROOM` | Current room |
| `0x005` | `VAR_OVERRIDE` | Cutscene skip status |
| `0x00B..0x00D` | `VAR_TMR_1..3` | Accumulating timers |
| `0x011`, `0x012` | `VAR_CAMERA_MIN_X`, `VAR_CAMERA_MAX_X` | Camera limits |
| `0x013` | `VAR_TIMER_NEXT` | Frame jiffies |
| `0x014`, `0x015` | `VAR_VIRT_MOUSE_X`, `VAR_VIRT_MOUSE_Y` | Room pointer |
| `0x018` | `VAR_CUTSCENEEXIT_KEY` | Skip key |
| `0x01C`, `0x01D` | `VAR_ENTRY_SCRIPT`, `VAR_ENTRY_SCRIPT2` | Entry hooks |
| `0x01E`, `0x01F` | `VAR_EXIT_SCRIPT`, `VAR_EXIT_SCRIPT2` | Exit hooks |
| `0x020` | `VAR_VERB_SCRIPT` | Input script |
| `0x023`, `0x024` | `VAR_CUTSCENE_START_SCRIPT`, `VAR_CUTSCENE_END_SCRIPT` | Cutscene hooks |
| `0x025` | `VAR_CHARINC` | Character delay |
| `0x02A`, `0x02B` | `VAR_RESTART_KEY`, `VAR_PAUSE_KEY` | Control keys |
| `0x02C`, `0x02D` | `VAR_MOUSE_X`, `VAR_MOUSE_Y` | Screen pointer |
| `0x02E`, `0x02F` | `VAR_TIMER`, `VAR_TIMER_TOTAL` | Frame/total jiffies |
| `0x030` | `VAR_SOUNDCARD` | Sound device |
| `0x031` | `VAR_VIDEOMODE` | Video mode |

The v4 contract additionally defines `VAR_SCROLL_SCRIPT` (`0x01B`), `VAR_DEBUGMODE` (`0x027`), `VAR_MAINMENU_KEY` (`0x032`), `VAR_FIXEDDISK` (`0x033`), `VAR_CURSORSTATE` (`0x034`) and `VAR_USERPUT` (`0x035`). Thus a slot holding game data in v3 may be reserved in v4. Source: ScummVM [variable setup](https://github.com/scummvm/scummvm/blob/master/engines/scumm/vars.cpp), checked 11 October 2026. This is a selected reference, not a complete table for every platform or version.

## Script operands, input and scheduling (v3/v4)

- Variable/direct flags and local arguments are bytecode rules. Script targets can be variable operands; arguments are copied into the child's locals. A script number's story role comes from the game.
- Global scripts, room entry/exit blocks, room-local scripts and object-verb blocks have distinct owners. A room-local script ID needs its room context.
- `startScene` ends room, object and local scripts; a global script can resume after `loadRoom`. `chainScript` replaces the current script. Recursive and freeze-resistant starts affect which slots survive.
- `delay`/`delayVariable` pause a slot; `breakHere` yields; `waitFor` retries until its actor, camera or message condition clears. Frame processing advances timers and slot delays.
- Input calls `VAR_VERB_SCRIPT` with room/verb-area/key areas 2/1/4. The click button argument differs: v3 supplies 0, versions above v3 supply the input code. Key bindings and verb meanings are game-defined.
- Cutscene overrides record a skip jump; aborting resumes there with `VAR_OVERRIDE` set. Cutscene hook variables select game scripts.

Sources: ScummVM [v3](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v3.cpp), [v4](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v4.cpp), shared [v5 handlers](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v5.cpp), [scheduling](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script.cpp) and [input](https://github.com/scummvm/scummvm/blob/master/engines/scumm/input.cpp). An `o5_` handler name alone does not establish v5-only behavior.

## Verb slots and drawing (v3/v4 small-header path)

`verbOps` updates engine verb slots. Object-verb code is a separate dispatch table. Neither gives a verb ID a universal gameplay meaning.

- `getVerbSlot` returns 0 for a missing slot; image updates are ignored there. `setVerbObject` takes the image from an object in the current room.
- Bitmap verb geometry is strip-based. `findVerbAtPos` takes the last enabled matching slot; overlap can change the dispatched ID.
- Enabled/dimmed verbs draw immediately; disabling restores the old rectangle. A complete screen redraw draws slots in slot order. Verb images, drawn strings and `drawBox` can cover one another.

Sources: ScummVM [verb operations](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v5.cpp) and [rendering](https://github.com/scummvm/scummvm/blob/master/engines/scumm/verbs.cpp). Screen partitions, positions and image objects belong to the game.

## Messages and charset (DOS v3)

- `print` selects text slots: actor `0xFE` uses drawn-string slot 1, `0xFD` debug slot 2, `0xFC` dialog slot 3, others talk slot 0. `0xFF` is a system line. v1–v3 retain position, height and color defaults, but not centering. Initial defaults are position (0, 0), height 0, color 15 and right edge 319.
- Print suboperation 6 is line height in v3; later versions use left alignment. Escapes include newline (`FF 01`), wait (`FF 03`), number (`FF 04`), name (`FF 06`) and string (`FF 07`); `@` is undrawn filler.
- Actor talk requires the actor in the current room and uses its talk color; a system line uses the slot color. v3 talk delay starts at 60 jiffies plus `VAR_CHARINC` per character; newlines add nothing. Frame processing reduces the delay; scene changes stop talk. `VAR_HAVE_MSG` starts at `0xFF`, then reflects `_haveMsg` (1 during talk, 0 after stopping). The talk-stop key sets the delay to zero.
- Virtual-screen boundaries govern clipping; room text x is relative to the camera's left edge. Drawn strings persist until pixels are repainted/erased; unrelated prints do not evict them.
- The DOS v3 charset loader reads file `99 - n`. EGA text uses the low four color bits; LOOM's v3 shadow/outline flags are game-specific renderer handling, described in its map.

Sources: ScummVM [print operations](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v5.cpp), [text](https://github.com/scummvm/scummvm/blob/master/engines/scumm/string.cpp), [charset](https://github.com/scummvm/scummvm/blob/master/engines/scumm/charset.cpp) and [v3 loading](https://github.com/scummvm/scummvm/blob/master/engines/scumm/resource_v3.cpp). Check the v4 path before applying these DOS v3 details to CD/VGA.

## Actors, walking and camera (DOS v3)

- `animateActor` splits commands into `chore = 0x3F - (n >> 2) + 2`, `dir = n & 3`. `FC..FF` stop/stand, `F8..FB` set facing, `F4..F7` turn; others start frame `n`. Costume animation uses `facing + n * 4`; frames 56–60 redirect to init/walk/stand/talk-start/talk-stop.
- Classic costumes have per-limb command ranges: length `& 0x7F`, hold `& 0x80`; start `0x79` hides and `0x7A` shows a limb. Rendering moves from (−72, −100) across limbs 0–15, mirrors left unless header bit `0x80` prevents it, and maps the palette byte through the actor's entry 0. Format, palette, frame contents and assigned IDs are resource/game facts.
- Room actors draw in the engine's y exchange-sort order at `(x, y - elevation)`; the sort is not stable.
- In the v4-and-older class path, actor class 22 controls ignoring boxes; class 0 clears it. Clipping prioritizes class 21 (AlwaysClip), class 20 (NeverClip), then the box mask. Flags `0x08/0x10` flip x/y (v3 actor classes 19/18 also do); `flags & 7` locks facing: 1 retains 90, else 270; 2 retains 0, else 180; 3–6 force 270/90/0/180. Placement also remaps facing.
- Walking adjusts a destination into a box, routes through gates and steps each leg. `calcMovementFactor` uses x speed, or y speed + 1 on steep legs; a leg faces vertically when `|dy| * 3 > |dx|`. Its first step turns/starts walking, then Bresenham steps snap within one step. Completion stands and turns to the destination direction; `walkActorToObject` uses `actordir & 3`. Turns interpolate; placement/hiding can stop a walk.
- `loadRoomWithEgo` uses the object's walk point unless entry code placed the ego, then walks when x is not −1. Actor/object operand distinction depends on actor range; LOOM's threshold follows its actor count.
- The v3 camera moves after actors in eight-pixel strips. Follow mode starts outside trigger strips 10–30; limits constrain it. `setCameraAt` sets normal mode, `panCameraTo` pans, `actorFollowCamera` follows (jumping only beyond 160 pixels, otherwise panning); `loadRoomWithEgo` places it on the ego. `waitFor camera` compares current/destination strips.
- Old-v3 rooms locate the box table through the word at offset `0x15`: count byte, 18-byte boxes, routing matrix before the image. Flags `0x80`/`0x20` mean invisible/player-only. This layout does not describe the v4 CD resource format.

Sources: ScummVM [actors](https://github.com/scummvm/scummvm/blob/master/engines/scumm/actor.cpp), [costumes](https://github.com/scummvm/scummvm/blob/master/engines/scumm/costume.cpp), [camera](https://github.com/scummvm/scummvm/blob/master/engines/scumm/camera.cpp), [rooms](https://github.com/scummvm/scummvm/blob/master/engines/scumm/room.cpp) and [boxes](https://github.com/scummvm/scummvm/blob/master/engines/scumm/boxes.cpp). These v3 algorithms do not establish the v4 walker.

## Scope: games whose scripts are not read

The browsed games (Monkey Island, Indy3 256 colours, Maniac Mansion and Zak McKracken, classic and enhanced) are read for rooms, costumes and sounds only: this reference says nothing about their scripts, variables or verbs, and the explorer shows no script page for them. Their `SC`/`LS` blocks (v4) and script directories exist in the data, but no dialect (`ScummProfile`) is defined for them; the v4 dialect of LOOM CD should not be assumed for Monkey Island without its own check, and the v1 and v2 script formats differ again. See [the resource formats](SCUMM_RESOURCE_FORMATS.md).

## Explorer evidence

Decoded instructions, engine interpretations, source readings, observed execution and explorer preview assumptions are different evidence categories. Catalog arrivals, seeded randomness, synthetic input timing and direct story edits retain their labels. Dated LOOM session mappings and validation remain in the [game map](../loom/LOOM_SCRIPT_MAP.md#historical-session-mapping-september-baseline).
