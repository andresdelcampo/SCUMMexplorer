# Room "Sounds" menu and a note for every sound (4 October 2026)

## "Other possible music" is replaced by "Sounds ▴"

The old button lived in the room's MUSIC panel, which only exists in inspection mode (in the interactive mode the game plays its own music; 30 September decision), and it listed only music of 15 seconds or more. It is removed. A "Sounds ▴" button now sits in the room's action row beside Interact and Play Cutscene and opens the same upward pop-up (`OpenPopupMenu`): heading "SOUNDS IN THIS ROOM", then every sound the room's scripts may start, music first (longest first), then effects. It does not overlap Interact (objects) or Play Cutscene (scenes); it is the audio explorer. It appears in both modes whenever the room has at least one candidate; disabled while a scene plays.

- Source: `LoomRoomSoundCandidates` (static scan of the room entry and object verbs, following scripts they start and the sound numbers they hand down), plus sounds the session itself started in this room. These are possible starts: the button's tooltip says the game does not necessarily play all of them; in Advanced each row's tooltip shows the path that starts it.
- Row text: plain "▶ Sound 13" (see the decision below).
- Click: stops the game's own sounds, then plays that sound on demand through the existing room-cue player with the selected device (PC Speaker or AdLib; with No Sound it says sound is off). An exploration action; the game starting a sound stops it. While one plays the menu offers "■ Stop".

## Decision (user, 4 October): sounds stay plain

A first version labelled every sound from the data (music or effect, and the rooms and scripts that start it: 17 of 65 found a start, 48 "no start found"). The user was not convinced: music/effect is sometimes misplaced and most sounds have no room. All of it was removed, including the three glossary names ("opening music", "music for Room 37's entry", "trial music"): every sound reads "Sound NN" in the Advanced tree, tooltips and the Sounds menu, and the menu lists them in number order. `LoomResourceNames.Entry` returns no name for sounds. The room scan (which sounds a room may start) is unchanged and only decides which sounds the menu offers.

## Action row in Advanced mode

Step and the Sounds button made the row too wide in Advanced, so Play Cutscene wrapped alone. The buttons are now two groups inside the wrapping bar: playback controls (Pause, Step, Skip, Skip line) and room actions (Interact, Sounds, Play Cutscene). They share one line when they fit (normal mode, 480 px) and otherwise the actions drop to a second line as a whole (Advanced, 285 x 51). Shots: `bar-1400-normal.png`, `bar-1400-adv.png`, `bar-1180-adv.png`. The 1 October layout harnesses (StaffCheck, StatusRefinementCheck, RefinementCheck) assert that the room's Play Cutscene is a direct child of PlaybackBar right after Skip line; that is no longer so and they were not updated.

Checks: scratch harness over the real MainWindow (sound labels plain, the Sounds button visible in Rooms 3 and 17, the bar in both modes and two window widths), a screen capture of the menu earlier. Build only; playback was not listened to.
