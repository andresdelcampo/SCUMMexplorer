# Sound polish (19 to 20 October 2026)

Families MonkeyFloppy (Monkey Island 1 floppy VGA and EGA), Indy3Ega, Indy3Vga; stage S3 polish. LOOM keeps its constants and its tests. No support cell changes.

## ScummVM sources read (GitHub master, fetched 19 October 2026)

`engines/scumm/scumm.cpp` (`setupMusic`, `setTimerAndShakeFrequency`), `sound.cpp` (`readSoundResourceSmallHeader`, `convertADResource`, `updateMusicTimer`, `isSoundRunning`), `players/player_ad.cpp` (`startMusic`, `updateMusic`), `players/player_v2.cpp` and `player_v2base.cpp` (`startSound`, `stopSound`, `getSoundStatus`, `chainSound`, `nextTick`, `getMusicTimer`, `execute_cmd`), `imuse/imuse_player.cpp` (`getMusicTimer`, `setSpeed`, `loadStartParameters`), `imuse/imuse.cpp` (`getMusicTimer`), `audio/midiparser.cpp` (`setTempo`, `onTimer`), `audio/adlib.cpp` (`getBaseTempo`). No value was set by ear.

## 1. Monkey Island 1's AD music (was: half the LOOM tempo, by ear, 11 October)

Finding: `setupMusic` gives `Player_AD` only to DOS LOOM and Indy3. Every other game with AD blocks (Monkey Island 1 floppy VGA and EGA) gets iMUSE: `readSoundResourceSmallHeader` passes the AD block (v4: after its 6-byte header) to `convertADResource`, which writes one MIDI track at 480 ticks a quarter with:

- a tempo of `500000 * 256 / ticks` µs a quarter (the tempo byte, `data[3]`; LOOM v3 and Indy3 have other formulas there, used only on that path);
- `ppqn / 3` = 160 ticks before the first event (the AD event bytes are copied unchanged: they are already MIDI events with variable-length deltas);
- for a looping song (`data[4]` = 0), a `maybe_jump` sysex in place of the end event, always taken, to beat 1 tick 160: the first event (the AD loop offset is not used).

iMUSE plays it with `MidiParser`: `_psecPerTick = (tempo + ppqn / 4) / ppqn`, an event plays in the timer step whose end time has reached it, the timer step is the AdLib driver's `getBaseTempo()` = 1000000 / 250 = 4000 µs (speed 128 and tempo factor 100: the MI1 `MDhd` is zeros). The music timer is `Player::getMusicTimer` = tick × 2 / 480, times the timer frequency (1193182 / 5041) over 240, and 0 when no song plays.

Result: `ScummAdlibTiming.ImuseConversion` (`ImuseClock`), chosen by `ScummProfile.AdlibTiming` for v4 games that are not LOOM or Indy3. `ScummAdlibAudio`'s music player runs that clock (250 Hz steps against its 472 Hz callbacks, exact integer arithmetic) when the timing says so; the notes still go through the `Player_AD` voice port (iMUSE's AdLib driver has its own instrument and volume handling; not ported: timbre is unchanged, only the timing). The session's lifetimes give the iMUSE music timer as timer values that restart at the loop and are not held. The half-tempo estimate (`halfTempo`) is removed everywhere. Per MIDI tick the old estimate was `0.2505 / tempo` s, iMUSE's is `0.2667 / tempo` s: the music was about 6 % too fast.

Indy3 256 colours (room files, also read through `V4Disks`) used the same half-tempo estimate; ScummVM plays it with `Player_AD` (GID_INDY3), so it now uses Indy3's constants (`Standard`).

## 2. Indy3's AdLib constants in the sound player (R-13.9)

Finding: `Player_AD::startMusic`: `_timerLimit = isLoom ? 473 : 256`, `_musicTicks = data[3] * (isLoom ? 2 : 1)`, delays `>> (isLoom ? 2 : 1)`. Result: `ScummAdlibAudio.RenderWave(resource, timing)`; the browse player, the play renderer and the built-in scene renderer pass `ScummProfile.For(game).AdlibTiming`. Indy3 EGA songs 29, 35 and 39 now render 39.2, 51.6 and 41.8 s (the session's 37.7, 50.2, 41.4 s plus release), where LOOM's constants gave 18.8, 24.5, 19.3 s. R-13.9's note is updated.

## 3. Loops longer than the three-minute rendering

Finding: in play, iMUSE and disc loops already restarted; the v3/v4 AdLib and PC speaker songs did not, and every looping song was cut at 180 s in browse mode. Survey (`LongestLoopsOfTheOldGames`): AdLib loops are all under 90 s (Indy3 longest 88.4 s; MI1 56.3 s), but PC speaker songs loop much later: LOOM sound 11 at 433.9 s, Indy3 sounds 14 at 364.5 s, 16 at 277.7 s, 50 at 433.9 s (MI1 floppy's speaker loops are under 2 minutes).

Result: `ScummAdlibAudio.ReadLoop` (the first jump back and the time the loop target first played) and `ScummPcSpeakerAudio.ReadLoop` (the first repeat of the whole channel control state, now kept as a 128-bit digest so the search can run ten minutes). A looping song renders three minutes, or through its first restart and ten seconds more when that is longer. In play, `MainWindow.PlaySoundLoop` gives v3/v4 songs a loop from those points, so the wave goes on from the loop target as the drivers do.

## 4. PC speaker timing for `isSoundRunning`

Finding: a version 3 or 4 DOS game with the PC speaker gets `Player_V2` (`setupMusic`), whose `getSoundStatus` is `_current_nr == nr || _next_nr == nr`: a sound runs while its channel scripts have time left (236 ticks a second), which is not the AdLib song's length. Its version 3 music timer is a player counter (`_isV3Game`): reset by `chainSound`, increased in `nextTick` every 65536 ticks, or every 126 once any sound set parameter 14 (`_ticks_per_music_timer = 125`), and not cleared by `stopSound`.

Result: `ScummGameSession.PcSpeakerTiming`, set by the app when the device is the PC speaker (`UpdatePlaySoundCard`); the session's sound clock then reads `ScummPcSpeakerAudio.ReadV3Lifetime` (old bundle: 4-byte header; v4 disks: the `WA` block, 6). The music flag stays the resource's. Changing the device forgets the cached lengths; sounds already playing keep theirs. Not for v1/v2 (always `Player_V2`) nor v5 (iMUSE's clock is the same for every device), nor LOOM's own session.

Labelled estimates (in the code and the Indy3 tracker): the timer's divisor is taken per sound (ScummVM keeps 125 for the rest of the run once set); after a sound ends the timer is held where `Player_V2` keeps counting; `Player_V2`'s single current-plus-next queue (a lower-priority sound does not start) is not modelled, as the AdLib clock does not model channel contention either.

## Tests

`ScummSoundPolishTests` (new): the iMUSE clock on a built AD song (end at callback 807, loop restart 203, iMUSE timer not held; Player_AD's constants differ); the profile's choice per game folder (MI1 EGA/VGA floppy iMUSE; Indy3 EGA/VGA and LOOM EGA/CD Player_AD); Indy3 EGA renders match the session's lengths and differ from LOOM's constants; a 184 s loop renders to 194 s; a timer that survives a stop is held, a v2 channel timer is not; Indy3 EGA's speaker lengths match the rendered speaker waves and differ from AdLib's for some sounds; the play session switches to speaker timing, a v5 game does not.

Run with `SCUMM_INDY3_EGA_TEST_ROOT`, `SCUMM_SOUND_TEST_ROOT`, `SCUMM_MT32_ROM_ROOT` set: the sound, iMUSE, LOOM audio, music timer, v2 lifetime, PCjr, speaker, MonkeyFloppy and Indy3 tests, 134 passed and 1 skipped; the boot snapshot, Maniac, Zak, game session and architecture tests, 219 passed. The play routes' sound ids are unchanged (`ScummMonkeyFloppyPlayTests`, `ScummIndy3PlayTests`).

## Listening checks (for the user)

1. Monkey Island 1 floppy (EGA or VGA), AdLib, sound player: sounds 101 (the SCUMM Bar), 103 and 109. The tempo should be a little slower than before (about 6 %), like ScummVM with AdLib.
2. Indy3 EGA, AdLib, sound player: sounds 29, 35, 39. They should play about twice as long as before (the tempo of the game, as in ScummVM), not double speed.
3. Indy3 256 colours, AdLib: sounds 38, 41, 67: a little slower than before (Indy3's constants, not the half-LOOM estimate).
4. PC speaker, sound player: LOOM sound 11 and Indy3 sound 14: the wave should now run past three minutes (to 7 min 24 s and 6 min 15 s), through the point where the song starts again.
5. Play, Monkey Island 1 floppy, AdLib: stay in the SCUMM Bar (music 101) for more than 30 s: the music should go on from its start instead of stopping. With the PC speaker, the same for a speaker song that loops.
6. Play, Indy3 EGA with the PC speaker: the opening and a door or the first conversation should go on as with AdLib (scripts wait on the speaker's lengths now).
