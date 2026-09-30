# Native AdLib effects and unified sound modes

The user now sees an always-visible No Sound / PC Speaker / AdLib selector. It selects music and effects for scene playback, room cues and Advanced sound previews. Advanced no longer has a separate format selector. AdLib uses the resource's native OPL instrument/envelope data, never the speaker decoder. Changing mode closes current voices and cancels pending manual renders; subsequent script commands use the new device. Muted commands are consumed, not queued for replay.

Source: ScummVM Player_AD, https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_ad.cpp. Adapted instrument/note/loop/channel parsing, 472 Hz callbacks with effects every fourth callback, packed envelope stages, timed/repeating notes and deterministic random sustain. Bounds checks reject malformed commands. Synthesis uses existing NukedOPL3Sharp; GPL attribution remains in source and the license is already included.

Release App build: zero warnings/errors. Suite: 249 pass with SCUMM_SOUND_TEST_ROOT=Games/loom; unrelated untracked LoomObjectLookAuditTests excluded. Synthetic fixture checks finite audible deterministic envelope output and malformed instrument/untimed-loop rejection. Supplied tests render effects 1-12 and music 13-23/25-32. Effect PCM is stereo/nonzero and differs from speaker PCM. Sound 24 has no valid DOS bundle at its index; 33 has a different layout. Both reject explicitly, without resource-specific decoder exceptions.

AudioCheck final/audio-check.txt: 10 PASS, zero failures, real MainWindow and WPF MediaPlayers with synthetic commands/selector changes. Native music/effect open and overlap; mute stops all and new commands stay silent; speaker mode applies to both; pending renders cannot resurrect; manual preview follows mode; unsupported bundle shows a message. No physical input, independent listening or ScummVM runtime comparison.

run1 failed in the exploratory resource dump before UI checks. run2's overlap assertion failed because sound 1 ended before sound 25's long cold render opened. Final harness opens music first, then starts the effect and verifies two live voices. Reports retained separately.

Limits: resources render on separate chips and mix through WPF. Shared hardware allocation/priorities, continuous loops beyond three minutes and sound-card script-state changes are not emulated. The selector chooses rendition; it does not rewrite VAR_SOUNDCARD or fabricate different reached events.

Core rendering/session state and click/loading paths unchanged. RoomAudit, Blockers, PNG refresh and doorway checks skipped for this task; score task's audit and mode task's six-room doorway results remain applicable. Build cwd: D:\Documents\Personal\Code Projects\SCUMMexplorer. Command: dotnet build src/SCUMMexplorer.App/SCUMMexplorer.App.csproj -c Release. Artifact: Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe.
