# Does the room media player still make sense? (item I, 30 September 2026)

The analysis, then the change the user chose. The room music player (the "MUSIC" row under the room: `UpdateRoomMusicActions`, `PlayRoomCueAsync`, `_audioPlayer`, the RoomCue handlers) is another agent's code (27–28 Sep: 494f0e1, 798d094).

## What the two paths do

- **The MUSIC panel** (since 27 Sep). For the room shown it lists music tracks of 15 s or more from two sources: sounds this session's route reached in the room (`LoomSoundCueResolver.ResolveForRoom`), and "possible" sounds from static paths through the room's scripts and the global scripts they start (`LoomRoomSoundCandidates`). The longest is the default; "Other possible music" lists the rest. Play renders it (AdLib) and plays it in `_audioPlayer`, on its own, from the start. It is shown in both modes.
- **The scene sound player** (`SceneSoundPlayer`, 29 Sep). In the interactive mode, it plays every `startSound`/`stopSound`/`stopMusic` the session reaches, on the frame it is reached, while the playback clock runs: cutscenes, the opening, and the frames after a click or key. This is the game's behaviour (reached events).

## Measurements

- Per room (session probe, catalog visit to each of the 76 rooms plus 30 frames; [music-probe.tsv](music-probe.tsv)): the panel offers music in 11 rooms.
  - Rooms 16, 24, 30, 36: the same track the room's entry starts (sounds 30, 28, 28, 27). Room 24 also offers sound 29.
  - Rooms 02, 03, 08, 09, 10, 66: sounds 31 and 25, which the cutscenes start (script 039 `startSound local[0x000]`, called by scripts 044, 046, 052), labelled "possible room music".
  - Room 41: its entry starts only an effect (sound 12). The other 64 rooms: no music either way.
- MusicPanelCheck before the change ([music-panel-check-before.txt](music-panel-check-before.txt); `.verification/room-music-analysis/MusicPanelCheck`, `<Games\loom> <output folder>`, about 70 s; real MainWindow, interactive mode):
  1. Catalog visit to Room 16. The panel says "Possible: Sound 30 · from room script data … those paths have not been reached", although the room's code does start it. The scene sound player plays nothing on the visit.
  2. First click in Room 16 (assumed input): only now does sound 30 start (frame 1 of the click's playback, script 039 +0x0038). The panel then collapses to "No cue established".
  3. Room 08, the panel's Play: sound 31 plays; it stops any scene sounds ("another sound was started in the app").
  4. Then the trial from the cutscene menu: script 039 starts sound 31 in the scene player while the panel's player keeps playing its own copy (position 0:28 after 25 s). The same track plays twice, out of step. The scene player never stops the panel's player. The user heard this run: "two background musics playing at the same time, with offset, same piece".

## Analysis

- In the interactive mode the panel duplicates what the game now does, and it disagrees with it: it calls reached music "not reached", it offers cutscene music as room music, and its copy overlaps the game's.
- The one thing the panel gives there that the game does not yet: music on arrival after a catalog visit. That is a gap in the scene sound path, not a reason for a second player: the entry's reached `startSound` should play when the room is shown, not at the next click.
- In the inspection mode no script runs, so the panel is the only link from a room to its music. Its "possible" label is accurate there. Every track can also be heard from the tree's Sound resources (`AudioPanel`, `AudioSurface.Resource`).

## Recommendation

1. Hide the MUSIC panel in the interactive LOOM mode, and stop the panel's player when the interactive mode starts a scene sound (so they never overlap).
2. In our own code (the scene sound path, not the panel): after a catalog visit, play the sounds the entry reached as the room is shown, so Rooms 16, 24, 30 and 36 play their music on arrival.
3. Keep the panel in the inspection mode, as it is (labelled "possible"). Or remove it altogether if the user prefers the Sound resources for listening.

Changing the panel (1, 3) needs the user's agreement, because it is another agent's code.

## The user's choice and the change

The user chose "hide in the interactive mode" (1 and 3, with 2).

- `UpdateRoomMusicActions`: in the interactive LOOM mode the panel stays hidden and a cue it is playing stops (`StopAudio`). The inspection mode is unchanged. Turning the interactive mode on or off reapplies the room state, so the panel hides or shows at once.
- `SceneSoundPlayer.Starting`: raised when a reached `startSound` is about to play; MainWindow stops the app's other player (the room panel's or a Sound resource's) then. The reverse (another player's `MediaOpened` stops the scene sounds) was already there.
- Recommendation 2 (the entry's music on arrival after a catalog visit) is not done. Script 039 stops the old track, runs `breakHere`, and starts the new one on the next frame (+0x0021, +0x0038, decoded). After a catalog visit, the app runs no frame until the first click, so the music starts then. Running the arrival's frames at once changes the room-load path, so it is a separate task.

## What the user now sees

- In the interactive mode there is no MUSIC row under the room. Only the game's own music plays (from the frames the app runs), so a cutscene's track no longer plays twice.
- In the inspection mode, the MUSIC row is as before ("possible" music from the room's script data).
- In the interactive mode, a room's music after a catalog visit still starts only with the first click (Rooms 16, 24, 30, 36).

## Evidence after the change

- MusicPanelCheck, 0 failures ([music-panel-check.txt](music-panel-check.txt)). In Room 16 (interactive) the panel is hidden. In Room 08 the panel is shown in the inspection mode and its Play works. Turning the interactive mode on hides it and stops its music. The panel's cue was then started through reflection in the interactive mode, and the trial was played: after 25 s the trial's own sound 31 plays and the other player is stopped. Limit: in that step, `PlayKnownCutscene` reapplies Room 08's state, which already stops the panel's player through the hide. So the check does not isolate the `Starting` path. The `Starting` path was not tested on its own (for example, a Sound resource playing when a click starts music).
- `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests 244/244 (App-only change).
- Not rerun: SceneSoundCheck, the playback checks, DoorwayClickCheck (no room-click or room-load change; the scene sound player only gained an event).
