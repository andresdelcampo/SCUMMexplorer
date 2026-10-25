# UI parity analysis: LOOM (reference) against the other games

25 October 2026. Read from `MainWindow.xaml`, `MainWindow*.cs` and `docs/REQUIREMENTS.md` (R-6.x, R-16.9). Nothing was run; the look is taken from the XAML and the code that fills it. Game Status is left out as asked.

"Generic" = every game played through `ScummGameSession` (Indy3, Monkey Island, Indy 4, Maniac Mansion, Zak) and their static previews. "LOOM" = LOOM EGA and CD.

## 1. What is already one shared control (no action)

The bar (Pause, Step, Skip, Skip line, room Play Cutscene, Interact, Sounds), the header Play Cutscene picker, Options expander and its radios, hotspot and doorway rows and overlays, popup menus, script page (title, Readable/bytecode toggles, event buttons, note), costume page, sound page, Game Texts and Cutscene picker windows, room cards and tree. These are filled by `IRoomControlProvider` / `IRoomHotspotProvider` (R-16.9, R-6.30 to R-6.42). Nothing below repeats them.

## 2. Gaps found (LOOM does X, generic does Y)

### A. Advanced right panel (the one the user noticed)

| LOOM | Generic | Requirement today |
|---|---|---|
| `DOORWAYS`, `HOTSPOTS` rows (shared). | Same (shared). | R-6.43 ok |
| `ACTIONS · <object>`: one flat full-width button per verb of the selected object (verb word), then a grey `Used by:` line. | Not present; the actions only exist inside the `INTERACTIONS` expanders and the Interact popup. | R-6.43 says "the selected object's actions" is fixed; generic does not build it. |
| `Room entry` expander: one-line summary (`N instructions · camera x … · sounds …`), then **event buttons** (`starts Script 05 · name`, `goes to Room 12`, `maybe plays Sound 7`, `×N`, one click opens that resource in the tree), a grey `Also: 3 lines of dialogue, 2 object changes` line, then a nested expander whose text is the **Readable** script plus the session interpretation. | Gold heading `ROOM SCRIPTS`, then one **expander per script** (`Room entry`, `Room exit`, `Local script N`) each holding a no-wrap raw code box; heading `INTERACTIONS` with one expander per (object, verb) holding detail and raw code; heading `RELATIONSHIPS` with one flat list of buttons for every instruction of all of them together. | R-6.43 and R-6.25 items 4 and 6 **allowed** this ("raw source ... a readable v1/v2 view is a later item"; "LOOM's Room entry stays LOOM's own panel"). The user now rejects it. |
| `Last action` expander: summary, timeline (readable, one line per step), issues, nested raw trace. | None. | R-6.11 (LOOM only). |
| `Assumptions` expander (`No assumptions` / `Assumptions · N`). | None. | R-6.11 (LOOM only). |
| Object preview + description under the lists. | Not built. | R-6.11 / R-6.43 |
| Script page `Used by` (references to this script/object, from `LoomUsageIndex`). | Absent (index built only for LOOM). | R-9.1 |
| Script-page and Room-entry text is the Readable view. | Raw only until today's commit `f6f694d0`. | R-9.x |

### B. Main room area

| Element | LOOM | Generic |
|---|---|---|
| Room picture | Room image + actor overlay + 4 overlays in a scroll viewer, `Original framing` plus scroll buttons (`Scroll room left/right`) and Left/Right keys. | Whole `RoomDisplayGroup` is collapsed in Play and replaced by the composed game screen (`PlayScreenImage`): no scroll buttons, no `Original framing` effect, no verb strip control, **no corner full-screen button** (it lives inside the collapsed group; F11 still works). Unclear if intended. |
| Verb strip | Under the room, 56 rows, hover tooltip Advanced, full-screen button on hover. | The game's own verb area is part of the composed screen. |
| `Interactive` checkbox (right panel) | Shown when a session exists; off = inspection mode. | Never shown (`_session` is the LOOM session). R-6.7 says every game. |
| `↺ Restart game state` | Advanced, header row. | **Absent**: it recreates a `LoomExplorationSession`. R-15.2 says Restart is the app's. |
| `Connections` / `Explore links` | LOOM EGA v3 and CD, Advanced. | Absent (relationship index is LOOM-only). |
| Music strip `RoomMusicPanel` (`MUSIC ▶ Ⅱ ■ title time`) | Shown in inspection mode / Advanced for LOOM. | Not used. |
| Session status line / `ObjectsNote` | Texts per mode. | Different/empty in Play. |
| Header `Play Game Intro` | Chosen by `GameId` inside App (R-6.38 "not yet met"). | Same code path, game test. |
| Esc in full screen, one key handler, LOOM CD Sounds rows, double-click hint | Listed "not yet met" in R-6.34, R-6.40, R-6.45 and the 24 Oct checkpoint. | |

### C. Wording that is LOOM-only but used for all
Interactive tooltip (Bobbin, distaff keys), `ScriptNote` initial texts by game, page subtitle chains by `GameId`/version (`MainWindow.xaml.cs:1230`), `ShowObjectVerbChoices` ("Choose an action to explore it").

## 3. Proposed target (pending the answers below)

One Advanced panel for every game, exactly LOOM's order and widgets, filled through the provider:
`DOORWAYS` > `HOTSPOTS` > `ACTIONS · <object>` (flat verb buttons + `Used by`) > preview/description > `Room entry` (summary, event buttons that open tree resources, nested Readable script) > `Last action` > `Assumptions`.
The provider returns *data* (entry/exit/local scripts as `ScummScriptDisassembly`, events, last action record, assumptions); the App builds one widget set. `Inspection()` with `ROOM SCRIPTS` / `INTERACTIONS` expanders and the no-wrap raw boxes are removed. Room exit script and room-local scripts become event buttons (to tree nodes) like entry-script events; where the tree has no node for a local script (Maniac Mansion, Zak, Indy3 EGA) the node is added (`Room NN local script N`, as LOOM CD and v4/v5 already have).

Requirement edits (before any code): rewrite R-6.43 (LOOM panel is the contract for all games), strike R-6.25 items 4 and 6 and the "readable v1/v2 later" clause, widen R-6.11 and R-9.1 tags to `SCUMM, APP`, add rows for Restart (R-15.2), Interactive (R-6.7), Used by, Connections, and keep the "not yet met" items in R-6.30 as the migration list.

## 4. Open questions
See the chat message.
