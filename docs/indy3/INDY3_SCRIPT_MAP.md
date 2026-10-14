# Indiana Jones and the Last Crusade script map

This map records **Indy3-specific** script roles, variable meanings, verb ids and room behaviour. Engine-defined variables and opcode rules are in the [SCUMM script reference](../engine/SCUMM_SCRIPT_MAP.md); the engine's boot sequence is in [SCUMM_BOOT_SEQUENCE.md](../engine/SCUMM_BOOT_SEQUENCE.md).

## Edition scope

All rows come from the EGA English edition (`Games/indy3 ega en`, DOS floppy, old-v3 LFL files, dialect `V3Indy3`), read on 14 October 2026 (S4b). Numbers are not transferred to other editions (R-14.2). The index declares 139 global scripts (`00.LFL`); local scripts of a room are numbered from 200.

## Row format (every table below)

Each row: **item** | **meaning** | **evidence** (`decoded` / `engine` / `reading` / `play`, R-2.3) | **source** (`Script NN +0xOFFS`, `Room NN local script NN`, `Room NN object NN verb 0xHH`) | **date**. A row without a source is not allowed. Unnamed things keep their raw form. `decoded` here means read from the disassembly and confirmed by a headless run of the generic session; nothing here is `play`.

## Boot and hooks

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Script 1 | boot script: sets timer rate 6, skip key 0x1B, restart key 0x142, pause key 0x20, 12 actors, ego 1, eight message strings, then the opening cutscene | decoded | Script 1 +0x0000 to +0x01B6 | 2026-10-14 |
| Opening order | cutscene; loads rooms 79 and 76, costumes 0x63, 0x60, 0x3B, sound 0x23; enters Room 76, then 79; waits on `var[0x039]` and the music timer; ends the cutscene; `loadRoom 0` clears the screen; starts script 118; puts the ego in Room 1; lecture scene in Room 6 and Room 1; copy-protection Room 92; Room 73 | decoded | Script 1 +0x01B6 to +0x0569 | 2026-10-14 |
| End of the boot | `doSentence 0x0A, 0x0342, 0x0000` (verb 10 on object 834 in Room 73), waits for it, ends the cutscene and chains to script 119 | decoded | Script 1 +0x0569 to +0x05BC | 2026-10-14 |
| Room 73 | first playable room: the boot's last `loadRoom` | decoded | Script 1 +0x0564 | 2026-10-14 |
| Cutscene hooks | `var[0x023]` = 22 (start), `var[0x024]` = 23 (end) | decoded | Script 118 +0x0156, +0x015B | 2026-10-14 |
| Hook values at Room 73 | entry hooks 5 and 6, exit hook 7, input script 4, sentence script 2, inventory script 12 (the setters of 5, 6, 7 and 4 were not located) | decoded | boot run, `var[0x01C]`, `var[0x01D]`, `var[0x01E]`, `var[0x020]`, `var[0x021]`, `var[0x022]` | 2026-10-14 |
| Input script during the opening | `var[0x020]` = 8 | decoded | Script 1 +0x01D0 | 2026-10-14 |

## Engine variables as this game uses them

| Variable | This game's use | Evidence | Source | Date |
|---|---|---|---|---|
| `var[0x031]` (video mode) | Room entry scripts test it for 4 (CGA) to choose palettes | decoded | Room 79 entry +0x0000, Room 92 entry +0x0000 | 2026-10-14 |
| `var[0x00E]` (music timer) | the opening waits for 22, then 0x27, 0x39, 0x48, 0x59 and 0x69 | decoded | Script 1 +0x01EE; Room 79 local script 200; Room 79 local script 205 | 2026-10-14 |
| `var[0x029]` (extra timer) | seed of the random loop that picks the copy-protection symbols | decoded | Room 92 local script 201 +0x0005 | 2026-10-14 |
| `var[0x013]` | 6 at boot, 5 in Room 76 | decoded | Script 1 +0x0000; Room 76 local script 201 +0x0000 | 2026-10-14 |

## Game variables and bits

| Name | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| `var[0x039]` | "scene part finished" flag: set by the opening's room scripts, by the answering script of Room 92 and by Room 73's scene; script 1 waits on it | decoded | Room 76 local script 201 +0x00D2; Room 92 local script 204 +0x009F, +0x00B4; Script 1 +0x01E7 | 2026-10-14 |
| `bit[0x5E4]` | the copy-protection check failed ("I'm disappointed in you!" instead of "Good work, Indy!") | decoded | Room 92 local script 204 +0x0022; Script 1 +0x04E1 | 2026-10-14 |
| `var[0x063..0x066]` | the four correct code-wheel answers of Room 92, computed from three random numbers | decoded | Room 92 local script 205 | 2026-10-14 |
| `var[0x067..0x069]` | the three random numbers (section, row, column picks) | decoded | Room 92 local script 201 +0x002C | 2026-10-14 |
| `var[0x06A]`, `var[0x06B]` | question number 1 to 4 and count of failed rounds (3 lets play go on) | decoded | Room 92 local script 204 +0x007F, +0x008E, +0x0098 | 2026-10-14 |
| `var[0x041]` | copy of `var[0x026]` kept by script 6 | decoded | Script 6 +0x0006 | 2026-10-14 |
| `var[0x036..0x038]` | sentence-script repeat counter and the last two sentence objects | decoded | Script 2 +0x0000; Script 17 +0x0008 | 2026-10-14 |
| `var[0x043]`, `var[0x052..0x057]` | first inventory item shown and the six shown ids | decoded | Script 12 +0x002E, +0x0070 | 2026-10-14 |

## Global scripts

| Script | Role | Evidence | Source | Date |
|---|---|---|---|---|
| 1 | boot and the opening cutscene, see above | decoded | Script 1 | 2026-10-14 |
| 2 | sentence script: walks the ego to the objects, tests `getVerbEntrypoint`, runs the object's verb code with `startObject` | decoded | Script 2 +0x0000 to +0x0335 | 2026-10-14 |
| 6 | start of the input handling for a sentence: starts scripts 15 and 16, `doSentence 0xFE` | decoded | Script 6 | 2026-10-14 |
| 12 | inventory script: lists up to six inventory objects as verbs 0x65 to 0x6A, scroll verbs 0x6B and 0x6C | decoded | Script 12 | 2026-10-14 |
| 17 | background loop of random numbers | decoded | Script 17 | 2026-10-14 |
| 22, 23 | cutscene start (freezes scripts) and end (restores verbs saved as id 2, empties the sentence stack, unfreezes) | decoded | Script 23 +0x0000 to +0x0025 | 2026-10-14 |
| 73 | scene with two men (actors 10 and 11) walking to the ego in Room 6 ("Dr. Jones, come with us"); started by Room 6's entry while script 1 runs | decoded | Script 73; Room 06 entry +0x0021 | 2026-10-14 |
| 118 | start-up: `pseudoRoom`, resource loading, sets the cutscene hooks | decoded | Script 118 | 2026-10-14 |
| 119 | the script script 1 chains to at the end of the boot | decoded | Script 1 +0x05BC | 2026-10-14 |

## Rooms

| Room | What it is, entry/exit script facts, local scripts | Evidence | Source | Date |
|---|---|---|---|---|
| 76 | opening: entry starts object 842 verb 0xFA and script 201 (local) if `var[0x039]` is 0; local 201 starts sound 35 and ten timed scripts | decoded | Room 76 entry +0x0013; Room 76 local script 201 | 2026-10-14 |
| 79 | opening credits scrolling over three screens (text "TM & ... 1989 Lucasfilm Ltd."), timed by the music timer; local 205 moves actor 1 across | decoded | Room 79 local script 200 +0x0047; local script 205 | 2026-10-14 |
| 0 | "no room" (ScummVM room 0): used to clear the screen between scenes | engine | Script 1 +0x0219 | 2026-10-14 |
| 6 | verb names "To Henry's House", "To the Plane to Venice", "Cancel" (saved as verb set 6); title card "Barnett College, New York, 1938" | decoded | Room 06 entry +0x0035; Script 1 +0x0257 | 2026-10-14 |
| 1 | dialogue with Marcus ("Hello Marcus!") before the copy-protection screen | decoded | Script 1 +0x02F0 | 2026-10-14 |
| 92 | copy-protection screen: local 201 draws random symbols, 203 prints "Check Section ..., Row ..., Column ...", 204 checks answers, 205 computes them; answer objects 932 and up (verb 0xFF) start 204 | decoded | Room 92 local scripts 201 to 205; object verbs | 2026-10-14 |
| 73 | first playable room (a gym with a ring, two boxers and Indy); local scripts 200 to 204 | decoded | Script 1 +0x0564; Room 73 | 2026-10-14 |

## Verbs and the input chain

| Verb / script | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Verb 0x0A | the verb the boot uses on object 834 in Room 73 | decoded | Script 1 +0x0569 | 2026-10-14 |
| Verbs 0x5A to 0x5C | Room 6's three choices | decoded | Room 06 entry +0x0035 | 2026-10-14 |
| Verbs 0x65 to 0x6C | inventory items and scroll arrows | decoded | Script 12 +0x0081 | 2026-10-14 |
| Input script (`var[0x020]`) | 8 during the opening, 4 in Room 73; the click chain is S6 work | decoded | Script 1 +0x01D0 | 2026-10-14 |
| Sentence script 2 | called with verb, object A, object B as locals 0 to 2 | engine | Script 2 | 2026-10-14 |
| Dialogue verbs 120 to 125 | see the conversations plan | reading | [INDY3_CONVERSATIONS_PLAN.md](INDY3_CONVERSATIONS_PLAN.md) | 2026-10-04 |

## Objects and classes

| Object / class | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Object 842 (Room 76) | started with verb 0xFA by the room's entry | decoded | Room 76 entry +0x0013 | 2026-10-14 |
| Objects 0x034A, 0x034B | owned by actors 14 and 2 by Room 76's entry | decoded | Room 76 entry +0x000B, +0x000F | 2026-10-14 |
| Object 834 (Room 73) | target of the boot's `doSentence`; its verb 0x0A starts the room's local script 203 | decoded | Script 1 +0x0569; Room 73 object 834 verb 0x0A | 2026-10-14 |
| Class 0x85, 0x87, 0x88, 0x8E, 0x8F, 0x90 | tested by the sentence script (actor, can-pick-up and similar roles; meanings not decoded) | reading | Script 2 | 2026-10-14 |

## Actors and costumes

| Actor / costume | Meaning (look, palette, talk colour, who wears what) | Evidence | Source | Date |
|---|---|---|---|---|
| Actor 1 | the ego; costume 0x2B in Room 6, 0x01 in Room 1, 39 in Room 73 | decoded | Room 06 entry +0x0011; Script 1 +0x0591; boot run | 2026-10-14 |
| Actors 10, 11 | the two men of script 73, costume 0x2B | decoded | Script 73 +0x0000, +0x0012 | 2026-10-14 |
| Actor 4 | Marcus in Room 1 | decoded | Script 1 +0x02DD | 2026-10-14 |

## Sounds and music

| Sound | Meaning (device parts, who starts it) | Evidence | Source | Date |
|---|---|---|---|---|
| 35 (0x23) | opening music (AdLib part); the opening ends only once its music timer has passed 105 (0x69) | decoded | Room 76 local script 201 +0x0023; Script 1 +0x01CD | 2026-10-14 |
| 30 | effect started by Room 92's answering script, ends before the next answer | decoded | Room 92 local script 204 +0x0005 | 2026-10-14 |

## Open questions

- Script 73 is started by Room 6's entry while script 1 runs and is still pending at the end of the boot (waiting at +0x00A3 for actor 1 to come near actor 10, which stays in Room 6). Whether the game behaves so, and what the player sees, needs the S5 comparison with the bundled ScummVM.
- Who sets the hook values at Room 73 (entry 5 and 6, exit 7, input 4).
- The meaning of the object classes the sentence script tests.
- Display-only operations the boot reports as not applied: `roomOps`, `oldRoomEffect`, `resourceRoutines`, `stringOps`, `lights` (what they change on screen is S5).
