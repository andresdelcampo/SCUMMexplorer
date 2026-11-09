# Indy 3 Game Status (9 November 2026)

User: "Would it make sense to add Game Status to Indiana Jones and the Last Crusade? Similar to the one from Maniac Mansion -even simpler ... The only thing is potentially status flags like we added for LOOM", then "sure, makes total sense to add those to status, please do" (with health, outfit and IQ points). REQUIREMENTS R-12.15 to R-12.17.

## What the scripts hold (found by role, `ScummStatusSchema`)

| Value | Indy 3 EGA | Evidence |
|---|---|---|
| Health | `var[0x0C1]`, 0 to 1000 | set to 1000 in 7 places (Room 24's first aid kit among them); Script 42 lowers it by the blow and tests 0; Script 81 is the knockout. `var[0x0C3]`, the opponent's, is reset by scripts only. |
| Outfit | `var[0x0B4]`: 403 IndyWear (costume 2), 405 servant uniform (4), 406 grey uniform (3) | Script 52 +0x014A writes it and dresses actor 1; the guards (Scripts 58, 60) compare it with what they saw. Script 25 also branches on it (other costumes) but does not write it. |
| IQ points | string 7 (one slot per award), total `var[0x0F4]`; 68 awards, 775 points | Script 125 (slot, points): `setStringChar 7, local[0x000], local[0x001]`, `add var[0x0F4] += local[0x001]`; Script 83 sums string 7 for the IQ screen (`var[0x0F5]`, Room 94). |
| Story flags | 46 bits set to 1 | `move bit[...] = 1` across the scripts; titled by the scene that sets them, or the line spoken nearest. |
| Inventory | Indy (actor 1) the one recipient; 31 catalog items | `pickupObject` of the object or a literal, `setOwnerOf` a literal to the hero; grants as `o4_pickupObject` (slot, owner, untouchable, state 1), then the inventory script. Indy is named by the scripts' most frequent `actorName` for actor 1 ("Indy", Script 118, Room 25) until a script names him. |

## Checks

- `Indy3GameStatusTests` (2): the schema's roles; Indy the only recipient, a grant, health, an IQ award (slot and total), a story flag, and the grey uniform granted then worn (variable 406, costume 3, owner 14).
- Window (`window/`, WindowCheck step `GS`): Room 13, Game Status: Inventory (Indy's, no recipient choices), Indy (health 1000 of 1000; "Indy's own clothes: no outfit held yet"), IQ points (0 of 775), Story. Maniac Mansion's Game Status unchanged but in an Inventory tab (`window-maniac/`).

The fist fights did not quite work (user, 9 November); the health value is the one they use.
