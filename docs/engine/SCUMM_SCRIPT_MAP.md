# SCUMM script reference

This reference separates engine-defined variables and script behavior from the meanings a game assigns to scripts, variables, bits and verbs. Version and format limits are stated per section, including the separate DOS v1/v2 contract. A shared opcode handler does not make every operand or behavior identical across versions.

For LOOM's script IDs, story state, draft verbs and room examples, see the [LOOM script map](../loom/LOOM_SCRIPT_MAP.md). Its EGA numeric tables are edition-specific evidence; CD/VGA uses SCUMM v4 and must be decoded independently.

## SCUMM v1/v2 live input (17 October 2026)

ScummVM `ScummEngine_v2::checkExecVerbs` and `runInputScript` dispatch through script 4 and the old click/keypress globals, not the later `VAR_VERB_SCRIPT` and local argument contract. Verb/scene clicks use mode 1; sentence area 5 and inventory area 3 use mode 0. The engine supplies the sentence and inventory hit regions and scrolling keys. Scripts still compose and execute the action; the explorer must not interpret a first object click as an automatically completed sentence. Source: `verbs.cpp`, `script.cpp`, read 17 October 2026. Implementation: `ScummSessionPlay`; proposed interface requirements R-7.19 to R-7.21. Game-specific keys and choices belong in the Maniac/Zak script maps.

For v1/v2 scene previews, direct object actions bind `ScummVars.V2.ActiveVerb`, `ActiveObject1` and `ActiveObject2` before object code runs, matching the invocation context of `o2_doSentence` subcommand 1. These engine arguments are distinct from story prerequisites; stale arguments can select another object's response. Native cutscene begins retain their source script and offset for run attribution. Sources: ScummVM `script_v2.cpp`, `script.cpp`, read 18 October 2026. The scan accepts the old dialect's bare literal script calls and `[text "..."]` syntax; old `printEgo` text counting is version-gated to preserve later-engine lists.

## Engine variables (v3/v4)

The following v3/v4 slots must not be applied to v1/v2; those roles are recorded separately below.

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
| `0x021`, `0x022` | `VAR_SENTENCE_SCRIPT`, `VAR_INVENTORY_SCRIPT` | Script `doSentence` queues into; script `pickupObject` and `setOwnerOf` run (`vars.cpp`, read 14 October 2026) |
| `0x023`, `0x024` | `VAR_CUTSCENE_START_SCRIPT`, `VAR_CUTSCENE_END_SCRIPT` | Cutscene hooks |
| `0x027..0x029` | (Indiana Jones 3 only) | `scummLoop` adds the frame's jiffies to variables 39 to 41 for non-Macintosh Indiana Jones 3 and FM-Towns Zak (`scumm.cpp`, 14 October 2026); in v4 slot `0x027` is `VAR_DEBUGMODE` |
| `0x00E` | `VAR_MUSIC_TIMER` | Music engine timer, see the [boot sequence](SCUMM_BOOT_SEQUENCE.md) |
| `0x025` | `VAR_CHARINC` | Character delay |
| `0x02A`, `0x02B` | `VAR_RESTART_KEY`, `VAR_PAUSE_KEY` | Control keys |
| `0x02C`, `0x02D` | `VAR_MOUSE_X`, `VAR_MOUSE_Y` | Screen pointer |
| `0x02E`, `0x02F` | `VAR_TIMER`, `VAR_TIMER_TOTAL` | Frame/total jiffies |
| `0x030` | `VAR_SOUNDCARD` | Sound device |
| `0x031` | `VAR_VIDEOMODE` | Video mode |

In v4 `resetScummVars` also gives `VAR_HEAPSPACE` (`0x028`) = 1400 and `VAR_FIXEDDISK` (`0x033`) = 1 at start (`vars.cpp`, 14 October 2026). `o5_actorOps` sub-opcode 17 (scale, raw `0x11`; raw `0x13` in small-header games) reads **one** operand in version 4 and two in version 5 (`script_v5.cpp`, 14 October 2026). `actorFollowCamera` on an actor outside the current room runs `startScene` for that room (`camera.cpp` `setCameraFollows`, 14 October 2026).

The v4 contract additionally defines `VAR_SCROLL_SCRIPT` (`0x01B`), `VAR_DEBUGMODE` (`0x027`), `VAR_MAINMENU_KEY` (`0x032`), `VAR_FIXEDDISK` (`0x033`), `VAR_CURSORSTATE` (`0x034`) and `VAR_USERPUT` (`0x035`). Thus a slot holding game data in v3 may be reserved in v4. Source: ScummVM [variable setup](https://github.com/scummvm/scummvm/blob/master/engines/scumm/vars.cpp), checked 11 October 2026. This is a selected reference, not a complete table for every platform or version.

**Version 5 slots** (`vars.cpp` `setupScummVars`, `_game.version >= 5`, ScummVM master read 17 October 2026; `ScummVars`): `VAR_V5_TALK_STRING_Y` (`0x036`, versions 4 and 5, -0x50 at start), `VAR_SOUNDRESULT` (`0x038`), `VAR_TALKSTOP_KEY` (`0x039`), `VAR_FADE_DELAY` (`0x03B`, 3 at start), `VAR_NOSUBTITLES` (`0x03C`), `VAR_SOUNDPARAM`..`VAR_SOUNDPARAM3` (`0x040` to `0x042`), `VAR_INPUTMODE` (`0x043`: 1 keyboard, 2 joystick, 3 mouse; 3 at start), `VAR_MEMORY_PERFORMANCE` (`0x044`), `VAR_VIDEO_PERFORMANCE` (`0x045`), `VAR_ROOM_FLAG` (`0x046`), `VAR_GAME_LOADED` (`0x047`), `VAR_NEW_ROOM` (`0x048`). The v5 opcode differences met so far are in the [boot sequence](SCUMM_BOOT_SEQUENCE.md), v5 section.

## Script operands, input and scheduling (v3/v4)

- Variable/direct flags and local arguments are bytecode rules. Script targets can be variable operands; arguments are copied into the child's locals. A script number's story role comes from the game.
- Global scripts, room entry/exit blocks, room-local scripts and object-verb blocks have distinct owners. A room-local script ID needs its room context.
- `startScene` ends room, object and local scripts; a global script can resume after `loadRoom`. `chainScript` replaces the current script. Recursive and freeze-resistant starts affect which slots survive.
- `delay`/`delayVariable` pause a slot; `breakHere` yields; `waitFor` retries until its actor, camera or message condition clears. Frame processing advances timers and slot delays.
- Input calls `VAR_VERB_SCRIPT` with room/verb-area/key areas 2/1/4. The click button argument differs: v3 supplies 0, versions above v3 supply the input code. Key bindings and verb meanings are game-defined.
- Cutscene overrides record a skip jump; aborting resumes there with `VAR_OVERRIDE` set. Cutscene hook variables select game scripts.
- **Bit variables (v3 against v4, read 15 October 2026, `script.cpp` `readVar`/`writeVar`):** up to version 3 (except FM-Towns Indy3 and PC-Engine LOOM) `bit[n]` is bit `n & 15` of the word variable `n >> 4`; from version 4 the bits live in their own array (`_bitVars`) and never touch a word variable. The explorer keeps them apart for every v4 game (`ScummProfile.SeparateBitVariables`; LOOM CD set it in its own session before). Without it a v4 game's bits 0x040 to 0x04F overwrote `VAR_ROOM` (S5 MonkeyFloppy, Room 30).
- **Pseudo rooms (v3/v4, `o5_pseudoRoom`, `room.cpp` `startScene`, read 15 October 2026):** `pseudoRoom R, [b...]` maps every listed byte from 0x80 up to the real room R (`_resourceMapper[b & 0x7F] = R`). A load of such a number sets `VAR_ROOM` (and `_currentRoom`) to the pseudo number and loads room R, so one room picture serves several places that its entry script tells apart by `VAR_ROOM`; actors put into the pseudo number are in that room. The session loads the real room, sets `VAR_ROOM` to the number loaded, stores actors put into a pseudo room under the real room and answers `getActorRoom` with the pseudo number while the room is current.

- **Cursor and input variables (v4, read 16 October 2026, `script_v5.cpp` `o5_cursorCommand`, `vars.cpp` `setupScummVars`):** from version 4 every `cursorCommand`, whatever its sub-opcode, ends with `VAR(VAR_CURSORSTATE) = _cursor.state; VAR(VAR_USERPUT) = _userPut` (variables 52 and 53; v3 has no such slots). Monkey Island's interface loop runs its object finder only while variable 52 is above 0, so without the copy nothing is ever hovered (S6 MonkeyFloppy). The explorer copies them for every engine version 4 game (`ScummProfile.PublishesCursorVariables`).
- **Engine keys of v4 (`input.cpp` `processKeyboard`, read 16 October 2026; `ScummEngine_v4 : ScummEngine_v5`, `scumm_v4.h`):** the key in `VAR_MAINMENU_KEY` (variable 50, version 4 and later) opens the engine's own save/load menu and never reaches the scripts; Esc (`abortCutscene`) and `.` (talk stop) are hard-coded; the pause and restart keys come from `VAR_PAUSE_KEY`/`VAR_RESTART_KEY`. `VAR_TALKSTOP_KEY` exists only from version 5. In play the explorer refuses the main-menu key as it refuses restart (`ScummProfile.MainMenuKeyVariable`).
- **Clicks of v4 (`verbs.cpp` `checkExecVerbs`):** the third argument of `runInputScript` is 1 for the left button and 2 for the right from version 4 (0 in v3); the version that counts is the engine's (`EngineVersion`: the 256-colour Indy3 is version 3).
- **The built-in cursor of v3 and v4 (`cursor.cpp`, read 16 October 2026):** `ScummEngine_v5::resetCursors` cursor 0, a 16 × 16 cross-hair (rows `0x0080` × 6, `0x0000`, `0x7E3F`, `0x0000`, `0x0080` × 6, `0x0000`; bit 15 is the left column) with its hotspot at (8, 7) in the gap of the cross; `setBuiltinCursor(idx)` paints it in `default_cursor_colors[idx]` = 15, 15, 7, 8 and `animateCursor` steps the index every second frame. Versions 3 and 4 inherit it; only v0 to v2 draw the 23 × 21 crosshair of `ScummEngine_v2::setBuiltinCursor`; LOOM redefines its cursor from a charset glyph (`redefineBuiltinCursorFromChar` asserts LOOM).
- **Text verbs of v4 (`verbs.cpp` `drawVerb`, `script_v5.cpp` `o5_verbOps`):** a new verb takes `charset_nr` from the charset in use (`cursorCommand charsetSet` before `verbOps new`), colour 2, dim colour 8 and highlight colour 0 (14 only in version 3); it is drawn with that charset through `drawString` slot 4, its rectangle from the drawn text (`_charset->_str`). With `CharsetRendererClassic` (v4) the colour is a whole palette index for pixel value 1 and the charset's colour map gives the others.

Sources: ScummVM [v3](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v3.cpp), [v4](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v4.cpp), shared [v5 handlers](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v5.cpp), [scheduling](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script.cpp) and [input](https://github.com/scummvm/scummvm/blob/master/engines/scumm/input.cpp). An `o5_` handler name alone does not establish v5-only behavior.

## Display operations of the old bundle (v3, S5, ScummVM `script_v5.cpp`, `gfx.cpp`, `room.cpp`, read 14 October 2026)

- **`roomOps` sub-opcode 2 (`roomColorLegacy a, b`, small-header games):** `_roomPalette[b] = a`. Every pixel of the room picture and of its objects is drawn through `_roomPalette`; a full redraw follows, so the change colours the whole room picture. Actors are not drawn through it. The table is the identity at every room load (`startScene`).
- **`roomOps` sub-opcode 4 (`roomPalette a, b`):** `_shadowPalette[b] = a`, applied when the screen is shown (the opening's title fade). Also reset at room load.
- **`roomOps` 3 (`setScreen`), 5/6 (`shakeOn`/`shakeOff`), `oldRoomEffect` (`switchEffect`):** screen geometry, shaking and a transition effect; nothing of them stays in the room picture.
- **`lights a, b, c`:** `c = 0` sets `VAR_CURRENT_LIGHTS` (variable 9 in v3 to v5) to `a`; `c = 1` sets the flashlight's size in strips (`a` by `b`). Bits of `VAR_CURRENT_LIGHTS`: 0 actors use the base palette, 1 room lights on, 2 flashlight on, 3 actors use their own colours. With the room lights off the game draws the room dark except under the flashlight (`scummLoop_handleActors`). The engine starts the variable at 1 | 2 | 8 (`resetScummVars`).
- **`stringOps` (`loadString`, `copyString`, `setStringChar`, `getStringChar`, `createString`):** the game's string resources; `getStringChar`/`setStringChar` read and write a byte (a write a few bytes past the end is allowed by the safety area, the rest denied).
- **`resourceRoutines`:** loads, locks and unlocks resources; nothing visible.

## Verb slots and drawing (v3/v4 small-header path)

`verbOps` updates engine verb slots. Object-verb code is a separate dispatch table. Neither gives a verb ID a universal gameplay meaning.

- `getVerbSlot` returns 0 for a missing slot; image updates are ignored there. `setVerbObject` takes the image from an object in the current room.
- Bitmap verb geometry is strip-based. `findVerbAtPos` takes the last enabled matching slot; overlap can change the dispatched ID.
- Enabled/dimmed verbs draw immediately; disabling restores the old rectangle. A complete screen redraw draws slots in slot order. Verb images, drawn strings and `drawBox` can cover one another.

Sources: ScummVM [verb operations](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v5.cpp) and [rendering](https://github.com/scummvm/scummvm/blob/master/engines/scumm/verbs.cpp). Screen partitions, positions and image objects belong to the game.

## Play input, verbs, classes and charsets (v5, S6 MonkeyCd; ScummVM master read 17 October 2026)

- **Untouchable class:** `getClass`/`putClass` (`object.cpp`) map `kObjectClassUntouchable` to 24 only with `GF_SMALL_HEADER` (versions 3 and 4); version 5 stores class 32 itself. `findObject` and `getActorFromPos` skip it (`ScummProfile.UntouchableClass`).
- **`setVerbObject`** (`verbs.cpp`): with `GF_OLD_BUNDLE` or `GF_SMALL_HEADER` the image comes only from a local object of the current room; otherwise (version 5) from `findObjectInRoom(object, room)` in the room the script names, current or not (`ScummProfile.VerbImagesFromNamedRoom`).
- **Input** (`verbs.cpp` `checkExecVerbs`): from version 4 a click passes mode 1 (left) or 2 (right) to `runInputScript`; areas 1 (verb, or outside the main screen), 2 (main screen), 4 (key, mode 1); a key equal to an enabled verb's key is that verb's click.
- **Code 7 in a message** (`convertStringMessage`): versions 3 (and 6+) read the string number from a variable; versions 4 and 5 take the operand as the string number.
- **`cursorCommand charsetColors`** (case 14): the list's values replace entries 0, 1, 2… of the current charset's colour map (`_charsetData`); entry 1 is replaced by the text's colour when drawn.
- **Charsets:** a `CHAR` block (index directory `DCHR`) holds the font 29 bytes into the resource (8 header bytes, then the v4 layout: size, 0x0363, 15 colours, the font; `CharsetRendererCommon::setCurID`).
- **Cursor:** `ScummEngine_v5::resetCursors` cursor 0 (the cross-hair, hotspot 8, 7) in colours 15, 15, 7, 8 (`default_cursor_colors`), animated every two frames; no DOS-specific branch. Monkey Island CD never changes it (no `cursorImage`/`cursorHotspot` in its global scripts).
- **CD tracks** (`sound.cpp` `triggerSound`, `soundcd.cpp`): a `SOUN` block of chunk type 2 starts `playCDTrackFromSoundID`; `stopSound` calls `stopCDSound` first. The iMuse "stop all sounds on a new music" workaround is only for `GID_MONKEY_VGA`/`GID_MONKEY_EGA`, not the CD release.

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

## SCUMM v1/v2 engine roles (17 October 2026)

Source: ScummVM `engines/scumm/vars.cpp`, `ScummEngine_v2::setupScummVars`, read 17 October 2026. These are engine slot meanings, not game story state. Code constants live in `ScummVars.V2`.

| Role | Decimal slot | Evidence |
|---|---:|---|
| Ego | 0 | engine |
| Camera x / message active / room / override | 2 / 3 / 4 / 5 | engine |
| Machine speed / displayed character count | 6 / 7 | engine |
| Active verb / active object 1 / active object 2 | 8 / 9 / 10 | engine |
| Number of actors / lights / current drive | 11 / 12 / 13 | engine |
| One-level indirect read aliases (writes address the original slot) | 14 / 15 / 16 | `script_v2.cpp::readVar` / `writeVar` |
| Music timer / verb allowed | 17 / 18 | engine |
| Actor range minimum / maximum | 19 / 20 | engine |
| Cursor state | 21 | engine |
| Camera minimum / maximum / next-frame jiffies | 23 / 24 / 25 | engine |
| Sentence verb / object 1 / object 2 / preposition | 26 / 27 / 28 / 29 | engine |
| Virtual mouse x / y | 30 / 31 | engine |
| Click area / verb / object | 32 / 33 / 35 | engine |
| Room resource / last sound / backup verb | 36 / 37 / 38 | engine |
| Keypress / cutscene exit key / talking actor | 39 / 40 / 41 | engine |

Version 1 and version 2 actor operations differ in palette operand length. Actor-name operations carry a zero-terminated name. The delay instruction stores the complemented 24-bit duration, and `beginOverride` embeds a relative jump whose target must also be decoded. These corrections are pinned in the [decoder record](../../.verification/maniac-decoder-20261017/README.md), with source references and edition results. They do not establish gameplay support.

The v2 engine's fixed scripts (input 4, sentence 2, post-entry 5), camera/mouse units and native text delay are described in [the boot sequence](SCUMM_BOOT_SEQUENCE.md#scumm-v1v2-foundation-17-october-2026). The classic and enhanced dialects remain distinct; neither uses the v3 global-hook slots or frame-timer variables.

## Explorer evidence

### SCUMM v1/v2 closest query and actor approach distance (18 October 2026)

`script_v2.cpp` dispatches opcodes `66`/`E6` to `o5_getClosestObjActor`. It searches the inclusive engine actor range (words 19/20) in descending order, starting with result/distance `FF`. Only a strictly shorter distance replaces the result: the highest id wins ties; distances of 255 or more never select a candidate. There is no self exclusion. The native do/while examines the maximum once even if the range is inverted. Distance uses the existing `getObjActToObjActDist` rules, including native old coordinate units, inventory-owner position, absence and the zero distance for two actors together in a non-current nonzero room. Unknown preview inputs remain unresolved; estimated ranges/positions retain their provenance.

`ScummEngine_v5::walkActorToActor` multiplies every v1/v2 approach distance by eight before adding/subtracting it from the target's pixel position. In these versions `FF` is the literal distance 255 (2040 pixels), **not** the later-engine width sentinel. The executor normalizes this operand beside the other v1/v2 coordinates; the shared actor walker retains the native current-room/valid-actor guards. This prevents a four-pixel destination offset from being fed to a walker stepping in eight-pixel native units.

Evidence: engine. Sources: ScummVM [`script_v2.cpp`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp), [`script_v5.cpp`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v5.cpp) (`o5_getClosestObjActor`, `walkActorToActor`), [`object.cpp`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/object.cpp) (`getObjActToObjActDist`) and [`vars.cpp`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/vars.cpp), master read 18 October 2026. [Bounded checks and retained limits](../../.verification/maniacclassic-fidelity-20261018/README.md). These facts establish execution semantics, not original-game visual or timing acceptance.

Decoded instructions, engine interpretations, source readings, observed execution and explorer preview assumptions are different evidence categories. Catalog arrivals, seeded randomness, synthetic input timing and direct story edits retain their labels. Dated LOOM session mappings and validation remain in the [game map](../loom/LOOM_SCRIPT_MAP.md#historical-session-mapping-september-baseline).
