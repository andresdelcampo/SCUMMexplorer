# Header: fixed placement for sound, Advanced and Open (4 October 2026)

The header is right-aligned, so everything to the right of a button that appears or disappears used to shift. The sound dropdown and the Advanced switch sat before Play Cutscene / Game Texts / Game Status, which come and go with the game and with Advanced. They now sit after those buttons, with Open Game Folder, as a fixed group at the right edge; the buttons that come and go are to its left.

Measured in the real MainWindow (`report.txt`): sound dropdown x = 960 (w 142) and Advanced switch x = 1116 (w 94) at width 1400 in all three states: no game, game loaded, Advanced on. Shots: `1-nogame.png`, `2-loaded.png`, `3-advanced.png`.

## Game Status shown from the loaded game (same day)

Game Status needs the exploration session, not a room: the session is created when the game opens, and the status window only syncs the room view when a room happens to be open. The button was nevertheless shown only after the first room visit (the playback-controls update). It now appears as soon as a LOOM game is loaded, so the header no longer changes when the first room is chosen. Harness: after load the button is visible and enabled, and `BuildGameStatusWindow` builds with no room chosen. Fixed group positions unchanged (960 / 1116).
