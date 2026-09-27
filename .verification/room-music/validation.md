# Validation: room music row (27 September 2026)

Status: **done.** Builds with 0 warnings; **170/170 tests** pass (2 new in `LoomSoundCueTests`). Room audit: 0 gaps in 76 rooms, **0 changed renders** (`compare-png.ps1`). MusicRowCheck: **before 4 FAIL, after 5/5 PASS** (plus one unchecked step).

## Problem

After the boot, the room music row showed "Started: Sound 25 · from this session's route" in every room. Sound 25 is the intro's: script 044 +0x02C3 starts script 039 with argument 0x19 while Room 02 is current, before the intro's last Room 02 entry (+0x0536). Script 039 (+0x0038 `startSound local[0]`) then loops while `isSoundRunning` reads it as playing, which the session estimates because track length is not simulated. So the cue never ended, and every room inherited it. A room's own cue also followed the player: after Room 16 started sound 30, Room 17 showed it too.

## Rule

The row shows a reached cue only when it is:

1. **started since the room was entered** (after the previous room's exit code), from any script; or
2. **still active and started by the room itself**, meaning the chain of scripts that ran `startSound` began in this room's entry, exit, local or verb code, possibly on an earlier visit.

A chain the engine starts (the boot, the room hooks in `var[0x01C..0x01F]`) has no room origin. A started script inherits its starter's origin, and a chained script inherits the chaining script's. Stops still apply across the whole route: a cue stopped anywhere is not shown. An unresolved stop makes the row uncertain only if it came after a cue the row would show. Otherwise the row keeps its labels: "Started: … from this session's route" for a reached cue, and "Possible: … from room script data" for static candidates. The note says which rule applies: "The cue above began after you entered this room." or "This room's code started the cue above on an earlier visit, and nothing reached has stopped it."

## Code

- `LoomExplorationSession`: `ScriptFrame.OriginRoom` (room-owned code sets it; started and chained scripts inherit it). `SoundEvents` lists each sound command with its sequence on the route, the current room and the origin room. `RoomEntrySequence` marks where the current room was entered.
- `LoomSoundCueResolver.ResolveForRoom(events, room, entrySequence, isMusic)` applies the rule. `LoomReachedSoundCue.StartedBeforeEntry` tells the app which note to show. `Resolve` keeps its old whole-route behaviour.
- `MainWindow.UpdateRoomMusicActions` uses `ResolveForRoom` for the session's current room.
- `VisitProbe` has a `sounds` step that lists the reached sound commands.

## MusicRowCheck (real MainWindow, synthetic tree selection and Continue)

[`MusicRowCheck`](MusicRowCheck/Program.cs) opens `Games\loom` and runs through steps: browse to a room, or press Continue once. After each step it records the cue title, evidence, note and "Other possible music" header, and saves a capture. There is no physical input.

```powershell
dotnet build .verification\room-music\MusicRowCheck\MusicRowCheck.csproj
Start-Process -Wait -NoNewWindow -WorkingDirectory $PWD -FilePath Exe\MusicRowCheck\Debug\net10.0-windows\MusicRowCheck.exe -ArgumentList 'Games\loom','.verification\room-music\after','2:none','3:none','16','+:30','17:none','16:30*'
```

| Step | Before ([report](before/music-row-check.txt)) | After ([report](after/music-row-check.txt)) |
| --- | --- | --- |
| Room 02 (boot) | FAIL: Started: Sound 25 | PASS: no cue; 4 possible tracks |
| Room 03 | FAIL: Started: Sound 25 | PASS: no cue; sound 25 is now listed as **possible** (Room 03 entry +0x0124 can start it) |
| Room 16 | no cue (script 039 waits one `breakHere` before `startSound`) | same |
| Continue | PASS: Started: Sound 30 | PASS: Started: Sound 30, "began after you entered this room" |
| Room 17 | FAIL: Started: Sound 30 | PASS: no cue |
| Room 16 again | FAIL (wrong note) | PASS: Sound 30, "started … on an earlier visit" (the entry does not restart it, since `bit[0xDBE]` is set) |

Captures: [Room 02](after/step-01-room-02.png), [Room 03](after/step-02-room-03.png), [Room 16 after Continue](after/step-04-room-16-continue.png), [Room 17](after/step-05-room-17.png), [Room 16 revisited](after/step-06-room-16.png). Before: [Room 02](before/step-01-room-02.png), [Room 17](before/step-05-room-17.png). By eye: the music bar reads "Started: Sound 30 · from this session's route" in Room 16 and "No cue established" in Rooms 02, 03 and 17, and the note appears under "Other possible music".

## Decoded evidence used

- Script 039: +0x0000–0x0015 stops the previous sound (`var[0x0A4]`, `var[0x0A3]`); +0x001A skips the rest unless `var[0x0A2]` is 0; +0x0021 `breakHere`; +0x0038 `startSound local[0]`; +0x003C–0x0042 loops while the sound runs.
- Room 16 entry +0x0090–0x009A: when `bit[0xDBE]` is 0, it sets the bit and starts script 039 with sound 30 (0x1E). So the cue plays on the first visit only, and a later visit inherits it while it runs.

## Tests

`LoomSoundCueTests.RoomCueKeepsOnlyCuesStartedSinceEntryOrByTheRoomItself` (the rule, with synthetic events) and `IntroSoundIsNotRoomMusicButRoomSixteensOwnCueIs` (real data: boot, Room 03, Room 16 plus two frames, Room 17).
