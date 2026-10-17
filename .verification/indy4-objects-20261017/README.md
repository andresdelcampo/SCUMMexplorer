# Indiana Jones and the Fate of Atlantis S5: the rooms show the first visit of the generic session

| | |
|---|---|
| Family / stage | Indy4 / S5 |
| Fixture and edition | `Games/indy4 vga en` (the English talkie CD, SCUMM v5, disc image) |
| Date and author | 2026-10-17, S4b/S5 Indy4 agent |
| Commits | 458d0ac (getActorScale, wait for sentence), 1d612ec (one image per state), 39246b8 (first visit on, tests), then this record's commit |
| Registry change | unchanged, because the Objects cell stays partial until S6 (REQUIREMENTS section 3.1). The tracker records "S5 done". |

## What was done

- `ScummProfile.HasFirstVisitScenes` is true for the v5 format of Indy4 too (after MonkeyCd). `ScummRoomFirstVisit.Run` reads the starting states from `DOBJ`.
- **One image per object state.** A v5 object stores one image per state (`OBIM` > `IM01`, `IM02`, …); ScummVM's `drawObject` draws `getObjectImage(ptr, getState(obj))`, nothing for a state without its own image. The archive reader returned every state image under the same object id, so 43 of 96 rooms threw "An item with the same key has already been added" in `RoomObjectSelection` (Monkey Island CD has no multi-state room that the audit reached). Now `ScummV4ObjectImage.State` carries the image's state, `ScummV5DataSource.ReadObjectImages(room)` gives each object its first stored state, and `ReadObjectImages(room, states)` the image of the given state; the first-visit scene asks for the settled states.
- Executor: `o5_getActorScale` (v5 0x3B) answers the actor's scale (actorOps `scale`; walk-box scaling, `setupActorScale`, is not simulated, so the answer is labelled an estimate), also nested in `expression`; the v5 reader's `waitFor sentence` (`o5_wait` sub-opcode 4) waits as the old `waitForSentence` does.
- Harness `RoomAudit/` (copied from the MonkeyCd record, renamed `Indy4RoomAudit`; `AUDIT_STACK=1` prints the exception's stack): `room-audit.txt`, `room-audit.tsv`, `room-32.png` (the desert dig), `room-46.png`, `room-50.png`.

## Commands run (through RTK)

```powershell
rtk.exe proxy dotnet build .verification\indy4-objects-20261017\RoomAudit\Indy4RoomAudit.csproj
rtk.exe proxy dotnet Exe\Indy4RoomAudit\Debug\net10.0-windows\Indy4RoomAudit.dll "D:\Juegos\Lucas\indy4 vga en" <scratch>   # SCUMM_EXPLORER_PROFILE_ROOT private (scratch)
rtk.exe proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~Indy4"
rtk.exe proxy dotnet test ... --filter "...~Loom|...~Monkey|...~Indy|...~ScummGameSession|...~FirstVisit|...~Play|...~Support|...~Architecture|...~Profile|...~ScriptReader|...~V5|...~Executor|...~Cutscene|...~Costume|...~SoundClock"
```

## Results (numbers)

- First audit run: 96 rooms, 53 ran without a gap, **43 could not run** (duplicate object id, the state images). Then 96 run, 0 gaps, 9 items not followed (`getActorScale` 6, `waitFor sentence` 1, camera after a walk 2 plus one condition on it).
- Final: **96 rooms; 96 run, 0 gaps, 0 could not run**; outcomes Settled 60, FrameLimit 25 (ambient scripts keep changing the room; the state at the limit is shown), RoomLeft 11 (Rooms 1, 8, 9, 11, 15, 25, 30, 41, 47, 62, 82: a script leaves the room; shown as it last was).
- Not followed, written down (R-9.19): Room 77 (Object 1052 verb 0x0A) and Room 87 (Object 1144 verb 0x0A): the camera follows the ego's walk, whose end is unknown; Room 76 local script 214 +0x0001 branches on that camera position (`var[0x002]`).
- The index states alone show no gap in any room (the audit's "before S5" line): the gain of S5 is the script-drawn state and the right state image.
- Pinned (`ScummIndy4FirstVisitTests`): Room 32 active 354, 356 to 360, 363 to 370; Room 46 active 547 to 550, 562 to 564, 566, 568, 575; Room 50 active 598, 599, 610 to 613, 615, 617, 624, 630, 631, 650 (all Settled, no gap; among the most script-heavy rooms: 2222, 3087 and 1612 room-script instructions). Room 5's object 75 gives different pixels for states 1 and 2 and no image for a state without one.
- Tests: `Indy4` filter 9 passed. The final neighbour filter above (LOOM, Monkey, Indy, session, first visit, play, support, architecture, profile, reader, v5, executor, cutscene, costume, sound clock): **985 total, 980 passed, 5 skipped (LOOM dumps), 0 failed**.

## Comparison with ScummVM (source files and date; runtime comparison if any)

`object.cpp` `drawObject`/`getObjectImage` and `script_v5.cpp` `o5_getActorScale`, `o5_wait` from memory of ScummVM master (not re-read this run, 17 October 2026). **No picture comparison with the bundled ScummVM** (not run, per the brief): the S5 acceptance's ten-room comparison is not done.

## Differences and uncertainties kept

- Ten-room picture comparison with ScummVM: not done (user check below).
- Rooms 76, 77, 87 as above.
- `ScummPlayScreen` (the play view, S6) still asks the data source for the first-state image of each object; drawing the current state's image there is S6 work (owned by the play code).
- 25 rooms end at the frame limit; their state is the one at the limit. 11 rooms are left by a script; several of them (8, 9, 30, 47) have no objects and are cutscene rooms.
- Rooms entered without the route (state preview) take a seeded arrival pick, labelled in the audit.

## User checks (exact steps and where; none if none)

- Open the Indy 4 CD in the explorer and compare the room cards of Rooms 4, 32, 46 and 50 with the same rooms at their first visit in ScummVM.

## Where the facts went

[tracker](../../docs/indy4/SUPPORT_STATUS.md), [script map](../../docs/indy4/INDY4_SCRIPT_MAP.md), [boot sequence v5](../../docs/engine/SCUMM_BOOT_SEQUENCE.md), CHANGELOG 17 October.
