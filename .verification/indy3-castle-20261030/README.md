# Indy 3 castle Rooms 20, 28, 32: mostly black, never settling (30 October 2026)

| | |
|---|---|
| Item | S5 open item ([indy3-objects-20261014](../indy3-objects-20261014/README.md)): "Rooms 20, 28 and 32 show mostly black pictures; their scripts never rest"; possibly the user's "thin black rooms" (28 October) |
| Fixture | `D:\Juegos\Lucas\indy3 ega en` |
| Tools | `SourceDump` (disassembly through `IScummDataSource`, any game; `prep:<room>` runs the catalog's play preparation headless), `../monkeycd-rooms-20261030/StartFrames` (real `MainWindow`, picks a room, saves the window) |

## Finding (decoded; not compared with the running game)

**The black is the game's own design.** The three rooms are Castle Brunwald's cut-away floor plans. Each entry starts global Script 25 with its own two object sets and a count: Room 20 `(280, 288, 7)`, Room 28 `(408, 418, 9)`, Room 32 `(470, 479, 8)` (Room NN entry +0x0000). Script 25 turns the first set off and the second on (+0x005D..+0x0092), then loops for ever (+0x009F..+0x015A): for each walk box it draws the section image (`drawObject`) of the boxes Indy (actor 1) stands in, and a second image for sections whose first-set object has state 1. Everything else stays the background's black. The room picture therefore shows only the section around Indy ([window](window/room-20-still.png)).

**Never settling, and a still picture, is also right.** Local script 200 of Room 20 sets the guards' rooms, costumes and patrol points (`var[0x077]..var[0x093]`, first visit only) and starts Script 57, which starts Script 58 per guard. Script 58 walks its guard between two points for ever and, whenever the guard comes within 10 of Indy (or actor 2 in the same room), `chainScript 0x3C`: Script 60, the confrontation (+0x0056..+0x007A and three more sites). So the room can begin a scene at any moment: `PrepareManualRoom` refuses it ("pending scene initialization", 2400 frames, all three rooms), and by R-8.5b the catalog shows a still picture. Making the guards ambient in the settle test was tried and reverted: the refusal comes from the possible scene, not from the moving actors.

## Not changed

No code change. The Room 20 card thumbnail looks entirely black in the window (the first-visit picture shows the section near Indy's start); not investigated.

## Answered (6 November 2026): live

The user chose live: [record](../user-reports-20261106/README.md). The refusal came from the possible scene *and*, as it turned out, from the moving guards: both are rules now.

## Question for the user (gate G)

Should a room whose guards can start a confrontation at any time (Indy 3 castle) be live from the catalog, with that risk, instead of a still picture? Today R-8.5b keeps it still. Listed in [ROADMAP (Your checks)](../../docs/ROADMAP.md).
