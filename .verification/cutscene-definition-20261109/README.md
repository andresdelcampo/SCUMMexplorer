# What a cutscene is (9 November 2026)

User, 9 November: "I think we should extend the cutscene definition to include: Anything that changes room -even if it comes back to the same room after playing-; Anything that triggers an animation (objects or characters); BUT NOT anything that just shows some lines of text with nothing else changing", and "keep duplicated ... the interaction that triggers a cutscene and the cutscene itself". Then, after the first numbers moved scenes out of the menus: it should bring more scenes, not fewer, without noise ("agreed! please proceed"; after the numbers below: "great! keep going").

REQUIREMENTS: R-8.10 (the rule), R-8.2b (the trigger in Interact, the scene in Play Cutscene), R-8.9 (LOOM's catalog categories give way to the rule once the shared engine has run the replay), R-8.41 (which entries are candidates, the widened ones included).

## How it is measured

From the entry's run (with its setup): `ScummSceneActivity` reads the instructions the scene's own scripts run (those the scan found, the trigger's code, the scripts they start while it plays; the script executor records them only for those scripts). A cutscene shows another room, moves or animates someone other than the hero, moves the hero to somewhere other than what was picked, moves the camera, or plays a sequence on an object (what it shows changes twice or more). Not counted: talking; the hero's own gestures; the hero's walk up to what was picked; one change (a door opening, an item picked up, a lamp switched on); poses (`animateActor` 244 and up, and the costume's basic animations 1 to 5, from version 3; the facing values 248 to 251 in versions 1 and 2); an actor named by a script variable other than the hero's (a helper moving "whoever"); a doorway's door verb (navigation).

**Widened candidates:** an object verb whose code, or a script it starts, can animate or show another room is also an entry, without taking control (`ScummCutsceneIndex.Animating`); its run decides. `SCUMM_WIDE_SCENES=0` leaves them out for a harness.

## Measured (runs 76 to 79, `../scene-setup-20261031/*-run7[6-9].txt`; LOOM: `../loom-answer-key-20261031/compare-run-kinds.txt`, `../loom-editions-20261031/run-kinds-en.txt`)

| Game | Old rule: scenes | Cutscenes now (run 79) | Widened entries that are cutscenes |
|---|---|---|---|
| LOOM English, reference catalog | 79 of 238 | 201 of 238 | n/a (the catalog's list) |
| LOOM English, shared engine scan | 82 of 190 | 169 of 193 | 2 |
| Maniac Mansion classic / enhanced | 32 of 40 | 42 of 49 / 40 of 48 | 5 / 3 |
| Indy 3 EGA / 256 | 75 of 156 | 80 of 160 | 2 |
| Zak McKracken classic / enhanced | 43 of 79 / 43 of 82 | 129 of 175 / 132 of 180 | 66 / 69 |
| Monkey Island 1 EGA / VGA | 64 of 165 / 62 of 160 | 87 of 191 / 86 of 187 | 7 / 8 |
| Monkey Island 2 | 76 of 190 | 176 of 259 | 65 |
| Indy 4 | 182 of 426 | 301 of 520 | 63 |

How the rule got there (noise found in the samples and cut): run 77 counted every Pick up and light switch in Maniac Mansion and Zak McKracken (two state bits written, a lamp's `lights`), and every exit in Indy 4 and Monkey Island 1; run 78 counted Monkey Island 2's conversations by their talk poses and Monkey Island 1's exits by Script 34's `putActor local[0x000]`. LOOM: 123 of the catalog's reactions are cutscenes now (drafts that show their effect: an effect actor animated over the target, LOOM Room 6's wool dyed; Looks where Bobbin gestures or the camera pans); one catalog scene (Room 51, the other draft response at the Dead One) is a reaction.

How they play (run 80, the focus games): Maniac Mansion 36 of 42 cutscenes the game's own way, 6 started at their cutscene (Room 4's keypad door, Room 10's door, Room 12's entry, the wires, the developer tray, Room 30's ending); Indy 3 73 of 80 its own way, 6 at their cutscene (3 of them arrival scenes, started so by design: Rooms 15, 36, 40) and 1 run directly (Room 73's mallet); LOOM's shared engine 216 of 218 (2 at their cutscene). The widened candidates that do not begin are single changes (Maniac Mansion's gargoyle opens the secret panel until the kid moves, the loose panel toggles): reactions.

Checked in the window (Maniac Mansion enhanced): Room 20 lists "Encender equipo de música" (Interact has it too), Room 7 gains "Da bote de Pepsi", Room 1 lists its own scenes and those that pass through it.
