# Monkey Island 2 S5: first-visit rooms from the generic session

| | |
|---|---|
| Family / stage | Monkey2 / S5 |
| Fixture and edition | `Games/mi2 talkie en` (`D:\Juegos\Lucas\mi2 talkie en`), as the [S4b record](../monkey2-session-20261017/README.md) |
| Date and author | 2026-10-17, S4b/S5 Monkey2 agent |
| Commits | the S5 commits of 17 October (executor/data source, profile, tests and harness, transparency, audit rerun); record commit after them |
| Registry change | unchanged: Objects stays partial until S6 (REQUIREMENTS §3.1) |

## What was done

- `ScummProfile.HasFirstVisitScenes` for the v5 format of MonkeyCd **and Monkey2** (the boot is followed since S4b): the room view and cards come from `ScummRoomFirstVisit` (boot, a visit that does not play the route, frames until the room settles).
- Engine items the rooms needed (shared code, small): `waitFor sentence` (`o5_wait` SO_WAIT_FOR_SENTENCE: waits while a sentence is queued or `VAR_SENTENCE_SCRIPT` runs; Room 20 local script 208); `getActorScale` (the actor's scale; Room 20 local script 206); expressions with indexed variables (`var[0x11D+local[0x001]]`, Script 49 +0x003E) and, from version 4, a bit destination (`bit[0x02E]`, Room 33 local script 206 +0x0084); an object without a `VERB` block has no verb, so `startObject` does nothing (10 rooms reported "could not be decoded").
- v5 object transparency: a strip coded with a transparent codec (0x22–0x26, 0x2C–0x30, 0x54–0x58, 0x7C–0x80; ScummVM `gdi.cpp`) leaves the room's `TRNS` colour undrawn (`RoomObjectImage.Transparent`, used by `RoomObjectSelection.Paint`). Before it, Room 55's guests stood in magenta boxes (no gap was counted: the audit cannot see a wrong colour).
- State images (S4b): the view shows, for each object, the `IMnn` image of its settled state.
- Harness `RoomAudit` (beside this file, copied from the MonkeyCd record, unchanged otherwise).

## Commands run (through RTK)

```powershell
rtk.exe proxy dotnet build .verification\monkey2-objects-20261017\RoomAudit\Monkey2RoomAudit.csproj
rtk.exe proxy dotnet Exe\Monkey2RoomAudit\...\Monkey2RoomAudit.dll "D:\Juegos\Lucas\mi2 talkie en" <scratch>
rtk.exe proxy dotnet test tests\SCUMMexplorer.Tests\SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~ScummMonkey2"
rtk.exe proxy dotnet test ... --filter "FullyQualifiedName~Monkey|...~FirstVisit|...~RoomObject|...~V5|...~Support"
rtk.exe proxy dotnet test ... --filter "FullyQualifiedName~Loom"
```

## Results (numbers)

- **110 rooms; 107 without a gap; 0 could not run; 0 items not followed** (`room-audit.txt`, `room-audit.tsv`). Before S5 (index states only): 3 rooms with 10 gaps.
- Outcomes: 95 settled, 11 at the frame limit (ambient scripts that never rest: 2, 4, 14, 26, 33, 44, 45, 47, 50, 80, 95), 4 left by their own scripts (20, 63, 66, 77). Objects drawn in 41 rooms, actors in 75.
- Remaining gaps: **Rooms 104, 105, 106** (3, 4 and 3): rooms with no background picture of their own, no entry or local script (one instruction each) and objects at (0,0): image holders that other rooms' scripts draw from. Their colour-0 rectangles are counted as gaps; nothing in their own code draws them. Written down, not filled (R-1.9).
- Before the fixes: 13 rooms with an unfollowed expression, 1 with `waitFor sentence`, 1 with `getActorScale`, 10 with "no VERB block"; Room 103 had one more gap (the transparent colour).
- Pinned rooms (`ScummMonkey2FirstVisitTests`): **Room 39** (objects 391, 409; Guybrush and actor 9), **Room 55** (the party: 25 objects drawn by its scripts, 615–638 and 647, `room-55.png`), **Room 79** (772, 776; three actors). Each settles with no gap and the ego in the room; one image per object; Room 55's guest 615 has transparent pixels the room shows through.
- Tests: `ScummMonkey2FirstVisitTests` (5) and `ScummMonkey2SessionTests` (5) passing; Monkey/FirstVisit/RoomObject/V5/Support filter: 169 passed; LOOM regression 836 passed, 5 skipped, 0 failed.

## Comparison with ScummVM (source files and date; runtime comparison if any)

`script_v5.cpp` (`o5_wait`, `o5_getActorScale`, `o5_expression`), `gdi.cpp` (`decompressBitmap` transparent codecs, `_transparentColor` from `TRNS`), `object.cpp` (`drawObject` finds `IMnn` by state), from memory of GitHub master on 17 October 2026 (not re-read this session). **The ten-room ScummVM picture comparison was not done** (ScummVM is not run, per the brief): a user check.

## Differences and uncertainties kept

- First visits are state previews: an assumed arrival (seeded pick where several), the route not played (labelled in each room's notes).
- Rooms at the frame limit are shown at the frame they stopped; their ambient objects in one phase of their loop.
- The live play screen (`ScummPlayScreen`, S6) still reads one image per object without the state choice: S6 item.
- Hit test class numbers for v5 (the untouchable class 32) are not revisited here (S6 item, as for MonkeyCd).

## User checks (exact steps and where; none if none)

Open Monkey Island 2 (`mi2 talkie en`) in the app, Rooms view: look at Rooms 39, 55 and 79 and at a few frame-limit rooms (4, 33) against the game at their first visit; compare ten rooms with ScummVM if wanted.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Monkey Island 2 script map](../../docs/monkey2/MONKEY2_SCRIPT_MAP.md), [tracker](../../docs/archive/monkey2/SUPPORT_STATUS.md), CHANGELOG 17 October.
