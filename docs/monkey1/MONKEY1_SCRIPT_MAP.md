# The Secret of Monkey Island script map

This map records **The Secret of Monkey Island-specific** script roles, variable meanings, verb ids and room behaviour. Engine-defined variables and opcode rules are in the [SCUMM script reference](../engine/SCUMM_SCRIPT_MAP.md); the engine's boot sequence is in [SCUMM_BOOT_SEQUENCE.md](../engine/SCUMM_BOOT_SEQUENCE.md) (v4 section).

## Edition scope

All rows come from the **four-disk 256-colour floppy release** (`Games/monkey1 ega en`: `DISK01.LEC` to `DISK04.LEC` and `000.LFL`; the script's own version string says "The Secret of Monkey Island (VGA) -- 1.0  12/3/90"; the folder name says EGA, the data is VGA), SCUMM v4, dialect `V4`, read on 14 October 2026 (S4b MonkeyFloppy). Numbers are not transferred to other editions (R-14.2): the CD release (`MonkeyCd`, v5) has its own block in S4b of that family. The eight-disk 16-colour Spanish release (`monkey1 ega es`) runs the same boot route (record, section "Differences"), but its numbering was not compared row by row. Global scripts are numbered below 200, local scripts of a room from 200.

## Row format (every table below)

Each row: **item** | **meaning** | **evidence** (`decoded` / `engine` / `reading` / `play`, R-2.3) | **source** (`Script NN +0xOFFS`, `Room NN local script NN`, `Room NN object NN verb 0xHH`) | **date**. A row without a source is not allowed. Unnamed things keep their raw form. `decoded` here means read from the disassembly and confirmed by a headless run of the generic session; nothing here is `play`.

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
| `var[0x020]` (input script) | 4 in play: its first argument is the click area (1 verb, 2 room, 4 key) | decoded | Script 1 +0x060E; Script 4 +0x0034 | 2026-10-14 |

## Game variables and bits

| Name | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| `var[0x069]` | last key or click value the protection reads (0 none, 0xFFFF a key that is no digit) | decoded | Script 151 +0x00BF; Script 152 +0x0138; Script 153; Script 155 | 2026-10-14 |
| `var[0x10A]` | the right year of the protection question (a character of the string table 0x1D plus 0x5E2) | decoded | Script 152 +0x00CD to +0x00EE | 2026-10-14 |
| `var[0x0B3]` | the year typed so far (four digits weighted 1000, 100, 10, 1) | decoded | Script 152 +0x015E, +0x01C1 | 2026-10-14 |
| `var[0x127]`, `var[0x128]` | the symbol pair chosen at random for the question (position in the string table = `var[0x127]` * 7 + `var[0x128]`) | decoded | Script 150 +0x002D to +0x0057; Script 152 +0x00CD | 2026-10-14 |
| `var[0x15F]` | counter of open cutscenes (+1 at the cutscene start script, −1 at the end script) | decoded | Script 18 +0x0000; Script 19 +0x0000 | 2026-10-14 |
| `var[0x165]` | tested for 1 where the highlighted verb's colour is set to 4 instead of 2 (a mode flag; its meaning is not read) | decoded | Script 12 +0x004B; Script 18 +0x0028 | 2026-10-14 |
| `var[0x06B]` | the verb of the sentence being built: the interface loop compares it with 13 (Talk to) and 3 (Give) | decoded | Script 23 +0x003F, +0x006A | 2026-10-14 |

## Global scripts

| Script | Role | Evidence | Source | Date |
|---|---|---|---|---|
| 1 | boot | decoded | Script 1 | 2026-10-14 |
| 4 | the input script of play: its first argument is the click area (4 tests a key: function keys and others, with debug-mode variants) | decoded | Script 4 +0x0034 | 2026-10-14 |
| 12 | sets the sentence line (verb 0x64): blank when `var[0x06B]` is 100, else its name built from string codes, colour 3 (4 when `var[0x165]` is 1) | decoded | Script 12 | 2026-10-14 |
| 18, 19 | cutscene start and end hooks: soft-off or soft-on of cursor and user input, freeze or unfreeze scripts, save or restore the verbs for cutscene type 1 or 3 | decoded | Script 18; Script 19 | 2026-10-14 |
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
| 33 | the first playable room (a village dock at night; the ego's costume 1 stands at (346,133)); its entry script starts scripts 0xC9, 0xCA and 0xCB depending on `var[0x065]` and `var[0x0C3]`; local script 202 changes the ego's walk speed by walk box. First visit (S5): no object image active, the ego only | decoded | Room 33 entry +0x0000 to +0x0056; Room 33 local script 202 | 2026-10-15 |
| 58 | a forest maze loaded only as pseudo rooms 201 to 220: the entry `drawObject`s nine or ten path tiles (objects 638 to 669) at strip positions per `VAR_ROOM`, sets box flags and the untouchable class (24) of the edge exits; local scripts 201 animate actors 3 and 4 (costume 114). The map (Room 85) object 866 enters it as 218 with the ego at object 668 | decoded | Room 58 entry +0x0000 to +0x09B2; Room 85 object 866 verb 0x0A +0x0000 | 2026-10-15 |
| 1 | a beach loaded only as pseudo rooms 130 to 132 (three parts of one beach picture); as 130 it sets objects 21 to 23 to state 1 and object 296 when `bit[0x215]` is 0; no `loadRoomWithEgo` with literal operands enters it | decoded | Room 1 entry +0x0066 to +0x00A0 | 2026-10-15 |
| 30 | a shop: the entry starts local scripts 205 (three random prices in `var[0x0DC..0x0DE]`), 209 or 208 (objects 412 and 932 on or off by `var[0x064]`) and 203; the shopkeeper is actor 11 (costume 28) | decoded | Room 30 entry +0x008B to +0x00DA; Room 30 local scripts 203, 205, 208, 209 | 2026-10-15 |
| 70 | objects 746 to 748 are set to state 1 by the entry only when `bit[0x17F]` is set (later in the story) | decoded | Room 70 entry +0x006B to +0x0078 | 2026-10-15 |

## Verbs and the input chain

| Verb / script | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| The twelve verbs | Open 1, Close 2, Push 6, Pull 7, Walk to 10, Pick up 11, Talk to 13, Give 3, Use 8, Look at 9, Turn on 4, Turn off 5; each has a key (o, c, s, y, w, p, t, g, u, l, n, f) | decoded | Script 22 +0x0003 to +0x00D8 | 2026-10-14 |
| Verbs 0x64, 0x65 to 0x6A, 0x6B, 0x6C | 0x64 the sentence line; 0x65 to 0x6A the six inventory slots (keys '1' to '6'); 0x6B and 0x6C the inventory arrows (keys q and a) | decoded | Script 22 +0x022B to +0x0303 | 2026-10-14 |

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

## Open questions

- The AdLib music timer the session gives the credits is an estimate (iMuse in the engine; the explorer plays the song's `AD` block at the browsed disks' half tempo). The length of the credits in the session was not measured against the bundled ScummVM (S5/S6).
- The ego stands at (346,133) in Room 33 after the opening; the first-visit objects were compared with the room's own scripts at source level only (S5 record), not with a ScummVM picture.
- Which of Room 1's pseudo numbers (130 to 132) comes first in play is not decoded (the map rooms reach it through scripts with non-literal arguments); the first visit uses 130, labelled.
- The index (`RN` block) names none of the rooms the boot enters (the scanner shows "Room NN"); Rooms 90, 10, 38 and 96 are described above by what their scripts do.
- The "insert disk" and "unable to find" strings of script 1 (+0x00E0, +0x0110) are never reached: all disks are assumed inserted.
