# Sound device picker as icons (3 October 2026)

The header's sound drop-down is now three small icon buttons: an X (No Sound), a speaker (PC Speaker) and a music note (AdLib). The chosen one gets a quiet lighter fill and border; tooltips and automation names carry the device names. The X and the speaker are plain vector shapes drawn here. The AdLib button uses the user's `Assets/AdLib_logo.svg` (one path, converted to a WPF path with the even-odd rule `F0`), filled with the button's own colour, not the SVG's red. Buttons are 38 x 34 with a 24 px icon area. Close-ups: `z0.png` (X), `z1.png` (speaker), `z2.png` (AdLib selected).

`SoundModeCombo` stays in the window, collapsed, as the single source of the choice (about a dozen harnesses drive it); the buttons set it (`SoundIcon_Click`) and mirror it (`SyncSoundIcons`, also after load). The icon panel's enabled state is bound to the combo's, so it is disabled during a scene exactly as before.

Scratch harness over the real MainWindow: start combo 2 / AdLib lit; click speaker -> combo 1, mode PcSpeaker; click X -> combo 0, mode None; combo set to 2 -> AdLib lit; combo disabled -> panel disabled. Shots: `sound-adlib.png`, `sound-speaker.png`, `sound-none.png`. No other harness was run.
