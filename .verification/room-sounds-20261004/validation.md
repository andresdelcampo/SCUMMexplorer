# Room "Sounds" menu and a note for every sound (4 October 2026)

## "Other possible music" is replaced by "Sounds ▴"

The old button lived in the room's MUSIC panel, which only exists in inspection mode (in the interactive mode the game plays its own music; 30 September decision), and it listed only music of 15 seconds or more. It is removed. A "Sounds ▴" button now sits in the room's action row beside Interact and Play Cutscene and opens the same upward pop-up (`OpenPopupMenu`): heading "SOUNDS IN THIS ROOM", then every sound the room's scripts may start, music first (longest first), then effects. It does not overlap Interact (objects) or Play Cutscene (scenes); it is the audio explorer. It appears in both modes whenever the room has at least one candidate; disabled while a scene plays.

- Source: `LoomRoomSoundCandidates` (static scan of the room entry and object verbs, following scripts they start and the sound numbers they hand down), plus sounds the session itself started in this room. These are possible starts: the button's tooltip says the game does not necessarily play all of them; in Advanced each row's tooltip shows the path that starts it.
- Row text: "▶ Sound 13 · music · 0:02" (a glossary name replaces the kind: "▶ Sound 25 · opening music · 2:23").
- Click: stops the game's own sounds, then plays that sound on demand through the existing room-cue player with the selected device (PC Speaker or AdLib; with No Sound it says sound is off). An exploration action; the game starting a sound stops it. While one plays the menu offers "■ Stop".

## A description for every sound (Advanced tree and tooltips)

Before: three of the 65 sounds had a name ("opening music", "music for Room 37's entry", "trial music"). Now every sound carries a note from the data alone: music or effect (does the resource hold music data), and the rooms and scripts whose decoded code starts it (`LoomUsageIndex` literals plus the room scan above), or "no start found". Glossary names still win where there is one. The tooltip gives the evidence lines, and says plainly that starts through other variables are not found.

Result on the LOOM data (`sound-labels.txt`): 65 of 65 labelled; 17 name a room or script, 48 say "no start found" (most effects and a few music tracks; the data does not show who starts them; the note makes no claim about them). Notes are computed in the background after loading and the tree updates when ready.

Checks (scratch harness over the real MainWindow, plus a screen capture of the open menu): the button is visible in Rooms 3 and 17 (1 and 2 choices), the menu opens upward with its rows, the tree labels fill in. Build only; the other harnesses were not run. Playback itself was not listened to.
