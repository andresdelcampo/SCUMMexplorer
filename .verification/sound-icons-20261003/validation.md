# Sound device picker and Advanced switch (3-4 October 2026)

First version (3 October): three icon buttons. Replaced on 4 October at the user's request by a dropdown that shows each device's icon and name, in the closed box and in the list rows:

- No Sound: an X. PC Speaker: a speaker. AdLib: the AdLib logo.
- The AdLib mark is a simplified rebuild of `Assets/AdLib_logo.svg` (kept unchanged): the same note silhouette (flag polygon plus head ellipse) with 7 thick slots cut out instead of about 24 thin ones, so it survives at 20 px. It takes the text colour like the other icons (no red).
- `SoundModeCombo` is the visible control again (harnesses keep working); the icon comes from an item template keyed on the item text. While a scene plays the combo is disabled and its tooltip says the device can be changed when the scene is over.
- Advanced is a small switch (track and knob, amber when on) with its label; it is still the `AdvancedModeToggle` CheckBox.

Scratch harness over the real MainWindow: selecting each item sets the player mode (None, PcSpeaker, Adlib); the Advanced switch toggles on and off. Shots: `header-0/1/2.png` (each device in the closed box), `header-adv-on.png` (Advanced on shows the extra buttons), `rows.png` (list rows). No other harness was run.
