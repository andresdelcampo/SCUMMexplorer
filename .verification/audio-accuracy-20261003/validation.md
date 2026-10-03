# PC speaker and AdLib accuracy against ScummVM (3 October 2026)

Reference: ScummVM master `engines/scumm/players/` (`player_v2.cpp`, `player_v2base.cpp`, `player_ad.cpp`), downloaded verbatim.

## Method
- `oracle/oracle.cpp` compiles ScummVM's own `Player_V2`/`Player_V2Base` code (tables, `execute_cmd`, `next_freqs`, `readBuffer`, `squareGenerator`, `lowPassFilter`, `generateSpkSamples`) with a thin stub class (g++ 15, msys2). It renders a sound resource to raw 16-bit samples at 22050 Hz.
- `oracle/oracle_ad.cpp` compiles ScummVM's `Player_AD` (music and SFX) with a fake OPL that logs every register write per 472 Hz callback (volume 255, so no volume scaling).
- `SpeakerCompare.Program.cs` / `AdlibCompare.Program.cs` run both against every LOOM sound in `Games/loom` and compare.

## Results
- **PC speaker** (`LoomPcSpeakerAudio`, rewritten as a direct port): sample-for-sample identical to the C++ for all 27 renderable sounds. The three looping tracks (11, 26, 31) differ only after our 180 s render cap. Modulation table: all 1282 values equal.
- **AdLib** (`LoomAdlibAudio` music, `LoomAdlibEffects`): identical register-write streams for 12 effects and 19 tracks (`adlib-register-compare.txt`).
- Not covered: the OPL chip itself (NukedOPL3Sharp; ScummVM defaults to a different OPL core), ScummVM's music/sfx volume scaling (we keep the original hardware levels), the priority/restart queue of the real game session.

## Deviations found in the old speaker renderer (now fixed)
1. `0xFD` switches the script's current channel in ScummVM (later parameter writes, hull and modulation curves apply to the cleared target). The old code kept writing to the current channel, so chord channels lost their transpose/hull/modulation setup (e.g. sound 1 played an octave or two too high on the second voice).
2. Hull 11's release steps were taken from the following table block (44000/-733) instead of ScummVM's volume 0.
3. Note and pause lengths were forced to at least 1 tick; ScummVM keeps 0 (a zero-length note ends the script / silences the target).
4. The modulation phase was reset on every note; ScummVM keeps it.
5. `0xFA` returned immediately; ScummVM keeps reading, and `0xFF` ends the script only for parameter byte 0.
6. `0xFD`/`0xFA` cleared tempo and time left, which ScummVM keeps; parameters 4, 10-15 were not aliased to the channel fields.
7. Synthesis used floating point; now the integer 16.16 square generator and integer low-pass filter of ScummVM (same 22050 Hz rate, so its `_update_step` truncation makes pitch about 0.1% flat, as in ScummVM at that rate).

## AdLib edge cases made exact
Note frequency wraps as an int8 and the octave is not clamped; meta event 0x58 skips 5 bytes without a length; the track end always writes BD=0.
