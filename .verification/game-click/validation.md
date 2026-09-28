# Validation: the game's click by default (M1e, 28 September 2026)

Status: **done.** Builds with 0 warnings; **198/198 tests** pass. Core is unchanged (app and harnesses only), so RoomAudit renders cannot change and were not rerun. No physical input.

## What the user sees now

- In the explore mode ("Interactive", the default), **a click on the room is the game's click**. It goes through scripts 005–007 as `LoomExplorationSession.Click` sends it. Bobbin walks to the object and verb 0x38 runs: use it, or go through an exit. A click on empty floor walks Bobbin there. A **double click** (a second press within the system double-click time) also runs verb 0x36 (look). The hover tooltip says this and labels the click as assumed input.
- The optional "Click as game input" setting is gone. Clicks in the room no longer run a direct verb or open a verb menu, and they no longer cycle artwork in the explore mode. The hotspot list (advanced mode) still offers direct verbs as exploration actions.
- **Room hotspots still navigate.** When the game's click reaches no room load on a doorway, the doorway's verb runs directly. That happens when the click is refused (user input off) or the route falls short. The status tooltip labels it: "The doorway's verb then ran directly, an exploration action, not the game's click."
- The **inspection mode** (unchecking "Interactive") cycles an object's stored images, and its status line says so: "Inspection mode: a click cycles an object's stored images; it is not the game's click and runs no script."
- Until M2 draws Bobbin and M1a–c draw text, a click on a non-exit object shows its reached state only (for example the verb-area close-up for class-1 objects).

## Harnesses ([reports](wpf/), [doorways](doorway/doorway-clicks.txt))

| Check | Result | Notes |
| --- | --- | --- |
| ClickInputCheck | **12/12** | The explore mode is on by default, with no "Click as game input" setting. `HandleRoomPoint(450, 60)` in Room 02 reaches Room 03 through 005 → 006 → 007 → object 153 verb 0x38. The queued single click reaches Room 04 (object 162, Room 03 local 200). The mouse-down pairing sends a double click (object 156 verb 0x36). In the inspection mode a click sends nothing and the mode's text says so. |
| DoorwayClickCheck | **75/75** | 29 through the game's click and 46 through the labelled direct-verb fallback. Of the fallbacks, 33 were refused because user input is off (`cursorCommand`) in that catalog visit's state, and 13 reached no room load (mostly untouchable doorways, which `findObject` skips). Room 22 object 400 lies wholly under 402 and is untouchable after the arrival through Room 16 object 348 (`var[0x071]` = 0x15C). `findObject` picks 402 → Room 18, which is the game's rule. The harness now labels that case instead of expecting Room 17. |
| Room03ActionCheck `162 4 167 38` | **46 PASS** | The coordinate action is now the game's click (Room 03 → Room 04). The harness accepts a game-input action for the clicked object. |
| VerbAreaCheck | 14 PASS, **1 FAIL** | The known "off again" failure from the UI refresh (task 5). The "off" case for strip clicks is now the inspection mode. |
| KeyInputCheck | **10/10** | |

The other harnesses (Pointer, ReverseHover, Highlight, MusicRow, CameraPan, Presentation) do not touch the click path and were not rerun. A run earlier in this task, before the final harness edits, gave the same counts as before: PointerInputCheck 13/13, ReverseHoverCheck 22/22, HighlightCheck 19/19, MusicRowCheck 5/5, CameraPanCheck 9 PASS/2 FAIL (known), PresentationCheck 40/40.

## Files

- `src/SCUMMexplorer.App/MainWindow.xaml(.cs)`: `GameClickActive` is the explore mode itself. `HandleRoomPoint(x, y, doubleClick)` sends the game's click, with the doorway fallback. The toggle was removed. Tooltips and the inspection text were updated.
- Harnesses: ClickInputCheck, DoorwayClickCheck (reports the path and the fallback reason), Room03ActionCheck and VerbAreaCheck.
