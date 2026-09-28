# Validation: the verb area as an optional presentation (28 September 2026)

Status: **done.** Builds with 0 warnings; **188/188 tests** pass (2 new in `LoomVerbAreaTests`). Room audit: 0 gaps in 76 rooms, **0 changed renders**. [VerbAreaCheck](VerbAreaCheck/Program.cs) **10/10** ([report and captures](wpf/VerbAreaCheck/)). Rerun of the other WPF checks ([reports](wpf/)): ClickInputCheck **11/11**, DoorwayClickCheck **75/75**, Room03ActionCheck `162 4 167 38` **46 PASS**, PointerInputCheck **13/13**, ReverseHoverCheck **22/22**, HighlightCheck **19/19**, MusicRowCheck **5/5**, CameraPanCheck **11/11**, PresentationCheck **39/39**. No physical input.

## What LOOM's verb area is (ScummVM and decoded)

- ScummVM `initScreens(16, 144)` for v3 (`scumm.cpp`): the room occupies screen rows 16–143 and the verb screen rows 144–199 (56 rows), 320 pixels wide.
- `drawVerb` draws an image verb whose mode is not 0 (on or dim) with `drawVerbBitmap`: `imgw` = the image's width / 8 strips from `x / 8`, `imgh` = its height / 8 strips; `curRect` keeps the verb's x and y with that size (`verbs.cpp`). The verb loop redraws slots in order, so a later slot covers an earlier one.
- The verb bar is built by Room 01 local 211 during the intro (images from Room 01 objects 100–151). **Script 018** (decoded) sets `var[0x064]` = its argument, turns verbs 0x01–0x08 (the distaff notes) on at y 0x90 = 144 (0x98 in expert mode), and outside expert mode also 0x31 at (48, 168) and 0x32 at (24, 168); it prints the note letters and marks with `print 0xFE` into the verb area (not drawn here). Script 003 (cutscene end) starts it with argument 1 unless `bit[0xD61]` is set. Script 008 turns the close-up (verb 0x36) on at (248, 152).
- After the session's boot every verb is off (Bobbin has no distaff yet), so the strip is empty until a script turns verbs on or the pointer or a click shows a close-up.

## What the app does now

- Core `LoomVerbArea.Compose` draws the 320×56 strip from the session's verb slots: each image verb that is on or dim, in slot order, with its object's image from the room it was taken from, at its position (x rounded down to a strip). Verbs whose mode, image or position is unknown, and text verbs, are listed as not drawn. `LoomVerbArea.FindVerbAt` ports `findVerbAtPos` (from the last slot down, the first verb in mode 1 whose rectangle holds the point) for the next task.
- App "Show verb area" (off by default; its tooltip says it is presentation only): the strip sits under the room at the room's scale, and the room shrinks so both fit. The note under it says "presentation of the session's verb slots, not the game's drawing" and lists the verbs that are on; its tooltip lists each verb's image, room and position, and what is not drawn (text verbs, the note letters, highlight colours, redraw timing).

## Checks

VerbAreaCheck (real MainWindow): off by default; on, the strip is 320×56 at the room's scale and fits with the room (Room 02: room view 258.8 → 196.8 px high); after the boot it is empty and labelled; after script 018 (started through the session's test seam, as the tests do) it draws the distaff notes at x 16–192, row 144, and lists 0x01–0x08, 0x31, 0x32; with the pointer over Room 02 object 156 it adds the close-up (verb 0x36, object 159's image at (248, 152)); off again, the room gets its size back. Captures: [boot](wpf/VerbAreaCheck/room-02-verb-area-boot.png), [distaff](wpf/VerbAreaCheck/room-02-verb-area-distaff.png), [close-up](wpf/VerbAreaCheck/room-02-verb-area-close-up.png).

Tests: `TheDistaffNotesScript018TurnsOnAreDrawnAndFound` (empty after the boot; script 018 draws 0x01–0x08, 0x31, 0x32 with Room 01's images; `FindVerbAt` finds note 0x03 inside its rectangle and nothing above the verb area), `FindVerbAtTakesTheHighestSlotThatIsOn` (last slot first; dim verbs are not found).

## Not modelled

The note letters and marks scripts 018 and 027 print into the verb area; text verbs; highlight colours; the verbs' redraw timing. Verb-area clicks are the next task.
