# First-room timing (10 October 2026)

`FirstRoomTiming <game folder> <room> [room...]` opens the folder in the real `MainWindow` and prints how long `OpenGameFolder` and
each room selection take (it also times a few Core builders directly).

Finding: on the English reference edition the first room took ~10 s because `UpdatePlaybackControls` asked `RoomCutsceneItems` for
the room's cutscene buttons, which built the notes of all ~328 catalog entries; each note derives a start setup (earlier scenes run
unseen). Now only the room's entries are built (`PlayableCutsceneItems(onlyIds)`): the first room loads in ~1.6 s (Debug build), the
second in ~0.3 s. The script draw batches (0.7 s) are built once on a worker (`DrawBatchesAsync`) and shared by room cards, first
room and Game Texts. What remains of `OpenGameFolder` (~1.1 s) is mostly the live session boot (~0.9 s), glossary (~0.1 s) and
ShowGame (~0.15 s); the catalog fingerprint is ~25 ms (hashed in parallel).
