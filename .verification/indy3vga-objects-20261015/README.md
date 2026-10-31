# Indiana Jones 256 colours S5: every room shows its first visit from the game's own scripts

| | |
|---|---|
| Family / stage | Indy3Vga / S5 |
| Fixture and edition | `Games/indy3 vga en` (read from `D:\Juegos\Lucas\indy3 vga en`), English 256-colour DOS release, one file per room |
| Date and author | 2026-10-16, S4b/S5 Indy3Vga agent (folder dated with the run's start, 15 October) |
| Commits | the S4b commits (eac366c: the profile offers first visits for the room-file layout, `HasFirstVisitScenes` by format and dialect), then the S5 commit (tests, audit, record, docs) |
| Registry change | unchanged: Objects stays partial until S6 (REQUIREMENTS §3.1: full needs state and click responses) |

## What was done

- No new engine code was needed for S5: the room view and room cards of this release come from `ScummRoomFirstVisit` (boot, a visit that does not play the route, frames until the room settles) as soon as `ScummProfile.HasFirstVisitScenes` is true, which S4b made it for the room-file layout with the Indy3 dialect (R-6.12a, R-5.3a). The App's room view and cards already follow the profile (`MainWindow` room card and room view paths), so no App change.
- Room audit harness `RoomAudit` (beside this file; a copy of the Monkey Island audit, itself a copy of the Indy3 EGA one), run on all 87 rooms. Output: `room-audit.txt`, `room-audit.tsv`, `rooms-sheet0.png` to `rooms-sheet3.png` (each room's first visit with objects and actors, the first 320 pixels of wide rooms).
- Pinned test `ScummIndy3VgaFirstVisitTests` (3): the gym (Room 73, the boot's own room) and Rooms 45 and 90 (entered without their route) give the same active objects as the EGA release's pinned rooms; nine script-heavy rooms open without a gap, and their hotspots are touchable objects of positive size.

## Commands run (through RTK)

```powershell
rtk.exe proxy dotnet build .verification\indy3vga-objects-20261015\RoomAudit\Indy3VgaRoomAudit.csproj
rtk.exe proxy dotnet Exe\Indy3VgaRoomAudit\Debug\net10.0-windows\Indy3VgaRoomAudit.dll "D:\Juegos\Lucas\indy3 vga en" <scratch folder>
rtk.exe proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~ScummIndy3Vga"
# regression, SCUMM_SOUND_TEST_ROOT and SCUMM_INDY3_EGA_TEST_ROOT set: Loom, FirstVisit, GameSession, MonkeyFloppy, Support, Profile
```

## Results (numbers)

- **87 of 87 rooms without a gap**; none failed to run. Before S5 (index states only) also 0 gaps: this release's stored object images leave no colour-0 holes in the backgrounds.
- How the runs ended: 68 settled, 15 reached the 2400-frame limit (ambient scripts), 4 were left by their own script. Objects shown in 39 rooms, actors in 67.
- Rooms entered under a pseudo number (as every load in the game's code does): 17 (168), 18 (181), 21 (129), 23 (141), 24 (146), 33 (166), 72 (192).
- Room 73 (first room): active objects 826, 828, 830, 832; the ego (costume 1) at (119,83) and the boxer (actor 9, costume 38) at (256,86); settled after 587 frames.
- Not followed (written down, R-9.19), in 8 rooms: `actorFollowCamera` of the ego in another room (Script 23 in Rooms 18, 78, 80 and Script 73 in Room 6; ScummVM `setCameraFollows` would load that room; the same open item as Indy3 EGA, version 3 profile keeps it unmodelled), started scripts whose arguments read unknown variables (Room 03 entry +0x006F; Script 131 +0x0038 in Rooms 12, 13, 77).

## Comparison

- **ScummVM runtime pictures: not made** (the brief forbids running the bundled ScummVM this run). This leaves acceptance (b) of S5 open: 0 of 10 rooms compared by picture.
- **Same game, other release:** the active objects of all 87 rooms against the Indy3 EGA audit of 14 October (`.verification/indy3-objects-20261014/room-audit.txt`, paint-order lists): **74 identical**. Different: Rooms 17, 18, 21, 23, 24, 33, 72 (pseudo rooms, modelled after the EGA audit; S5 MonkeyFloppy reported the same change for EGA), Room 1 (never rests: the ambient phase at the frame limit differs), and Rooms 26, 36, 38, 55, 61 (one object each: 382/384, 543/542, 578/579, 705/706, 740 748/739 747). The last five are object ids next to each other, which suggests alternative images or random picks that fall differently; not investigated.

## Differences and uncertainties kept

- The five one-object differences from EGA above.
- Wide rooms are shown on the sheets by their first 320 pixels only; the room view scrolls.
- The session's AdLib music timer is the half-tempo estimate (S4b record); rooms whose scripts wait on music settle on that clock.

## User checks

Optional: in Normal mode open `Games/indy3 vga en` rooms 73, 45, 90, 26, 36, 38, 55, 61, 82, 15 and say which look wrong (compare with the same rooms of `Games/indy3 ega en`).

## Where the facts went

[Indy3 tracker, Indy3Vga block](../../docs/archive/indy3/SUPPORT_STATUS.md), [script map VGA note](../../docs/indy3/INDY3_SCRIPT_MAP.md), CHANGELOG 15 October.
