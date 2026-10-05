# ScummVM's MAME OPL core as a C# AdLib renderer (archived 5 October 2026)

Tried and shelved: the AdLib music was rendered through a C# port of ScummVM's default OPL2 emulation instead of Nuked,
as a second "AdLib (ScummVM)" device. It worked and was verified, but once the hardware chip (Nuked) received its
register writes with the real chip's latency (`NukedOplChip`, buffered writes) the two sounded almost identical, and the
user kept the hardware rendering only. The story and measurements are in `../validation.md`.

## What is here (`.txt` so they are not compiled)

- `MameOpl2.cs.txt`: the port of ScummVM's `audio/softsynth/opl/mame.cpp` (MAME `fmopl` 0.37a modified), namespace
  `SCUMMexplorer.Core.Formats`. Mono output at the rate given; implements `IOplChip`. Identical to the C++ sample for
  sample on music (sounds 25, 26, 27, 31) and on the 12 AdLib effects.
- `AdlibEmulation.cs.txt`: the `AdlibEmulation` enum (`Hardware`, `ScummVm`), the `IOplChip` interface, `NukedOplChip`
  (as it was then) and `ScummVmVolumeChip` (ScummVM's `Player_AD::writeReg` volume scaling, default 192 of 255).
- `MameOpl2Tests.cs.txt`: unit tests of the port, the volume wrapper and the emulation choice.
- `mame_ref_harness.cpp`: the C++ oracle. ScummVM's own `mame.cpp` code with a small stub, reads a register log
  (`callback register value` lines) and writes raw 16-bit stereo; build with `g++ -std=c++17 -fpermissive -w`.
  `nuked_ref_buffered.c`: the same for the reference Nuked-OPL3 C code with buffered writes (needs `opl3.c`/`opl3.h`
  from nukeykt/Nuked-OPL3). The register logs came from `oracle/oracle_ad.cpp` (ScummVM's `Player_AD`).

## Bringing it back

The code is also in git: `f0bb35c` (the port), `3895995` (the dropdown with two AdLib entries, `SceneSoundPlayer.Emulation`,
the level per emulation), `a791f87` (buffered Nuked writes), and the commit that removed it again.

1. Restore `MameOpl2.cs` and `AdlibEmulation.cs` into `src/SCUMMexplorer.Core/Formats/` (from the `.txt` files or git)
   and `LoomAdlibAudio(AdlibEmulation)` with `Chip = ...Create(emulation, SampleRate)` in `MusicPlayer` and
   `EffectPlayer` (see `3895995`).
2. App: the sound-device dropdown entries "AdLib (hardware)" and "AdLib (ScummVM)", the `adlib=` line in
   `view-options.txt`, and the per-emulation default level (hardware +6 dB, ScummVM +10 dB; see `3895995`).
3. The key scale level in the MAME core is read as 1 = 1.5 dB/oct and 2 = 3 dB/oct, the opposite of the YM3812
   documentation. That is why it is "ScummVM's sound", not the hardware's.
