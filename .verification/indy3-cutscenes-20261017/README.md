# Indiana Jones 3 S7: cutscenes from the scripts, playable (EGA and 256 colours)

| | |
|---|---|
| Family / stage | Indy3Ega and Indy3Vga / S7 |
| Fixture and edition | `Games/indy3 ega en` (English EGA, old-bundle `NN.LFL`) and `Games/indy3 vga en` (English 256 colours, one `NN.LFL` per room, v4-style blocks); data in `D:\Juegos\Lucas` (`TestGames.Root`) |
| Date and author | 2026-10-17, S7 agent (Claude) |
| Commits | see the branch `worktree-agent-a911c1b9b4fa76020`: the shared move first, then the list, play and docs |
| Registry change | Cutscenes: Indy3Ega – → ◐, Indy3Vga – → ◐ (list derived from the scripts, playable; ✔ stays reserved for a hand-validated catalog, R-8.18) |

## What was done

1. **The shared scan moved to the engine layer** (K-06, playbook S7 step 1). `LoomCutsceneIndex` → `ScummCutsceneIndex` (`src/SCUMMexplorer.Core/Scumm/Analysis/ScummCutsceneIndex.cs`), `LoomCutscene` → `ScummCutscene`, `LoomCutsceneGroup` → `ScummCutsceneGroup`; the title builder → `ScummCutsceneTitles` (same folder; `LoomCutsceneTitles.Format`/`Title` forward to it). What the scan knew of LOOM by number (0x36 look, 0x37 walk to, 0x38 use, drafts from 0x39 their own entries and titled "… at …") is now data: `ScummCutsceneVerbs`, with LOOM's table in `Loom/Scenes/LoomCutsceneVerbs.cs` (`LoomCutsceneVerbs.Scan` is the LOOM scan). Harnesses and tests follow the rename. **LOOM before/after: identical** (below).
2. **Titles with the game's own verbs.** `ScummCutscenePlanner.VerbsOf` reads the text verbs a booted session holds, by the names the game's scripts gave them (Indy3: Script 96, `verbOps name`): `0x01 Open, 0x02 Close, 0x03 Give, 0x04 Turn on, 0x05 Turn off, 0x06 Push, 0x07 Pull, 0x08 Use, 0x09 Look, 0x0A Walk to, 0x0B Pick up, 0x0C What is, 0x0D Talk, 0x20 Travel, 0x21 To Indy, 0x22 To Henry` (and the prepositions). No verb gets a LOOM-style role. An object's verbs whose table entries point at the same code block are named after the lowest of them, so they are one entry (`NamesSharedCode`, on for the generic planner, off for LOOM).
3. **Generic headless run and planner** (`Scumm/Session/ScummCutsceneRun.cs`, `ScummCutscenePlanner.cs`): a fresh `ScummGameSession` (`BootForPlay`), the room visited as a state preview, then the room's entry played again (`ScummGameSession.EnterRoomAgain`, new: a `loadRoom` of the current room, ScummVM `startScene`) or the object's verb run directly, frames until the cutscene and its lines end (600-frame trial, 6000-frame cap, no Escape), as `LoomCutsceneRun`. The planner caches each run, says in the note whether a fresh start begins the scene, and exports the room lists for the cache.
4. **App** (`MainWindow.PlayCutscenes.cs`, small hooks in `MainWindow.xaml.cs`, `MainWindow.Cutscenes.cs`, `MainWindow.Play.cs`): for a game played through its verbs whose registry Cutscenes cell is above None, the picker (Advanced) and the room's `Play Cutscene` button list the scanned groups; one background run per group (parallel on half the cores for file-per-room games, one at a time for archive/disk games), with the CD's turning arc and gold progress line, and the room lists cached (`CdRoomScenesCache`). Playing keeps the running play session, opens the room when another is shown, then plays the entry again or runs the verb directly, in the play screen (`ScummPlayScreen`) with its sounds. The playback bar now shows during play: Pause (= Space), Skip (= Esc, only from the override point), Skip line (= '.'), Play Cutscene. In Normal mode a scene whose fresh run began nothing is not offered (R-1.13); Advanced lists it with its note.
5. **Requirements:** R-8.32 and R-8.33 Proposed (17 October, Indy3Ega/Indy3Vga); R-8.2 intro, R-8.10, R-8.21, R-8.24 name the new classes. Registry ◐ for both families; matrix regenerated (`SCUMM_UPDATE_SUPPORT_MATRIX=1`).

## Commands run (through RTK)

```
rtk proxy dotnet build SCUMMexplorer.slnx
rtk proxy dotnet build .verification/indy3-cutscenes-20261017/CutsceneList/CutsceneList.csproj
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\loom ega mt32 en" loom   (before and after the move)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\loom vga en" loom        (before and after the move)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\indy3 ega en" scan run  -> indy3-ega-list.txt
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\indy3 vga en" scan run  -> indy3-vga-list.txt
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~ScummIndy3Cutscene|FullyQualifiedName~GameSupport|FullyQualifiedName~SupportConformance|FullyQualifiedName~SupportMatrix"   (SCUMM_UPDATE_SUPPORT_MATRIX=1)
rtk proxy dotnet test ... --filter "FullyQualifiedName~Loom|FullyQualifiedName~Cutscene|FullyQualifiedName~Play|FullyQualifiedName~Support|FullyQualifiedName~ScummGameSession|FullyQualifiedName~Architecture"
CutsceneWindowCheck "D:\Juegos\Lucas\indy3 ega en" <scratch>\window     (private SCUMM_EXPLORER_PROFILE_ROOT and SCUMM_ROOM_CACHE_DIR)
```

## Results (numbers)

**LOOM before/after the move** (`CutsceneList loom`: one line per group with title, category, verb label, scripts, counts, first text, destinations and the other triggers): the outputs are byte-identical (SHA-256 equal) on the reference EGA (`loom ega mt32 en`: 400 triggers, 218 groups, 94 scenes) and on LOOM CD (`loom vga en`: 404 triggers, 223 groups, 98 scenes), checked after the move and again after every later scan change. A first try at merging verbs that share code for every game changed 2 LOOM EGA and 2 CD groups (drafts whose code is the look verb's): that is why `NamesSharedCode` is off for LOOM. The LOOM cutscene tests (`Cutscene`, `SceneSetupPlanner`): 57 passed, 3 skipped (dumps), before and after. Regression at the end (filter `Loom|Cutscene|Play|Support|ScummGameSession|Architecture`, LOOM EGA and CD included): 897 passed, 5 skipped, 0 failed; the support tests with the new Indy3 cells: 23 of 23.

**Indy3, per release** (full listings: `indy3-ega-list.txt`, `indy3-vga-list.txt`):

| | EGA | 256 colours |
|---|---|---|
| Triggers (entries and object verbs whose code reaches a `cutscene` span, depth 3) | 333 | 332 |
| Groups (entries in the list) | 153 | 153 |
| Static category: Scenes / Dialogue and reactions | 71 / 82 | 71 / 82 |
| Fresh-start run began a cutscene | 95 of 153 (34 scenes, 61 dialogue) | 95 of 153 (34 scenes, 61 dialogue) |
| Runs that entered another room | 14 | 14 |
| Planner (one boot and the scan) | 2.4 s | 1.7 s |
| One run | about 0.7 s (1.6 s for the 6000-frame ones) | about 0.45 s |
| Background derivation in the window (8 workers) | 59 s for 153 runs | – |

Indy3 wraps most object actions in a short cutscene (the doors' Open/Close, `global script 26`/`27`), so most entries are "Dialogue and reactions"; 58 groups begin nothing from a fresh start (their code tests state the boot does not reach: the Room 1 office door with the students, `global script 55`; Room 21's door; Room 73's own entry, locals 205/206; several room entries whose locals wait for a story variable). No start setup is derived for those yet (R-8.33).

**Acceptance (three scenes, each begins from a fresh start headless; `ScummIndy3CutsceneTests`, both releases, 4 of 4 passed):**

| Scene | Trigger found by the scan | Code it reaches | Fresh-start run (EGA / 256 colours) |
|---|---|---|---|
| Room 73 · the light switch ("HEY!! WHO TURNED OUT THE LIGHTS!!") | Room 73 object 837, verbs 0x05 Turn off (representative), 0x06 Push, 0x07 Pull, 0x08 Use: one code block | `Room 73 object 837 verb 0x05` (span of 27 instructions, 2 lines; `beginOverride` +0x024B in 256 colours, script map) | began; 66 frames; stays in Room 73 / same |
| Room 22 · Entry ("Meanwhile, elsewhere in the castle…") | Room 22's entry script | `global script 77`, `global script 129` (97 instructions, 5 lines, 2 room loads) | began; 98 frames / same |
| Room 80 · Entry ("Dad! How'd you get here before me?"), late game | Room 80's entry script | `Room 80 local script 201` (31 instructions, 4 lines) | began; 136 frames / same |

The Marcus/Brody conversation is not an object-verb or entry trigger (Talk runs the room's conversation script through `var[0x032]` and Script 2, script map), and the Room 1 office door's students scene (`Room 01 · Walk to door`, `global script 55`) is found but does not begin from a fresh start, so Room 22 and Room 80 were taken instead.

**Window check** (`CutsceneWindowCheck`, real `MainWindow`, EGA, private profile and cache; `window/cutscene-check.txt`): play started in Room 73 with the playback bar and the room's button shown and the progress line at 9%; the 153 background runs finished in 59 s; Room 73's list held 4 entries (Open door, Use locker room, Pick up mallet, Turn off light switch); playing the light switch from the room list began a cutscene (status `Playing Room 73 · Turn off light switch from a stated start …`) and ended in Room 73; the picker's list (Normal mode: 95 entries) played `Room 22 · Entry`: the window opened Room 22, the play session entered it and a cutscene began (`window/cutscene-04-room-22-entry.png`).

## Comparison with ScummVM (source files and date; runtime comparison if any)

Source only (the bundled ScummVM was not run, per the brief): `script_v5.cpp` `o5_cutscene`/`o5_endCutscene` (the span the scan reads; v3 uses the same opcodes), `script.cpp` `beginCutscene`/`endCutscene`/`abortCutscene` (Skip from the override point), `room.cpp` `startScene` (a load of the current room runs the exit and entry code again: `EnterRoomAgain`), `verbs.cpp` `o5_verbOps` (verb names), all as read for S6 (15–16 October 2026). The scan and runs are the explorer's; how a scene looks and sounds against the original is the user's check.

## Differences and uncertainties kept

- A scene run from the list is an **exploration action**: the verb is run directly (the hero does not walk there; the sentence script, Script 2, is not used), and the room is a state preview when the session was elsewhere. The note says so (Advanced).
- 58 groups per release begin nothing from a fresh start; the generic planner does not derive prerequisites yet (LOOM's planner, `LoomCdSceneSetupPlanner`, is LOOM-coupled: guards, story bits, staff). Normal mode hides them once their run is known; Advanced lists them.
- Categories use the static rule (R-8.10) without verb roles, so a silent Use is not a scene in Indy3; not compared with the real game.
- An entry played again from the room the session is in runs the room's exit code first (as `startScene` would); for Room 73 the entry's own cutscene still does not begin (it waits on its locals' state).
- The room lists come from fresh runs: a scene that would visit other rooms only later in the story is listed only in the rooms its fresh run entered.

## User checks (exact steps and where; none if none)

1. Open `D:\Juegos\Lucas\indy3 ega en` in the app, open Room 73: the bar under the game shows Pause, Skip (Esc), Skip line and `Play Cutscene`, and a gold line counts the background scan for about a minute (first launch only).
2. Press `Play Cutscene ▴` in Room 73 and choose `Room 73 · Turn off light switch`: the lights go out, Toby shouts, the lights come back; Esc (or Skip) skips once the scene allows it; Space pauses and resumes sound and picture.
3. Switch Advanced on, press `Play Cutscene` at the top, choose `Room 22 · Entry` (or `Room 80 · Entry`): the room opens and its scene plays with the music. Look and listen: does it start as in the game?
4. The same in `D:\Juegos\Lucas\indy3 vga en`.

## Where the facts went (links to the script map / engine notes / tracker entries)

- Scene rows: [`docs/indy3/INDY3_SCRIPT_MAP.md`](../../docs/indy3/INDY3_SCRIPT_MAP.md) ("Cutscene triggers (S7)").
- Trackers and decision log: [`docs/indy3/SUPPORT_STATUS.md`](../../docs/indy3/SUPPORT_STATUS.md) (both families).
- Requirements: R-8.32, R-8.33 (Proposed), §8.2, R-8.10, R-8.21, R-8.24; K-06 in `docs/archive/REQUIREMENTS_FINDINGS.md`.
- **For Monkey Island floppy (the next S7 agent):** the shared part takes a v4 game as it is: `ScummCutsceneIndex.Scan` reads through `IScummDataSource` (`ScummV4DataSource` has `ReadObjectVerbs`, `RoomEntryScript`, `RoomLocalScript`, `GlobalScript`), `ScummCutscenePlanner` boots with `ScummGameSession.BootForPlay` (v4 index, disks) and reads the verb names from the booted session, and the App runs a game with `V4Disks`/`V4Archive` one run at a time. To light it up: (1) check `PlaysThroughVerbs` and that the boot ends with the verbs created (Monkey's verb script must have run; otherwise `VerbsOf` returns no words and titles read `verb 0xHH`); (2) raise `GameFamily.MonkeyFloppy` Cutscenes to `P` with a test like `ScummIndy3CutsceneTests` (three scenes that begin from a fresh start; the ids, verbs and texts from Monkey's scripts); (3) measure one run (a disk game runs single-threaded in the App: 150 runs at 1 s is 2.5 minutes); (4) if Monkey's verb table points several verbs at one block, `NamesSharedCode` already groups them. Nothing in `Scumm/` names Indy3.
