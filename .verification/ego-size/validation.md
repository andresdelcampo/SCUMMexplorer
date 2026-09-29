# The ego's size after a catalog visit (30 September 2026)

## The user's report

Item A of the 29 Sep list: "Switching rooms to Room 02 can show big Bobbin instead of the small one, or actor 12 big instead of the boat." Added at the start of this session: "also to other rooms where Bobbin should be small — generic issue".

## What the game does (decoded)

- Script 017 gives the ego one of three costumes: `startScript 0x11, [n]` stores n in `var[0x07C]` (+0x005A) and uses `var[0x077 + n]` (costumes 1, 2, 3: large, middle, small) at +0x009B.
- Most room entries pass a literal size (Room 03 +0x001E: 2; Room 08 +0x0006: 1; Room 37 +0x000A: 0). Rooms 02, 15, 17, 18, 22, 23, 24 and 36 pass `var[0x07C]`: the ego keeps the size it had in the room it came from, which matches the door it came through. Their walk-box watchers (Room 02: script 045) change the size later as the ego crosses the boxes.
- Room 02's entry runs script 017 only while `bit[0xD6E]` ("in a cutscene") is 0 (+0x004D).
- Script 029 (the learning glow: actor 12, costume 4, at the ego) picks animation 9 when `var[0x07C]` is 0 and 6 otherwise (+0x005C): the glow's size follows the ego's. Its only start in Room 02 is the end of script 049 (Room 02 object 156, verb 0x3A), after a draft has been played (it waits on bits D66, D67 and D7A).

## What was wrong

1. A catalog visit skips the route, so `var[0x07C]` still held the size of the room shown before. Probe (fresh boot, catalog visits, then Room 02 via the assumed arrival through Room 03's door object 160 at (568, 114)):

   | Before Room 02 | var[0x07C] | Ego costume |
   | --- | --- | --- |
   | Room 37 | 0 | 1 (large) |
   | Room 41 | 1 | 2 |
   | Room 08 | 1 | 2 |
   | Room 03 | 2 | 3 (small) |

   The same held in Rooms 15, 17, 18, 22, 23, 24 and 36.
2. A catalog visit made while a cutscene played (the scene-leave fix, 29 Sep) ran the cutscene end script (003, which clears `bit[0xD6E]`) after the new room's entry. Room 02's entry then skipped script 017, and Bobbin kept the cutscene's costume (costume 1 or 2 after the trial).

## The fix

- `LoomEgoArrivalIndex` also records every literal `startScript` with its first argument (`StartsOf`).
- On a catalog visit into a room whose entry passes `var[0x07C]`, the session estimates the size from the assumed arrival's route: the size its own code set before the room load (a global script), or the size its source room's entry sets, followed back through rooms that pass `var[0x07C]` on. It is labelled as an estimate (`var[0x07C]` is in the estimated variables, and an uncertainty names the route). With no literal size on the route, the kept value is labelled as unknown. LOOM only.
- A catalog visit that leaves a cutscene now ends it before the room is entered, so the entry runs outside a cutscene.

## Result (session probe and tests)

| Route | var[0x07C] | Ego costume | Estimated from |
| --- | --- | --- | --- |
| 37 → 02, 41 → 02, 08 → 02, 03 → 02 | 2 | 3 (small) | Room 03 entry +0x001E |
| 37 → 15 | 2 | 3 | Room 23 (keeps the size), then Room 16's entry |
| 03 → 17 | 2 | 3 | Room 16 entry |
| 16 → 22 | 2 | 3 | Room 16 entry |
| 37 → 36 | 0 | 1 (large) | Room 37 entry +0x000A (the game's route through Room 37's door gives the large Bobbin too) |
| the trial left at frames 300, 1200, 2500, 2800 → 02 | 2 | 3 | Room 03 entry |

What the user sees: after picking Room 02 (or another of those rooms) in the tree, Bobbin has the size the game gives him at the door he is assumed to have come through, whatever room was shown before, also after leaving a cutscene.

Actor 12: no route in the session puts actor 12 into Room 02 except script 029 at the end of script 049, which needs a draft played first; it was not reproduced. The glow's size now follows the corrected `var[0x07C]`. If the user sees it again, the route that shows it is needed.

## Checks

- Tests 239/239 (new: `ACatalogVisitGivesTheEgoTheSizeOfItsAssumedRoute` for Rooms 37, 41, 08; `LeavingTheTrialForRoom02EndsTheSceneBeforeTheEntry`).
- SceneLeaveCheck: 0 failures. ActorDrawCheck: 0. DoorwayClickCheck on Rooms 02, 03, 04, 08, 10, 37: 16/16.
- Blockers: started but stopped unfinished when the user parked the session (30 Sep); not rerun.
- Skipped: RoomAudit (room images unchanged), the playback checks (the playback path is unchanged; only catalog visits changed).
