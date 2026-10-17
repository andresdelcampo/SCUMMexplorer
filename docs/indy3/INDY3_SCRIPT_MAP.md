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
| `bit[0x5E4]` | the copy-protection check failed ("I'm disappointed in you!" instead of "Good work, Indy!"); 0 on the explorer's correct-answer route (S5) | decoded | Room 92 local script 204 +0x0022; Script 1 +0x04E1 | 2026-10-14 |
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
| 73 | the Donovan scene: two men (actors 10 and 11) come for the ego in Room 6 ("Dr. Jones, come with us"), then Room 9 with Donovan. Started by Room 6's entry (`ifState 0x7C 0`) only when script 1 is **not** running, so not during the boot; **not pending after the boot** (S5; S4b started it by a session bug) | decoded | Script 73; Room 06 entry +0x0008 to +0x0021 | 2026-10-14 |
| 118 | start-up: `pseudoRoom`, resource loading, sets the cutscene hooks | decoded | Script 118 | 2026-10-14 |
| 119 | the script script 1 chains to at the end of the boot | decoded | Script 1 +0x05BC | 2026-10-14 |

## Rooms

| Room | What it is, entry/exit script facts, local scripts | Evidence | Source | Date |
|---|---|---|---|---|
| 76 | opening: entry starts object 842 verb 0xFA and script 201 (local) if `var[0x039]` is 0; local 201 starts sound 35 and ten timed scripts | decoded | Room 76 entry +0x0013; Room 76 local script 201 | 2026-10-14 |
| 79 | opening credits scrolling over three screens (text "TM & ... 1989 Lucasfilm Ltd."), timed by the music timer; local 205 moves actor 1 across | decoded | Room 79 local script 200 +0x0047; local script 205 | 2026-10-14 |
| 0 | "no room" (ScummVM room 0): used to clear the screen between scenes | engine | Script 1 +0x0219 | 2026-10-14 |
| 6 | exterior of Barnett College; entry starts Script 73 and changes the ego's costume only while script 1 is not running (`isScriptRunning 1`); verb names "To Henry's House", "To the Plane to Venice", "Cancel" (saved as verb set 6); title card "Barnett College, New York, 1938" | decoded | Room 06 entry +0x0035; Script 1 +0x0257 | 2026-10-14 |
| 1 | dialogue with Marcus ("Hello Marcus!") before the copy-protection screen | decoded | Script 1 +0x02F0 | 2026-10-14 |
| 92 | copy-protection screen: the first click on the full-screen object 929 (fallback verb) starts local 201, which picks the three random numbers and starts 205 (computes `var[0x063..0x066]`) and 203 (prints "Check Section ..., Row ..., Column ..."); local 204 checks each answer with `isEqual var[0x06A], n` then `isNotEqual var[0x062+n], local[0x000]`; the twelve answer objects 932 to 943 (fallback verb) start 204 with their number 0 to 11 in local 0; after four right answers `var[0x039]` = 1 and `bit[0x5E4]` stays 0 | decoded | Room 92 object 929 verb fallback +0x0013; local scripts 201 to 205; objects 932 to 943 | 2026-10-14 |
| 73 | first playable room (a gym with a ring, two boxers and Indy); local scripts 200 to 204 | decoded | Script 1 +0x0564; Room 73 | 2026-10-14 |
| 73 (cutscenes, S7) | the entry's span is in locals 205 and 206 and begins nothing from a fresh start, even with the entry played again; the room's list (fresh runs) holds Open door (`global script 26`), Use/Walk to locker room (local 203, `global script 111`, "Ten minutes later"), Pick up mallet (local 205) and the light switch (object 837) | run (S7 headless run, both releases) | Room 73 entry; `.verification/indy3-cutscenes-20261017/indy3-ega-list.txt` | 2026-10-17 |
| 22 | castle room: the entry starts `global script 77` and `global script 129`, a cutscene of 97 instructions with 5 lines ("Meanwhile, elsewhere in the castle…") and 2 room loads; begins from a fresh start (98 frames) | run (S7 headless run, both releases) | Room 22 entry | 2026-10-17 |
| 80 | the entry's local 201 holds a cutscene of 31 instructions and 4 lines ("Dad! How'd you get here before me?"); begins from a fresh start (136 frames) | run (S7 headless run, both releases) | Room 80 entry; Room 80 local script 201 | 2026-10-17 |
| 1 (cutscenes, S7) | the office door's Walk to (object 23 verb 0x0A) reaches `global script 55` (the students, "STUDENTS! STUDENTS! PLEASE BE PATIENT!", 7 lines, 1 room load), which begins nothing from a fresh start (its condition is not decoded) | decoded (scan); run (not begun) | Room 01 object 23 verb 0x0A; global script 55 | 2026-10-17 |

## Verbs and the input chain

| Verb / script | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Verb 0x0A | the verb the boot uses on object 834 in Room 73 | decoded | Script 1 +0x0569 | 2026-10-14 |
| Verbs 0x5A to 0x5C | Room 6's three choices | decoded | Room 06 entry +0x0035 | 2026-10-14 |
| Verbs 0x65 to 0x6C | inventory items and scroll arrows | decoded | Script 12 +0x0081 | 2026-10-14 |
| Input script (`var[0x020]`) | 8 during the opening, 4 in Room 73; the click chain is S6 work | decoded | Script 1 +0x01D0 | 2026-10-14 |
| Script 96 (verb screen) | creates the play verbs with their keys: 0x06 Push `q`, 0x07 Pull `a`, 0x03 Give `z`, 0x01 Open `w`, 0x02 Close `s`, 0x09 Look `x`, 0x0A Walk to `e`, 0x0B Pick up `d`, 0x0C What is `c`, 0x08 Use `r`, 0x04 Turn on `f`, 0x05 Turn off `v` (on); 0x0D Talk `t`, 0x20 Travel `g` (dim); 0x21 To Indy, 0x22 To Henry `b` (off); prepositions 0x81 in, 0x82 with, 0x83 on, 0x84 to (off); inventory slots 0x65 to 0x6A (`y u h j n m`), arrows 0x6B `o`, 0x6C `l`; sentence line 0x64 at y 0x90; colours by `var[0x031]` (CGA) | decoded | Script 96 +0x0000 to +0x0250 | 2026-10-15 |
| Script 4 (play input script) | locals: area (1 verb, 2 scene, 4 key; it turns inventory clicks into 3 and a click on the sentence line into 5), value, mode. Keys go through Script 24 first. Inventory slots give the shown object (`var[0x053+n]`), arrows move `var[0x043]` by 2 and chain to Script 12. A verb click sets `var[0x03B]`; the same verb twice runs the sentence. In the scene, Script 20 finds the object under the pointer; "Walk to" (0x0A) runs on one click, other verbs on a second click of the same object; Use (0x08) asks Script 11 for a preposition into `var[0x03E]`, Give (0x03) sets it to 0x84; then `doSentence 0xFE` and `doSentence var[0x03B], var[0x03C], var[0x03D]`. "Walk to" on the floor: `walkActorTo` the ego to the pointer unless `bit[0x595+ego]`. "What is" (0x0C) loops while the pointer is in the verb area (`var[0x015]` > 0x86), naming what is under it | decoded | Script 4 +0x0000 to +0x029C | 2026-10-15 |
| `var[0x03B..0x03F]` | sentence parts: verb, object A, object B, preposition verb, default verb (`var[0x03F]`, value not read) | decoded | Script 15; Script 4 +0x020D | 2026-10-15 |
| Script 15, Script 16 | 15 resets the sentence to the default verb; 16 names verb 0x64 from `var[0x03B..0x03E]` with the verb-name and object-name escapes (the sentence line) | decoded | Script 15; Script 16 +0x0005, +0x0021 | 2026-10-15 |
| Script 20 | object or actor under the pointer (`actorFromPos`, `findObject` at `var[0x014]`, `var[0x015]`, optional class filter) into `var[0x039]` | decoded | Script 20 | 2026-10-15 |
| Script 24 | key handler: F5 (319) stops the input script and chains to Script 9 (if `bit[0x5E4]` is 0); `<` `>` text speed (`var[0x042]`); Ctrl-V version; Ctrl-R machine rating; returns 0 in `var[0x039]` | decoded | Script 24 | 2026-10-15 |
| Script 18 (dialogue input script) | installed in `var[0x020]` by the dialogue scripts (which restore the previous value after); a choice 120 to 124 turns verbs 0x65 to 0x7C and 14 off, prints choices 120 to 122 as the ego's line and sets `var[0x072]` to the verb id; an inventory click during a dialogue offers the item ("Take this …") | decoded | Script 18 +0x0000 to +0x0108; Script 29 +0x0039, +0x05C8; Script 30 +0x005F, +0x0615 | 2026-10-15 |
| Other input scripts | `var[0x020]` is also set to 10 (Script 83 +0x003E), 19 (Script 35 +0x0121), 28 (Script 25 +0x000D), 97 (Script 90 +0x0007) and 201 (Script 9 +0x004E) in special scenes | decoded | as listed | 2026-10-15 |
| Sentence script 2 | called with verb, object A, object B as locals 0 to 2 | engine | Script 2 | 2026-10-14 |
| Script 113 | turns Talk (0x0D) on when `var[0x032]` (the room's conversation script) is not 0, Travel (0x20) on when `var[0x033]` is not 0 | decoded | Script 113 +0x0000..+0x001C | 2026-10-16 |
| `var[0x032]` | the conversation script of the room: Room 1's entry sets 29 (Marcus) while actor 4 is there; Script 2 +0x0041 starts it for Talk | decoded | Room 01 entry +0x00A4; Script 2 +0x003A | 2026-10-16 |
| Room 73 objects in play | 836 the mallet (Pick up: pickupObject, starts local 205: the chain gives it back near the bell); 837 the light switch, a child of the door 830 (parent state 0: findable only with the door closed), Push/Pull/Use/Turn off: cutscene with `beginOverride` +0x0008; 830 the door: Open/Close (Scripts 26/27), Walk to leaves with `loadRoomWithEgo 0x1C, 0x01` (Room 1) | play | Room 73 objects 830, 836, 837; local 205 | 2026-10-16 |
| Room screen | `roomOps setScreen 0x10, 0x90`: text rows 0–15, room rows 16–143, verb screen 144–199 | decoded | Script 1 +0x021B | 2026-10-16 |
| Dialogue verbs 120 to 125 | see the conversations plan | reading | [INDY3_CONVERSATIONS_PLAN.md](INDY3_CONVERSATIONS_PLAN.md) | 2026-10-04 |

## Objects and classes

| Object / class | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Object 842 (Room 76) | started with verb 0xFA by the room's entry | decoded | Room 76 entry +0x0013 | 2026-10-14 |
| Objects 0x034A, 0x034B | owned by actors 14 and 2 by Room 76's entry | decoded | Room 76 entry +0x000B, +0x000F | 2026-10-14 |
| Object 834 (Room 73) | target of the boot's `doSentence`; its verb 0x0A starts the room's local script 203 | decoded | Script 1 +0x0569; Room 73 object 834 verb 0x0A | 2026-10-14 |
| Object 837 (Room 73), the light switch | verbs 0x05 Turn off, 0x06 Push, 0x07 Pull and 0x08 Use point at one code block: one cutscene of 27 instructions and 2 lines ("HEY!! WHO TURNED OUT THE LIGHTS!!"), one entry in the cutscene list; begins from a fresh start (66 frames) | decoded (verb table); run (S7) | Room 73 object 837 verb table; Room 73 object 837 verb 0x05 | 2026-10-17 |
| Class 0x85, 0x87, 0x88, 0x8E, 0x8F, 0x90 | tested by the sentence script (actor, can-pick-up and similar roles; meanings not decoded) | reading | Script 2 | 2026-10-14 |

## Actors and costumes

| Actor / costume | Meaning (look, palette, talk colour, who wears what) | Evidence | Source | Date |
|---|---|---|---|---|
| Actor 1 | the ego; costume 0x01 in Room 1 and in Room 73 at the end of the boot. Room 6's entry gives costume 0x2B only when script 1 is not running (so not during the boot). Room 73's local script 203 keeps costume 1 (walks in) and gives other costumes 0x0E, 0x3C, 0x27 on its other branch | decoded | Room 06 entry +0x0011; Room 73 local script 203 +0x0012; boot run (S5 corrected S4b's costume 39) | 2026-10-14 |
| Actors 10, 11 | the two men of script 73, costume 0x2B | decoded | Script 73 +0x0000, +0x0012 | 2026-10-14 |
| Actor 4 | Marcus in Room 1 | decoded | Script 1 +0x02DD | 2026-10-14 |

## Sounds and music

| Sound | Meaning (device parts, who starts it) | Evidence | Source | Date |
|---|---|---|---|---|
| 35 (0x23) | opening music (AdLib part); the opening ends only once its music timer has passed 105 (0x69) | decoded | Room 76 local script 201 +0x0023; Script 1 +0x01CD | 2026-10-14 |
| 30 | effect started by Room 92's answering script, ends before the next answer | decoded | Room 92 local script 204 +0x0005 | 2026-10-14 |
| 3 | the door closing (Script 27, the gym door's Close cutscene) | run (S6 sound route, Room 73) | Script 27 +0x002B | 2026-10-16 |
| 2 | the door opening (Script 26) | run (S6 sound route, Room 73) | Script 26 +0x002B | 2026-10-16 |
| 13 | Room 73: the light switch (twice: lights out, lights back) and the chain that takes the mallet back | run (S6 sound route) | Room 73 object 837 verb 0x06 +0x0002 and +0x0063 (256 colours: +0x02B0); Room 73 local script 205 +0x0058 | 2026-10-16 |
| (engine) | a music start (AD byte 2 = 0x80) ends the music playing (`Player_AD::startSound`), effects overlap | ScummVM source (16 Oct) | `ScummProfile.MusicStart` | 2026-10-16 |

Script census (S6 sound, 16 October): `startSound` 138 (256 colours 137), `stopSound` 86 (87), `isSoundRunning` 26, no `stopMusic`, `startMusic` or `soundKludge` in any global, entry, exit or local script.

## First-visit facts (S5)

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Room 92, the player's route | the explorer's boot clicks object 929 once and answers the four questions with the values the game computed (`var[0x063..0x066]`), derived from the answering script's own checks; labelled "Player input assumed" and "Player answer assumed" | decoded | Room 92 object 929 +0x0013; local script 204 +0x002E, +0x0044, +0x005A, +0x0070 | 2026-10-14 |
| `var[0x009]` (current lights) | starts 11 (base palette, room lights, actors' own colours); `lights a, b, 0` sets it. Eight `lights` instructions exist; three rooms start dark in the explorer's visit (47, 60, 66) | decoded | Script 5 +0x0000; room audit | 2026-10-14 |
| `roomColorLegacy` | 64 instructions set the room's colour table slot by slot (EGA palette changes by room entry scripts, e.g. Room 4 entry +0x0007, Room 92 entry +0x0007 for CGA) | decoded | Room 04 entry +0x0007; Room 92 entry +0x0007 | 2026-10-14 |
| Rooms whose first visit never rests | rooms 1, 6, 15, 20, 28, 32, 39, 44, 47, 60, 66, 79, 80, 83, 84, 94 keep changing within 2400 frames (ambient or long scenes); the state at the limit is shown | decoded | room audit (`.verification/indy3-objects-20261014`) | 2026-10-14 |
| Rooms a script leaves | rooms 37, 41, 43, 82 load another room during the visit; the state at the last frame in the room is shown | decoded | room audit | 2026-10-14 |

## VGA note: the 256-colour release (`Games/indy3 vga en`), where it differs

Read on 15 October 2026 (S4b Indy3Vga) from `00.LFL` and the room files, dialect `V3Indy3`, confirmed by a headless boot of the generic session (record: `.verification/indy3vga-session-20261015`). Only differences from the EGA rows above are listed; everything not listed was not compared.

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Layout | one plain `NN.LFL` per room with v4-style blocks (RO, OC, OI, BX with 18-byte boxes, CO, SO, LS, EN, EX), `00.LFL` the index ("0R", "0S", "0C", "0N", "0O"); 130 global scripts in the "0S" directory; local scripts numbered from 200 | decoded | `00.LFL`; `ScummV4Archive.LoadRoomFiles` | 2026-10-15 |
| Opening order | as EGA up to the lecture, but **no copy-protection room**: Script 1 loads rooms 76 (+0x01EA), 79 (+0x0208), 0 (+0x0223), 6 (+0x0248), 1 (+0x02DE) and 73 (+0x0461); the game has no Room 92 | decoded | Script 1 loadRoom instructions; room list of the index | 2026-10-15 |
| End of the boot | `doSentence 0x0A, 0x0342, 0x0000` at +0x0466, `endCutscene` +0x04B0, `chainScript 0x77` +0x04B1 (the EGA offsets differ, same instructions) | decoded | Script 1 +0x0461 to +0x04B7 | 2026-10-15 |
| Hook values at Room 73 | input script 4, sentence script 2, inventory script 12 (as EGA) | decoded | boot run, `var[0x020]`, `var[0x021]`, `var[0x022]` | 2026-10-15 |
| Costumes | 101 costumes, all format 0x58 (16 colours through the costume's table into the room palette); one byte per limb command in an animation | decoded | CO blocks; `ScummIndy3VgaSessionTests` | 2026-10-15 |
| Interface scripts | Scripts 2, 12, 16, 18, 20, 23, 24, 27, 29, 96 and 113 are the same instructions as EGA (so F5 = 319 chains to the save/load Script 9 as there); Script 4 has one more branch (next row), its later offsets are EGA's + 0x0A (doSentence +0x0267, walkActorTo +0x0288) | decoded | instruction-by-instruction comparison of the two releases (S6) | 2026-10-16 |
| Script 4 +0x006F | a click on an inventory item (verbs 0x65 to 0x6A) while the verb is Talk (`var[0x03B]` = 0x0D) chains to Script 3 | decoded | Script 4 +0x006F `isEqual var[0x03B], 0x000D`, +0x0076 `chainScript 0x03` | 2026-10-16 |
| Script 9 | the save/load screen adds the hint-book order lines (`drawBox`, three centred prints in colour 14) | decoded | Script 9 | 2026-10-16 |
| Script 119 +0x01A2 | the ego greets the player in Room 73: "Hi, I'm Indiana Jones. Welcome to my game." then the F5 save hint (absent from EGA) | decoded | Script 119 +0x01A2 printEgo; route run (frame 53) | 2026-10-16 |
| Room 73 object 837 verb 0x06 (light switch) | darkens the gym with `roomOps roomPalette` loops instead of `lights 0`: in each of the rows 0x00, 0x50, 0xD0, 0xE0, 0xF0 the shadow slots +0 to +14 show 0xB0–0xBE, in the rows 0x10–0x40, 0x60, 0x70 they show 0xC0–0xCE (+0x0004 to +0x0244); `beginOverride` +0x024B, skip to +0x02A7; restores slot i to i for 0–254 (+0x02B2); Toby's talk colour is 15 during the cutscene (+0x025B) and 2 after (+0x02A7) | decoded | Room 73 object 837 verb 0x06; `ScummIndy3VgaPlayTests` | 2026-10-16 |
| Room 73 and Room 1 objects | the same ids, boxes, parents and walk points as EGA | decoded | object directories of both releases | 2026-10-16 |
| Verb colours at Room 73 | as EGA: verbs 2 / highlight 14 / dim 8, sentence line 3 / 11, inventory 5 / 13, arrows 1 / 9 (raw indices into the 256-colour room palette, whose first 16 entries are the EGA colours); text colours in the global scripts are 0x00–0x0F and 0x8x (shadow), the same values as EGA | decoded | boot run; global scripts' `color`/`talkColor` operands | 2026-10-16 |
## Open questions

- Who sets the hook values at Room 73 (entry 5 and 6, exit 7, input 4).
- The meaning of the object classes the sentence script tests.
- Script 23 (cutscene end) follows the ego with `actorFollowCamera` and, with the ego in another room, ScummVM's `setCameraFollows` loads that room; the session does not model that load (Rooms 18, 78, 80 in the explorer's visit).
- Rooms 20, 28 and 32 show a mostly black picture in the explorer's visit (the picture of ScummVM was not compared at runtime); their ambient scripts never rest.
- Script 131 (+0x0038) starts script 132 with arguments from unknown variables in Rooms 12, 13 and 77; Room 03 entry +0x006F starts local script 200 the same way; Room 33's entry depends on `bit[0x1517]`: the story state the visit does not have.
