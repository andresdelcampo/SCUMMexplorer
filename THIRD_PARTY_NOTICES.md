# Third-party audio code

## ScummVM

The PC speaker envelope, modulation and synthesis implementation in
`src/SCUMMexplorer.Core/Formats/LoomPcSpeakerAudio.cs` and the companion
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

## NukedOPL3Sharp 1.3.1

The AdLib synthesizer uses NukedOPL3Sharp, copyright 2025-2026 Stefan Hueg and
nukeykt, licensed under LGPL-2.1-only.

- [Source](https://github.com/codengine/NukedOPL3Sharp/tree/c6171530c68403fa05ec659e155b8f382bb979d0)
- [License](licenses/NukedOPL3Sharp-LGPL-2.1.txt)
- [Upstream notices](licenses/NukedOPL3Sharp-NOTICES.md)
