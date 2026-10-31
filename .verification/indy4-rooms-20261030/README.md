# Indy 4: Rooms 76, 77, 87, the first-state images and Room 4's magenta line (30 October 2026)

| | |
|---|---|
| Items | [ROADMAP](../../docs/ROADMAP.md) / [tracker](../../docs/indy4/SUPPORT_STATUS.md): "Rooms 76, 77, 87 have an unfollowed camera-after-walk item"; "the play view still draws each object's first-state image"; "a magenta line crosses Room 4's picture after the boot" |
| Fixture | `D:\Juegos\Lucas\indy4 vga en` |
| Tools | `../indy4-objects-20261017/RoomAudit`, `../indy3-castle-20261030/SourceDump` (`visit:`, `box:`, `v:` modes added), `../monkeycd-rooms-20261030/StartFrames` |

## Findings

| Item | Finding | Change |
|---|---|---|
| Rooms 77, 87 | Run with nothing unfollowed today (closed by earlier work). | none |
| Room 76 | The catalog visit's assumed arrival is Room 77's door (`loadRoomWithEgo 0x0419, 76, walkTo=(735, 126)`, Room 77 object 1052 verb 0x0A +0x0023), but the entry reads `var[0x0E0]` (previous room) as the boot's room and puts the ego at object 0x040B (+0x005F..+0x0063), box 13. Boxes 0–15 have no matrix route to box 17 (`box:76:735:126:1049`). The at-once walk then gave up ("where the walk stops is not modelled"), the ego and camera became unknown, and local script 214 (the parrot's "Bwaaaaak!" when `var[0x002]` < 450) could not be followed. ScummVM `walkActor`: `getNextBox` < 0 sets the destination box to the current one and the last leg, so the walk ends where it started with the stand frame (our stepped walk already did this). | `ScummActorTable.WalkAtOnce` ends a walk with no route where it started (stand frame, look note) instead of forgetting the actor. Room 76 now settles with the ego at (50, 114), box 13, camera 160 labelled as the arrival's estimate. |
| First-state images | Stale: `ScummPlayScreen` picks v5 object images by state (`ReadObjectImages(roomId, snapshot.ObjectStates)`). | none |
| Room 4 magenta line | Not seen in the real window after the boot ([window](window/room-04.png)); likely closed by the 19 October credits fix. | none |

Not addressed: the inconsistency between a catalog visit's assumed arrival and the previous-room variable the entry reads; it is the existing labelled estimate of catalog visits.

## Checks

- Room audit 76, 77, 87: all settle or run to their limit with nothing unfollowed.
- `ScummIndy4FirstVisitTests.AnArrivalWalkWithNoRouteEndsWhereItStarts`.
- Full suite: 1831 passed, 0 failed, 5 skipped.

## Addendum, 30–31 October: the first launch froze the window

`LaunchStalls/` (real `MainWindow`, fresh private profile and room cache; a 100 ms UI timer logs gaps, `Dispatcher.Hooks` names every operation over 0.5 s and totals the short ones; `SCUMM_LAUNCH_MODE=reopen` lets the window reopen the game as at startup) and `ComposeTiming/` (headless play-screen compose, alone or beside the derivation and the room cards).

**Finding.** Indy 4's first launch held the UI thread for 85 of its 87 seconds. Not the derivation (24.5 s headless, on workers), not GC (7.8 s paused in all), not `Compose` (0.04 s even under load). The play view's first frame waited in `ShowPlayFrame` → `RefreshPlayOverlays` → `ReadPlayDoorwayTargets`, which, like `ReadGenericRoomActions` and `ScanGenericRoomSounds`, called `GameFolderScanner.Reopen(game)` (a complete rescan of the game, for a reader of its own) on every room change, two of them on the UI thread; under the derivation's load one rescan took up to 56 s.

**Changes.**
1. `MainWindow.Inspection.cs`: one inspection copy per opened game, reopened once in the background when the game opens; the three readers take turns on it (`UseInspectionCopy`).
2. The room actions and the doorway targets are read on a worker and published when done (`LoadGenericRoomActionsAsync`, `LoadPlayDoorwayTargetsAsync`). MI CD's dock: 8 room actions and 3 doorway objects after the load, as before.
3. The last folder reopens on the window's first `ContentRendered` (one-shot) instead of `Loaded`, so the window is on screen before the game's work starts.

**Result (Indy 4, Room 4, fresh profile).** Play view's first frame: 87 s → 18.5 s; UI blocked: 85 s → 15 s. MI CD: first frame 2–3 s, UI never blocked. Full suite 1835 passed.

**Still open.** Two stalls remain for MI2 and Indy 4 only (not MI CD): the synchronous game scan in `OpenGameFolder` (5.5 s) and about 10 s in the first render after the game opens (whatever dispatcher operation is running when it happens; layout itself 0.33 s; not the speech-line naming, ruled out by skipping it; not a single dispatcher operation). Needs a profiler (dotnet-trace or dotnet-stack, not installed). The derivation itself takes about 70 s in the window against 24.5 s headless, because the room cards run beside it; it no longer blocks anything.

## Addendum, 31 October: the profile (dotnet-trace, installed with the user's permission)

`dotnet-trace collect --profile dotnet-sampled-thread-time --format Speedscope` on `LaunchStalls` (reopen mode), then the UI thread's stack replayed from the speedscope events every 50 to 500 ms (a short Python script; the traces stay in the scratchpad).

1. **The game scan of a talkie game.** Nearly every UI sample in the scan was `GameFolderScanner.Scan` → `ArchiveLayout.ListBrowsed` → `ScummSpeechBank.ListSamples` → `FolderFileSource.ReadRange`, which opens `MONSTER.SOU` for each of the walk's small reads (headers and block lengths, thousands of times for MI2's 6808 and Indy 4's 7029 samples). `ListSamples` now reads through a 256 KB read-ahead window. The sample lists are unchanged (`ScummTalkieSpeechTests` pins both counts).
2. **The "second" 10 s stall was a harness artifact.** `App.xaml`'s `StartupUri` opens a second `MainWindow` when `Application.Run` starts; in reopen mode it reopened the last folder too. `LaunchStalls` now pumps the dispatcher itself (`Dispatcher.Run`) and has one window. Earlier window harnesses that call `app.Run()` with an empty private profile (StartFrames, IntroWindowCheck) had an idle second window: their results stand, but their timings include it.
3. The reopen on `ContentRendered` keeps a once-only flag (harmless; the double call came from the second window).

**Final measurements (one window, fresh profile and room cache, Release):**

| Game | Path | Play view first frame | UI blocked in total |
|---|---|---|---|
| Indy 4 | startup reopen | 87 s before 30 Oct → 4.5 s | 85 s → 2.1 s |
| MI2 talkie | startup reopen | 4.2 s | 1.9 s |
| MI CD | startup reopen | 2.1 s | 0 s |
| Indy 4 / MI2 / MI CD | Open Game Folder | 4.9 / 5.1 / 3.3 s | 1.2 / 1.0 / 0 s |

Room actions and doorway objects fill in for all three (Indy 4 38/1, MI2 56/7, MI CD 8/3). The cutscene derivation still runs in the background for about a minute. Full suite 1835 passed.
