# Indiana Jones and the Fate of Atlantis script map

This map records **Indiana Jones and the Fate of Atlantis-specific** script roles, variable meanings, verb ids and room behaviour. Engine-defined variables and opcode rules are in the [SCUMM script reference](../engine/SCUMM_SCRIPT_MAP.md).

## Edition scope

Facts below are from the English talkie CD (`Games/indy4 vga en`, `ATLANTIS.cue`/`.img`, the S4b/S5 fixture named by `SupportConformanceTests`), 17 October 2026. The Spanish classic and talkie copies are not checked (S9).

## Row format (every table below)

Each row: **item** | **meaning** | **evidence** (`decoded` / `engine` / `reading` / `play`, R-2.3) | **source** (`Script NN +0xOFFS`, `Room NN local script NN`, `Room NN object NN verb 0xHH`) | **date**. A row without a source is not allowed. Unnamed things keep their raw form.

## Boot and hooks

Boot script number, what it sets (ego, hook scripts, input script, cutscene hooks, text speed), the order in which it starts rooms and scripts.

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Script 1, boot parameter | `local[0]` 0 and 0xE13F (-7873, ScummVM's copy-protection bypass for `GID_INDY4`) both jump to +0x3539, the normal start; 0x0A9E and other values go to debug starts (0x26AC…0x26B5, 0x044D…, 0x1A09…: direct room starts) or Room 81 (+0x3518) | decoded | Script 1 +0x0696, +0x06A0, +0x06AA, +0x3518 | 17 Oct 2026 |
| Script 1, low-memory alert | prints "more memory needed" in room 0 when `var[0x028]` (VAR_HEAPSPACE, 1400 at start) is not above 300/345 | decoded | Script 1 +0x0638 to +0x065D | 17 Oct 2026 |
| Script 1, normal start | fills string 0x16, clears `bit[0x28D]`, starts Script 74 (the logo) and waits for it, sets the charset 2 and `var[0x13B]` = 2, loads Room 4 (Barnett College), turns cursor and user input on | decoded, play (session) | Script 1 +0x3539 to +0x35A9 | 17 Oct 2026 |
| Copy protection | none met on this path: the talkie CD's boot asks nothing; assumed input none | decoded, play (session) | Script 1 +0x0696 | 17 Oct 2026 |

## Global scripts (boot)

| Script | Role | Evidence | Source | Date |
|---|---|---|---|---|
| 74 | the logo: cutscene, loads Room 68, draws object 939, fades in, a 480-jiffy delay (overridable), loads room 0, ends the cutscene | decoded | Script 74 +0x0000 to +0x006F | 17 Oct 2026 |

## Engine variables as this game uses them

Values this game selects for engine-defined slots (see the engine table). One row per slot actually used.

| Variable | This game's use | Evidence | Source | Date |
|---|---|---|---|---|
| `var[0x020]` (VAR_VERB_SCRIPT) | 78 in the playable credits (prologue), 4 with the full interface, 17 during a dialogue | decoded, play | Room 4 local 206 +0x019D; Room 5 local 214 +0x008B; Room 10 local 208 | 18 Oct 2026 |

## Game variables and bits

| Name | Meaning | Evidence | Source | Date |
|---|---|---|---|---|

## Global scripts

| Script | Role | Evidence | Source | Date |
|---|---|---|---|---|

## Rooms

| Room | What it is, entry/exit script facts, local scripts | Evidence | Source | Date |
|---|---|---|---|---|
| 68 | the LucasArts logo: entry chains to local 200 (the animation, actors 5 to 7) and starts 201 (keeps sound 39 playing) unless `bit[0x024]`; else sets `var[0x02A]` = 322, starts sound 150, fades in and gives objects 933, 937, 938 to their owners. Exit: `soundKludge` lowers and fades sound 150, then stops it (iMUSE command 9) | decoded, play (session) | Room 68 entry, local 200, 201, exit +0x0006 to +0x0024 | 17 Oct 2026 |
| 4 | Barnett College (the storage room): with `bit[0x045]` clear (the normal start; Script 1 sets it at +0x06DF and other debug starts) the entry loads sounds 21, 22, 29, 30 and costumes 2, 10, 138 and chains to 206 (the opening); with it set the entry skips the opening, draws the room's objects and starts 220. Local 206: cutscene, Indy (costume 26, then 2) put at (-31,140), Marcus' line "Alright, Jones. How are you going to find that STATUE in all this JUNK?", end of cutscene, starts 221 (or 221 with 1), Script 79, `var[0x020]` = 78, cursor and user input on: **the first moment the player has control** | decoded, play (session) | Room 4 entry, local 206 +0x0000 to +0x01A4 | 17 Oct 2026 |
| 5 | the second room of the credits; local 214 waits on `var[0x0D1]` under an override: Esc skips the credits, sets up the statue rooms, `var[0x020]` = 4, loads Room 1 | decoded, play | Room 5 local 214 +0x000D to +0x0095 | 18 Oct 2026 |
| 1 | Smith's scene (local 200, cutscene), then Room 10 | play | Room 1 entry, local 200, 220 | 18 Oct 2026 |
| 10 | New York: marquee 142, newspaper 141 (Pick up), ticket taker 143 (Talk to: local 208, the lines "But--" to "Um--"), back door 136 (Biff's conversation), crates 145/146 and 137/139, fire escape 138 (class 6 blocks it; Use chains to local 207, a cutscene with an override, then Room 12) | decoded, play | Room 10 objects, local 207, 208 | 18 Oct 2026 |
| 28 | local 208 waits for the sentence script (`o5_wait` sub-opcode 4) after a `doSentence` | decoded | Room 28 local 208 +0x00F5 | 17 Oct 2026 |

## Verbs and the input chain

| Verb / script | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Script 78 | prologue input: a click on an object walks Indy to it and runs its verb 0x0A (else 9); class 8 objects run 0x0A at once; a click elsewhere walks | decoded, play | Script 78 +0x002A to +0x00A7 | 18 Oct 2026 |
| Script 79 | prints the name of the object under the pointer above it (code 6 of `var[0x007]`) | decoded, play | Script 79 +0x007B | 18 Oct 2026 |
| Script 19 | verb sets: 1 saves verbs, 2 restores the full interface (verbs 3 to 0x0B, sentence 0x64 "Walk"), 5 creates the dialogue verbs 0x78 to 0x80 (rows 145 up, keys 1 to 9, names: bullet + code 7 of strings 0x29 to 0x31) | decoded, play | Script 19 +0x0030, +0x0134 | 18 Oct 2026 |
| verbs | 5 Give, 3 Open, 4 Close, 0x0B Pick up, 0x0C Talk to, 9 Look at, 8 Use, 6 Push, 7 Pull, 0x0A Walk to; inventory 0x65 to 0x6E (icons from Room 98), arrows 0x6F/0x70 | play | Script 19, Script 10 | 18 Oct 2026 |
| Script 17 | dialogue input: the chosen line (`local[1]`) is said through code 5 of the verb (+0x00D9), `var[0x0A5]` holds it | decoded, play | Script 17 +0x007F to +0x00EC | 18 Oct 2026 |
| Script 4 / Script 2 | full-interface input and sentence scripts | play | route | 18 Oct 2026 |

## Objects and classes

| Object / class | Meaning | Evidence | Source | Date |
|---|---|---|---|---|

## Actors and costumes

| Actor / costume | Meaning (look, palette, talk colour, who wears what) | Evidence | Source | Date |
|---|---|---|---|---|

## Sounds and music

| Sound | Meaning (device parts, who starts it) | Evidence | Source | Date |
|---|---|---|---|---|
| all | 207 sounds; 200 have an iMUSE AdLib part (`ADL `), most also a Roland part (`ROL `); the session's sound lengths for script waits come from the AdLib part's default-path timeline (estimate) | reading | `ScummV5Archive.ReadSounds`, `Indy4BootTrace sounds` | 17 Oct 2026 |
| 150 | the logo music (Room 68 entry), stopped by the exit's `soundKludge [9, 150]` | decoded | Room 68 entry +0x0022, exit +0x0024 | 17 Oct 2026 |
| speech | each spoken text carries four `0xFF 0x0A` codes: the sample's offset in MONSTER.SOU and its VCTL size (Marcus' first line 0x0507BB6E, 215 jiffies) | decoded, play | Room 4 local 206 +0x011C | 18 Oct 2026 |
| 21 | Barnett College music, loaded by the Room 4 entry | decoded, play (session) | Room 4 entry +0x000B | 17 Oct 2026 |

## Open questions

What is not decoded or not verified, and what would settle it.
