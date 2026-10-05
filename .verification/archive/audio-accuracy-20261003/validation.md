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

## Addendum, 5 October 2026: instruments that sounded different from ScummVM

Reported: some AdLib instruments, already in the title music (sound 25), sound low or different compared with ScummVM.

- Our driver and chip are exact (above, plus a sample-for-sample match with the reference Nuked-OPL3 C code on sounds 25, 26, 27 and 31).
- ScummVM 2.2.0 (`opl_driver=auto`) plays AdLib through its **MAME OPL2** core. Measured with the same register streams (a harness around ScummVM's `audio/softsynth/opl/mame.cpp`, not committed): MAME is exactly 2.02x louder than Nuked (+6 dB) on everything, and differs in shape only in a few features.
- The main audible difference is the **key scale level** (KSL, bits 7-6 of registers 0x40-0x55). MAME reads 1 as 1.5 dB/oct and 2 as 3 dB/oct; the YM3812 documentation (https://moddingwiki.shikadi.net/wiki/OPL_chip) and Nuked say 1 = 3 and 2 = 1.5. LOOM's instruments use KSL 2 on modulators and carriers (title instruments 9, 7, 5), which changes their level and brightness.
- Remaining differences (not changed): MAME's attack is about 1.5-2x louder in the first 50-150 ms, and vibrato instruments differ in depth.
- Decision (user, 5 October): the music should sound as ScummVM plays it. `LoomAdlibAudio.ChipValue` exchanges KSL values 1 and 2 on the way to the chip (music and effects), always on. The driver's own register state is not touched, so the register streams above still hold. Verified: the shipped renderer equals reference Nuked fed the exchanged log, sample for sample, on sounds 25 and 27. The +6 dB level stays the app's own gain setting.
