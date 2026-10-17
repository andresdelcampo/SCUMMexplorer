# Zak McKracken script map

This map records **Zak McKracken-specific** script roles, variable meanings, verb ids and room behaviour. Engine-defined variables and opcode rules are in the [SCUMM script reference](../engine/SCUMM_SCRIPT_MAP.md).

## Edition scope

Classic v1 (`Games/zak ega en`, DOS EGA, English; global scripts decoded in the v2 dialect), recorded 14 October 2026. Rows explicitly marked **Enhanced** use `Games/zakE ega en` (DOS v2, English), decoded again on 17 October after the actor palette/name operand correction. Classic rows are preserved separately.

## Row format (every table below)

Each row: **item** | **meaning** | **evidence** (`decoded` / `engine` / `reading` / `play`, R-2.3) | **source** (`Script NN +0xOFFS`, `Room NN local script NN`, `Room NN object NN verb 0xHH`) | **date**. A row without a source is not allowed. Unnamed things keep their raw form.

## Boot and hooks

Boot script number, what it sets (ego, hook scripts, input script, cutscene hooks, text speed), the order in which it starts rooms and scripts.

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| ScummVM runs script 1 at boot (`runBootscript`); Script 01 sets actor 1 costume 30 (+0x11) and palette 0 (+0x15) then 14 (+0x10C), actor 10 costume 31 (+0x104) palette 7 (+0x108) | the start looks | decoded | Script 01 | 2026-10-14 |

## Engine variables as this game uses them

Values this game selects for engine-defined slots (see the engine table). One row per slot actually used.

| Variable | This game's use | Evidence | Source | Date |
|---|---|---|---|---|
| Enhanced `var[0x000]` (ego) and `var[0x00B]` (actor count) | Boot selects actor 1 and count 13 | decoded | Script 1 +0x000D, +0x0000 | 2026-10-17 |
| Enhanced `var[0x011]` (music timer) | The opening waits for timer 4, then watches later changes while Sound 95 runs | decoded | Script 1 +0x0157, +0x0177 to +0x01A0 | 2026-10-17 |

## Game variables and bits

| Name | Meaning | Evidence | Source | Date |
|---|---|---|---|---|

## Global scripts

| Script | Role | Evidence | Source | Date |
|---|---|---|---|---|
| Enhanced Script 1 | Starts the opening in Room 46, starts Script 127, then ends the opening and starts Script 108 | decoded | Script 1 +0x0058, +0x0078, +0x0533 to +0x053F | 2026-10-17 |
| Enhanced Script 108 | Names and configures actors; starts Scripts 9 and 19; places actor 1 in Room 1. The post-dream branch runs object 90 verb 1 through the sentence queue, waits, walks actor 1 to script coordinates (21,56), and restores cursor/user input | decoded | Script 108 +0x00DE to +0x0165, +0x0264 to +0x0266, +0x0290 to +0x02EC | 2026-10-17 |

## Rooms

S4b execution, 17 October 2026: each English fixture independently traverses rooms `46,0,46,0,58,51,55,49,56,0,0,0,1`. Enhanced reaches control in 1828 frames; Classic in 1804. Both remain in Room 1 with control and no cutscene for 120 more frames. Script 108 finishes its post-dream sentence/walk and input restoration. Bedroom random scripts are seeded, labelled estimates. See [.verification/zak-session-20261017](../../.verification/zak-session-20261017/README.md); this runtime evidence supplements, rather than transfers between, the edition-specific static rows above.

| Room | What it is, entry/exit script facts, local scripts | Evidence | Source | Date |
|---|---|---|---|---|
| Enhanced 23, loaded as 207 | Clears 403–411's intrinsic/touch flags, then selects 403, 406, 407 and 411 for this layout; other literal draws belong to other pseudo numbers | decoded | Entry +0x0004–0x0014, +0x0082–0x00CB | 2026-10-17 |
| Enhanced 36, loaded as 199 | Initial clear loop followed by the 199 branch draws 568, 566, 567, 564; object 565 is conditional on object 302 being intrinsic-on | decoded | Entry +0x0004–0x000E, +0x0110–0x0179 | 2026-10-17 |
| Enhanced 53, loaded as 158 | Draws 748, 749, 747, 741–745 in the wide-layout branch; the two extra ornaments 739/740 belong to 156 and are not drawn for 158 | decoded | Entry +0x003C–0x007F, +0x0092–0x00CA, +0x00FE–0x012C | 2026-10-17 |
| Enhanced 8, loaded as 144 | Draws object 197, clears/touches 192; other travel-office layouts select different images. Entry starts native object scripts; the preview can leave the room | decoded | Entry +0x024F–0x0282, +0x000E/+0x0022 | 2026-10-17 |
| Enhanced 14, loaded as 129 | Makes 309 intrinsic-on and touchable; 298 is conditional on its lock bit. Other layouts' 294/299–308 writes are not this branch | decoded | Entry +0x002C–0x006A | 2026-10-17 |
| Enhanced 9, loaded as 146 | Sets 222 intrinsic-on and draws 224 after clearing the shared alternatives | decoded | Entry +0x001F–0x0031, +0x004D–0x0079 | 2026-10-17 |
| Enhanced 33, loaded as 182 | First branch sets walk-box flags and resources, then skips the object writes used by 189–191 | decoded | Entry +0x0004–0x007D | 2026-10-17 |
| Enhanced 37, loaded as 170 | Clears 572–575, runs object 570's background verb; draws 572/573 or their closed alternatives based on variables, then optional 574/575 | decoded | Entry +0x0085–0x00F2 | 2026-10-17 |
| Enhanced 35, loaded as 195 | Takes projection-room branch, not the 194 branch that draws 559/560; object-script background work is distinct from entry's literal draws | decoded | Entry +0x000B–0x00A1 and +0x00AB/+0x00B0 | 2026-10-17 |
| Enhanced 27, loaded as 185 | Clears 445 and runs object 460's background verb; 446/454/456 writes belong to the alternative 184 layout. Object 451's script is conditional on its intrinsic state | decoded | Entry +0x002C–0x00A0, +0x00A3–0x00C6 | 2026-10-17 |

## Verbs and the input chain

| Verb / script | Meaning | Evidence | Source | Date |
|---|---|---|---|---|

## Objects and classes

| Object / class | Meaning | Evidence | Source | Date |
|---|---|---|---|---|

## Actors and costumes

| Actor / costume | Meaning (look, palette, talk colour, who wears what) | Evidence | Source | Date |
|---|---|---|---|---|
| Enhanced actors 1 / 2 / 3 / 4 / 7 | Source names: Zak / Annie / Melissa / Leslie / representative | decoded | Script 108 +0x00DE / +0x00E6 / +0x00F0 / +0x00FC / +0x0107 | 2026-10-17 |
| Enhanced actor 1 costume | 30 during the opening, 1 when Script 108 configures the playable state | decoded | Script 1 +0x0011; Script 108 +0x011A | 2026-10-17 |
| Palette per actor (constant `actorOps` sub 2): actor 1: 0, 13, 14; 4: 8 (Script 05 +0x1C); 8: 2, 12; 9: 3; 10: 1, 7, 9, 14; 11: 1, 9; 12: 1, 2, 12, 14. Actors 2, 3, 5, 6, 7 never get a palette (they stay 0) | actor palettes | decoded | global and room scripts | 2026-10-14 |
| Costume to actor (constant sub 4): actor 1: 1, 5, 30; 2: 6; 3, 4: 7; 5: 12, 13; 6: 14, 15; 7: 28; 8: 26; 9: 11; 10: 22-25, 29, 31-34; 11: 10, 27; 12: 18, 19, 20, 28; Room 38 entry: costume 21 on `var[0x042]` with palette 9 | who wears what | decoded | global and room scripts | 2026-10-14 |
| Costumes no constant instruction gives to anyone: 2, 3 (worn by actors 2 and 3 from the start), 4, 8, 9 (initial actors with later palettes), 16, 17, 35: their colours are labelled estimates | costume colours still estimated | decoded | scan of all scripts | 2026-10-14 |

## Sounds and music

| Sound | Meaning (device parts, who starts it) | Evidence | Source | Date |
|---|---|---|---|---|
| Enhanced Sound 81 / Sound 95 | The opening starts and waits for each sound; Sound 95 also supplies music-timer values through 42 | decoded | Script 1 +0x011E / +0x0141; +0x0148 / +0x0516; own sound control trace in `.verification/zak-session-20261017` | 2026-10-17 |

## Open questions

What is not decoded or not verified, and what would settle it.

- The colour derivation ignores a variable actor in the Room 38 entry script and control flow inside a costume/palette span; settling the 10 estimated costumes needs a running session (S4/S6). Zak classic Script 39 and a few entry scripts jump outside their resource.
