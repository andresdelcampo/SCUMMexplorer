# Sound device picker as icons (3 October 2026)

The header's sound drop-down is now three small icon buttons: an X (No Sound), a speaker (PC Speaker) and a music note (AdLib). The chosen one gets a quiet lighter fill and border; tooltips and automation names carry the device names. The icons are plain vector shapes drawn here; the note is a generic music note, **not** the AdLib card's own logo.

`SoundModeCombo` stays in the window, collapsed, as the single source of the choice (about a dozen harnesses drive it); the buttons set it (`SoundIcon_Click`) and mirror it (`SyncSoundIcons`, also after load). The icon panel's enabled state is bound to the combo's, so it is disabled during a scene exactly as before.

Scratch harness over the real MainWindow: start combo 2 / AdLib lit; click speaker -> combo 1, mode PcSpeaker; click X -> combo 0, mode None; combo set to 2 -> AdLib lit; combo disabled -> panel disabled. Shots: `sound-adlib.png`, `sound-speaker.png`, `sound-none.png`. No other harness was run.
