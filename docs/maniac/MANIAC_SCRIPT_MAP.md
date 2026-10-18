# Maniac Mansion script map

This map records **Maniac Mansion-specific** script roles, variable meanings, verb ids and room behaviour. Engine-defined variables and opcode rules are in the [SCUMM script reference](../engine/SCUMM_SCRIPT_MAP.md).

## Edition scope

Classic v1 (`Games/mmansion ega en`, DOS EGA, English; ScummVM v1 tables) and enhanced v2 (`Games/mmansionE ega es`). Tables below come from the **classic v1** English copy unless a row says otherwise (14 October 2026).

## Row format (every table below)

Each row: **item** | **meaning** | **evidence** (`decoded` / `engine` / `reading` / `play`, R-2.3) | **source** (`Script NN +0xOFFS`, `Room NN local script NN`, `Room NN object NN verb 0xHH`) | **date**. A row without a source is not allowed. Unnamed things keep their raw form.

## Boot and hooks

Boot script number, what it sets (ego, hook scripts, input script, cutscene hooks, text speed), the order in which it starts rooms and scripts.

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|

## Engine variables as this game uses them

Values this game selects for engine-defined slots (see the engine table). One row per slot actually used.

| Variable | This game's use | Evidence | Source | Date |
|---|---|---|---|---|

## Game variables and bits

| Name | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Classic party references `var[0x02F]`, `var[0x030]`, `var[0x031]` | Boot selects Dave in the first slot; the native menu fills the other two from the player's choices. The input script copies these live values into ego for the three kid-selection slots. They identify the current party, not every named actor. | decoded | Script 1 +0x0195; Script 114 +0x0033/+0x003C; Script 4 +0x00DC/+0x00EF/+0x0102 followed by `actorFollowCamera` | 2026-10-18 |
| Enhanced party references | This edition's actor-switch input also reads the live selected-party variables; derive their bindings from its own instructions. | decoded | Enhanced Script 4 +0x00D7/+0x00EC/+0x0101 moves ego from `var[0x02F]`/`var[0x030]`/`var[0x031]`, each followed by `actorFollowCamera`; [enhanced S8 record](../../.verification/maniacenhanced-state-20261018/README.md) | 2026-10-18 |

## Global scripts

| Script | Role | Evidence | Source | Date |
|---|---|---|---|---|
| Classic 174 | Closest-actor query reads C8 into C9, checks FF and actor rooms, copies the result/fallback into C8 and stops. The genuine doorbell route now continues through Ed's outdoor response and return. | engine | Script 174 +0x0000/+0x0003/+0x0009/+0x0016/+0x002B/+0x002E; [native continuation](../../.verification/maniacclassic-fidelity-20261018/continuation.md), 484 observed queries over 3000 continuation frames | 2026-10-18 |
| Classic / Enhanced 55 | Computes native room lighting. Enhanced explicitly resizes the flashlight to 6x4 strips; Classic inherits that native engine default. Its source distinguishes held active flashlight and dark room state. | decoded | Classic Script 55 +0x0040; Enhanced +0x0000/+0x0044; [lighting evidence](../../.verification/maniacenhanced-fidelity-20261018/README.md) | 2026-10-18 |
| Classic 87 | Doorbell response: begins a cutscene, loads Room 26, prints Ed's doorbell/package lines, then restores the previous scene. It continues its own source movement after the cutscene ends. | decoded | Script 87 +0x001F begin, +0x0020 room load, +0x0035/+0x004C lines, +0x00EA end; Room 1 object 394 verb 9 calls it at +0x003D | 2026-10-18 |
| Classic 47 | Reactor trouble response: the branches distinguish power loss, empty pool and repeated problems; it places Fred and Purple Tentacle in Room 31 and ends its cutscene after their exchange. | decoded | Script 47 +0x0001 begin, +0x0020 room load, +0x0045 power line/+0x007A pool line, +0x0279 end; Room 8 object 157 verb 5 +0x0055 starts Script 12, whose +0x0031 starts 47 | 2026-10-18 |
| Classic 112 | TV advert: places actor 17 in Room 40 and prints Mark Eteer's publishing offer and stamp reminder; its end restores the previous scene. | decoded | Script 112 +0x0000 begin, +0x0009 room load, +0x0019 offer/+0x00B3 reminder, +0x00D3 end; Room 17 object 190 verb 4 +0x0024 starts it | 2026-10-18 |
| Enhanced 87 | Edition-local doorbell response: the Room 1 object calls Script 87, which visits Room 26 and returns to Room 1 in the fresh preview (187 frames). | engine | Room 1 object 394 verb 9 +0x003B; Script 87 +0x001F begin/+0x00F5 end; enhanced S7 proof | 2026-10-18 |
| Enhanced 47 | Edition-local circuit-breaker response: the correct power-off line and Fred/Tentacle exchange run in Room 31, returning to Room 8 (888 frames). | engine | Room 8 object 157 verb 5 +0x004E starts 55/+0x0050 starts 12; Script 47 +0x0001 begin/+0x0047 power line/+0x0288 end; enhanced S7 proof | 2026-10-18 |
| Enhanced 112 | Edition-local TV advert visits Room 40 and returns to Room 17 (390 frames). | engine | Room 17 object 190 verb 4 +0x0024 starts 112; Script 112 +0x0000 begin/+0x00E1 end; enhanced S7 proof | 2026-10-18 |

## Rooms

| Room | What it is, entry/exit script facts, local scripts | Evidence | Source | Date |
|---|---|---|---|---|
| Classic 01 | The selected-party preview settles with visible images 56, 57, 62, 63, 64; state flags other than intrinsic visibility do not paint their images. | engine | Classic Room 01 entry; indexed states and reached session, `ScummManiacClassicFirstVisitTests`; [S5 record](../../.verification/maniacclassic-objects-20261017/README.md) | 2026-10-17 |
| Classic 02 | The bounded preview settles with no active object image; objects 141 and 167 retain state 1, which is not the intrinsic visibility bit. | engine | Classic Room 02 entry and reached session; `ScummManiacClassicFirstVisitTests` | 2026-10-17 |
| Classic 18 | Six 8-by-8 coin-slot rectangles already contain 58 black pixels each in the room background. The stored-image gap detector reports them even when the entire object composite equals the background; no filling is applied. | decoded | Classic Room 18 background; object records 242–247 at x=192/248/312/368/424/488, y=88; S5 audit and regression | 2026-10-17 |
| Classic 30 | The entry tests object 176's intrinsic bit; the selected-party catalog preview reaches the 2400-frame settling bound. | engine | Classic Room 30 entry +0x001B; [source comparison](../../.verification/maniacclassic-objects-20261017/final/source-comparison.md) | 2026-10-17 |
| Classic 44 | First playable room from the reviewed Syd/Bernard party clicks; object states 258=0 and 278=0, no active object image. | engine | Classic Room 44 reached through boot/input scripts; `ScummManiacClassicFirstVisitTests` | 2026-10-17 |
| Enhanced 01, 02, 44 | Reviewed selected-party previews: Room 01 shows images 56, 57, 62, 63, 64; Rooms 02 and 44 show no active object image. These are edition-local test results, not reused classic state writes. | engine | Enhanced fixture's entry/index/boot scripts; `ScummManiacEnhancedFirstVisitTests`; [S5 record](../../.verification/maniacenhanced-objects-20261017/README.md) | 2026-10-17 |
| Enhanced 30, 45 | Bounded previews reach the 2400-frame limit; all other 51 indexed rooms settle under the selected-party recipe. | engine | Enhanced room entries and reached child scripts; [source comparisons](../../.verification/maniacenhanced-objects-20261017/source-comparisons.md) | 2026-10-17 |

## Verbs and the input chain

The [Use-with investigation](../../.verification/maniac-usewith-review-20261018/README.md) identifies the prior composition stops: Classic Script 4 +0x0242 draws an automatic-preposition sentence; enhanced Script 4 +0x0247 explicitly reads the object preposition. Batteries 167 have native preposition 1 (`in` / `en`), while flashlight 84 has 2 (`with`). Do not replace their own header data with the colloquial name of the puzzle. After selecting the two objects, a repeated click on the selected second object follows the native execute branch (Classic Script 4 +0x021D/+0x0222; enhanced +0x01F2). A sentence-line click alone did not execute in this reached state. These are source observations; final acceptance and boundaries are in the [Use-with checkpoint](../checkpoints/20261018-UseWith-ManiacZak.md).

| Verb / script | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Classic party menu | Native clicks on Syd `(76,104)`, Bernard `(196,104)` and Start `(292,60)` select actors 1/5 beside Dave. The menu supplies the player's choice; this edition has no Talk to verb. | engine | Room 45 object 260 fallback +0x0013/+0x0052; object 263 fallback +0x0013/+0x005D; object 403 fallback +0x0041; `ScummManiacClassicPlayTests` | 2026-10-17 |
| Classic outdoor introduction skip | After the opening reaches Room 44, Escape at the established override resumes Script 131 at its skip jump +0x000A; the ending restores player control. | engine | Script 131 +0x0009 `beginOverride`, +0x0228 ending; native `PressKey(27)` regression | 2026-10-17 |
| Classic verb strip | Fifteen verbs in five columns and three rows at y=160/168/176. Walk to is 13, Pick up 14, Read 12, What is 15, New kid 7, Unlock 8, Fix 6. | engine | Reached Script 164 verb setup after boot; `ScummManiacClassicPlayTests`; [S6 record](../../.verification/maniacclassic-play-20261017/README.md) | 2026-10-17 |
| Classic input composition and execution | Pick up shortcut `d`, followed by a first mat click, displays `Pick up door mat`; the second click queues verb 14/object 54. The sentence script walks Dave and invokes the object's verb code. | engine | Script 4 +0x0242 `doSentence` sub 2 (draw), +0x026C sub 0 (queue); Script 2 +0x016C walk and +0x019F sub 1 (run object); `ScummManiacClassicPlayTests` | 2026-10-17 |
| Classic inventory selection | Acquired object 55 paints the native first inventory slot (y=184); a click at `(8,188)` reaches area 3/object 55 and displays `Walk to key`. | engine | Script 4 inventory branch and native v1/v2 inventory renderer; key pickup at Room 1 object 55 verb 14 +0x0018; `ScummManiacClassicPlayTests` | 2026-10-17 |
| Enhanced party menu and skip | Genuine selections 260/263 and Start 403 choose Syd/Bernard; Escape resumes the outdoor introduction at Script 131's skip jump +0x0008. | engine | Room 45 objects 260/263/403 fallback +0x0013; object 403 +0x0040 sets start variable 110; Script 131 override; `ScummManiacEnhancedPlayTests` | 2026-10-17 |
| Enhanced sentence and inventory | Native input composes `Coger felpudo` and `Coger llave`; selecting the acquired inventory key displays `Ir a llave`. The continuous route takes 1554 frames and thirteen delivered inputs plus Escape. | engine | Script 4 +0x0291 composition/+0x02BB queue; Script 2 +0x01AD object execution/+0x01B2 repaint; [enhanced S6 record](../../.verification/maniacenhanced-play-20261017/README.md) | 2026-10-17 |

## Objects and classes

| Object / class | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Room 7 flashlight 84, both editions | Direct pickup executes the native verb. Switch on preserves the battery prerequisite; without batteries no beam is enabled. | engine | Classic verb 14 +0x0029 / verb 4 +0x002C; Enhanced verb 14 +0x002D / verb 4 +0x0030; [lighting record](../../.verification/maniacenhanced-fidelity-20261018/README.md) | 2026-10-18 |
| Enhanced Room 2 batteries 167 | Use requires second object 84, writes the native battery variable to 2 and consumes the batteries. This source condition is not a supplied status edit. | decoded | Object 167 verb 11 +0x0018/+0x001E/+0x0022; own V2 object disassembly | 2026-10-18 |
| Room 2 radio 142 and batteries 167, both editions | Battery object has no pickup entry. The radio's pickup stores the batteries with container owner 14; its Open transfers them to ego. After native header-preposition lookup, inventory Use composes with flashlight 84; repeating the target executes insertion, mode 2 and consumption. A wrong held target preserves the batteries. | engine | Enhanced radio Open +0x0057, battery +0x001E/+0x0022; Classic own Open +0x005A, battery +0x0022/+0x0026; [Use-with checkpoint](../checkpoints/20261018-UseWith-ManiacZak.md). Earlier failed diagnostic retained. | 2026-10-18 |
| Classic Room 44 object 258 | Walk-to response loads Room 1 at object 257. A native click reaches this exit in the continuous S6 route. | engine | Room 44 object 258 verb 13 +0x0013 `loadRoomWithEgo 0x0101, 0x01`; `ScummManiacClassicPlayTests` | 2026-10-17 |
| Classic Room 1 objects 54/55 | Pick up mat 54 turns its intrinsic bit on; key 55 is parented to local record 4 with required state 8. Key's Pick up response performs `pickupObject`, giving it owner 3 (Dave) on the checked route. | engine | Room 1 object 54 verb 14 +0x001F/+0x0038; record 55 parent 4/8; object 55 verb 14 +0x0018; `ScummManiacClassicPlayTests` | 2026-10-17 |
| Enhanced exit and mat/key | Exit 258 loads Room 1 at object 257. Lifting mat 54 reveals key 55; pickup gives key owner 3. Edition-local click points are `(376,84)` and `(416,84)` before camera/screen conversion. | engine | Room 44 object 258 verb 13 +0x0013; Room 1 object 54 verb 14 entry +0x001E/state-on +0x0034; object 55 verb 14 +0x001A pickup; `ScummManiacEnhancedPlayTests` | 2026-10-17 |

## Actors and costumes

| Actor / costume | Meaning (look, palette, talk colour, who wears what) | Evidence | Source | Date |
|---|---|---|---|---|
| `actorOps` sub 1 sound, 2 palette (v1: index = actor number), 3 name, 4 costume, 5 talk colour | how scripts set an actor's look | engine | ScummVM `script_v2.cpp` `o2_actorOps` | 2026-10-14 |
| The only costume instruction in the global and room scripts sets costume 0 on actor `var[0x060]` | an actor is hidden | decoded | Script 61 +0x010E | 2026-10-14 |
| Each actor's start costume is its own number; colours are `v1MMActorPalatte1/2[actor]` | costume to colour rule | engine | ScummVM `costume.cpp` | 2026-10-14 |
| Classic Script 1 actor names and sounds | Genuine boot code, not a data table: initializes ego 3, then names actors beginning with Syd and Razor. The earlier few-instruction decode was superseded by the v1/v2 adapter. Script 0's role is not established here. | decoded | Classic Script 1 +0x0008, +0x0019, +0x0021, +0x0025, +0x002F | 2026-10-17 |
| Classic selected-party names | On the verified Syd/Bernard route, the current party is Dave, Syd and Bernard. Actor names are executed script data; other named actors include NPCs and must not become editable inventory recipients merely because they have names. | decoded | Script 1 actorOps names: Syd +0x0019, Dave +0x0033, Bernard +0x0050, Dr Fred +0x0092; party references above; [S8 source extraction](../../.verification/maniacclassic-state-20261018/source.txt) | 2026-10-18 |
| Enhanced selected-party names | The verified current party has edition-executed names Syd, Dave and Bernard. | decoded | Enhanced Script 1 names Syd +0x001D, Dave +0x0047, Bernard +0x0079; [enhanced S8 record](../../.verification/maniacenhanced-state-20261018/README.md) | 2026-10-18 |

## Sounds and music

| Sound | Meaning (device parts, who starts it) | Evidence | Source | Date |
|---|---|---|---|---|

## Open questions

What is not decoded or not verified, and what would settle it.
