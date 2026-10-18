# Monkey Island 2 S7: cutscenes from the scripts, playable

| | |
|---|---|
| Family / stage | Monkey2 / S7 |
| Fixture and edition | `Games/mi2 talkie en` (Monkey Island 2: LeChuck's Revenge, DOS, with the fan "Talkie CD 0.2 beta" patch; SCUMM v5, `MONKEY2.000`/`MONKEY2.001`), data in `D:\Juegos\Lucas` (`TestGames.Root`) |
| Date and author | 2026-10-18, S7 Monkey2 agent (Claude) |
| Commits | branch `worktree-agent-acef55edf340338f6` (see the run report for the ids) |
| Registry change | Cutscenes: Monkey2 – → ◐ (list derived from the scripts, playable; ✔ stays reserved for a hand-validated catalog, R-8.18) |

## What was done

1. **The shared scan reads Monkey Island 2 without change to the scan.** `ScummCutsceneIndex.Scan` goes through `ScummV5DataSource`; `ScummCutscenePlanner` boots with `ScummGameSession.BootForPlay` (the boot ends at the campfire, Room 4, with a verb bar on, so the S7 Indy4 `UntilAVerbIsOn` step is not needed: `verb source` says the boot).
2. **Verbs after the boot:** Script 22's names `0x02 Open, 0x03 Close, 0x04 Give, 0x05 Push, 0x06 Pull, 0x07 Use, 0x08 Look at, 0x09 Pick up, 0x0A Talk to, 0x0B Walk to`, prepositions `0x81 in … 0x84 to` (the same numbers as Monkey Island CD). Raw numbers stay where the game names no verb: `verb 0x50` (given to someone), `0x14`, `0x16`, `0x17`, `0x1A`, `0x1E`, `0x23` (inventory objects used on things).
3. **First texts (shared, `ScummTextDecoder.DecodeLine`):** the talkie lines start with four `\xFF\x0A` speech controls whose operand bytes are often letters ("R  [  I think I know…"). Code 10's operands now read as spaces. The S7 Indy4 agent made the same change at the same time; the merge keeps master's text (identical code). LOOM, Indy3, Monkey floppy and MonkeyCd lists byte-identical (below).
4. **A song's beat (shared, Core):** many MI2 scripts wait on a playing song's position through `soundKludge [0x0100, sound, 7]` (iMUSE `doCommand` player command 0, `Player::getParam` 7 = `getBeatIndex`, tick / 480 + 1; 8 = the tick in the beat): Script 76 (LeChuck arrives, Part 3), Scripts 52, 120, 165, 199, Room 5's campfire story (local scripts 201/203), Room 60, Room 86 (`CutsceneList find 'soundKludge [0x0100'`, 110 sites). The session answered 0 (S4b estimate), so Script 76 looped at +0x0090..+0x00DF for ever. `ScummScriptRuntimeState.SoundParameter` → `ScummSession.SoundParameter` (virtual, null) → `ScummGameSession` answers params 7 and 8 from the song's AdLib timeline at the seconds it has played on the session's clock (the default path; a looping song wraps at its restart point; the tick of the last event played). Labelled estimate in the limitations; other parameters (3 transpose, 0x12–0x14 parts) stay 0 as before. Side effect: the play boot now reaches the campfire after **2841 frames (3409 before)**, since the campfire story follows sound 108's beat; the Monkey2 play/session tests and the Indy4 tests pass.
5. **Derivation time:** the App already derives a v5 game from own copies on 8 workers (MonkeyCd rule); no App change.
6. **Registry and test:** Monkey2 Cutscenes – → ◐, matrix regenerated (`SCUMM_UPDATE_SUPPORT_MATRIX=1`: README and REQUIREMENTS one cell each); `tests/SCUMMexplorer.Tests/ScummMonkey2CutsceneTests.cs` (3 tests).
7. **Harness:** `CutsceneList` (Indy3 record) gained `script <room|0> <id|entry>`, `find <text>` and `tail <room> <object> <verb> <frames>` (the pending scripts after a run). `CutsceneWindowCheck/` is the real-window check, from MonkeyCd's; it plays the named scenes only when the window would offer the picker (no cutscene playing).

## Commands run (through RTK)

```
rtk proxy dotnet build .verification/indy3-cutscenes-20261017/CutsceneList/CutsceneList.csproj
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\mi2 talkie en" scan          (-> monkey2-scan.txt; 3.2-4.4 s)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\mi2 talkie en" prun 8        (-> monkey2-prun8.txt; 153 s, 179 s before the beat change; same results)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\mi2 talkie en" scan run 7 12 79 86 / 74 13 9
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\mi2 talkie en" script 7 entry / 7 202 / 7 203 / 80 entry / 0 76 / 0 101
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\mi2 talkie en" tail 50 0 00 3000 / tail 74 755 09 3000 / tail 95 0 00 3000
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\mi2 talkie en" find "soundKludge [0x0100"
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\<loom ega mt32 en | loom vga en>" loom   (before, after, after the merge)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\<indy3 ega en | indy3 vga en | monkey1 ega en | monkey1 vga en>" scan   (idem, timing line dropped)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\indy4 vga en" scan run   (after the merge, against indy4-list.txt, timings dropped)
rtk proxy dotnet build SCUMMexplorer.slnx
rtk proxy dotnet test ... --filter "FullyQualifiedName~ScummMonkey2Cutscene"
rtk proxy dotnet test ... --filter "FullyQualifiedName~Monkey2|FullyQualifiedName~Indy4|FullyQualifiedName~SoundKludge|FullyQualifiedName~Imuse"
rtk proxy dotnet test ... --filter "FullyQualifiedName~GameSupport|FullyQualifiedName~SupportConformance|FullyQualifiedName~SupportMatrix"   (SCUMM_UPDATE_SUPPORT_MATRIX=1, before and after the merge)
rtk proxy dotnet test ... --filter "FullyQualifiedName~Loom|FullyQualifiedName~Cutscene|FullyQualifiedName~Play|FullyQualifiedName~Support|FullyQualifiedName~Monkey2"
Monkey2CutsceneWindowCheck "D:\Juegos\Lucas\mi2 talkie en" <scratch>\window2 "<title>"...   (private SCUMM_EXPLORER_PROFILE_ROOT and SCUMM_ROOM_CACHE_DIR)
```

## Results (numbers)

| | Monkey Island 2 (talkie en) |
|---|---|
| Triggers (depth 3) | 536 |
| Groups (entries in the list) | 260 |
| Static category: Scenes / Dialogue and reactions | 76 / 184 |
| Fresh-start run began a cutscene of its own | 165 of 260 (Normal mode lists these 165) |
| Planner (one boot and the scan) | 3.2–4.4 s |
| All runs, 8 workers, own copies (harness `prun 8`) | 153 s (179 s before the beat change, machine shared with the Indy4 agent) |
| Background derivation in the window, while play runs (8 workers, first launch) | **140 s** and **174 s** (two runs, machine shared with another agent's runs); 384 s in a third run under heavier load |

Each run boots its own session (about 0.9 s for MI2, against about 0.3 s for MonkeyCd), so 260 runs cost about twice MonkeyCd's 160. Already parallelised as MonkeyCd (own copies, 8 workers); a snapshot of the booted session shared by the runs would cut it, not done (decision log). Eight scenes wait for the player (a dialogue choice, the bone-song sheet's click) and run to the 6000-frame limit.

**Acceptance (three scenes, each begins from a fresh start headless; `ScummMonkey2CutsceneTests`, 3 of 3 passed):**

| Scene | Trigger found by the scan | Code it reaches | Fresh-start run |
|---|---|---|---|
| Room 09 · Talk to barkeeper (Woodtick, the Bloody Lip: "Yeah, boy?") | Room 9 object 73 verb 0x0A | Room 9 local scripts 213, 212, 203, global script 70 (20 lines) | began; 41 frames (the dialogue's choices follow) |
| Room 13 · Entry (Woodtick, Largo's room at the inn: "Who let you into my room?!!") | Room 13's entry script | Room 13 local scripts 205, 206, 213, 211, 212 (13 lines); local script 211 waits 1800 jiffies, then begins the cutscene at +0x0010 | began; 406 frames; goes on to Room 12 |
| Room 74 · Pick up voodoo key to jail (Part 3: LeChuck comes in, "Guybrush Threepwood^") | Room 74 object 755 verb 0x09 | global script 76: `cutscene` +0x0000, `startSound 0xB0` +0x0019, the beat wait +0x0090..+0x00DF | began; past the beat after 34 frames; runs to the dialogue (6000 frames) |

**iMUSE music in play:** Room 74's scene from the picker, in the window: `start 176 | Script 76 +0x0019 startSound 0xB0`, `opened 176 … audio True, duration 184,952 s` (AdLib; the log's kind says "effect"); then "I have searched every island^", "Sailed every sea^", "And now you are mine." on screen. Room 13's entry starts sound 182 (`Room 13 local script 211 +0x0072`, 200.7 s) and the barkeeper's dialogue sound 152 (`Script 151 +0x0028`).

**Lists of the other families unchanged:** `CutsceneList loom` for `loom ega mt32 en` and `loom vga en` and `CutsceneList scan` for `indy3 ega en`, `indy3 vga en`, `monkey1 ega en`, `monkey1 vga en` byte-identical (SHA-256) before and after this branch's commits, and again after merging master (S7 Indy4). A first reading of the text fix (blank every operand byte) changed LOOM's text counts and Monkey Island's "trial @ twice", so it was narrowed to code 10. Indy4: see the run report (`scan run` after the merge against `indy4-list.txt`).

**Window check** (`CutsceneWindowCheck/`, real `MainWindow`, private profile and cache; `window/cutscene-check-run2.txt`, `-run3.txt`, captures): play started in Room 4 with the playback bar, the room button and the progress note; the game went on to Woodtick (Room 7) by itself; the 260 runs finished (140 s / 174 s), planner 536 / 260 / 76 / 165 begun; Room 9's list shows 12 entries starting "Room 09 · Talk to barkeeper"; the picker offers 165 entries in Normal mode. Run 2: `Room 13 · Entry` began nothing in the 15 s the harness waited (local script 211's 30-second wait, see below), `Room 74 · Pick up voodoo key to jail` began with sound 176. Run 3 (waits 60 s): `Room 13 · Entry` began, "Who let you into my room?!!", "I … uh … thought it was my room?", "Well, it ain't.", ending in Room 12; then `Room 09 · Talk to barkeeper` began ("Excuse me^"). The first run (`cutscene-check-run1-harness-order.txt`) started scenes while a dialogue cutscene was still on, which the window's picker does not allow; the harness now waits for the picker.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Source only (the bundled ScummVM was not run, per the brief): `imuse/imuse.cpp` `doCommand_internal` (scope 1, command 0 → `Player::getParam`), `imuse/imuse_player.cpp` `getParam` (7 `getBeatIndex`, 8 the tick in the beat) and `getBeatIndex` (`_parser->getTick() / TICKS_PER_BEAT + 1`), `script_v5.cpp` `o5_isGreater`/`o5_isLessEqual` (the loop's exit at beat 9), `charset.cpp` `handleNextCharsetCode` case 10; read 18 October 2026.

## Differences and uncertainties kept

- The beat is an **estimate**: the AdLib part's default path (hooks a script arms are not counted for the beat), the tick of the last event played, the session's clock. Script 76 got past its wait 34 frames after the song started, which means sound 176's played events reach beat 9 at once (an early jump in the song is the likely cause; not checked against ScummVM at runtime).
- The play boot's length changed (3409 → 2841 frames) because the campfire story now follows sound 108's beat; not compared with the original.
- **Largo at the bridge (Room 07 · Entry) does not begin from a fresh start**: the entry starts local script 203 only when `bit[0x168]` is set, and 203 waits for Guybrush to walk west of x 670 (+0x0043..+0x0072) before its cutscene, a player walk. Normal mode hides it (R-1.13); Advanced lists it with its note. The same holds for 95 entries in all (their code tests state or input the fresh start does not give).
- Room 13's scene begins 1800 jiffies (30 s at the game's pace) after the room is entered (local script 211 +0x0004): in the window the player waits about half a minute, longer while the background runs slow play down.
- Two first texts read `\` (Room 21 · Open matches, Room 22 · Entry): their first print is a string control `\xFF\x07\"\x00` whose escaped quote ends the scan's text match early; fixing the match would change other lists' text counts (a Room 33 count moved in a trial), so it is left.
- A scene is an **exploration action** from a state preview, as for the other families (Advanced note); no start setup is derived (R-8.33).
- The speech of MI2's talkie patch (`monster.sou`) is not played in scenes: the S7 Indy4 `TalkieSpeechPlayer` is wired for Indy4's edition; whether it reads this fan patch's file was not checked (S9 or a later speech item).

## User checks (exact steps and where; none if none)

1. Open `D:\Juegos\Lucas\mi2 talkie en` and select Room 4 (the campfire): play starts; the gold progress line under the game counts the background scan (about two to three minutes on first launch) and disappears when done.
2. Once Guybrush is in Woodtick (Room 7), press the picker's `Play Cutscene` and choose `Room 74 · Pick up voodoo key to jail`: LeChuck's music starts, and after a few seconds LeChuck walks in ("I have searched every island…"). The scene ends in a dialogue: pick a line.
3. Choose `Room 13 · Entry`: Largo's room opens; after about half a minute Largo comes in ("Who let you into my room?!!") and throws Guybrush out to Room 12.
4. Open Room 9's `Play Cutscene ▴` and choose `Room 09 · Talk to barkeeper`: the dialogue starts.
5. Listen to whether LeChuck's entrance in step 2 comes at the moment the music builds up (the beat estimate); compare with ScummVM if possible.

## Where the facts went (links to the script map / engine notes / tracker entries)

- Script map rows: [`docs/monkey2/MONKEY2_SCRIPT_MAP.md`](../../docs/monkey2/MONKEY2_SCRIPT_MAP.md) (Global scripts 76, 101; Rooms 7, 9, 13, 74, 80; Sounds 176, 182; `getParam`).
- Tracker and decision log: [`docs/monkey2/SUPPORT_STATUS.md`](../../docs/monkey2/SUPPORT_STATUS.md).
- Requirements: R-8.32 and R-8.33 (Proposed) apply; no new row.
