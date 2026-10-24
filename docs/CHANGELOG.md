# Changelog

Recent changes: 22–23 October 2026. Current support and acceptance limits belong in [REQUIREMENTS](REQUIREMENTS.md); current work and handoff are in [CURRENT_WORK](CURRENT_WORK.md). [Earlier entries through 20 October](archive/CHANGELOG_THROUGH_2026-10-20.md) are optional history, not startup reading. Retain roughly the latest two calendar days here.

## 24 October: room overlays survey of the verb-based games

Surveyed Indy3 EGA/256 colours, Monkey Island 1 floppy/CD, Monkey 2 and Indy 4 in the real window on master 71d55e98: Hotspots, Doorways (own list and markers), depth areas, walk boxes and the Advanced room scripts/interactions already work through the shared generic path; none of them has a Game Status editor, so the button is correctly absent. No product change. [Survey](../.verification/room-overlays-20261020/README.md).

## 23 October: room controls and audio lifecycle (Maniac/Zak, shared UI)

- Requirements now make Normal/Advanced room cutscene and direct Interact access, native room-selector following, effective overlays and Advanced room source inspection explicit for the supported games. Plain native names replace verb bytes in Normal mode.
- Live hotspots and doorways use their own display modes and native eligibility; depth areas and walkboxes align with the actual game screen. Native doorway travel reveals the selected card and preserves its entry cutscene.
- Game Status validates and applies choices, then closes in one click; rejected batches stay open and session-bound Undo remains available after reopening. Source-associated Sounds menus work outside LOOM, and changing folders retires every old audio source and pending render.
- All four editions pass final action, overlay and audio/status windows. Required full suite: **1,739 passed, zero failed, five expected skips**; final build zero warnings/errors; three regenerated-matrix checks pass. Current bounded availability: [checkpoint](checkpoints/20261019-RoomControls-ManiacZak.md). No support levels are promoted; current classic costume estimates and derived cutscene limits remain in REQUIREMENTS.
