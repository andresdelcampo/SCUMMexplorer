# Validation: one doorway button per target room (27 September 2026)

## Change

`MainWindow.UpdateRoomNavigationActions` now creates one "Open Room NN" button for each target room. Before, it created one per (object, verb, target) candidate. The button's tooltip lists every source that leads there: object, verb labels, code offset, room-load instruction and path (direct or through a started script). Verbs that share one code block are merged into a single source line. The tooltip still says each source is a static candidate and that opening the room directly does not simulate conditions or effects. The on-image doorway overlay is unchanged and still marks each hotspot separately. The click-choice panel (`FindNavigationCandidatesAt`) already showed one button per target.

## Check

The real `MainWindow`, through [WindowCheck](../WindowCheck/Program.cs), which now reports each doorway button's label and tooltip ([text](window-check/window-check.txt), captures of Rooms 03, 17 and 38):

| Room | Before | After |
| --- | --- | --- |
| 17 | Open Room 16 ×2, Open Room 22 | Open Room 16 (objects 352 and 356), Open Room 22 |
| 38 | Open Room 37 ×2 | Open Room 37 (objects 609 and 623) |
| 03 | Room 02, then Rooms 05, 04 and 10 repeated once per verb | Open Room 02, 05, 04, 10 with one button each. Room 10 lists objects 163 (Verb 0x36, Verb 0x38) and 164 (Verb 0x38). |

Build: 0 warnings. Tests: 101/101. Audit: 0 gaps (`run-verify.cmd`). The change is UI-only, and no unit test covers `MainWindow`. The WindowCheck text is the regression evidence.
