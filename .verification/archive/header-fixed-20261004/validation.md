# Header: fixed placement for sound, Advanced and Open (4 October 2026)

The header is right-aligned, so everything to the right of a button that appears or disappears used to shift. The sound dropdown and the Advanced switch sat before Play Cutscene / Game Texts / Game Status, which come and go with the game and with Advanced. They now sit after those buttons, with Open Game Folder, as a fixed group at the right edge; the buttons that come and go are to its left.

Measured in the real MainWindow (`report.txt`): sound dropdown x = 960 (w 142) and Advanced switch x = 1116 (w 94) at width 1400 in all three states: no game, game loaded, Advanced on. Shots: `1-nogame.png`, `2-loaded.png`, `3-advanced.png`.

## Game Status shown from the loaded game (same day)

Game Status needs the exploration session, not a room: the session is created when the game opens, and the status window only syncs the room view when a room happens to be open. The button was nevertheless shown only after the first room visit (the playback-controls update). It now appears as soon as a LOOM game is loaded, so the header no longer changes when the first room is chosen. Harness: after load the button is visible and enabled, and `BuildGameStatusWindow` builds with no room chosen. Fixed group positions unchanged (960 / 1116).

## Dialog buttons: no ellipsis, no arrow; menus open upward (same day)

Decision (user): no trailing ellipsis on buttons that open a window (an old convention); nothing at all. So "Play Cutscene", "Game Texts", "Game Status", "Open Game Folder" and the Game Texts window's "Export visible text" carry no ellipsis or arrow.

A menu arrow stays only where a real pop-up menu opens, and it points the way the menu opens: Interact ▴ (it always opened upward; the arrow said ▾ before). The room's Play Cutscene now behaves like Interact when the room has several cutscenes: "Play Cutscene ▴" opens the same dark pop-up upward (heading "CUTSCENES IN THIS ROOM", one row per cutscene; in Advanced the row's tooltip shows its source note) instead of the picker dialog; with one cutscene it still plays directly and shows no arrow. The pop-up code is shared (`OpenPopupMenu`); the room picker window builder was removed. The global Play Cutscene (all cutscenes, with search) stays a dialog. Checked on Room 2 (6 cutscenes) in the real window with a screen capture: the pop-up opens above the button with the six rows (the capture itself is not kept, it shows the desktop). Release build clean. "Other possible music ▾" is a context menu whose direction was not checked.
