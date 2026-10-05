# MT-32 (Roland upgrade) support: record of 5 October 2026

Tools: `.verification/Mt32Probe` (`<game>` lists the Roland streams; `compare` sets Roland lengths against the AdLib twins; `render <game> <roms> <out> <ids>` writes WAVs), `.verification/RolandCheck` (the real window). Data: `Games/loom ega mt32 en`, ROMs in `Games/mt32` (`MT32_CONTROL.ROM` + `MT32_PCM.ROM`). Outputs here: `roland-streams.txt`, `roland-vs-adlib.txt`, `roland-check.txt`. Rendered samples (not committed): `Exe/mt32-samples/sound-{33,41,45,53,56}.wav`.

## What the data is (checked on the files)

- Sounds **33 to 64** of the English and Spanish EGA folders (files `82.LFL`–`85.LFL`, installed by the Roland upgrade disk, `READ.ME`) are the Roland versions. Sounds 1–32 are the PC speaker + AdLib bundles (a 16-bit length, the speaker resource with 22 at byte 6, then the AdLib resource). `LoomRolandSong.IsRoland` tells them apart by that byte.
- A Roland sound is a 16-bit length, two bytes skipped, then a tagless MIDI-like stream, read as ScummVM does (`readSoundResourceSmallHeader` prepends "RO"; `MidiParser_RO`): `F0 n` waits n ticks, `F1 n` waits n + 256, `Ax` is a marker (no time), `8x/9x/Bx` carry two data bytes, `Cx` one (a program number), `00` ends, `FF` ends and loops. Tempo 500000 µs per quarter note, 120 ticks per quarter: 4.1667 ms a tick. No SysEx; instruments are chosen by program number only.
- Channels used: 1–8 and 9 (and 0 once, in the overture). The MT-32's default setup is parts 1–8 on MIDI channels 2–9 and the rhythm part on 10, so the stream is passed to the synth unchanged (as the original driver passes it to the MIDI port; `LoomMt32Audio`). Channel 0 has no part by default.
- The scripts choose Roland sounds themselves when `var[0x030]` is 4: script 001 loads sounds 33–39 instead of 1–7; 039 and 040 add 0x20 to the sound id; 046, 095 and 104 load 0x31/0x30/0x35 where the other cards load 0x11/0x17/0x10/0x15; 044 takes its Roland path (overture, sound 56, which no other edition has: the READ.ME's "two-minute musical overture"). The rule "Roland id = other id + 32" holds in every one of those places. Test `TheRolandSoundCardMakesTheScriptsChooseRolandSounds`: with the card at 4 the first 1500 frames start only sounds 33–64, with 0 only 1–32.
- Timing check against the AdLib twins (`roland-vs-adlib.txt`): the lengths of 45–47, 51–55, 57–59, 61, 62 and 64 are within 1–4 % of their AdLib twins (sound id − 32). 48–50, 60 and 63 differ more (the two arrangements loop or section differently; not examined). All Roland streams end at the end of their data; none uses the loop marker.

## Choice of synthesiser (criteria of the user: free, least restrictive licence, best sound)

**Munt `mt32emu`** (the reference emulator, also used by ScummVM), through **Mt32emu.net 1.0.0** (a NuGet wrapper, LGPL-2.1-or-later, bundling the `libmt32emu.dll` x64 build). The library is loaded dynamically, so the LGPL does not reach the app's own code. It needs the user's own ROM images (the PCM ROM holds the samples), which the app never ships or commits. Analog output mode "Accurate" (the library's best; 48 kHz output). Risk to know: the wrapper and its native DLL come from a single third-party author (not Munt's own build); the plan's alternative is building `mt32emu` from the Munt sources.

## What was built

- `LoomRolandSong` (parser, lifetime for the game's sound clock), `LoomMt32Audio` (offline render, ~9 s for the 156 s overture) and `Mt32RomSet` (MT-32 pair first, then CM-32L) in Core; tests in `LoomRolandAudioTests`.
- `LoomExplorationSession(…, soundCard)` and `SoundCard`: the sound-card variable is set before the boot script runs, and may change later.
- App: a fourth device, "Roland MT-32". Choosing it checks that the folder has the upgrade's sounds and finds the ROMs (environment `SCUMM_MT32_ROM_ROOT`, the saved folder `%LOCALAPPDATA%\SCUMMexplorer\mt32-rom-folder.txt`, a `mt32` folder beside the game folder, else asks). The device sets the session's sound card to 4 at once, so the game's scripts choose the Roland sounds from their next read on; the other devices keep 0 (as before; ScummVM would use 3 for AdLib, which changes script 044's opening path, so it is left alone). The device can change only when no scene runs (existing rule). Opening another folder without the upgrade falls back to AdLib.
- The sound panel and the room sounds menu render a sound 1–32 through its Roland twin on this device, and refuse a Roland-only sound on the other devices.

## Check through the real window (`roland-check.txt`)

Combo lists four devices; Roland sets the card to 4; sound 53 shows 0:11 (twin 21 the same); slot 24 shows 2:36 (the overture, 56); on AdLib sound 53 is refused with the Roland-only text; the opening under card 4 starts exactly sound 56 at frame 3 ("music (Roland MT-32)"); Restart then AdLib returns the card to 0. The refused change while the opening plays is the existing rule.

## Not done / open

- **Listening is the user's check**: an A/B of `Exe/mt32-samples/*.wav` against ScummVM with the same ROMs (the app plays the stream unchanged; ScummVM's IMuse scales channel volume with its own `_vol_eff` and may allocate parts, which could differ slightly).
- Mixed-mode edge: sounds the scripts started on one device keep playing on it when the device changes; a Roland id on another device is silent.
- Rooms' static "possible sounds" menu still lists the sound ids the other cards use (rendered through the twin on Roland).
- ROM versions: `Games/mt32` also has `rev0/` (an older MT-32); only the top-level pair is used. The CM-32L pair is accepted as a fallback.
- No General MIDI fallback (planned as optional, labelled as a different sound).

## Follow-up, 5 October (user)

- **Hold until rendered.** A track the game starts that is not rendered yet (the overture takes about 9 s the first time) now holds the game's frames (`SceneSoundPlayer.IsPreparing`; the playback and idle loops wait, status "Preparing the music…") and the music starts from its beginning with the scene, instead of being cut by the time the rendering took. Later plays use the cache. `RolandCheck` shows `preparing True` for 9 s, then the log `start/opened`.
- **The device choice is remembered** in `%LOCALAPPDATA%\SCUMMexploreriew-options.txt` (`sound=`). A saved Roland choice looks for the ROMs when a game opens; a folder without the upgrade, or without ROMs, falls back to AdLib for that session without overwriting the saved choice. Checked: a file with `sound=Roland` starts the app on Roland with card 4.

## Follow-up 2, 5 October (user): stuck overture, wait feedback

- **The overture got stuck.** Script 044's Roland path times its fades on `VAR_MUSIC_TIMER` (`var[0x00E]`): it waits for the playing song to reach 4, 0xC6 and 0xCC. ScummVM's iMuse reports the markers (`Ax` events) the song has passed (`MidiParser_RO::getTick` counts them; `Sound::updateMusicTimer` writes the variable each frame; the largest over the playing songs, 0 when none plays). The session never wrote the variable, so the opening stayed in Room 69. Now `LoomRolandSong.MarkerTicks` → `LoomSoundClock.MusicTimer` (60 jiffies a second, four ticks a jiffy) → the session writes `var[0x00E]` each frame, only when a Roland song plays or has just ended (other cards unchanged). `Mt32Probe opening 4`: Room 69 → 1 → 2 → 1 → 2 → 53 → 2, boot over, like card 0. Test `TheOpeningUnderTheRolandCardRunsItsCourseAndEndsInRoom02`.
- **Wait feedback.** Opening a scene took seconds with no sign (a new session runs the boot scripts, 1.7 s here, and the first visit of a room 0.5 s, on the UI thread; `Mt32Probe timing`). Choosing a room now shows the wait cursor and "Loading the scene…" over the room at once (a synchronous render pass before the blocking work), until the room is shown. While a track is being rendered the game waits and the same badge reads "Initialising the Roland MT-32 and preparing its music…" (other devices: "Preparing the music…"). `roland-check.txt`: cursor Wait + badge during the preparation and after choosing Room 01, normal/collapsed afterwards.
- A harness note: `RolandCheck` runs depend on the window state (a refused `PlayOpening` leaves the game-open session, whose boot already ran the opening); the runs recorded here start from the saved `sound=Roland` and your usual options.

## The overture overlaps the title: the game does this (user, 5 October)

Heard in the app: the title graphics appear while the overture is still playing, and the intro music (sound 57) starts later. The scripts say so, and the bundled ScummVM (`--music-driver=mt32`, the same ROMs, `--extrapath=Games/mt32`) does the same, as the user saw. `Mt32Probe opening 4`: sound 56 starts at frame 3; at marker 204 (113 s, where the song's last section begins: `Mt32Probe activity 56` shows the note count dipping and new instruments at 110 s) the opening goes to Rooms 2 and 1 (the title); script 039 stops 56 and starts 57 at frame 1495 (about 150 s, 3 s before the overture would end). No change made: the explorer follows the game's timing.

## The title fades out (user, 5 October)

ScummVM shows the overture's title dimming in colour and then going; the explorer made it vanish at once. `roomOps roomPalette a, b` (small-header games) sets `_shadowPalette[b] = a`: colour slot b is shown as colour a. Script 044 steps slot 15 (the title) through 1, 9, 11, 15 on the way in and 11, 9, 1, 0 on the way out, on the music timer. The executor skipped the instruction. Now `LoomScriptRuntimeState.ShadowPalette` (identity at room entry; a speculative route does not touch it), `LoomExplorationSession.ShadowPalette`, and the window draws the room and the actors through it and redraws when it changes (`DisplayPalette`). The test follows slot 15 through the Roland opening. This is general, not Roland-only: every script that fades with `roomPalette` now fades on screen.

## Room loading text (user, 5 October)

The welcome text "Choose a room" stayed on screen while a room loaded, because the "Scene / Reading the room background…" swap came only after the blocking session start. Choosing a room now swaps it at once (title "Scene", text "Loading the scene…", old room hidden, wait cursor) before the blocking work; the badge is kept for music preparation only. `roland-check.txt`: "at start" shows "Choose a room", "just after choosing Room 04" shows the wait cursor and "Scene / Loading the scene…".
