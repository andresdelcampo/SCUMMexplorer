# Monkey Island 1 CD S7: cutscenes from the scripts, playable

| | |
|---|---|
| Family / stage | MonkeyCd / S7 |
| Fixture and edition | `Games/monkey1 vga en` (The Secret of Monkey Island CD, English, SCUMM v5: `The_Secret_of_Monkey_Island.cue`/`.bin`, `MONKEY.000`/`MONKEY.001` on the data track, music on the audio tracks); data in `D:\Juegos\Lucas` (`TestGames.Root`) |
| Date and author | 2026-10-17/18, S7 MonkeyCd agent (Claude) |
| Commits | branch `worktree-agent-ad3b02843dde747b3`: registry and test, then the App fix with this record and the docs |
| Registry change | Cutscenes: MonkeyCd – → ◐ (list derived from the scripts, playable; ✔ stays reserved for a hand-validated catalog, R-8.18) |

## What was done

1. **The shared scan reads the CD without change.** `ScummCutsceneIndex.Scan` goes through `ScummDataSources.For` → `ScummV5DataSource` (the v5 resource file decoded from the disc image); `ScummCutscenePlanner` boots with `ScummGameSession.BootForPlay` (the boot ends at the lookout, Room 38). No Core code changed.
2. **Verbs after the boot:** the CD's verb script (Script 22) has run, so titles use the game's words with the CD's numbers: `0x02 Open, 0x03 Close, 0x04 Give, 0x05 Push, 0x06 Pull, 0x07 Use, 0x08 Look at, 0x09 Pick up, 0x0A Talk to, 0x0B Walk to`, the insult verbs `0x12 Tease` … `0x1D Poke`, `0x3E <ADVERTISEMENT>`, the prepositions `0x81 in … 0x84 to`. Raw numbers stay only where the game names no verb: `verb 0x50` (6 entries: friendly natives, important-looking pirates, prisoner, piranha poodles, troll, Sword Master) and `verb 0x3D` (Room 7's chest), as on the floppy.
3. **Background derivation, own copies:** the App's rule from the floppy work already covers the CD (`V4Disks` holds the `ScummV5Archive`): the scan and each of the 8 workers boot from their own copy of the game, read from the disc image (`GameFolderScanner().Scan`, one per worker thread). Kept as it is (decision log: the v5 archive is in memory and could be shared, but own copies cost little and are what the brief asks).
4. **App fix (shared, `MainWindow.PlayCutscenes.cs`):** playing a room's entry from the picker while play is in another room opened the room (the play visit runs its entry) and then entered it again at once. On the CD's ship (Room 19) the second entry, run before any frame of the first, played nothing: no cutscene, no music (first window run, `window/cutscene-check-run1-before-fix.txt`). A room opened for the scene now keeps the visit's entry, which is what the headless fresh-start run does (`EnterRoomAgain` from another room is a `VisitRoom`); a room the player is already in is still entered again. The floppy window check still plays the poster and Room 37's entry (rerun 18 October, 42 s background list, 164 of 164).
5. **Registry and test:** MonkeyCd Cutscenes – → ◐, matrix regenerated (`SCUMM_UPDATE_SUPPORT_MATRIX=1`: README and REQUIREMENTS one cell each); `tests/SCUMMexplorer.Tests/ScummMonkeyCdCutsceneTests.cs` (4 tests).

## Commands run (through RTK)

```
rtk proxy dotnet build .verification/indy3-cutscenes-20261017/CutsceneList/CutsceneList.csproj
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\monkey1 vga en" scan          (1.8 s)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\monkey1 vga en" scan run      -> monkeycd-list.txt (83 s)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\monkey1 vga en" prun 8        (45 s; lines equal to "scan run")
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\monkey1 vga en" race 6        (new mode: 6 planners at once on own copies: 6 x 286/160/62)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\monkey1 vga en" dump 33 429 08  (and probe 33 429 08)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\<loom ega mt32 en | loom vga en>" loom   (before and after)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\<indy3 ega en | indy3 vga en | monkey1 ega en>" scan   (before and after)
rtk proxy dotnet build SCUMMexplorer.slnx
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~ScummMonkeyCdCutscene"
rtk proxy dotnet test ... --filter "FullyQualifiedName~GameSupport|FullyQualifiedName~SupportConformance|FullyQualifiedName~SupportMatrix"   (SCUMM_UPDATE_SUPPORT_MATRIX=1)
rtk proxy dotnet test ... --filter "FullyQualifiedName~Loom|FullyQualifiedName~Cutscene|FullyQualifiedName~Play|FullyQualifiedName~Support|FullyQualifiedName~MonkeyCd"
MonkeyCdCutsceneWindowCheck "D:\Juegos\Lucas\monkey1 vga en" <scratch>\window5     (private SCUMM_EXPLORER_PROFILE_ROOT and SCUMM_ROOM_CACHE_DIR)
MonkeyCutsceneWindowCheck "D:\Juegos\Lucas\monkey1 ega en" <scratch>\floppy        (the floppy's check, after the App fix)
```

## Results (numbers)

| | Monkey Island 1 CD | (floppy, for comparison) |
|---|---|---|
| Triggers (depth 3) | 286 | 294 |
| Groups (entries in the list) | 160 | 164 |
| Static category: Scenes / Dialogue and reactions | 62 / 98 | 63 / 101 |
| Fresh-start run began a cutscene of its own | 101 of 160 | 104 of 164 |
| Planner (one boot and the scan) | 1.5 s | 1.7 s |
| All runs, one thread (harness) | 83 s | 109 s |
| All runs, 8 workers, own copies (harness `prun 8`) | 45 s, results identical to one thread | 36 s |
| Background derivation in the window, while play runs (8 workers, first launch) | **51 s** (58 s in the instrumented run); 241–280 s in three runs while other agents' test runs loaded the machine | 55 s (17 Oct), 42 s (18 Oct) |

Full listing with every run: `monkeycd-list.txt`.

**Acceptance (three scenes, each begins from a fresh start headless; `ScummMonkeyCdCutsceneTests`, 4 of 4 passed):**

| Scene | Trigger found by the scan | Code it reaches | Fresh-start run |
|---|---|---|---|
| Room 33 · Look at poster (the dock: "Re-elect Governor Marley.") | Room 33 object 429 verb 0x08 (floppy: 438, 0x09) | the verb's own code: `cutscene [0x0002]` +0x00CF, `beginOverride` +0x00D4, `printEgo` +0x00D9, `endCutscene` +0x0134 | began; 74 frames; the line is on screen within 120 frames; trace `cutscene at Room 33 Object 429 Verb 0x08 +0x00CF` |
| Room 37 · Entry ("Hey!  I don't like visitors!  Who are you?") | Room 37's entry script | global script 60, global script 25, Room 37 local script 201 (39 lines) | began; 73 frames |
| Room 19 · Entry (the ship, Part II: "First of all^"), later in the game | Room 19's entry script | Room 19 local scripts 200 and 206 (13 lines) | began; 624 frames; went on to Room 7; local script 200 starts sound 102 at +0x0009, a disc track (`ScummCdAudioTracks.Plays(102)`) |

**Disc music in play:** Room 19's scene from the picker, in the window: `start 102 music | Room 19 local script 200 +0x0009 startSound 0x66`, then `opened 102 … audio True, duration 150,4 s` (the CD branch of `RenderPlaySound`; the scene-sound log names the device, AdLib, but the samples are the disc's). Room 37's entry starts sound 98, an iMuse sound of the resource file, not a disc track.

**LOOM, Indy3 and Monkey floppy lists unchanged:** `CutsceneList loom` for `loom ega mt32 en` and `loom vga en` byte-identical before and after (SHA-256 equal); `CutsceneList scan` for `indy3 ega en`, `indy3 vga en` and `monkey1 ega en` identical except the planner's timing line. No Core code changed.

**Regression** (filter `Loom|Cutscene|Play|Support|MonkeyCd`, LOOM EGA and CD included, Indy3 EGA, sound and MT-32 roots set): 946 passed, 5 skipped, 0 failed (before the App fix; the fix touches only the window, checked by the two window runs).

**Window check** (`CutsceneWindowCheck/`, real `MainWindow`, private profile and cache; `window/cutscene-check.txt`, captures `window/cutscene-0N-*.png`): play started (Room 38 selected; the game had moved on to Room 96, then the dock) with the playback bar, the room button and the progress line; the 160 background runs finished in 51 s (planner: 286 triggers, 160 groups, 62 scenes, 101 begun; scan 160 of 160); Room 33's list: `Room 33 · Look at poster`, `Room 35 · Walk to archway (played from Room 35)`; playing the poster began its cutscene with "Re-elect Governor Marley." on screen and Skip enabled, ending in Room 33; the picker (Normal mode, 101 entries) played `Room 19 · Entry`: Room 19 opened, a cutscene began, "First of all^" on screen, sound 102 playing from the disc (`window/cutscene-03-room-19-entry.png`).

## Comparison with ScummVM (source files and date; runtime comparison if any)

Source only (the bundled ScummVM was not run, per the brief): as the floppy and Indy3 S7 records (`script_v5.cpp` `o5_cutscene`/`o5_endCutscene`, `script.cpp` `beginCutscene`/`endCutscene`, `room.cpp` `startScene`, `verbs.cpp` `o5_verbOps`, `sound.cpp`/`soundcd.cpp` `playCDTrackFromSoundID`), read for S6 (15–17 October 2026).

## Differences and uncertainties kept

- A scene run from the list is an **exploration action**: the verb is run directly, and the room is a state preview when the session was elsewhere (Advanced note).
- 59 groups begin nothing from a fresh start (their code tests state the boot does not reach); no start setup is derived for this game (R-8.33).
- The residual attribution of the floppy record (a background begin from a script the trigger also reaches still counts, e.g. Scripts 25/26 behind `Open door` entries) applies here too.
- **Unexplained, three window runs:** in the first three window runs (machine loaded by other agents' tests, 241–280 s) the window's step total read 164 for a list of 160 entries, so the progress note stayed on screen at 97% after the list was done (`window/cutscene-check-run3-loaded.txt`: total 160 at 1.7 s after opening, 164 from the room selection on). The only writers of the total are the planner's task (160) and the LOOM paths (not run for this game); a trace of every write (temporary, reverted) showed only 0 and 160, and the two later runs (58 s, 51 s, lighter load) showed 160 of 160. Not reproduced, cause not found; if the user sees the note stuck below 100% after the list is ready, this is it.
- The first texts count `print` lines only (as decided for the floppy); many Guybrush lines (`printEgo`) give no first text.
- The CD's other language folders (FRENCH, GERMAN, ITALIAN, SPANISH on the disc) were not run (S9).

## User checks (exact steps and where; none if none)

1. Open `D:\Juegos\Lucas\monkey1 vga en` in the app and open Room 38 (the lookout): play starts; a gold line under the game counts the background scan (about a minute, first launch only) and disappears when done (see the uncertainty above if it stays below 100%).
2. Once at the dock (Room 33), press `Play Cutscene ▴` and choose `Room 33 · Look at poster`: Guybrush reads the poster; Esc skips once the scene allows it.
3. Press the picker's `Play Cutscene`, choose `Room 19 · Entry`: the ship's deck opens, "First of all…" is spoken on screen and the disc's music for the scene plays. Then `Room 37 · Entry`: the scene plays with its own sound.
4. Switch Advanced on and open the picker: entries a fresh start does not begin are listed with their note; Normal mode hides them.

## Where the facts went (links to the script map / engine notes / tracker entries)

- Scene rows: [`docs/monkey1/MONKEY1_SCRIPT_MAP.md`](../../docs/monkey1/MONKEY1_SCRIPT_MAP.md), CD section (Global scripts (CD): 60; Rooms (CD): 33, 19, the verb 0x50 entries).
- Tracker and decision log: [`docs/archive/monkey1/SUPPORT_STATUS.md`](../../docs/archive/monkey1/SUPPORT_STATUS.md) (MonkeyCd).
- Requirements: R-8.32 and R-8.33 (Proposed) apply; no new row.
