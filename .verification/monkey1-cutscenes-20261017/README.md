# Monkey Island 1 floppy S7: cutscenes from the scripts, playable

| | |
|---|---|
| Family / stage | MonkeyFloppy / S7 |
| Fixture and edition | `Games/monkey1 ega en` (the English four-disk 256-colour floppy release, SCUMM v4, `DISK01.LEC`..`DISK04.LEC`; the folder name says EGA, the data is VGA); data in `D:\Juegos\Lucas` (`TestGames.Root`) |
| Date and author | 2026-10-17, S7 MonkeyFloppy agent (Claude) |
| Commits | branch `worktree-agent-a8030d659c8693f3b`: `9b625aa` (shared run fix), `22d2835` (App, registry, test), then the docs commit |
| Registry change | Cutscenes: MonkeyFloppy – → ◐ (list derived from the scripts, playable; ✔ stays reserved for a hand-validated catalog, R-8.18) |

## What was done

1. **The shared part as Indy3 left it** (`ScummCutsceneIndex`, `ScummCutscenePlanner`, `ScummCutsceneRun`, `MainWindow.PlayCutscenes.cs`) takes the v4 disk game without change to the scan: `ScummCutsceneIndex.Scan` reads through `IScummDataSource` (`ScummV4DataSource` on the disk set), the planner boots with `ScummGameSession.BootForPlay` (credits, the protection answered as R-15.7, Room 33).
2. **Verbs after the boot** (task 1 of the brief): yes. Script 22 has run when the boot hands over, so the titles use the game's own words: `0x01 Open, 0x02 Close, 0x03 Give, 0x04 Turn on, 0x05 Turn off, 0x06 Push, 0x07 Pull, 0x08 Use, 0x09 Look at, 0x0A Walk to, 0x0B Pick up, 0x0D Talk to`, the hidden insult verbs `0x12 Taunt` … `0x1D Poke`, `0x3E <ADVERTISEMENT>` and the prepositions `0x81 in, 0x82 with, 0x83 on, 0x84 to`. Two verbs the scan meets have no name in the verb table and keep the raw form: `verb 0x50` (6 groups: friendly natives, important-looking pirates, prisoner, poodles, troll, Sword Master: the code an inventory object reaches when given or used on them) and `verb 0x3D` (Room 7's chest).
3. **Fresh-start runs count only the trigger's own cutscenes** (`ScummCutsceneRun`, shared, commit `9b625aa`). The first full run of Monkey's list counted 112 of 164 as begun, but 9 of them were not the trigger's: the SCUMM Bar's background scripts send a pirate through the kitchen door about 480 frames after a visit (global scripts 25/26, each wrapped in a short `cutscene`), so Close door, Use fireplace, Use mug, Walk to door and `verb 0x50 important-looking pirates` all "began" at frame 482; High Street (Room 35) did the same, and a visit whose entry was still inside its own cutscene (Room 19, Room 35) counted as begun at once. The run now reads the session trace's `cutscene at <script>` lines and counts a begin only when it comes from a script the scan says the trigger reaches (global scripts by number, local scripts by room and number, the object's own code, the room's entry), or from a room the trigger led to (Indy3's Room 77 → Room 12); a begin the trace does not show is counted as before. Probes (`CutsceneList … probe`) are quoted below.
4. **Background derivation** (task 3): the App ran a disk or archive game's runs one at a time because the sessions share the game's disk reader. Each worker now boots from its own copy of the game (`GameFolderScanner().Scan(folder)`, one per worker thread) and the scan reads its own copy too, never the play session's reader: in the first window run, the scan read through the shared reader while play ran and lost 11 groups (153 instead of 164). Eight workers at most (sixteen were slower). File-per-room games (Indy3) are unchanged.
5. **Registry and test**: MonkeyFloppy Cutscenes – → ◐, matrix regenerated (`SCUMM_UPDATE_SUPPORT_MATRIX=1`); `tests/SCUMMexplorer.Tests/ScummMonkeyFloppyCutsceneTests.cs` (4 tests).

## Commands run (through RTK)

```
rtk proxy dotnet build .verification/indy3-cutscenes-20261017/CutsceneList/CutsceneList.csproj
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\monkey1 ega en" scan run      -> monkey1-floppy-list.txt
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\monkey1 ega en" prun 8        (and prun 16)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\monkey1 ega en" probe 28 322 02   (and 28 322 0A, 35 457 09, 19 0 00, 37 0 00, 33 438 09)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\monkey1 ega en" dump 33 438 09    (and 28 322 0A)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\loom ega mt32 en" loom           (before and after)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\loom vga en" loom                (before and after)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\indy3 ega en" scan [run]         (and indy3 vga en)
rtk proxy dotnet build SCUMMexplorer.slnx
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~ScummMonkeyFloppyCutscene|FullyQualifiedName~ScummIndy3Cutscene|FullyQualifiedName~GameSupport|FullyQualifiedName~SupportConformance|FullyQualifiedName~SupportMatrix"   (SCUMM_UPDATE_SUPPORT_MATRIX=1)
rtk proxy dotnet test ... --filter "FullyQualifiedName~Loom|FullyQualifiedName~Cutscene|FullyQualifiedName~Play|FullyQualifiedName~Support|FullyQualifiedName~ScummGameSession|FullyQualifiedName~Architecture|FullyQualifiedName~MonkeyFloppy"
MonkeyCutsceneWindowCheck "D:\Juegos\Lucas\monkey1 ega en" <scratch>\window3     (private SCUMM_EXPLORER_PROFILE_ROOT and SCUMM_ROOM_CACHE_DIR)
```

## Results (numbers)

| | Monkey Island 1 floppy |
|---|---|
| Triggers (entries and object verbs whose code reaches a `cutscene` span, depth 3) | 294 |
| Groups (entries in the list) | 164 |
| Static category: Scenes / Dialogue and reactions | 63 / 101 |
| Fresh-start run began a cutscene of its own | 104 of 164 (112 before the attribution fix) |
| Planner (one boot and the scan) | 1.7 s |
| One run | about 0.5 s (0.6–0.8 s when nothing begins within the 600-frame trial; 1–3.5 s for the 6000-frame ones) |
| All runs, one thread (harness) | 109 s |
| All runs, 8 workers, own copies (harness `prun 8`) / 16 workers | 36 s / 45 s; results identical to the one-thread run |
| Background derivation in the window, while play runs (8 workers) | 55 s for 164 runs (first launch only; the room lists are cached) |

Full listing with every run: `monkey1-floppy-list.txt` (title, category, scripts, counts, first `print` text, run result, frames, rooms, time).

**Acceptance (three scenes, each begins from a fresh start headless; `ScummMonkeyFloppyCutsceneTests`, 4 of 4 passed):**

| Scene | Trigger found by the scan | Code it reaches | Fresh-start run |
|---|---|---|---|
| Room 33 · Look at poster (the dock: "Re-elect Governor Marley.") | Room 33 object 438 verb 0x09 | the verb's own code: `cutscene [0x0002]` +0x00CF, `beginOverride` +0x00D4, `printEgo` +0x00D9, `endCutscene` +0x0134 (4 span instructions) | began; 74 frames; the line is on screen within 120 frames and the trace shows `cutscene at Room 33 Object 438 Verb 0x09 +0x00CF` |
| Room 37 · Entry ("Hey!  I don't like visitors!  Who are you?") | Room 37's entry script | `global script 60` (two cutscenes, +0x0010..+0x0031 and +0x003C..+0x0076), `global script 25`, Room 37 local script 201 (39 lines, 2 loads after the span) | began; 73 frames |
| Room 19 · Entry (the ship's deck, Part II: "First of all^") | Room 19's entry script | Room 19 local script 200 (`cutscene` +0x0003 to `endCutscene` +0x039A), local script 206 (13 lines) | began; 624 frames; went on to Room 7 |

The other candidates of the brief:

- **The important-looking pirates** (Room 28 object 332): Look at and Talk to share local script 220 (one entry, `Room 28 · Look at important-looking pirates`, 28 lines, "What be ye wantin', boy?"); its fresh run begins a cutscene (23 frames) that hands over to the conversation's choices (Script 17 case 5, Script 14), so it is the conversation, not a scene that plays to its end; not taken as an acceptance scene.
- **Script 117, the first exit of the SCUMM Bar** ("Meanwhile^", 17 lines, 3 room loads): found (`Room 28 · Walk to door`, object 322 verb 0x0A), but its code first tests the door's state (`ifState 0x0142, 0x01` +0x0000: the door must be open) and `bit[0x1BE]` (+0x0009, first time only), so a fresh start, where the door is closed, begins nothing. No start setup is derived for this game yet (R-8.33); Normal mode does not offer it, Advanced lists it with its note.
- Other later-game room entries that begin from a fresh start: Room 45 (goes on to Room 33), Room 51 (6000 frames), Room 32. Entries that wait on story state and begin nothing: Rooms 1, 11, 20, 25, 38, 39, 40, 41, 61, 65, 70, 72, 74, 77, 78, 80, 83, 85.

**LOOM and Indy3 unchanged where they must be:** the `CutsceneList loom` outputs are byte-identical before and after (SHA-256 equal; `loom ega mt32 en`: 400 triggers, 218 groups, 94 scenes; `loom vga en`: 404, 223, 98); LOOM uses `LoomCutsceneRun`, which this work does not touch. The Indy3 scan output (`scan` without runs) is byte-identical for both releases. The Indy3 **run results** change by the attribution fix: 95 → 90 begun in each release (`indy3-ega-run-correction.txt`): Room 07 Pull table cloth and Open old book (the visit's local script 200 cutscene), Room 18 Open slab-I (the visit's local 205), Room 31 Use grating (`Script 106` from the visit), Room 39 Walk to hatch (`Script 92` from the visit): each a cutscene begun by the visit, not by the trigger (probes). The three Indy3 acceptance scenes still begin (`ScummIndy3CutsceneTests`, 4 of 4).

**Regression** (filter `Loom|Cutscene|Play|Support|ScummGameSession|Architecture|MonkeyFloppy`, LOOM EGA and CD included, Indy3 EGA root and sound roots set): 910 passed, 5 skipped (dumps), 0 failed.

**Window check** (`CutsceneWindowCheck/`, real `MainWindow`, private profile and cache; `window/cutscene-check.txt`): play started in Room 33 with the playback bar, the room's button and the progress line (4%); the 164 background runs finished in 55 s (planner: 294 triggers, 164 groups, 63 scenes, 104 begun); Room 33's list held `Room 33 · Look at poster` and three scenes whose fresh runs passed through Room 33 (marked "played from Room NN"); playing the poster from the room list began its cutscene with "Re-elect Governor Marley." on screen and Skip enabled (`window/cutscene-03-poster.png`), and ended in Room 33; the picker (Normal mode: 104 entries) played `Room 37 · Entry`: the window opened Room 37, the play session entered it, a cutscene began and "Hey!" was on screen (`window/cutscene-04-room-37-entry.png`).

## Comparison with ScummVM (source files and date; runtime comparison if any)

Source only (the bundled ScummVM was not run, per the brief): as the Indy3 S7 record (`script_v5.cpp` `o5_cutscene`/`o5_endCutscene`, `script.cpp` `beginCutscene`/`endCutscene`/`abortCutscene`, `room.cpp` `startScene`, `verbs.cpp` `o5_verbOps`), read for S6 (15–16 October 2026). ScummVM's cutscene stack is per engine, not per script, so a background script's door cutscene is a real cutscene in the game too; whether it belongs to the scene the user picked is the explorer's question, answered from the scan's script list.

## Differences and uncertainties kept

- A scene run from the list is an **exploration action**: the verb is run directly (Guybrush does not walk there; Script 2 is not used), and the room is a state preview when the session was elsewhere (Advanced note).
- 60 groups begin nothing from a fresh start (their code tests state the boot does not reach: an open door, a story bit, an inventory object); the generic planner derives no prerequisites yet (R-8.33).
- **Residual attribution**: a background begin from a script the trigger also reaches still counts. `Room 28 · Use mug` (Script 67, 25, 26) is counted begun at frame 482 by the kitchen door's Script 25, and `Room 35 · Open door` (local 207, Scripts 25, 26) at frame 121 by a townsperson's door: both may not begin on their own from a fresh start. Telling the two apart needs the script that started each script (not tracked by the session).
- **First texts**: the scan counts `print` lines only; Monkey's ego speaks through `printEgo`, so many entries show no first text (the poster's own line is a `printEgo`). Counting `printEgo` too would give them one, but would change the Indy3 lists' text columns (90 of 153 groups per release change their text count and first text; no group, title, category or script list changes), so it was not done; offered in the tracker.
- `verb 0x50` and `verb 0x3D` keep their raw form: no verb of that number is named by the game's verb script.
- Categories use the static rule (R-8.10) without verb roles; not compared with the real game.
- The Spanish eight-disk release (`monkey1 ega es`) belongs to the same family and gets the cell; it was not run (S9).

## User checks (exact steps and where; none if none)

1. Open `D:\Juegos\Lucas\monkey1 ega en` in the app, open Room 33 (the dock): play starts; the bar under the game shows Pause, Skip (Esc), Skip line and `Play Cutscene`, and a gold line counts the background scan for about a minute (first launch only).
2. Press `Play Cutscene ▴` in Room 33 and choose `Room 33 · Look at poster`: Guybrush reads the poster ("Re-elect Governor Marley.", then the slogan); Esc (or Skip) skips once the scene allows it; Space pauses and resumes.
3. Press `Play Cutscene` at the top (the picker), choose `Room 37 · Entry`: the room opens and its scene plays with its sounds. Then `Room 19 · Entry` (the ship, Part II): does it start and go on to the next room as in the game?
4. Switch Advanced on and open the picker: `Room 28 · Walk to door` (Script 117, the first exit) is listed with a note that a fresh start does not begin it; Normal mode hides it.

## Where the facts went (links to the script map / engine notes / tracker entries)

- Scene rows: [`docs/monkey1/MONKEY1_SCRIPT_MAP.md`](../../docs/monkey1/MONKEY1_SCRIPT_MAP.md) (Global scripts 25/26, 60, 117; Rooms 19, 28, 33, 37).
- Tracker and decision log: [`docs/monkey1/SUPPORT_STATUS.md`](../../docs/monkey1/SUPPORT_STATUS.md) (MonkeyFloppy).
- Requirements: R-8.32 and R-8.33 (Proposed) now name MonkeyFloppy; no new row.
