# Room overlays, Advanced panel and Game Status across the live games, 24 October 2026

Request (20 October): bring hotspots, depth areas, walk boxes, separate doorways, the Advanced right panel and one-click Game Status to every live game, LOOM as reference. Maniac/Zak was already merged to master by the Codex orchestrator (`71d55e98`, checkpoint `docs/checkpoints/20261019-RoomControls-ManiacZak.md`); this record surveys the other verb-based games on that merged master, with no local copy of the Codex work.

## Method

`OverlaySurvey/` (this folder): the real `MainWindow` with a private `SCUMM_EXPLORER_PROFILE_ROOT` and `SCUMM_ROOM_CACHE_DIR`, opens a game, selects a room as a user does, waits for live play, then measures changed pixels of the displayed room (`RoomViewportHost`) against the plain view for Hotspots Always, Doorways Always, and (Advanced) Show depth areas and Show walk boxes; lists the right panel's sections; opens Game Status from its header button, adds one item where the window offers one, and invokes Apply once. `run-survey.ps1 <label> [games]` drives it. Raw logs: `results/`; screenshots: `shots/`.

## Result on merged master (nothing is missing for the verb-based games)

| Game (room) | Hotspots | Doorways (own list and markers) | Depth areas | Walk boxes | Advanced panel | Game Status |
|---|---|---|---|---|---|---|
| Zak classic (1) | draws, 70592 px | draws; DOORWAYS 1, HOTSPOTS 16 | 20416 px | 18073 px | room scripts, 95 interactions, relationships | one click, closed |
| Indy3 EGA (73) | 48276 px | 16858 px; DOORWAYS 1, HOTSPOTS 5 | 25045 px | 17134 px | entry/exit/7 local scripts, 26 interactions, relationships | not offered (no Status editor) |
| Indy3 256 colours (73) | 48276 px | 16858 px; DOORWAYS 1, HOTSPOTS 5 | 25045 px | 17134 px | same | not offered |
| Monkey 1 floppy (33) | 2799 px | 1645 px; DOORWAYS 2, HOTSPOTS 1 | 52289 px | 7603 px | scripts, interactions | not offered |
| Monkey 1 CD (38) | 3488 px | 30492 px; DOORWAYS 4, HOTSPOTS 1 | 20377 px | 2689 px | scripts, interactions | not offered |
| Monkey 2 (7 Woodtick) | 2127 px | 29119 px; DOORWAYS 6, HOTSPOTS 3 | 33278 px | 7680 px | scripts, 60+ interactions | not offered |
| Indy 4 (35) | 28254 px | 24054 px; DOORWAYS 1, HOTSPOTS 2 | 22149 px | 17350 px | scripts, interactions | not offered |

Boot rooms with nothing to show are honest: Monkey 2 Room 1 (title, no objects, no depth data) correctly hides Doorways and disables Show depth areas; Indy 4 Room 4 (9 hotspots, no exits in its scripts) lists no DOORWAYS. LOOM EGA Room 13 is the reference and unchanged.

Game Status: the only Status editors are LOOM's and the Maniac/Zak inventory editor (`SupportFeature.Status`); the header button is correctly absent in Indy3, Monkey 1, Monkey 2 and Indy 4, which have none (R-12.14 applies where one exists; a new editor for those games is not part of this request).

## Decision

No product code changes: the generic path (`GenericRoomActions` for `PlaysThroughVerbs`, `ScummRoomOverlays`, `ScummDoorwayFinder`, `InventoryStatusWindow`) already serves every verb-based game. This commit adds the survey harness and this record, plus a clarifying sentence on R-12.14.

The harness leaves the app hanging after Game Status on some runs; it now ends with `Environment.Exit` and a 240 s watchdog.
