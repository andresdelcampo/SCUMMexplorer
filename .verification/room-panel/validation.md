# Validation: compact room panel (27 September 2026)

## Problem

The right panel (`RoomObjectPanel`, 220 px) stacked, in one fixed column: the mode toggle, Restart/Continue (Continue was clipped at "Continue sc"), the status, a three-line note, the interaction panel, "Possible doorways" as full-width "Open Room NN" buttons in a 120 px scroller, room music (title, evidence, three large buttons, status, a note and "Other possible music" buttons in a 90 px scroller), the advanced-details expander, the asset preview, and last the hotspot list, which got whatever height was left. In the [before captures](before/), Room 17's hotspot list showed one row, and Room 03's was pushed off the panel entirely.

## Change

`MainWindow.xaml`:

- The panel is 290 px wide and has two parts:
  - **Fixed top:** "IN THIS ROOM" with the mode toggle ("Interactive") on the same line, then "↺ Restart" and "Continue ▸" in two equal columns, the session status and the room note. The tooltips keep the full wording.
  - **One scrolling body:** Interaction (only when a hotspot is chosen), then **Doorways · N** and **Hotspots · N** (or **Scene details · N** in inspection mode) as collapsible sections, both open by default, then **Music**, the selected image (inspection mode), and **Advanced room details** (collapsed, unchanged content: entry code, session interpretation, script events).
- Doorways are compact "Room NN" buttons in a wrap panel. The tooltip keeps every source (object, verbs, code offset, room-load instruction, path), and the section tooltip says that a button opens the room directly for exploration, while a doorway box on the room runs its script.
- Music is one row: ▶ Ⅱ ■ and the cue ("Possible: Sound 25", "Started: …" or "No cue established"), then time, evidence ("from room script data" or "from this session's route") and playback status in small text. The reached/possible distinction and its note are kept, behind a collapsed "Other possible music · N" with the other candidate tracks.
- The nested scrollers inside the panel were removed, so the mouse wheel scrolls the whole body and no section is clipped.
- The room-view checkboxes ("Show doorways", "Show depth areas") had a dark box background, which hid the check glyph: "Show doorways" looked unchecked while the overlay showed. The dark backdrop is now on their container, and the boxes use the default look.

`MainWindow.xaml.cs`:

- Hotspot rows show the object's **stored name** (the name in its code header), read by the new `LoomTextExtractor.ReadObjectNames`, which the text extractor also uses now. A doorway without a name shows the rooms it can load ("→ Room 04"), with the same label as its box on the room. Other hotspots show "Hotspot N". When labels repeat (Room 04's four "agujero", Room 38's two "puerta"), the local index is appended ("agujero · 5"). The tooltip keeps the hotspot index, object id, stored name, position and size. Scripts can rename objects (`setObjectName`); the label is the stored name.
- The routine note is shorter ("Click a doorway box to move, or a room image to cycle its artwork. Hotspots run script interactions."). The estimate note keeps its count and source label and drops the instructions.
- The `x:Name`s that harnesses use are unchanged. `ObjectList` is still a `StackPanel`; `RoomNavigationButtons` is now a `WrapPanel`, which harnesses only read as a `Panel`.

## Checks

- [WindowCheck](../WindowCheck/Program.cs), rebuilt, real `MainWindow`: [before](before/window-check.txt) and [after](after/window-check.txt) captures of Rooms 03, 04, 17, 22 and 38.
  - Room 03: Restart and Continue are both whole. Doorways · 4 (02, 05, 04, 10) fit on two rows, and Hotspots · 6 are visible ("→ Room 02", "→ Room 05", "→ Room 04", "→ Room 10 · 4", "→ Room 10 · 5", "cielo").
  - Room 04: the estimate note stays in the fixed top ("4 scene details use an artwork estimate … first stored image …"). Hotspots · 11 ("→ Room 03", "agujero · 5"–"· 8", "tumba → Room 64", …) and Music scroll in the body.
  - Room 17: Hotspots · 5 ("→ Room 16 · 1", "→ Room 22", "→ Room 16 · 5", "tumba · 6", "tumba · 7"), all visible (before: one row).
  - Room 22: Doorways · 3 and Hotspots · 6 ("→ Room 17", "→ Room 16", "→ Room 18", "campana", "cristal", "cáliz").
  - Room 38: Hotspots · 6, with the two Room 37 doorways as "→ Room 37 · 1" and "→ Room 37 · 15".
- [Room03ActionCheck](../Room03ActionCheck/Program.cs) `162 4 167 38`, rebuilt: [46 PASS, 0 FAIL](room03-action/window-check.txt). It still finds hotspots in `ObjectList` by tag, activates them by keyboard and click, and presses Continue.
- [DoorwayClickCheck](../doorway-clicks/DoorwayClickCheck/Program.cs), rebuilt: [75 of 75 boxes navigate](doorway-clicks.txt), as before.
- Build: 0 warnings. Tests: 138/138. New: `StoredObjectNamesLabelRoom17Hotspots`.

## Limits

- Captures are WPF renders of the real window at the harness size (1180 × 760). The minimum window width (900) leaves about 270 px for the room image; that size was not captured.
- The Spanish stored names come straight from the release; they are not translated.
- No physical mouse or keyboard input was used.
