# Sound in cutscenes (29 September 2026)

The user reported (29 Sep) that cutscenes play without music or sound. The session already recorded every sound
command its scripts reach (`LoomExplorationSession.SoundEvents`); only the room music panel read them, and only to
offer a cue on demand. That panel belongs to another agent's work and was not edited.

## What the user now sees (and hears)

- While the playback clock runs (the opening, a known cutscene, the frames after a click), each sound a script
  starts begins on the frame the script reaches its `startSound`; `stopSound` stops that sound and `stopMusic` stops
  all of them (ScummVM `o5_stopMusic` → `stopAllSounds`). Sounds overlap, as the game's channels do.
- The opening: sound 25 (AdLib music, 2:23) starts on the second frame (script 039 +0x0038, started by script 044)
  and plays through the opening.
- The Elders' trial (046): sound 31 (music) from its first frame; at frame 1788 in Room 09 it stops and the four
  notes 8, 4, 5, 1 (effects) play 10 frames apart, then the short AdLib cues 23, 16 and 17 in Rooms 09, 03 and 08,
  the notes three more times, and sound 25 again in Room 11 at frame 2475.
- The firewood scene (086): Room 37's entry starts sound 27 (music, 1:13) before the scene; it plays from the first
  frame (session probe; no harness).
- Pause (space) pauses the sounds; Play resumes them. The sounds play on after a playback ends, as the game's music
  does between scenes. A new stated start ("Play a cutscene"), a session reset, another game, turning the
  interactive mode off, or starting any sound in the app's own player stops them.

## Game behaviour and presentation

- Game behaviour: which sound starts or stops, and on which frame (reached events).
- Presentation: music tracks play as the AdLib rendition (`LoomAdlibAudio`, gain +6 dB as the room panel uses).
  The game's AdLib sound effects (ScummVM `Player_AD` sfx) are not decoded, so effects (a resource whose AdLib part
  is not a music track, byte 2 != 0x80) play as the PC speaker rendition of the same resource. A track still being
  rendered when its frame comes starts late, at the position the game would have reached (after 20 s on the clock,
  the opening's music stood at 18.9 s).
- Sounds started between playbacks (a catalog visit's entry, a direct verb) play with the next playback frame.
- An instant playback (the internal `_playInstantly` option) sorts its sounds out once at its end.

## Changes

- New `src/SCUMMexplorer.App/SceneSoundPlayer.cs`: follows one session's `SoundEvents`, renders each track once per
  game (cached WAV in %TEMP%), plays it on its own `MediaPlayer`, logs what it did (`Log`, for the harness).
- `MainWindow.xaml.cs` (playback code only): `_sceneSound`; `PlaybackStep` calls `Update()` after each frame;
  `FinishPlayback` flushes; `StartPlayback` attaches to a session it does not follow; `PlayOpening` and
  `PlayKnownCutscene` attach right after building their session; pause/resume; stops on reset, game load, mode off,
  and when the app's own `_audioPlayer` opens a sound (one added event handler). The room music code is untouched.

## Verification

- New SceneSoundCheck (`.verification/cutscene-sound/SceneSoundCheck`, `<Games\loom> <output folder>`, about 70 s):
  the opening on the real clock for 20 s, then script 046 stepped through the playback's own timer tick for 2500
  frames; compares the app player's starts and stops per frame with a separate session's reached events from the
  same stated start. Result ([scene-sound-check.txt](scene-sound-check.txt)): 0 failures. The opening: 1 of 1 at the
  same frame; sound 25 audible (MediaPlayer position 18.9 s after 20 s, HasAudio). Script 046: 28 of 28 starts and
  stops on the same frames; 31, 23, 16, 17, 25 as AdLib music, 8, 4, 5, 1 as PC speaker effects. A new stated start
  stops the previous sounds.
- `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 230/230 (no Core change).
- Skipped (the change does not touch clicks, room loads or layout): DoorwayClickCheck, CutsceneLayoutCheck,
  FirewoodPlaybackCheck, OpeningPlaybackCheck, CutscenePlaybackCheck, the audit and Blockers.
- Not verified by ear: the user is asked to listen to the opening and the trial.
