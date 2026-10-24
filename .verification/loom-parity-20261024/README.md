# LOOM parity of the Maniac/Zak room controls — 24 October 2026

User, 24 Oct: "some of the changes already made to fix those games are not following guidelines. Meaning they are not the same behavior as LOOM. We should align them to LOOM and clarify REQUIREMENTS as needed." Rule recorded as R-6.21 to R-6.25 (`5f0a8935`).

Method: `ParityAudit/` (serial, private profile `SCUMM_EXPLORER_PROFILE_ROOT`, one real `MainWindow`, muted for Maniac/Zak) dumps the visible controls, tooltips and popups in Normal and Advanced. Baseline = merged master `bf0341ec` (static previews keep their bar): `before/`; after the changes: `after/`. Rooms: LOOM EGA 02 and 13, Maniac Classic/Enhanced 26, Zak Classic/Enhanced 16; `static` = catalog preview, `live` = native session after the genuine intro (Maniac: real party clicks and skip). Command: `ParityAudit.dll D:\Juegos\Lucas <out> "<folder>" <room> static|live`. LOOM "live" clears catalog-preview-only by reflection (emulates the first in-room action; harness only). Evidence: `before/*/audit-*.txt`, `after/*/audit-*.txt`, `Summarize.ps1`.

## Parity table (baseline, merged tree)

| Behaviour | LOOM | Maniac/Zak (baseline) | Same? | Decision |
|---|---|---|---|---|
| Playback bar, Pause/Play, Skip (Esc), Skip line | present, enabled by activity | same buttons and enablement | same | – |
| Pause tooltip | "Pause or play the scene (space). In Advanced mode, Step ▸ …" | "Pause or resume the game, including walking…" | differs | aligned (R-6.23c) |
| `Step ▸` (Advanced) | present, enabled while paused | absent | differs | aligned (R-6.23b), one frame of the game clock |
| `Play Cutscene` button, caption, popup heading `CUTSCENES IN THIS ROOM`, `Room NN · …` titles, `(played from Room NN)`, no tooltips in Normal, `(verb 0xHH)` + tooltip in Advanced, one entry plays directly | as stated | same format, same ordering (played-from first) | same | – |
| `Interact` | bar button `Interact ▴`, caption `Interact · <name> ▴`, popup headed by object name in capitals, rows = verbs' words, right click opens it | button hidden; an `Interact` expander under the bar listing every action of the room as `Verb · Object` | **differs** | **aligned** (R-6.22) |
| Hotspot rows (Advanced) | click opens the object's menu | click expanded the old list | differs | now opens the object's menu |
| Hotspots/Doorways options | `Never/On hover/Always`; `Doorways` hidden when none | same, but **no radio selected in any game, LOOM included** (orchestrator's observation confirmed: all six `IsChecked` false after construction) | reference itself deviated | fixed for all games (R-6.23a, `4fff7a75`) |
| Depth areas / walk boxes (Advanced) | toggles, tooltips with counts | toggles, enabled when data exist | same | – |
| `Sounds` menu | `SOUNDS IN THIS ROOM`; `▶ ♪ Music N · m:ss`, `▶ Sound effect N`; `■ Stop` only while playing | `■ Stop` always; `▶ Sound N` | differs | aligned (R-6.24) |
| Sounds in Advanced | `Sound 13 · ♪ Music 1 · Room 02 object 154 Verb 0x56` + path tooltip | same format | same | – |
| Advanced right panel | `DOORWAYS`, `HOTSPOTS`, `Room entry` (readable/raw) | `DOORWAYS`, `HOTSPOTS`, `ROOM SCRIPTS`, `INTERACTIONS`, relationships (raw) | partly | data difference (R-6.25.4); readable script view for v1/v2 left open |
| Static preview | bar + Play Cutscene + Interact + Sounds (since bf0341ec) | Zak: same; Maniac: `Interact` disabled | Maniac differs | justified: party choice R-7.20/R-8.34 (R-6.25.1) |
| Click/double-click | click walks+uses, double click looks | native verbs and sentence line | differs by design | R-7.7/R-7.19; double-click look left open (R-6.25.2) |
| Game Status | `Apply and close` / `Cancel`, 900×780 | `Apply and close` / `Cancel` / `Undo last change` | same one-click rule | data (R-6.25.3) |
| Normal-mode technical wording | none | none in the dumps | same | – |

## Changes (all App-only, shared path)

- `4fff7a75` radios default selected (`EnsureOverlayModesChecked` on game open).
- `0c41bad3` Sounds menu format of generic games.
- `7f28ae1b` Interact button + menu through `OpenPopupMenu` (the LOOM widget), right click where the app owns it, Step ▸, Pause tooltip. The old Interact expander is gone; `.verification/maniaczak-room-actions-20261019` drives that expander and is a record of that run only.
- `5f0a8935` requirements.

Generic-path changes also reach the Indy3, Monkey Island and Indy 4 sessions: the Interact button/menu (they show it instead of the expander), right click for engine version 3 and below, Step ▸, Sounds rows, radios.

## Checks

Solution build zero warnings/errors. 198 focused tests (overlay, architecture, support matrix, Maniac, Zak) pass; Core untouched, so the LOOM suites are unaffected. After-window runs: LOOM 13 live (Interact menu `OBJECT 323 / Use / go` unchanged), Maniac Classic 26 static and live, Maniac Enhanced 26 live (Interact `Interact · puerta ▴`), Zak Classic/Enhanced 16 live, Zak Enhanced static. Not re-run: Indy3/Monkey/Indy 4 windows (other agents).

## User checks

1. Open Maniac Mansion (any edition), play the intro, go to Room 26 (or any room). The bar shows `Interact ▴`; click an object (e.g. the door): the button reads `Interact · door ▴`; click it: a menu headed `DOOR` lists `Open`, `Close`, `Walk to`. Right click an object: same menu at the pointer.
2. Advanced: Pause, then `Step ▸` plays one frame.
3. Any game, Options: `Never` is selected under Hotspots (and Doorways when shown).
4. `Sounds ▴`: rows read `▶ Sound effect N` / `▶ ♪ Music N`; `■ Stop` only while a sound plays.
