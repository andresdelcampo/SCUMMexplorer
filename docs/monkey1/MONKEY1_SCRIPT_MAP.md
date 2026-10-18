# The Secret of Monkey Island script map

This map records **The Secret of Monkey Island-specific** script roles, variable meanings, verb ids and room behaviour. Engine-defined variables and opcode rules are in the [SCUMM script reference](../engine/SCUMM_SCRIPT_MAP.md); the engine's boot sequence is in [SCUMM_BOOT_SEQUENCE.md](../engine/SCUMM_BOOT_SEQUENCE.md) (v4 section).

## Edition scope

All rows come from the **four-disk 256-colour floppy release** (`Games/monkey1 ega en`: `DISK01.LEC` to `DISK04.LEC` and `000.LFL`; the script's own version string says "The Secret of Monkey Island (VGA) -- 1.0  12/3/90"; the folder name says EGA, the data is VGA), SCUMM v4, dialect `V4`, read on 14 October 2026 (S4b MonkeyFloppy). Numbers are not transferred to other editions (R-14.2): the CD release (`MonkeyCd`, v5) has its own block in S4b of that family. The eight-disk 16-colour Spanish release (`monkey1 ega es`) uses the same ids, rooms, verbs and sound ids on every checked path (S9, 17 October 2026, [record](../../.verification/monkey1-editions-20261017/README.md)); its offsets move with its texts, and rows marked "Spanish" below are its real differences. Global scripts are numbered below 200, local scripts of a room from 200.

## Row format (every table below)

Each row: **item** | **meaning** | **evidence** (`decoded` / `engine` / `reading` / `play`, R-2.3) | **source** (`Script NN +0xOFFS`, `Room NN local script NN`, `Room NN object NN verb 0xHH`) | **date**. A row without a source is not allowed. Unnamed things keep their raw form. `decoded` here means read from the disassembly and confirmed by a headless run of the generic session; `play` (from S6, 16 October) means seen on the played route of the [S6 record](../../.verification/monkey1-play-20261016/README.md), clicks and keys going through the game's own input script.

## Boot and hooks

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Script 1 | boot script: AdLib is `var[0x030]` = 3 when the engine says CMS (2); sets the version string, the "Game Paused", "Insert Disk %c" and save/load strings, the charsets 2 and 4, loads and locks the interface scripts and sounds, picks the sound-id table by `var[0x030]` (4 = Roland: 0x97..; else 0x65..), hooks and verbs; then the opening | decoded | Script 1 +0x0000 to +0x0741 | 2026-10-14 |
| Hook values | cutscene hooks `var[0x023]` = 18, `var[0x024]` = 19; sentence script `var[0x021]` = 2; inventory script `var[0x022]` = 9; entry hooks `var[0x01C]`, `var[0x01D]` = 5, 6; exit hook `var[0x01E]` = 7; input script `var[0x020]` = 4 (during the protection 153, then 155; during the credits 0xC9); skip key `var[0x018]` = 0x1B; restart key 0x142, pause key 0x20 | decoded | Script 1 +0x0041 to +0x0604; Script 151 +0x0022; Script 152 +0x0111; Script 149 +0x0002 | 2026-10-14 |
| Order of the opening | `startScript 0x98` (152, the protection) and wait until it ends; `startScript 0x95` (149, title and credits as a cutscene) and wait; `loadRoom 0`; `startScript 0x16` (22, the verbs); ego and actors set; with the boot parameter 0, `putActorInRoom` of the ego into Room 38 at (320,72), `startScript 0x17` (23, the interface loop) and **`actorFollowCamera` of the ego, which starts Room 38's scene** (`setCameraFollows`) | decoded | Script 1 +0x0753 to +0x0784, +0x0786 to +0x0E29 | 2026-10-14 |
| Boot parameter | `local[0]` of script 1 (0 in a normal start): other values jump to debug and test start points (`isEqual local[0], 0x1A6F`, 0x0457, 0x04BC, … put the ego in later rooms with the objects of that point of the story) | decoded | Script 1 +0x07F5 to +0x0E05 | 2026-10-14 |
| First playable room | Room 33: Room 38's opening cutscene ends by `startObject` 494 verb 0x0A whose fallback verb loads Room 96; Room 96's local script 200 plays a sound and then `loadRoomWithEgo` object 435 into Room 33 walking to (346,133) with user input back on | decoded | Room 38 local script 203 +0x02CA; Room 38 object 494 fallback verb +0x0044; Room 96 local script 200 +0x0020 | 2026-10-14 |
| Copy protection (Dial-A-Pirate) | Script 152 loads Room 90, runs script 151 ("WAIT! Before we begin ^ let's have a quick history quiz.", "Press ENTER to continue") and asks "When was this pirate hanged in <place>?" with four digits typed; the expected year is kept in `var[0x10A]` (+0x00EE) before it waits; a wrong answer repeats with new symbols and the fourth wrong one quits (`systemOps quit`, +0x002E) | decoded | Script 152 +0x0000 to +0x0213; Script 151 +0x0000 to +0x020E | 2026-10-14 |
| Pseudo rooms | script 1 maps pseudo rooms 201 to 220 to Room 58 (`pseudoRoom 58, [73..92]` with bit 7) and 130 to 132 to Room 1 (`pseudoRoom 1, [2, 3, 4]`); every load of those rooms in the game's own code uses the pseudo number, and their entry scripts tell the uses apart by `VAR_ROOM` | decoded | Script 1 +0x06C4 to +0x0718 | 2026-10-15 |
| Input scripts of the protection | 153 (area 1: `var[0x069]` = local 1; any other area: `var[0x069]` = 0xFFFF) and 155 (area 4 only: a key '0' to '9' sets `var[0x069]` to the digit, Backspace sets 0x0B) | decoded | Script 153 +0x0000; Script 155 +0x0000 | 2026-10-14 |

## Engine variables as this game uses them

| Variable | This game's use | Evidence | Source | Date |
|---|---|---|---|---|
| `var[0x031]` (video mode) | 4 (CGA) picks the colours of the protection and message screens | decoded | Script 151 +0x0000; Script 152 +0x0000 | 2026-10-14 |
| `var[0x030]` (sound card) | 3 (AdLib) and 4 (Roland) pick which music-timer marks the credits wait on (0x08, 0x0C, 0x1A, … up to 0xF7 for AdLib; 0x00.. for the others) | decoded | Script 149 +0x0020 to +0x0A2D | 2026-10-14 |
| `var[0x00E]` (music timer) | the credits' lines wait until it passes fixed marks while the music (sound `var[0x057]`) plays | decoded | Script 149 +0x0027 to +0x09B0 | 2026-10-14 |
| `var[0x027]` (debug mode, v4) | a start with a non-zero boot parameter needs it non-zero, otherwise script 1 prints "Debugging mode not active" and quits; it is 0 in a normal run | decoded | Script 1 +0x07FC to +0x0827 | 2026-10-14 |
| `var[0x004]` (`VAR_ROOM`) | the number the room was loaded as: Room 58's entry draws one of twenty path layouts for `VAR_ROOM` 201 to 220 (none under 58), Room 1's entry sets its walk boxes and objects for 130, 131 or 132; Room 30's local script 212 compares `getActorRoom` of actor 11 with it | decoded | Room 58 entry +0x01E1 to +0x0979; Room 1 entry +0x0066 to +0x011A; Room 30 local script 212 +0x0040 | 2026-10-15 |
| Bit variables | stored apart from the word variables (v4 `_bitVars`): the game's bits 0x040 to 0x04F are not `VAR_ROOM`; before 15 October the session aliased them and Room 30's entry left `VAR_ROOM` = 542 | engine | ScummVM `readVar`/`writeVar` (script.cpp); Room 30 first visit | 2026-10-15 |
| `var[0x020]` (input script) | 4 in play: its first argument is the click area (1 verb, 2 room, 4 key); 14 during a conversation (Room 28 local script 220); in the SCUMM Bar the room's own input script, Room 28 local script 202 (set by the entry +0x009A), which chains to 4 | decoded, play | Script 1 +0x060E; Script 4 +0x0034; Room 28 entry +0x009A; route of S6 | 2026-10-16 |
| `var[0x032]` (`VAR_MAINMENU_KEY`) | 0x13F (F5): the engine's save/load menu key; the explorer does not deliver it in play | decoded | Script 1 +0x077F | 2026-10-16 |
| `var[0x034]` (`VAR_CURSORSTATE`), `var[0x035]` (`VAR_USERPUT`) | the interface loop runs its object finder only while `var[0x034]` is above 0 (the engine copies `_cursor.state` there after every `cursorCommand`) | decoded, play | Script 23 +0x0000 | 2026-10-16 |

## Game variables and bits

| Name | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| `var[0x069]` | last key or click value the protection reads (0 none, 0xFFFF a key that is no digit) | decoded | Script 151 +0x00BF; Script 152 +0x0138; Script 153; Script 155 | 2026-10-14 |
| `var[0x10A]` | the right year of the protection question (a character of the string table 0x1D plus 0x5E2) | decoded | Script 152 +0x00CD to +0x00EE | 2026-10-14 |
| `var[0x0B3]` | the year typed so far (four digits weighted 1000, 100, 10, 1) | decoded | Script 152 +0x015E, +0x01C1 | 2026-10-14 |
| `var[0x127]`, `var[0x128]` | the symbol pair chosen at random for the question (position in the string table = `var[0x127]` * 7 + `var[0x128]`) | decoded | Script 150 +0x002D to +0x0057; Script 152 +0x00CD | 2026-10-14 |
| `var[0x15F]` | counter of open cutscenes (+1 at the cutscene start script, −1 at the end script) | decoded | Script 18 +0x0000; Script 19 +0x0000 | 2026-10-14 |
| `var[0x165]` | tested for 1 where the highlighted verb's colour is set to 4 instead of 2 (a mode flag; its meaning is not read) | decoded | Script 12 +0x004B; Script 18 +0x0028 | 2026-10-14 |
| `var[0x06B]` | the verb of the sentence being built: the interface loop compares it with 13 (Talk to) and 3 (Give); a verb click sets it (Script 4 +0x0368) | decoded | Script 23 +0x003F, +0x006A; Script 4 +0x0368 | 2026-10-16 |
| `var[0x06C]`, `var[0x06D]` | the first and second object of the sentence being built: the interface loop puts the object under the pointer in `var[0x06C]` (`var[0x06D]` while a second object is awaited, `var[0x06E]` non-zero) | decoded, play | Script 23 +0x00C7..+0x010B | 2026-10-16 |
| `var[0x0B4]`, `var[0x0B5]`, `var[0x0B6]` | the last object hovered; the default verb of the hovered object (each object's verb 0x5A sets it, e.g. the poster 9, a closed door 1, an open door 2); the verb drawn highlighted (colour 0x0E, back to 2 or 4 when it changes) | decoded, play | Script 23 +0x0110..+0x0142; Room 33 objects 437, 438 verb 0x5A | 2026-10-16 |
| `var[0x0C1]` | the conversation choice: Script 14 puts the clicked verb (0x78 to 0x80) there after Guybrush says it; the conversation waits on it | decoded, play | Script 14 +0x0105; Room 28 local script 220 | 2026-10-16 |
| `var[0x0C3]` | trials done ("Trials completed", debug key '#'): the bar's mugs and the important-looking pirates' scripts change above 3 | decoded | Script 4 +0x0139; Room 28 entry +0x0034; Room 28 object 332 verb 0x0D | 2026-10-16 |
| `var[0x0E4]`, `var[0x0E5]`, `var[0x0F9..]`, `bit[0x180..]` | the next choice's line y and key, the choice's object, whether it was said already: Script 32 resets them, Script 14 reads them | decoded | Script 32; Script 14 +0x0013..+0x0078 | 2026-10-16 |
| `bit[0x223]` | 0 (the start): the two-button interface, a right click (mode 2) on an object uses its default verb `var[0x0B5]`; 1: the one-button interface, a left click with "Walk to" selected uses the default verb; key 0x29 ')' toggles it and prints "Two button userface ON" / "One button userface ON" | decoded | Script 4 +0x0000..+0x002F, +0x0216 | 2026-10-16 |
| Right click (default verb) | `runInputScript(2, 0, 2)` (engine version 4 right click, R-7.16): with `bit[0x223]` = 0, `local[0x002]` = 2, no second object pending (`var[0x06E]` = 0) and a default verb `var[0x0B5]` (Script 23 +0x0128..+0x0142, from the hovered object's verb 0x5A), the sentence verb `var[0x06B]` becomes the default verb; the area 2 path then runs `doSentence var[0x06B], var[0x06C], var[0x06D]` and sentence Script 2 the object's verb (the poster 438: verb 0x09, its look) | decoded, play | Script 4 +0x0000..+0x0016, +0x03E0..+0x044B; `ScummMonkeyFloppyPlayTests.ARightClickOnAnObjectRunsItsDefaultVerb` | 2026-10-16 |

## Global scripts

| Script | Role | Evidence | Source | Date |
|---|---|---|---|---|
| 1 | boot | decoded | Script 1 | 2026-10-14 |
| 2 | the sentence script (`var[0x021]`): walks the ego to the object (`walkActorToObject`, `waitFor`) and starts the object's code for the verb (`startObject local[2], local[0]`, +0x039D); without verb code it chains to Script 3; Give to an actor and pick-ups of held objects have their own branches | decoded, play | Script 2 +0x0000..+0x03B1 | 2026-10-16 |
| 3 | the answer when an object has no code for the verb ("I can't…"), chained by Script 2 | decoded | Script 2 +0x0137; Script 3 | 2026-10-16 |
| 4 | the input script of play: its first argument is the click area (4 tests a key: function keys and others, with debug-mode variants; other keys go to Script 20); area 1 sets the verb (+0x0368) or scrolls the inventory (0x6B/0x6C, +0x0307) or takes an inventory object (0x65..0x6A); area 2 runs `doSentence var[0x06B], var[0x06C], var[0x06D]` (+0x044B) when the sentence is complete, else walks to the pointer with "Walk to" (+0x0475). Key 0x17 (Ctrl+W) asks "Are you sure you want to win? (Y/N)" and starts Script 127 | decoded, play | Script 4 +0x0034..+0x049E | 2026-10-16 |
| 9 | the inventory script (`var[0x022]`): names verbs 0x65 to 0x6A with code 6 of `var[0x086..0x08B]` (the held objects from `findInventory`), turns on the filled ones and the arrows | decoded, play | Script 9 +0x0048..+0x0105 | 2026-10-16 |
| 14 | the input script of a conversation: a click on a choice verb (0x78 to 0x80) turns the choices off, makes Guybrush say it (`printEgo` code 5 of the verb, +0x00F7) and puts it in `var[0x0C1]` | decoded, play | Script 14 +0x007F..+0x0105 | 2026-10-16 |
| 17 | saves and restores the verbs around cutscenes (cases 1, 2) and creates the conversation verbs 0x78 to 0x80 with charset 1, keys '1' to '9' (case 5) | decoded | Script 17 +0x008A..+0x0172 | 2026-10-16 |
| 20 | keys the input script does not handle: Ctrl+V (0x16) prints the version string | decoded | Script 20 | 2026-10-16 |
| 32 | resets a conversation's line y (`var[0x0E4]` = 0x91), key (`var[0x0E5]` = '1') and choice tables, cursor and input on | decoded | Script 32 | 2026-10-16 |
| 12 | sets the sentence line (verb 0x64): blank when `var[0x06B]` is 100, else its name built from string codes, colour 3 (4 when `var[0x165]` is 1) | decoded | Script 12 | 2026-10-14 |
| 18, 19 | cutscene start and end hooks: soft-off or soft-on of cursor and user input, freeze or unfreeze scripts, save or restore the verbs for cutscene type 1 or 3 | decoded | Script 18; Script 19 | 2026-10-14 |
| 25, 26 | open and close a door, each wrapped in a short cutscene (25: `cutscene` +0x0019 .. `endCutscene` +0x0036; 26: +0x0010 .. +0x002D); rooms' background scripts call them too (the SCUMM Bar's kitchen door about 480 frames after a visit; High Street's doors), so such a cutscene is not the clicked object's | decoded, run (S7 probe) | Script 25; Script 26; `.verification/monkey1-cutscenes-20261017` | 2026-10-17 |
| 60 | Room 37's arrival scene: two cutscenes (+0x0010 .. +0x0031, +0x003C .. +0x0076), "Hey!  I don't like visitors!  Who are you?"; begins from a fresh start (73 frames) | decoded (scan), run (S7) | Script 60; Room 37 entry | 2026-10-17 |
| 117 | the first exit of the SCUMM Bar ("Meanwhile^", 17 lines, 3 room loads), started by door 322's Walk to only while the door is open (`ifState 0x0142, 0x01` +0x0000) and once (`bit[0x1BE]` +0x0009); a fresh start (door closed) begins nothing | decoded (scan, verb code), run (not begun) | Room 28 object 322 verb 0x0A +0x0000..+0x0013; Script 117 | 2026-10-17 |
| 22 | defines the twelve verbs (Open, Close, Push, Pull, Walk to, Pick up, Talk to, Give, Use, Look at, Turn on, Turn off), the hidden insult verbs and the sentence and inventory verbs 0x64 to 0x6C | decoded | Script 22 | 2026-10-14 |
| 23 | the interface loop: every frame it finds the object under the virtual mouse, updates the sentence line and the highlighted verb | decoded | Script 23 | 2026-10-14 |
| 149 | the title and the credits as one cutscene in Room 10, timed by the music timer | decoded | Script 149 | 2026-10-14 |
| 150 | picks the protection's symbol pair at random | decoded | Script 150 | 2026-10-14 |
| 151, 152 | the protection screen: introduction and the question | decoded | Script 151; Script 152 | 2026-10-14 |
| 156 | a loop that only calls `getRandomNr` every frame (keeps the random sequence moving) | decoded | Script 156 | 2026-10-14 |

## Rooms

| Room | What it is, entry/exit script facts, local scripts | Evidence | Source | Date |
|---|---|---|---|---|
| 90 | the protection screen (`loadRoom 0x5A` by script 152) | decoded | Script 152 +0x0030 | 2026-10-14 |
| 10 | title and credits, entered by the credits cutscene | decoded | Script 149 +0x0051 | 2026-10-14 |
| 38 | the opening scene: the entry script starts object 497 verb 0xFA; local script 203 is a cutscene that ends by walking to object 494 | decoded | Room 38 entry +0x003B; Room 38 local script 203 | 2026-10-14 |
| 96 | title music: user input off, sound `var[0x051]` until it ends (skippable by the override), then the dock | decoded | Room 96 entry +0x0000; Room 96 local script 200 | 2026-10-14 |
| 87 (Spanish) | the entry starts actor 9 (costume 0x31, elevation -0x6C, put at (156, 0) in Room 87) every time, locking and loading the costume (+0x000D, +0x0010; the exit script unlocks it); English does it only when script 0x77 is not running (`isScriptRunning` +0x000D, branch +0x0011) | decoded | Room 87 entry +0x0000..+0x003B (Spanish), +0x0000..+0x003E (English); Room 87 exit +0x0000 (Spanish) | 2026-10-17 |
| 33 | the first playable room (a village dock at night; the ego's costume 1 stands at (346,133)); its entry script starts scripts 0xC9, 0xCA and 0xCB depending on `var[0x065]` and `var[0x0C3]`; local script 202 changes the ego's walk speed by walk box. First visit (S5): no object image active, the ego only | decoded | Room 33 entry +0x0000 to +0x0056; Room 33 local script 202 | 2026-10-15 |
| 33 (objects) | cliffside 435 → Room 38; archway 436 → Room 35 (`loadRoomWithEgo 0x1CA, 0x23`); door 437 (Open/Close start Scripts 25/26; Walk to, when open, `loadRoomWithEgo 0x142, 0x1C` → Room 28); poster 438 (the first Look at is a cutscene with an override, +0x00CF..+0x0134; later looks are lines chosen by bits 0x130, 0x055, 0x197) | decoded, play | Room 33 objects 435 to 438 | 2026-10-16 |
| 28 | the SCUMM Bar: its entry sets the room's input script 202 (chains to 4); the mugs 373 to 377 are untouchable (class 24 from the index; the entry sets 24 on owned ones) until the trials; the important-looking pirates 332: Talk to starts local script 220 (the "I want to be a pirate" conversation) while `var[0x0C3]` is not above 3, Script 59 otherwise; door 322 (open) leads back to Room 33, its first use runs Script 117 once (`bit[0x1BE]`) | decoded, play | Room 28 entry; Room 28 objects 322, 332, 373 to 377; route of S6 | 2026-10-16 |
| 33 (cutscenes, S7) | the poster's first Look at (object 438 verb 0x09) is the room's scene in the list (`Room 33 · Look at poster`); begins from a fresh start (74 frames, "Re-elect Governor Marley." on screen) | run (S7 headless run, window) | Room 33 object 438 verb 0x09 +0x00CF..+0x0134 | 2026-10-17 |
| 28 (cutscenes, S7) | the important-looking pirates' Look at and Talk to reach local script 220 (one entry, 28 lines): its fresh run begins the conversation's cutscene (23 frames) and hands over to the choices; Use mug and Open door also reach global scripts 25/26 (see there) | decoded (scan), run (S7) | Room 28 object 332 verbs 0x09, 0x0D; Room 28 local script 220 | 2026-10-17 |
| 19 | the ship's deck (Part II): the entry's local script 200 is a cutscene (+0x0003 .. +0x039A, with local 206 13 lines, "First of all^") that goes on to Room 7; begins from a fresh start (624 frames) | run (S7 headless run) | Room 19 entry; Room 19 local scripts 200, 206 | 2026-10-17 |
| 37 | its entry starts Script 60 (see Global scripts), local script 201 and Script 25; begins from a fresh start | run (S7 headless run, window) | Room 37 entry | 2026-10-17 |
| 35 | Mêlée's high street (from the dock's archway): door 452, when open, leads to Room 29 (`loadRoomWithEgo 0x17D, 0x1D`) | decoded, play | Room 35 object 452 verb 0x0A +0x002B..+0x0031 | 2026-10-16 |
| 29 | the Voodoo shop: the rubber chicken 391 is taken with Pick up (`pickupObject var[0x007]`); Guybrush says "Maybe no one will miss just this one thing…" | decoded, play | Room 29 object 391 verb 0x0B; route of S6 | 2026-10-16 |
| 58 | a forest maze loaded only as pseudo rooms 201 to 220: the entry `drawObject`s nine or ten path tiles (objects 638 to 669) at strip positions per `VAR_ROOM`, sets box flags and the untouchable class (24) of the edge exits; local scripts 201 animate actors 3 and 4 (costume 114). The map (Room 85) object 866 enters it as 218 with the ego at object 668 | decoded | Room 58 entry +0x0000 to +0x09B2; Room 85 object 866 verb 0x0A +0x0000 | 2026-10-15 |
| 1 | a beach loaded only as pseudo rooms 130 to 132 (three parts of one beach picture); as 130 it sets objects 21 to 23 to state 1 and object 296 when `bit[0x215]` is 0; no `loadRoomWithEgo` with literal operands enters it | decoded | Room 1 entry +0x0066 to +0x00A0 | 2026-10-15 |
| 30 | a shop: the entry starts local scripts 205 (three random prices in `var[0x0DC..0x0DE]`), 209 or 208 (objects 412 and 932 on or off by `var[0x064]`) and 203; the shopkeeper is actor 11 (costume 28) | decoded | Room 30 entry +0x008B to +0x00DA; Room 30 local scripts 203, 205, 208, 209 | 2026-10-15 |
| 70 | objects 746 to 748 are set to state 1 by the entry only when `bit[0x17F]` is set (later in the story) | decoded | Room 70 entry +0x006B to +0x0078 | 2026-10-15 |

## Verbs and the input chain

| Verb / script | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| The twelve verbs | Open 1, Close 2, Push 6, Pull 7, Walk to 10, Pick up 11, Talk to 13, Give 3, Use 8, Look at 9, Turn on 4, Turn off 5; each has a key (o, c, s, y, w, p, t, g, u, l, n, f) | decoded | Script 22 +0x0003 to +0x00D8 | 2026-10-14 |
| Verbs 0x64, 0x65 to 0x6A, 0x6B, 0x6C | 0x64 the sentence line; 0x65 to 0x6A the six inventory slots (keys '1' to '6'); 0x6B and 0x6C the inventory arrows (keys q and a) | decoded | Script 22 +0x022B to +0x0303 | 2026-10-14 |
| Verbs 0x78 to 0x80 | the conversation choices, created by Script 17 case 5 (charset 1, colour 2, highlight 0x0E, keys '1' to '9'), named and placed one under the other by the conversation script; a click goes to Script 14 | decoded, play | Script 17 +0x00C7; Room 28 local script 220; Script 14 | 2026-10-16 |
| The chain in play | a click: `runInputScript(area, value, 1)` → Script 4 (or the room's own input script) → `doSentence` → sentence Script 2 → the object's verb code; Script 23 runs every frame for the hover, the default verb and the sentence line (Script 12) | play | route of S6 ([record](../../.verification/monkey1-play-20261016/README.md)) | 2026-10-16 |

## Objects and classes

| Object / class | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Class 0x8D | with verb 13 (Talk to) selected, a hovered object of this class is replaced by the actor found at the pointer, and nothing is hovered if that actor is not of the class either | decoded | Script 23 +0x003F to +0x0065 | 2026-10-14 |
| Class 0x85 | the same test for verb 3 (Give) when `var[0x06E]` is 0 | decoded | Script 23 +0x006A to +0x0095 | 2026-10-14 |

## Actors and costumes

| Actor / costume | Meaning (look, palette, talk colour, who wears what) | Evidence | Source | Date |
|---|---|---|---|---|
| Actor 1, costume 1 | the ego ("Guybrush"), talk colour 0x0F | decoded | Script 1 +0x07D7 to +0x07E3 | 2026-10-14 |
| Actors 2, 3 (costumes 33, 48), 7 to 9 (costume 59) | actors of the opening and the credits scene (Rooms 38 and 10) | decoded | session actor table after the boot, set by Room 38 local script 201 and Room 10 local script 202 | 2026-10-14 |

## Sounds and music

| Sound | Meaning (device parts, who starts it) | Evidence | Source | Date |
|---|---|---|---|---|
| `var[0x057]` = 110 (non-Roland table) | the credits' music, started at the start of the credits and awaited to its end | decoded | Script 1 +0x0498; Script 149 +0x001D, +0x0A2E | 2026-10-14 |
| `var[0x051]` = 104 (non-Roland table) | Room 96's title music, awaited to its end | decoded | Script 1 +0x047A; Room 96 local script 200 +0x0009 | 2026-10-14 |
| `var[0x055]` = 108 | Room 38's music (the lookout scene of the opening); no script stops it: the engine's `triggerSound` workaround stops it when the title music starts | run (S6 sound) | Room 38 local script 203 +0x000E | 2026-10-16 |
| `var[0x04B]` = 101 (0x65; Roland table 0x97) | the SCUMM Bar's music: Room 28's entry starts it only while `isSoundRunning` says it is silent; the bar's door stops it as Guybrush leaves; the pirates' conversation stops and restarts it | run (S6 sound route) | Script 1 +0x0466 (+0x0404 Roland); Room 28 entry +0x0080, +0x008A; Room 28 object 322 verb 0x0A +0x0006; Room 28 local script 220 +0x162A, +0x168E | 2026-10-16 |
| 2 | a door opening (dock door, street door) | run (S6 sound route) | Script 25 +0x0034 | 2026-10-16 |
| 3 | a door closing (Room 29) | run (S6 sound route) | Script 26 +0x002B | 2026-10-16 |
| 100 | played by Script 117 on the first exit from the SCUMM Bar (Rooms 70, 72) | run (S6 sound route) | Script 117 +0x0031, stopped +0x053E | 2026-10-16 |
| 115 | Room 29's music (the Voodoo shop), started by its local script 200; Room 70's entry stops it (and 111) | run (S6 sound route) | Room 29 local script 200 +0x0021; Room 70 entry +0x002C, +0x002F | 2026-10-16 |
| `var[0x060]` = 117, `var[0x061]` = 118 (0x75, Roland 0xA7) | two pieces Script 140 plays one after the other (`isSoundRunning` +0x0004 between them); started by Script 135 and Room 83; Room 33's exit stops both | decoded | Script 1 +0x04BB (+0x0459 Roland); Script 140 +0x0000..+0x000E; Room 33 exit +0x0002, +0x0005 | 2026-10-16 |
| (engine) | any music start (AD byte 2 = 0x80) stops every sound first: ScummVM runs this game through iMuse, whose `Sound::triggerSound` has a Monkey EGA/VGA workaround | ScummVM source (16 Oct) | `ScummProfile.MusicStart` | 2026-10-16 |

Script census (S6 sound, 16 October): `startSound` 135, `stopSound` 68, `isSoundRunning` 24, no `stopMusic`, `startMusic` or `soundKludge` in any global, entry, exit or local script.

## Open questions

- The AdLib music timer the session gives the credits is an estimate (iMuse in the engine; the explorer plays the song's `AD` block at the browsed disks' half tempo). The length of the credits in the session was not measured against the bundled ScummVM (S5/S6).
- The ego stands at (346,133) in Room 33 after the opening; the first-visit objects were compared with the room's own scripts at source level only (S5 record), not with a ScummVM picture.
- Which of Room 1's pseudo numbers (130 to 132) comes first in play is not decoded (the map rooms reach it through scripts with non-literal arguments); the first visit uses 130, labelled.
- The index (`RN` block) names none of the rooms the boot enters (the scanner shows "Room NN"); Rooms 90, 10, 38 and 96 are described above by what their scripts do.
- The "insert disk" and "unable to find" strings of script 1 (+0x00E0, +0x0110) are never reached: all disks are assumed inserted.

## CD release (MonkeyCd, SCUMM v5)

Rows of this section come from the **CD release** only (`Games/monkey1 vga en`: `The_Secret_of_Monkey_Island.cue`/`.bin`, `MONKEY.000`/`MONKEY.001` on the data track, music on 24 audio tracks), SCUMM v5, dialect `V5`, read on 17 October 2026 (S4b MonkeyCd, [record](../../.verification/monkeycd-session-20261017/README.md)). Script numbers differ from the floppy's (R-14.2): no row of the sections above applies to the CD unless repeated here. Same row format.

### Boot and hooks (CD)

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Script 1 | boot script: clears `var[0x049]`, `var[0x03C]`, sets `var[0x033]` = 1, `var[0x006]` = 2, cursor and input off, builds strings 0x16, 0x15, 0x25, the cutscene skip key `var[0x018]` = 0x1B, restart 0x142, pause 0x20, main-menu key `var[0x032]` = 0 (no save/load key at this point), loads charsets 2 and 4; then the opening | decoded | Script 1 +0x0000 to +0x00A9 | 2026-10-17 |
| No copy protection | the boot enters no protection room and waits for no input: rooms entered 0, 10, 0, 0, 38 (the floppy's Room 90 and its Script 152 question are absent; the CD's Script 152 is the title credits) | decoded | boot trace of the record | 2026-10-17 |
| Script 152 | title and credits as a cutscene (`var[0x020]` = 0xC9 during it): loads Room 0 then Room 10, `startSound 0x6E` (sound 110, CD track 17) and waits until `var[0x00E]` (the CD music timer) passes 0xFA before the credits (`startScript 0xCC`, "Deep in the Caribbean", "The Island of Mêlée") | decoded | Script 152 +0x0000 to +0x00D6 | 2026-10-17 |
| Opening | Room 38's entry starts object 488 verb 0xFA, whose local script 203 is the opening cutscene; sound 108 (CD track 17 from frame 7173) plays; after `endCutscene` local script 203 starts object 486 verb 0x0B | decoded | Room 38 entry +0x0040; Room 38 local script 203 +0x0000 to +0x02CB | 2026-10-17 |
| First playable room | **Room 38 (the lookout)**: after the opening the player has control while object 486's fallback verb waits for sound 108 (`isSoundRunning 0x6C`, +0x0045) with `bit[0x18B]` = 0; when the track has ended it loads Room 96 (room name "part1"), whose local script 200 puts the ego into Room 33 at (346,133) | decoded | Room 38 object 486 fallback verb +0x003A to +0x0050; Room 96 local script 200 +0x003B | 2026-10-17 |

### Engine variables as this game uses them (CD)

| Variable | This game's use | Evidence | Source | Date |
|---|---|---|---|---|
| `var[0x00E]` (music timer) | jiffies since the CD track started (`playCDTrack` resets it); the credits wait for it to pass 0xFA | decoded, engine | Script 152 +0x0055; ScummVM soundcd.cpp | 2026-10-17 |
| `var[0x04A]` (74) | 1225 at start (ScummVM `resetScummVars` for `GID_MONKEY`); Script 176 compares it with 0x04B0..0x04E2 (1200..1250) | decoded, engine | Script 176 +0x0017, +0x001E; vars.cpp | 2026-10-17 |
| `var[0x020]` (input script) | 0xC9 during the credits (Script 152 +0x0002), 4 after the boot | decoded | Script 152 +0x0002; boot trace | 2026-10-17 |

### Sounds and music (CD)

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Sound 110 | CD track 17 (disc track 18) from its start, once: the title credits' music | decoded | `SOUN` 110 (Room 10); Script 152 +0x0049 | 2026-10-17 |
| Sound 108 | CD track 17 from frame 7173 to the end, once: the lookout's music; its end leads on to Room 96 | decoded | `SOUN` 108 (Room 38); Room 38 object 486 fallback verb +0x0045 | 2026-10-17 |
| Sounds 104, 122 | CD track 6 (Room 96), CD track 22 looping (the dock) | decoded | `SOUN` 104, 122; boot trace with 900 more frames | 2026-10-17 |
| Sounds 101, 100, 115 | CD track 8 looping (the SCUMM Bar, Room 28 entry +0x0071, stopped by the bar door's verb 0x0B +0x000C); CD track 12 looping (Script 120, the bar exit's flashback in Rooms 70 and 72, +0x0036 and its stop +0x053B); CD track 1 looping (the voodoo shop, Room 29 local script 200 +0x0021) | decoded | `SOUN` 101, 100, 115; `ScummMonkeyCdPlayTests` route | 2026-10-17 |
| Sounds 2, 3 | not CD tracks: iMuse sounds of the resource file (door creak, Script 25 +0x003A; Script 26 +0x0031 in Room 29) | decoded | `SOUN` 2, 3 have `SOU ` parts; route | 2026-10-17 |
| Room 33 exit script | stops sounds 122, 117, 118 (+0x0000, +0x0004, +0x0006) | decoded | Room 33 exit script | 2026-10-17 |

### Verbs and the input chain (CD)

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Verb ids | 2 Open, 3 Close, 4 Give, 5 Push, 6 Pull, 7 Use, 8 Look at, 9 Pick up, 0x0A Talk to, 0x0B Walk to (off, the default), 0x64 the sentence line, 0x78 to 0x80 dialogue lines, 0xC8 to 0xCF inventory slots, 0xD0/0xD1 inventory arrows; the floppy's numbers differ | decoded | Script 22; verbs after the boot (`ScummMonkeyCdPlayTests`) | 2026-10-17 |
| Script 22 | builds the nine text verbs with charset 6 (`cursorCommand charsetSet 6`, then `charsetColors [0, 6, 2]` at +0x0003), 14 rows high, in three columns from (6..113, 155..187) | decoded | Script 22 +0x0000, +0x0003; verb area | 2026-10-17 |
| Script 4 (input) | `VAR_VERB_SCRIPT` after the boot. Right click (mode 2) with `bit[0x223]` = 0 and no pending second object takes the hovered object's default verb from `var[0x0B6]` (+0x0005 to +0x0016; the floppy uses `var[0x0B5]`); slots 0xC8..0xCF pick inventory objects `var[0x085+n]` (+0x0304 to +0x0333), 0xD0/0xD1 scroll (`var[0x076]`, chain to Script 9); `doSentence var[0x06B], var[0x06C], var[0x06D]` at +0x0407 | decoded | Script 4 +0x0000 to +0x0440 | 2026-10-17 |
| Room 28 local script 202 | the SCUMM Bar's input script (`VAR_VERB_SCRIPT` = 202 in the bar; the floppy keeps Script 4) | decoded | route trace | 2026-10-17 |
| Script 14 | the dialogue's input script; its lines are verbs 0x78 up drawn with charset 1 at rows 145 to 185 | decoded | route | 2026-10-17 |
| Script 12 (sentence line) | verb 0x64 named "verb (code 5 of `var[0x06B]`) + string 49 (code 7, " " from `stringOps loadString 0x31`) + object (code 6)", colour 6 while composing, 3 after a run (Script 4 +0x0438) | decoded | Script 12 +0x0000 to +0x00DD | 2026-10-17 |
| Script 9 (inventory) | eight slots per page from `var[0x076]`; each slot gets an icon: the object's verb 0x5B (started with `startObject`) leaves the icon's object number in `var[0x178]`, else 0x407; `verbOps assignObject icon, 99` takes it from Room 99 (the rubber chicken's icon is object 961); arrows 0x404/0x405 on, 0x409 dim | decoded | Script 9 +0x0000 to +0x0133 | 2026-10-17 |

### Objects and classes (CD)

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Room 33 (dock) | 426 cliffside, 427 archway (to Room 35), 428 door (to Room 28), 429 poster (first look: cutscene with override +0x00CF..+0x0134, "Re-elect Governor Marley."), 430 unnamed | decoded | Room 33 object directory; object 429 verb 8 | 2026-10-17 |
| Room 28 (SCUMM Bar) | 315 door (to Room 33, through Script 120), 322 important-looking pirates (Talk to: the three trials) | decoded | route | 2026-10-17 |
| Room 35 and Room 29 | 444 door of Room 35 (to Room 29); 377 rubber chicken of Room 29 (Pick up) | decoded | route | 2026-10-17 |
| Untouchable | class 32 (`kObjectClassUntouchable` without translation: the CD has no small header); a picked-up object gets it | engine | object.cpp `getClass`; `o5_pickupObject` | 2026-10-17 |

### Global scripts (CD)

| Script | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| 60 | Room 37's arrival scene, as on the floppy: two cutscenes, "Hey!  I don't like visitors!  Who are you?"; started by Room 37's entry with Script 25 and local script 201; begins from a fresh start (73 frames). The entry starts sound 98 (an iMuse sound of the resource file, not a disc track) | decoded (scan), run (S7) | Room 37 entry +0x0000; Script 60 | 2026-10-18 |

### Rooms (CD)

| Room | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| 33 (cutscenes, S7) | the poster's first Look at (object 429 verb 8) is the room's scene in the list (`Room 33 · Look at poster`): `cutscene [0x0002]` +0x00CF, `beginOverride` +0x00D4, `printEgo` +0x00D9 "Re-elect Governor Marley.", `endCutscene` +0x0134; begins from a fresh start (74 frames; ends at frame 62 of the probe) | run (S7 headless run, window) | Room 33 object 429 verb 0x08 | 2026-10-18 |
| 19 | the ship's deck (Part II): the entry's local script 200 is a cutscene with local 206 (13 lines, "First of all^") that goes on to Room 7; local script 200 starts sound 102 at +0x0009, a disc track; begins from a fresh start (624 frames) | run (S7 headless run, window) | Room 19 entry; Room 19 local scripts 200, 206 | 2026-10-18 |
| 61, 25, 28, 31, 36, 57 (verb 0x50) | the code an inventory object reaches when given or used on the Sword Master (61/744), the friendly natives (25/292), the important-looking pirates (28/322), the prisoner (31/405), the poodles (36/467), the troll (57/655); verb 0x50 has no name in the CD's verb script, so its titles keep the number (as the floppy). Room 7's chest (77) has verb 0x3D, likewise unnamed | decoded (scan) | Script 22 (verb names); the objects' `VERB` blocks | 2026-10-18 |
