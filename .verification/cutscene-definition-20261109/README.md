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

## Played its own way without taking control (run 84)

A scene whose own way (the game's sentence or its verb run directly) animates or shows another room without ever taking the player's control is that scene, played the game's own way, when it shows at least as much as its cutscene started directly: more lines, or as many and as many rooms (`ScummCutscenePlanner.PlanSetup`). The hero walking out to another room is navigation and does not count (Maniac Mansion Room 4's inner door). Moved from their cutscene, none showing less: Maniac Mansion Room 12's entry, the Green Tentacle on the stairs ("You can't pass until you feed me!", 15 lines; its cutscene was kid 5's branch: 1 line), both copies; Monkey Island 1 Room 25's door (35 lines, was none) and Room 41; Monkey Island 2 Room 79, Wally (28 lines, was 6); Indy 4 Room 57; Zak McKracken enhanced 1. Kept at their cutscene because it shows more: Monkey Island 1's SCUMM Bar (219 lines against 23), Indy 3 Room 33 (3 against none). Started at their cutscene in the five non-LOOM copies: 5.0% → 3.7%. Checked in the window: Maniac Mansion Room 12 (the tentacle's line), Indy 3 Room 33. Full suite 1,925 passed.

Left at their cutscene in the focus games, each its own case: Maniac Mansion 5 (Room 4's keypad door, which waits for the player's code; Room 10's door, a chain through Room 7; the wires; the developer tray; Room 30's ending); Indy 3 4 (Room 13's entry, Room 21's door, Room 33's entry, Room 73's mallet run directly; the arrival scenes of Rooms 15, 36 and 40 start at their cutscene by design); LOOM 2 (Room 34's entry, which shows nothing on LOOM's own path either; Room 43's Use into the Room 46 sequence, 26 lines through Rooms 45 and 46).

## LOOM English's lists by each replay's own run; stock responses (run 85)

The window kept the reference catalog's reviewed category for every replay: the test for "a single replay" (`SequenceId is null`) never held, since each catalog replay carries its id there, so R-8.9's hand-over to the run never happened (Room 06 listed nothing, Room 28 only its reviewed Looks). Now each replay without steps follows the shared engine's run of its own trigger (`ScummCutscenePlanner.IsScene(trigger, fallback)`), not its group's first.

That brought in every Other draft response: 95 of the 328 replays, each the stock "that draft won't work here", animated by Script 41 +0x004C (the draft's sparkle at the object). R-8.10 now leaves out a game's **stock response**: an animation by a global script that the same verb reaches from three rooms or more (`ScummCutscenePlanner.StockRooms`, from the runs known; the run keeps every animating place, `ScummCutsceneRunResult.Animations`). A room's own response to a wrong draft still counts (Room 28's local script 218 moves the dragon's gold).

Window check (`window-loom-en/`, WindowCheck steps `K` and `RL:room`): LOOM English lists 213 of its 332 entries (64%) as cutscenes; Room 06 lists 5 (Bleaching and Dyeing at the wool, Dyeing at the heap, Bleaching at the cloth rack, Look at flask), Room 28 33. The other nine copies measured again (run 85, `../scene-setup-20261031/*-run85.txt`): every count unchanged from run 84 (no stock response decides an entry there).

## Scenes that need several values at once (run 86)

Four generic gaps kept Indy 3 scenes at their cutscene; each now plays the game's own way (`ScummSceneSetupTests.Indy3ScenesThatNeededSeveralValuesPlayTheGamesOwnWay`):

- **A bit numbered by a variable** (`bit[0x59D+var[0x069]]`): a setup candidate, resolved from the variable when applied. Room 21's door (else "It's locked!"): 7 lines through Room 31; Room 33's door begins too.
- **A room only entered under pseudo room numbers** (Room 33, Henry's, as 164 to 166): a value compared with VAR_ROOM is each pseudo number first, only where no arrival loads the room under its own number (Zak McKracken's Room 15 is loaded both ways). An actor put in a pseudo room is stored in its real room, as `putActorInRoom` stores it. Room 33's entry, Henry's first meeting ("JUNIOR!"), 3 lines.
- **Another costume than the one tested**: the costume's siblings, the others the same script dresses the actor in (Script 52's outfits), at most two.
- **Came in by any door** (`notEqualZero VAR_WALKTO_OBJ` in a room entry): each door the arrivals use, else the room's own exit objects, and a visit with no decoded arrival brings the hero in by the wanted object. Room 13's entry: Indy, in his suit, comes in from the ledge and changes into his action clothes, 3 lines.

Started at their cutscene or run directly, run 86: Indy 3 7 → 4 of 81 cutscenes (the arrival scenes of Rooms 15, 36 and 40, by design, and the mallet, Room 73, whose cutscene waits for Indy to carry it near the bell: local script 205's `getDist` loop, a second walk). Maniac Mansion unchanged, 5 of 42, each its own case: the wires (Script 101 waits about 4 minutes, two delays of 10800 and 3600 jiffies, before "the power's out"), Room 4's keypad (waits for the player's code), Room 10's door (a chain through Room 7), the Room 30 ending, Room 01's departure. Zak McKracken 16 → 15 (classic) and 17 → 16 (enhanced). Zak McKracken Room 15's button (widened) is now a reaction: its earlier "cutscene" was a run of 0 frames, 0 lines, with the hero stored in pseudo room 152 rather than Room 15. Cutscenes: Indy 3 81 of 160 (was 80), Zak McKracken 128 of 175 and 132 of 180; the other copies unchanged.
