# Third-party audio code

## ScummVM

The PC speaker envelope, modulation and synthesis implementation in
`src/SCUMMexplorer.Core/Scumm/Audio/ScummPcSpeakerAudio.cs` and the companion
`LoomPcSpeakerModulationTable.cs` adapt the ScummVM
SCUMM v2 player. Copyright the ScummVM contributors; licensed under
GPL-3.0-or-later. The adaptations implement an offline C# WAV renderer for
LOOM, with cancellation and resource validation.

Source references:

- [player_v2base.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_v2base.cpp)
- [player_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_v2.cpp)
- [ScummVM contributors](https://github.com/scummvm/scummvm/blob/master/COPYRIGHT)

The AdLib event decoder is also based on the behavior documented by ScummVM's
[player_ad.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_ad.cpp).

The GNU GPL version 3 is included in [licenses/GPL-3.0.txt](licenses/GPL-3.0.txt).

The separate VGA/v4 actor walker in `src/SCUMMexplorer.Core/Scumm/V4/ScummActors.V4.cs`
adapts ScummVM 2.2.0's [actor.cpp](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/actor.cpp)
and [boxes.cpp](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/boxes.cpp),
copyright the ScummVM contributors, licensed under GPL-2.0-or-later. The diagnostic
native-walk harness extracts unchanged method bodies from a local copy of those
files; generated source and binaries are not shipped.

## NukedOPL3Sharp 1.3.1

The AdLib synthesizer uses NukedOPL3Sharp, copyright 2025-2026 Stefan Hueg and
nukeykt, licensed under LGPL-2.1-only.

- [Source](https://github.com/codengine/NukedOPL3Sharp/tree/c6171530c68403fa05ec659e155b8f382bb979d0)
- [License](licenses/NukedOPL3Sharp-LGPL-2.1.txt)
- [Upstream notices](licenses/NukedOPL3Sharp-NOTICES.md)

## Munt mt32emu and Mt32emu.net 1.0.0

The Roland MT-32 device renders through Munt's `mt32emu` emulator (copyright the
Munt team, licensed under LGPL-2.1-or-later) using the Mt32emu.net 1.0.0 NuGet
package by Greg Divis (LGPL-2.1-or-later), which bundles the native
`libmt32emu` build. Both are separate libraries that the program loads at run
time; the program contains none of their code. The emulator needs the user's own
MT-32 or CM-32L ROM images, which are not part of this project. The
decoding of the Roland sound streams follows the behavior of ScummVM's
`midiparser_ro.cpp` and `sound.cpp` (see above for ScummVM's license).

- [Munt source](https://github.com/munt/munt)
- [Mt32emu.net source](https://github.com/gregdivis/mt32emu.net)
- The LGPL 2.1 text is in [licenses/NukedOPL3Sharp-LGPL-2.1.txt](licenses/NukedOPL3Sharp-LGPL-2.1.txt).
