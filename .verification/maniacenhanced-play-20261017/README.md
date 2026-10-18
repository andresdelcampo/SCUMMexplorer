# S6 ManiacEnhanced — Spanish DOS enhanced edition

17 October 2026. Bounded native Play route implemented and verified in the core; parent integration owns the actual WPF window checks and final support decision. No ScummVM process was run. No story variables, room teleports, catalog preparation, game files or savegames were changed by this route.

Fixture: `D:\Juegos\Lucas\mmansionE ega es`; edition language is recognized by reviewed room-file hashes in `known-editions.json`. Test: `ScummManiacEnhancedPlayTests.PartyChoiceSkipSentenceWalkingAndPickupUseTheNativeInterface`. Full command, assertions, reached input events, relevant disassembly and successful result are retained in `route-test.txt`.

## Continuous route

The test boots normally into Room 45. Screen coordinates below are the engine's 320×200 screen; room coordinates explicitly marked below follow the current camera.

1. Click `(76,120)`, wait 100 frames; `(196,120)`, wait 100; `(292,76)`, wait 100. These select Syd and Bernard through objects 260/263 and start through 403. The selection variables read 1 and 5 as established by the scripts.
2. Wait until Room 44 and an active override; press Esc. Script 131 resumes at its native skip jump `+0008` and reaches player control. This edition differs from classic's `+000A`.
3. Click screen `(8,136)` and wait for Room 1. Wait 120 frames. Click `(8,136)` and wait 200 frames twice. Camera left is now 352; ego is x512.
4. Press `d` (native slot shortcut for verb 14). Click room `(376,84)` twice, recomputing screen x as room x minus ViewLeft. First click shows **Coger felpudo**; second executes object 54's verb. Wait until its intrinsic bit is on, exposing child key 55.
5. Press `d`; click room `(416,84)` twice. First click shows **Coger llave**. Wait until key owner is ego 3. The key's native name is drawn in inventory rows 176–183.
6. Click screen `(8,180)`: input area 3, object 55, mode 0. The sentence becomes **Ir a llave**.

Successful run: **1554 post-constructor frames, 13 delivered clicks/keys, plus Esc**. Native actor walk, camera movement, room transition, choice effects, override skip, object state change, ownership, sentence glyphs, verb rectangles and inventory pixels are asserted. The first row of an 8-row glyph can be blank; the pixel assertion covers all eight rows.

## Source comparison

The disassembly retained with the test comes from this edition's own resources, not another edition:

| Source | Reached behavior |
|---|---|
| Room45 object260 fallback +0013 | selected Syd index1, description, Script114 |
| Room45 object263 fallback +0013 | selected Bernard index5, description, Script114 |
| Room45 object403 fallback +0013; +0040 | validates party size, sets start flag |
| Script131 +0007 override; +0008 jump | Esc enters native skip path |
| Script4 +0291 / +02BB | compose displayed sentence / queue sentence |
| Script2 +01AD / +01B2 | run object's selected verb / repaint sentence |
| Room44 object258 verb13 +0013 | loadRoomWithEgo object257, Room1 |
| Room1 object54 verb14 +001E / +0034 | test intrinsic flag, lift mat by turning it on |
| Room1 object55 verb14 +001A | pickupObject, ownership and inventory |

Engine source read 17 October 2026 from official ScummVM master (source inspection only):

- [verbs.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/verbs.cpp): `checkExecVerbs`, `redrawV2Inventory`, `checkV2Inventory`; native fixed inventory layout and input areas.
- [script.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script.cpp): `runInputScript` v2 globals and script4.
- [script_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp): `o2_doSentence`, `o2_drawSentence`, `drawPreposition`, `o2_verbOps`, pickup and loadRoomWithEgo.
- [string.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/string.cpp): `ScummEngine_v2::drawSentence`, native forty-character line and localized prepositions.
- [scumm.h](https://github.com/scummvm/scummvm/blob/master/engines/scumm/scumm.h): UserStates sentence0x20, inventory0x40, verbs0x80 (independently fetched by parent).
- [scumm.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/scumm.cpp): `resetScumm`, classic Maniac split16/152, other DOS old games16/144.
- [actor.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/actor.cpp): `Actor_v2::walkActor`, `actorWalkStep`, `remapDirection`, inherited `Actor_v3::calcMovementFactor`, `Actor_v2::prepareDrawActorCostume`.
- [room.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/room.cpp) and [boxes.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/boxes.cpp): dense old route matrix, native-unit closest points and next-box routing.
- [object.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/object.cpp): `getObjectOrActorXY` held objects use owner's position; original room code retained for held-object verbs.
- [cursor.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/cursor.cpp): old DOS 23×21 cursor, hotspot11/10; later v3+ cursor preserved.
- [input.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/input.cpp): old engine pause, save/load and restart controls remain the app's controls.

Font extraction and attribution are supplied by the classic worker's `ScummV2Charset`, including the Spanish native ASCII replacement slots; no folder-name language inference or Unicode accent substitution.

## Corrections and limits

Shared source prerequisites fixed the previously discarded dense walk matrix, v2 leg lifecycle, v2 direction remap tables (which mutate targetFacing), native actor strip offsets/elevation, old room decoding and intrinsic state mask, fixed script4 input globals, native sentence/inventory, and held-object code/position lookup. Room2 first-visit turn settling passes again. Twenty-five focused shared checks passed, including both Maniac first visits and LOOM actor/renderer regressions, before the final facing-offset regression was added. The parent runs the complete suite and merge protocol.

An early route copied classic coordinates/override offset; inspection of the enhanced resources corrected those test inputs. A sentence-line click did not execute this enhanced sentence in the current test; repeated object clicks demonstrably do. This observation is retained for source review rather than silently claimed as accepted sentence-line behavior.

This is a bounded lit route, not a complete game walkthrough. S5 original-interpreter picture acceptance remains open; classic estimated costume colours, native darkness/flashlight rendering, additional puzzles, and unsupported old object-preposition metadata are not resolved by this record. Party selection is a genuine game choice, not a fabricated Talk-to conversation; REQUIREMENTS proposed R7.20 is parent-owned. No S7/S9 claim is made here.

Final focused checks: 13 passed for old input, the four native facing/elevation cases, enhanced continuous route and LOOM actor renderer. The subsequent DOS F-key conversion (`processKeyboard`: F1..F9 become 1..9 in VAR_KEYPRESS; platform input trace retains the original key) passed seven old-input/enhanced-route checks. F5 and F8 remain refused as save/load and restart controls. Worktree is committed clean; integration performs the full suite.

Final shared review corrections: inventory scrolling resets when ego or inventory ownership changes (`verbs.cpp handleMouseOver(updateInventory)`); seven focused checks passed. `Actor_v2::isPlayer` selects Maniac v1's ego or the actor range in globals42/43 for other v1/v2 editions. Both script and session placements now use that predicate. This prevents Zak's player entering the NPC counter box with flags0xA0 and its intentionally self-looping matrix. Eleven focused checks passed, including the actual Room7 player/NPC distinction, the continuous enhanced Maniac route and LOOM walk boxes. The Zak worker verifies the complete shop response separately.

Walking limitation retained: closest-point selection currently applies generic polygon distance in native coordinate units. ScummVM's complete v2 binary-search boundary and distance metric have not been ported; this bounded route does not establish arbitrary-room walking fidelity.
