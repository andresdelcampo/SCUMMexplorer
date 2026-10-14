# v5 sound leftovers (14 October 2026)

Families MonkeyCd, Monkey2, Indy4 (SCUMM v5), stage S3. Scope: what needs no running script. The Sound cell stays **partial**.

Fixtures (main checkout `Games/`; the registry names `Games/FUTURE/...` but the folders now sit directly under `Games/`, so the `FUTURE/` conformance fixtures skip in a fresh worktree): `mi2 talkie en`, `mi2 mt32 en` (Roland data and the user's ROMs in `Games/mt32`), `indy4 vga en` (`ATLANTIS.cue`), `monkey1 vga en` (`The_Secret_of_Monkey_Island.cue`).

## ScummVM sources read (master branch, fetched 14 October 2026)

`engines/scumm/imuse/imuse_player.cpp` (`Player::onTimer` loop check, `setLoop`, `clearLoop`, `maybe_jump`, `jump`, `maybe_set_transpose`, `maybe_part_onoff`, `maybe_set_volume`, `maybe_set_program`, `maybe_set_transpose_part`, `setTranspose`, `part_set_transpose`), `imuse/sysex_scumm.cpp` (codes 0, 1, 16, 17, 48 to 53, 64, 80, 81), `imuse/imuse_part.cpp` (`set_transpose`, `set_onoff`, `volume`, `programChange`), `imuse/imuse.cpp` (`doCommand_internal`: the script commands), `audio/midiparser.cpp` (`jumpToTick`, `processEvent`, `allNotesOff`), `sound.cpp` (`triggerSound` type 2 = CD track), `soundcd.cpp` (`getCDTrackIdFromSoundId`, `playCDTrackFromSoundID`), `script_v5.cpp` (the only direct `playCDTrack` is LOOM CD's).

## 1. Looping (`ScummImuseTimeline`)

Before: the Roland, AdLib and PC speaker renderers ended the wave at the first jump back or at the **loop sysex 0x7D 80** (they treated it as "loop here"). That was wrong twice: sysex 80 is not a loop point, it *sets* the loop (count, to-beat, to-tick, from-beat, from-tick; refused unless to-beat + 1 < from-beat), and the jump happens later, when the position reaches the from-point. A song such as MI2 sound 120 (`80@110 [1000,4,400,84,398]`) therefore rendered about a tenth of a second.

Now one shared walk (`ScummImuseTimeline.Build`) gives the song as ScummVM plays it with no script:

- hook 0x7D 48 with id 0 always jumps to (beat - 1) * 480 + tick (a jump back is a loop, forward skips); other ids wait for a script and are never taken;
- sysex 80 sets the loop, 81 clears it; the jump to the target happens when the position passes the from-point (events at the from-tick itself still play), the counter counts down (1000 = for ever);
- a jump (`jumpToTick`) does not replay the events before its target (set-ups and controllers come once), plays the events from the target tick on, and first stops the sounding notes and sustain (synthetic CC 123 and CC 64 = 0 on every channel);
- a looping song is unrolled until the first jump back that is at 180 s or later (`MinLoopSeconds`, the same three-minute preview limit the other devices use; hard cap 420 s) and the wave ends there. A song whose count runs out (or a forward-only song) ends at the end of its track.

The three renderers consume the timeline (AdLib and PC speaker keep their 250 Hz quantization; Munt gets timed messages). Behaviour without a loop is unchanged: no jump means the same events at the same seconds. Not changed: instruments, tempo, pitch, the PC speaker pitch fix, sampled effects.

Data (all ROL/ADL/SPK parts of each fixture; `Timeline.Loops`): Monkey Island 2 talkie 407 parts, 176 loop; Indiana Jones 4 456 parts, 226 loop; Monkey Island 1 CD 130 parts, 15 loop. Every looping part ends at a loop point at 180 s or later. By device: MI2 ROL 75 of 144, ADL 85 of 196, SPK 16 of 67; Indy 4 ROL 105 of 204, ADL 103 of 200, SPK 18 of 52; MI1 CD ROL 0 of 13, ADL 12 of 76, SPK 3 of 41. Mechanisms in the data (counts are events over all device parts): the always-taken jump hook (1053 with id 0; 4036 with another id, which wait for a script) and the loop sysex 80 (12 events, in the parts of MI2 sounds 120, 125, 151, 181, 212).

Timing: MI2 sound 28 on the MT-32: 3 minutes of music rendered in 14 s (Munt), 0 s for AdLib and PC speaker.

Tests (`V5ImuseLoopTests`, all green): the counted loop returns to its target and plays the **same events** again (note-on at 1.0, 2.5, 4.0, 5.5 s, same tick and bytes, the set-up and the loop sysex only once, notes cut at each jump); a loop count of 1000 stops at the first jump at or after 180 s; a refused loop (to-beat + 1 >= from-beat) and a cleared loop do not jump; hook id 0 loops, hook id 3 does not; a forward hook skips its stretch; AdLib waveform: the second pass sounds the note again with the same energy (ratio 0.8 to 1.25) and silence at the jump; Roland (needs the ROMs): MI2 sound 28 reaches 180 s and the wave length matches; real data: more than 20 AdLib sounds loop.

### Uncertainties kept

- The jump is taken at the from-point to the tick; ScummVM takes it on the first 250 Hz iMUSE timer step at or after it, so events in the overshoot (less than 4 ms, one step) would also have played. Not modelled.
- Where the loop ends in the wave (at 180 s or the next jump) is ours; ScummVM repeats for ever.
- Nobody has listened to the new loops. The earlier by-ear confirmation covers the first pass only.

## 2. Hooks 49 to 53 with id 0 (found on the way)

The renderers ignored the unconditional (id 0) hooks other than the jump. ScummVM acts on them at once (`maybe_set_transpose`, `maybe_part_onoff`, `maybe_set_volume`, `maybe_set_program`, `maybe_set_transpose_part`; `if (cmd && p != cmd) return;`). In the three games (events over all device parts): 283 part on/off (50), 4 volume (51), 20 global transpose (49), no id 0 of 52 and 53. The AdLib and PC speaker players now apply 49 to 53, the MT-32 plan applies 50 to 52 (it ignores transposes, as before). Effect for the listener: a part switched off by such a hook is silent from there on, as in the game (the parts that a script switches back on with an id 1 or 2 hook stay off in the default path). Test: a part-off hook with id 0 silences the AdLib part, the same hook with id 2 does not.

## 3. The two unnamed Monkey Island 1 CD tracks

Finding: the track list was **off by one**. The sounds 100 to 129 hold a track byte from 1 to 24, and the disc has exactly 24 audio tracks (cue tracks 2 to 25 after the data track): the game counts the audio tracks from 1 (`AudioCDManager::play(track)`; track 0 would be the data track). The 12 October version matched the byte to the cue number, so every name was one track too late, sound 115 (track 1) matched nothing, and tracks 20 and 25 stayed "unnamed".

Corrected (`GameFolderScanner`): disc audio track N is the game's track N - 1. Evidence: the room names now fit the music (disc track 2 "fortune", 4 "high-stre", 6 "melee", 9 "bar", 15 "monkey-1", 18 "lookout" and "logo" = the intro/outlook track that ScummVM adjusts), the four chapter rooms "part1" to "part4" (sounds 104 to 107, one-shot) play the 13 s disc track 7, and the three other one-shot sounds (119, 129) play the short disc tracks 14 and 20 (18 s), where the old reading attached long looping tracks to them. Result: **one** track, disc track 21 (16 s, between the dock jingle and the jungle music), is started by no sound.

Why it stays unnamed: the only way ScummVM starts a CD track in this game is `startSound` of a sound with type 2 (`sound.cpp triggerSound`, `playCDTrackFromSoundID`); the direct `playCDTrack` of `script_v5.cpp` is LOOM CD's. The directory has no resources 124 and 125 (so nothing could play for them even if a script asked), and a scan of every script, entry/exit script and object verb (`ScummScriptReader` V5; 274 sound instructions) finds no literal start of 124 or 125. Six `startSound` operands are variables (script 101 `var[100]` four times, script 162 `local[4]`, room 75 local script 205 `local[0]`): which sound they start needs the running scripts (S6). No evidence names the track, so it stays "not started by a sound". Test: `V5SpeechTests.TheMonkeyIsland1CdTracksSayWhereTheyPlay` (needs `SCUMM_MI1CD`): 24 tracks, only "CD track 21" is unstarted, sound 115 on disc track 2, sound 129 on disc track 20.

Indy 4 CD: the image has one data track (the cue lists TRACK 1 MODE1 only), so it has no tracks to name; its music is iMUSE on the devices.

## 4. What only S6 (Play) can finish, with the ScummVM source behind each

1. **Hooks that wait for a script's id.** Jump 0x7D 48 with id other than 0 (4036 events in the three games), part on/off 50 (id 1: 286, id 2: 78), volume 51 (id 1: 18), program 52 (17), global transpose 49 (8), part transpose 53 (6). The script sets the id with the iMUSE command `setHook` (`imuse.cpp doCommand_internal`, player commands 12 and 20 -> `Player::setHook(cls, value, chan)`; consumed in `Player::maybe_jump`, `maybe_part_onoff`, ... `imuse_player.cpp`). Example: MI2 sound 151 (`48@5680 id1 t1 b4 k400`, `id1 t5 b1 k0`, ...) switches between sections only when the script says so.
2. **Loop control from scripts**: `setLoop` (command 9) and `clearLoop` (10) from a script change or end the loop that sysex 80 sets; sound 120's second half (`48 id2 ... b84 k400`, the ending at tick 109358) is reached only after the script ends the 1000-count loop.
3. **Markers and queued commands**: sysex 64 (`handle_marker`) fires a trigger a script queued with `enqueue_trigger` (14) / `enqueue_command` (15) / `clear_queue` (16) (`imuse.cpp`); the data holds 1647 marker events (MI2 sound 125 has 38 per part).
4. **Sound status for waits**: `isSoundRunning` (command 13, `getSoundStatus_internal`) and the **music timer** (`Sound::updateMusicTimer`, `VAR_MUSIC_TIMER` from `getMusicTimer`; for the CD game `getCDMusicTimer`) are what scripts wait on.
5. **Per-room music**: rooms start music with `startSound`/`startMusic` in entry/exit and local scripts (for example MI1 CD room 28 local script 206, room 59 local script 202, room 86 local script 201 use `startMusic`); which sound plays at which moment, and the six variable-operand starts of section 3, need the scripts running. ScummVM's special cases for several sounds at once (`IMuseInternal::startSound_internal`, MI2 sounds 100, 107, 113, 115, 118) and for stopping the old music when a new one starts also belong here.
6. **CD music for MI1**: the loops count and start/end frames of a CD sound (`ScummCdTrackSound`) and `pollCD` (a sound already playing is not restarted) are applied when a script starts it; the standalone list plays the whole track.

## User checks

See the final report of the run (sound numbers and steps).
