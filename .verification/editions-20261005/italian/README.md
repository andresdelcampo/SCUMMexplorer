# Italian LOOM EGA (`Games/loom ega it`), first review (10 October 2026)

Detected as LOOM / "Italian" from its LFL data (no `LOOM.EXE` in the folder; `known-editions.json`). Files 82-85 absent (no Roland upgrade).

| Check | Result |
|---|---|
| `RoomAudit` (first-visit rooms) | 72 rooms, 3 gaps before filling, 0 after (as every other edition) |
| `GlossaryCheck` against Spanish (`glossary-vs-es-it.md`) | 115 same, 27 n/a, 0 moved, 0 absent; bit shift 0 |
| `TextGalleryCheck` | 1224 entries, 0 unresolved, 133 object names. The harness's room-3 / search step fails here as on Spanish: it searches fixed Spanish words ("cielo"), not an Italian defect |
| `SoundPanelCheck` | sound 28 plays as an AdLib rendition (2:23); sound 24 is Roland-only and says "No playable audio" |
| Cutscene scan (`ega-seed-study`) | 216 groups, 94 scenes, rooms per scene identical to Spanish; 1 scene with no start (Room 34 entry) |

Not done: a look at the real window (the checks above are harness runs), and the Italian text quality (accents, charset) on screen.
