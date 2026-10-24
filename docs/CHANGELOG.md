# Changelog

Recent changes: 22–23 October 2026. Current support and acceptance limits belong in [REQUIREMENTS](REQUIREMENTS.md); current work and handoff are in [CURRENT_WORK](CURRENT_WORK.md). [Earlier entries through 20 October](archive/CHANGELOG_THROUGH_2026-10-20.md) are optional history, not startup reading. Retain roughly the latest two calendar days here.

## 24 October: LOOM parity of the room controls (R-6.21 to R-6.25)

Interact (bar button and object menu, right click), Step ▸, Sounds rows and the Hotspots/Doorways default radios of every verb-based game follow LOOM; the radios were unselected in LOOM too. [Record](../.verification/loom-parity-20261024/README.md).

## 24 October: static previews keep the room's controls and run only ambient scripts (all generic games, LOOM)

- A room that cannot be prepared live (MI2 Room 4, Indy 3 Room 1, LOOM static rooms) keeps the playback bar (Pause works), Play Cutscene, Interact, Sounds and Play Game Intro, and its permanent animation runs: only the scripts its entry starts that loop on pictures/sounds and never cut, print, load a room, move the camera or touch input (`ScummAmbientScripts`, decided from the game's code, no room list); none of the other entry code runs and no click reaches the game. Maniac Mansion keeps its own startup rules. Requirements R-8.5b, R-6.17, R-8.2a, R-13.11, R-13.12 extended. Room Sounds by number for MI2 and Indy 4. Record: [static-preview-controls-20261020](../.verification/static-preview-controls-20261020/README.md).

## 24 October: room overlays survey of the verb-based games

Surveyed Indy3 EGA/256 colours, Monkey Island 1 floppy/CD, Monkey 2 and Indy 4 in the real window on master 71d55e98: Hotspots, Doorways (own list and markers), depth areas, walk boxes and the Advanced room scripts/interactions already work through the shared generic path; none of them has a Game Status editor, so the button is correctly absent. No product change. [Survey](../.verification/room-overlays-20261020/README.md).

## 23 October: room controls and audio lifecycle (Maniac/Zak, shared UI)

- Requirements now make Normal/Advanced room cutscene and direct Interact access, native room-selector following, effective overlays and Advanced room source inspection explicit for the supported games. Plain native names replace verb bytes in Normal mode.
- Live hotspots and doorways use their own display modes and native eligibility; depth areas and walkboxes align with the actual game screen. Native doorway travel reveals the selected card and preserves its entry cutscene.
- Game Status validates and applies choices, then closes in one click; rejected batches stay open and session-bound Undo remains available after reopening. Source-associated Sounds menus work outside LOOM, and changing folders retires every old audio source and pending render.
- All four editions pass final action, overlay and audio/status windows. Required full suite: **1,739 passed, zero failed, five expected skips**; final build zero warnings/errors; three regenerated-matrix checks pass. Current bounded availability: [checkpoint](checkpoints/20261019-RoomControls-ManiacZak.md). No support levels are promoted; current classic costume estimates and derived cutscene limits remain in REQUIREMENTS.
