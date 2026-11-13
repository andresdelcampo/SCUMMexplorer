# The scenes that matter (12 November 2026, roadmap step 4 b)

The user's list (12 November): in every game the intro and the endings; LOOM: painting the sheep green and being captured by the dragon, the dragon killing the boy, Death killing the bishop, the Elders' trial, unwinding the whirlwind, opening the sky on Loom island; Indy 3: pulling with the whip, the Grail trials, boarding the zeppelin, leaving the zeppelin in the plane, going to Berlin, arriving in Venice, meeting Donovan; Maniac Mansion: launching the car into space, the mansion blowing up, the demo tape in the room with the speakers, the microwave, the tentacle scenes.

How they were found: `Measure` with `MEASURE_LIST=1` (every entry with title, plan and first text: `list-*.txt`) and `MEASURE_LINES=room,object,verb` (what a scene printed, in order: `lines-*.txt`); then each played in the real window (`play.sh`, `WindowCheck`, 45 to 120 seconds, pictures) and looked at (`sheet.ps1` contact sheets).

## Indy 3 (EGA English), in the window

| Named | Entry | Seen |
|---|---|---|
| Intro | Play Game Intro | Lucasfilm logo, the circus-train credits ✔ |
| Ending | Room 82 · Give Grail (99 lines, Room 94); Room 94 · Entry (final scores) | the ending's dialogue ("That was a just and noble thing you did, Junior"); the scores ✔ |
| Ending (game over) | Room 36 · Entry | "After recovering consciousness, our hero ... polish Hitler's artifacts", scores ✔ |
| Pulling with the whip | Room 13 · Use brick (whip); Room 61 · Use wooden plug (whip) | Indy whips a ledge and climbs the castle wall; the whip pulls the plug and the water pours ✔ |
| Grail trials | Room 81 · Entry, 82 · Entry, 85 · Entry, 86 · Entry (the knight), 86 · Use Holy Water ("He chose... poorly"), 91 · Look inscription | play ✔. The three trials themselves (the blades, the word, the leap) have no entry: the scan finds no cutscene in Rooms 87 to 90 |
| Boarding the zeppelin | Room 37 · Walk to airfield | "That was close! ... we just made it on board! / Tickets, please." ✔ |
| On the zeppelin | Room 39 · Walk to hatch | the zeppelin's girders, then the fistfight with a guard ✔ |
| Leaving the zeppelin in the plane | — | not found among the entries (Room 45's entry, "Indy!", begins and shows nothing) |
| Going to Berlin | Room 77 · Turn on motorcycle | **was** the Iskenderun branch ("Get in, Dad! We're going to Iskenderun!", 1 line); now the Berlin branch (5 lines, on to Room 72). Henry is not in the room, so his lines ("No, Junior. We're going to Berlin.") are not shown: Room 77's local script 200 places him only when the hero comes in by another way than object 848 |
| Arriving in Venice | Room 15 · Entry | "Ah, Venice! ... Dr. Jones?" ✔ |
| Meeting Donovan | Room 06 · Entry | "Dr. Jones, come with us, please." then Donovan in Room 9 ✔ |

## Maniac Mansion (classic English), in the window (`r:1` first, the assumed party)

| Named | Entry | Seen |
|---|---|---|
| Intro | Play Game Intro | stops at the kid selection: R-8.5c keeps your choice of kids, then the intro goes on ✔ (by rule) |
| Car into space | Room 16 · Use weird edsel | garage, the mansion at night, "Wheeeee!" after about a minute, then Dr. Fred's thanks ✔ (slow, the game's own pace) |
| Endings | Room 51 · Turn off switch (Dr. Fred freed); Room 01 · Give contract (the meteor's publishing contract) | ✔ |
| Mansion blowing up | Room 51 · Entry ("YOWL!"), Room 30 · Entry: "The house and everyone within a 5 mile radius have been destroyed in a massive nuclear meltdown"; Room 29 · Open water valve: "The pool is empty again! We're going to have another meltdown!!" | ✔ |
| Demo tape | Room 20 · Turn on mondo stereo; Room 05 · Give cassette tape | the Green Tentacle: "Hey, this is great stuff! I could use someone like you in my band!" ✔ |
| Microwave | Room 07 · Open microwave oven | "Oh no! Radioactive steam!" ✔ |
| Tentacles | Room 12 · Entry (the Green Tentacle on the stairs: "You can't pass until you feed me!"); the demo tape; Room 29's valve ("TENTACLE!!! FRONT AND CENTER!") | ✔. Room 07 · Give can of Pepsi shows the Green Tentacle in the kitchen door and nothing else: its code only turns him and opens the stairs (Script 54), and the setup had put him away (`var[0x0AE]` = 255) |

Found while doing it: after an ending (the car), a later pick from the whole-game list in the same window did nothing: Maniac Mansion's ending is a cutscene that never ends ("Press f4 to play again"), and Maniac Mansion and Zak McKracken do not start a scene while a cutscene runs (R-8.34), so the room's Play Cutscene button stays disabled during it. Opening another room ends it, and its scenes then play (13 November, `../scenes-that-matter-20261112/maniac-after-ending`: the car, then Room 20, then the demo tape). Each scene here was played in its own window.

## LOOM (EGA English, Roland), in the window (13 November 2026)

LOOM English's Normal lists play the reviewed catalog; each scene was played from its room's Play Cutscene list (`WindowCheck` `RL:room` lists it, `RP:room,index` plays it, `WINDOWCHECK_RP=seconds`). Names matched through the scripts' own texts (`MEASURE_GREP`).

| Named | Entry | Seen |
|---|---|---|
| Intro | Room 01 · The Intro | the title, then Room 53: "Rise, son of Cygna. It is the dawn of your seventeenth year." ✔ |
| Sheep green, captured by the dragon | Room 24 · Dyeing at sheep | Rooms 24, 27, 28: the dragon, "Well, well. Silly me. This is what I get for being in a hurry." ✔ |
| The dragon kills the boy | Room 34 · Walk to Rusty | "Rusty, this is awful! How did it happen?", then Rusty's own story in Room 57 (the strange kid who switched clothes with him, the forty-foot dragon) ✔ |
| Death kills the bishop | Room 44 · Look at Mandible (Script 95) | "I see Cob is being lax in his duties... the dawn of a new era", the Pattern torn open, Chaos ("I am Chaos.", Room 45 local 203) ✔ |
| The Elders' trial | Room 08 · The Elders' trial (named sequence) | Bobbin at the sanctuary, then the trial at the loom ("I have no defense or excuses, Elder Atropos.", "Why do you disobey us, Hetchel?") ✔ |
| Unwinding the whirlwind | Room 13 · Twisting (reverse) at waterspout | the waterspout spins down in a green swirl and is gone. Played straight after its arrival (Room 12 · Reached Room 13) it shows the waterspout first; picked on its own, the swirl is over within six seconds ✔ |
| Opening the sky on Loom island | Room 02 · Opening at sky | Rooms 02, 11, 03, 10: the swans ✔ (the shared engine's picture check names actor 3, the flying swan, moving on its first frame; probably right) |
| Also: Mandible at the cage | Room 43 · Opening at cage (Script 91) | "Give me that!", "Mandible! That distaff will never work for you." ✔ |
| The ending | **new:** Room 66 · The ending | was in no list (the catalog has no entry). Now a named sequence like the Elders' trial, a reviewed start (R-8.42): Room 66 object 834 (Bobbin) verb 0x54, the Transcend, starts Script 106; its two waits (+0x0020, +0x002B) hold until `bit[0xD66]` and `bit[0xD65]` are clear, the draft's playback over, and a session left mid-draft by an earlier scene waited for ever. The start clears both first (a new `bit` step of LOOM's replays, labelled with the code it stands in for). In the window: "Fly away while you can, Weaver boy.", "One day we will meet again!", "Bobbin. It's time!", Room 02; headless (`MEASURE_SEQ`) on to the credits and "The End" at about 2,100 frames ✔ |

## Fixes made for them (shared code, every game)

- **A pick waited silently while its plan was worked out** (a first launch; Give Grail took about half a minute), and looked like nothing happened: the window now shows "Preparing the scene…" until the plan is known (`PreparingScene`).
- **An owner test inside the scene** (Indy 3's motorcycle, +0x0064: Berlin while the diary's owner is 14) is offered as a setup value; the search keeps it only when the scene then shows more.
- **One second object or actor per setup** also when the search tries every value together (the Pepsi was given to actors 19, 8 and 14 at once).

Measured on all eleven copies (`../scene-setup-20261031/*-run77.txt` against `*-run76.txt`): 4 of 893 searched scenes changed their plan (0.4%): Indy 3's motorcycle in both copies (Berlin, 1 → 5 lines) and Monkey Island 2 Room 21's juju bag (2 → 7 lines) show more; Indy 4 Room 93's hole shows less (1 → 0 lines: the added candidate pushed "the room entered as pseudo room 221" past the search's 16 candidates). Normal offers the same as before.

## Not done (for a reviewed start, or a later shared fix)

- Indy 3: Henry absent from the Berlin scene; leaving the zeppelin in the plane (no entry found); the trials of Rooms 87 to 90 (no entries).
- Maniac Mansion: during an ending the room's Play Cutscene button is disabled until another room is opened (by rule R-8.34; no change made).
- LOOM: done (13 November); the ending's start is reviewed data in `MainWindow.LoomCutscenes.cs` (`EndingSteps`) and its room link in `loom-replays.json`.
