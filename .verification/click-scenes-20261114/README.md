# Clicks in the room as scene triggers (14 November 2026)

The user (14 November): the Grail trials are not started by an object; they are "a 'go to' non-object place in the screen". Build a trigger that clicks a
place in the room, reusable in other games; better the hero walks there than is teleported; one entry per distinct outcome ("only one per failing
animation"), one single click; they appear in the Play Cutscene lists, not in Interact.

## What was built (shared code, every game from version 3)

- **Scan** (`ScummCutsceneIndex.FindClickScenes`): a room whose entry script installs one of its own local scripts as the input script
  (`move var[0x020] = n`, `VAR_VERB_SCRIPT`) answers clicks itself. The scripts reached from that input script, and the room's local scripts the
  entry starts, are walked for scenes. Candidate points: the room's objects those scripts compare with (`isEqual … 0x036E`), then floor points
  (walk-box centres, spread left to right), at most 10 per room. Each is a `ScummCutscene` with `Click = (x, y)` and `VerbId` 0x200 + n, titled
  "Go to …" by object name or place ("the right", "the far left (front)"). In a click scan `cursorCommand userInputOff … userInputOn` counts as the
  scene's span (Room 84's jump takes control that way, not with `cutscene`). Versions 1 and 2 keep no input script in a variable: skipped.
- **Run** (`ScummCutsceneRun.Run`): the room visited, frames played until the game reads clicks, then `ScummSession.ClickRoomPoint` (the camera
  turned there first if the point is off screen) — the game's input script gets the click and the hero walks by its own code. A script taking the
  player's input after the click counts as begun. No direct cutscene start is tried for a click (`ScummCutscenePlanner`).
- **Duplicates**: a click whose plan shows the same lines, rooms and view as an earlier point of the same room is not offered in Normal ("plays as …").
- **App**: a picked click scene opens the room and sets `ClickRoomPointWhenReady` (clicked at the first frame the game reads clicks in that room, so
  an arrival still playing is seen). The note says it is a click answered by the room's input script.
- **Found on the way**: version 3 prints never resolved string codes (`\xFF\x07`, `convertStringMessage`); only version 5 had them. Room 84 said
  "In Latin, Jehovah is spelled ``." and now says "`IEHOVA`." (`ScummGameSession`: `StringText` from version 3 on).

## The Grail trials (Indy 3 EGA English), in the window (`indy3/`, `indy3-sheet.png`)

| Trial | Entry | Seen |
|---|---|---|
| Breath of God, passed | Room 83 · Go to the middle (object 878, the kneeling place) | "The penitent man is humble before God…", "Kneel!", Indy rolls under the blades, "I'm through!", Room 84 ✔ |
| Breath of God, failed | Room 83 · Go to the right (also: the far right, not offered: plays the same) | "Kneel!", the blades ("CENSORED"), Room 82: "Be back in a jiffy!" ✔ |
| Word of God, wrong letter | Room 84 · Go to the far left | Indy jumps to a stone, falls, Room 82: "Be back in a jiffy!" ✔ (the setup keeps `var[0x064]` = 5, Indy's column, a value Room 84 local 203 tests) |
| Path of God, the leap | Room 85 · Go to the far right (object 880, the chasm) | Indy steps out onto the hidden bridge and crosses to Room 86 ✔ |
| Marcus calls Indy back | Room 85 · Entry (already listed) | "Indy!!! Hurry up!…", "I know Marcus! I'm hurrying!" ✔ |
| Path of God, the fall | — | not reachable with one click: local script 201 falls only when Indy stands off the bridge line (y ≠ 94) as the chasm is clicked, so it needs a walk first and then the click |

Points that begin nothing (Room 83's left side: Indy only walks; Room 85's left) are not offered in Normal ("no start was found that begins it").

Other rooms whose input script answers clicks got offered entries too: Monkey Island 1 Room 34 (Mêlée's street: the far right into the alley,
Room 32, "Hello? Anybody in here?"; the archway, "Hey you. Come over here."), Monkey Island 2 Room 21 (the middle: "Guybrush Threepwood. It's been a
long time…"; the left: out through Room 20, no line), Indy 3 Rooms 15 and 56 (no line: the hero walks, a short span with control taken). A click
that shows no line, no other view and no animation cannot be told from the leap of Room 85 by what the run sees, so they stay; Advanced names each.

## Measured on all copies (`../scene-setup-20261031/*-run79.txt` against `*-run78.txt`)

Every scene that was there before kept its plan and kind (one difference: Monkey Island 1 EGA Room 35's pirates, a widened scene that plays on for
minutes, 175 → 188 lines). LOOM, Maniac Mansion and Zak McKracken are unchanged (LOOM's answer key the same: `../scenes-that-matter-20261112/loom-compare-run79.txt`).

| Copy | Click triggers | Offered in Normal before → after |
|---|---|---|
| Indy 3 EGA | 45 | 80 of 82 (97.6%) → 86 of 103 (83.5%): the trials (4) and Rooms 15, 56 |
| Indy 3 VGA | 45 | 79 of 81 (97.5%) → 85 of 102 (83.3%) |
| Indy 4 | 34 | 288 of 304 (94.7%) → 290 of 338 (85.8%) |
| Monkey Island 2 | 54 | 172 of 176 (97.7%) → 174 of 213 (81.7%) |
| Monkey Island 1 EGA | 34 | 87 of 87 (100%) → 89 of 90 (98.9%) |
| Monkey Island 1 VGA | 34 | 87 of 87 (100%) → 89 of 104 (85.6%) |
| Total (these six) | 246 | 793 of 817 (97.1%) → 813 of 950 (85.6%) |

The new entries not offered are click points that begin nothing (the hero only walks) or play as another point of the same room; the percentages
fall because those count as scanned scenes. 20 click entries are offered in all.
