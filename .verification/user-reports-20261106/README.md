# The user's Indy 3 tour of 6 November 2026

The user toured Indy 3 (256 colours, Spanish) and asked for generic fixes ("I expect them to be happening the same to other rooms, characters or games in the same way"). Each report was reproduced in the real window (`../scene-setup-20261031/WindowCheck`) and traced to a shared cause.

| Report | Cause | Fix (shared) |
|---|---|---|
| Full entry cutscenes play on entry | Mine, of 2 November: "a room only one script shows is shown by it" started that script on a pick, and it took scenes too (Script 55 Room 3, Script 83 Room 8, Script 49 Room 70, Script 9 Room 14: save and load, Script 33 Room 42). Also Room 70 rests for a moment, then crashes into Room 71 | `ShownOnlyBy` takes only a page: no time passes, no actor acts, nothing else is started (Script 90 Room 74, Script 126 Room 64). `PrepareManualRoom` looks 300 frames ahead on a copy: a room that leaves or begins a cutscene by itself stays a still picture |
| Room 6: played again does nothing, or jumps elsewhere; a scene runs into another | A picked scene ran in the session as the last scene left it: its flags set (Room 6's entry tests object 124), its leftover scripts still going. The plan was measured on a fresh boot, so the window played something else | A scene picked from a list begins from a copy of the boot with its plan's setup (`StartSceneFromBootAsync`), as the plan's runs do (R-8.5). Interact still acts on the room as it stands. Maniac and Zak keep the running session (their party is the player's) |
| Room 9 (Room 6's scene): one man disappears, one stays | `walkActorToActor` with distance 0xFF (by width, `ScummEngine::walkActorToActor`) was not implemented: it forgot the walker's position. Script 56 walks every follower this way, so Indy dropped out of the room, and the man following him stood still | Ported, in the executor |
| Room 12 cutscene stops midway, and jumps to other scenes | The jumps were the leftover scripts above. The stop is the script's own end: local script 200 +0x0082 walks Indy to (150, 166), ten pixels, then ends its cutscene and the player has control | Nothing more to change; see the note for the user |
| Room 12 Indy big, Rooms 28, 32 not from above; "almost always normal when he should not" | `DressHeroForPreview` (mine, 2 November) dressed the hero **after** the entry, overwriting what the room gave him (Room 12's costume 0x2A, Rooms 41, 47). And Script 25, the castle plans, chooses the top-down costume by what he wears (`var[0x0B4]`), a test a visit cannot answer, so no branch ran. A costume also followed him to the next room | The hero is dressed **before** the entry (`VisitRoomAsPicked`: the usual outfit, or what the boot left in rooms the opening shows), then, when the code the entry starts gives him costumes and he wears none of them, the first of them (`DressHeroFromRoomCode`, labelled). A picked scene is dressed the same way unless its code reads what he wears (`DressHeroForScene`) |
| Room 15: no Indy | Its fountains are ambient, so its still preview ran frames with no actors, although the waiting picture had him | An ambient preview keeps the actors of the room's first-visit picture (`PrepareAmbientRoom(standing:)`). Also: a room no `loadRoomWithEgo` enters but where a script stands the hero by hand (`putActorInRoom` + `putActor`, Room 9) now shows him (`ScummEgoArrivalIndex.PlacedInto`) |
| Room 15: no entry cutscene with Elsa | Script 91 loads the room and starts Script 98, the scene; the room's entry does not begin it, so the scan never listed it | The scan lists a global script's `loadRoom N` followed by a `startScript` of a script holding a cutscene as room N's scene (`FindArrivalScripts`, `ScummCutscene.IsArrivalScript`), started at its cutscene. Not LOOM's scan, not versions 1 and 2 |
| Room 32 map does not keep drawing | Script 25 draws its second section every frame, object 0 when Indy stands in one box. The executor ended a script at a `drawObject` of no object; `o5_drawObject` does nothing (`getObjectIndex` is -1). So the plan stopped drawing one frame in | No-op, in the executor. Walked headless (`Tour`, `TOUR_WALK=32`): the sections follow Indy through boxes 0 to 8 |

## Checked in the window

`final/`, `after1/`, `after2/`, on `indy3 vga es`. Room 6's scene, picked from Room 9: Indy and both men walk to the car (`after1/report.txt`, actor positions every half second), then Room 9. Played, skipped and played again: the same lines from the start, twice. Room 12's scene twice, the same. Room 12 costume 42; Room 28 the small top-down Indy (`after1/room-28.png`); Room 15 with Indy (`after1/room-15.png`) and its Entry playing Elsa's meeting (`after2/`, "Ah, Venecia!" … "Dr. Jones?").

Not checked against the running game, only against the scripts: whether the first costume Script 25's code gives is the one a player would see (he may wear another by then); the user's "three men look equal" (the two men's `actorOps palette` is applied, at this size they read alike; Indy's own costume in Room 6 is the same 43).

## Measured

`Tour` (this folder): every room of a copy picked as the app does, then 900 frames. `tour-before-indy3-vga-es.txt` is before the fixes, `tour-*.txt` after.

| Copy | Rooms | Live | Begin a cutscene after the pick | Leave after the pick | No hero in the picture |
|---|---|---:|---:|---:|---:|
| Indy 3 256 colours Spanish, before | 87 | 64.4% | 0% | 1.8% | 25.3% (7 rooms started a script on the pick) |
| Indy 3 256 colours Spanish, after | 87 | 63.2% | 0% | 0% | 21.8% (2 rooms shown through a script, both pages) |
| Indy 3 256 colours English | 87 | 63.2% | 0% | 0% | 21.8% |
| Indy 3 EGA | 88 | 62.5% | 0% | 0% | 22.7% |
| Monkey Island 1 floppy | 81 | 59.3% | 0% | 0% | 27.2% |
| Monkey Island 1 CD | 83 | 57.8% | 0% | 0% | 27.7% |
| Monkey Island 2 | 110 | 75.5% | 0% | 0% | 24.5% |
| Indy 4 | 96 | 50.0% | 0% | 0% | 20.8% |

Rooms with no hero are mostly pages, maps and close-ups. Lines after a pick in a live room are ambient (Monkey Island 1 Room 30, Indy 4 Room 71), 2.1% of their live rooms.

Scenes, `../scene-setup-20261031/*-run55.txt` against `*-run53.txt` (Maniac and Zak `*-run54.txt` against `*-run31.txt`, LOOM `../loom-editions-20261031/run35-*.txt` against `run34-*.txt`): no scene moved either way. Started directly at their cutscene: Indy 3 5.8% (of 155, two new arrival scenes), Monkey Island 1 8.5% and 8.1%, Monkey Island 2 6.3%, Indy 4 4.0%; 5.9% of the five copies; Maniac 22%, Zak 20%; LOOM unchanged. Scenes showing nothing: 11, as before.

## The castle plans live (the same day; decision 7, the user: yes)

A script that can go round for ever without beginning its scene is a watcher, not initialization (`RemainingSourceWatches`: Script 58, a guard's patrol), and in a pick's preparation an actor that moves all the time does not keep the room from resting (`SettleRoom(restlessActors:)`). When the room's own watcher takes the hero away at once, the room's other decoded arrivals are tried (`OtherArrivalObjects`; Room 32: a guard patrols through the door of the assumed arrival, so he comes in by the stairs at 284, 34). The look ahead is 600 frames.

`tour-*.txt` after this, `tour-before-live-*.txt` before. Live rooms: Indy 3 EGA 69.3%, 256 colours 70.1%, Monkey Island 1 72.8% and 71.1%, Monkey Island 2 81.8%, Indy 4 65.6%. In every copy 0% begin a cutscene or leave in the next 900 frames. Scenes (`*-run56.txt` against `*-run55.txt`): none moved. Checked in the window (`live4/`, `live3/`): Room 28's plan is live with the small Indy, and walking from Room 32 down the stairs into Room 28, where a guard reaching him starts the confrontation ("ALTO!"), as the scripts say: no disguise is assumed (`var[0x0B4]` stays 0), so every guard stops him. The full suite passed apart from two tests that named the old reason for Monkey Island Room 51 (still refused; the wording was updated and those tests and this record's rerun).

## Tests

`UserReports20261106Tests`. Harness additions: `Tour`; WindowCheck `k:x,y` (a click), `e` (Skip), `WINDOWCHECK_SCENE=seconds`, `WINDOWCHECK_EARLY=n`.
