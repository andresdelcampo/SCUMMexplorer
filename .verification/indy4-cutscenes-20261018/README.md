# Indiana Jones and the Fate of Atlantis S7: cutscenes from the scripts, playable with their speech

| | |
|---|---|
| Family / stage | Indy4 / S7 |
| Fixture and edition | `Games/indy4 vga en` (English talkie CD, SCUMM v5: `ATLANTIS.cue`/`.img`, `ATLANTIS.000`/`.001` and the speech bank `MONSTER.SOU` on the data track; no audio tracks); data in `D:\Juegos\Lucas` (`TestGames.Root`) |
| Date and author | 2026-10-18, S7 Indy4 agent (Claude), in parallel with S7 Monkey2 |
| Commits | branch `worktree-agent-a056fbb709f263cab`: two shared commits (Core, App), then the registry and test, then this record and the docs |
| Registry change | Cutscenes: Indy4 – → ◐ (list derived from the scripts, playable; ✔ stays reserved for a hand-validated catalog, R-8.18) |

## What was done

1. **The shared scan reads the game unchanged** (`ScummCutsceneIndex.Scan` through `ScummV5DataSource`): 666 triggers, 426 groups.
2. **Verb names after the credits (shared, `ScummCutscenePlanner`).** The play boot ends in the playable credits (Room 4, Script 78 as the input script, no verb on), so the titles came out raw: 187 of 426 had `verb 0x..`, and the boot named only the prepositions 0x81 to 0x84. The planner now runs a boot that shows no verb on: it presses Esc each time the game itself has set an override (`CutsceneSkippable`) and stops when a verb is on, the player has control and no cutscene is open (limit 6000 frames). On Indy 4 that is two skips, Room 4's at frame 0 and Room 1's (Smith's scene) at frame 1, then New York (Room 10) at frame 110, where Script 19 with 2 has named the verbs. The names are `0x03 Open, 0x04 Close, 0x05 Give, 0x06 Push, 0x07 Pull, 0x08 Use, 0x09 Look at, 0x0A Walk to, 0x0B Pick up, 0x0C Talk to` (plus `0x35 None` and the sentence line's `0x64 Walk`, which no object answers). `VerbSource` logs it: "the boot (Room 04) shows no verb; the verb names are those of Room 10 after 2 of the game's own skips (Esc) and 110 frames". That session serves only for the names; every scene's run still starts from the boot. A boot that shows a verb is left as it is, so the other games' lists are unchanged. Raw numbers remain only for `verb 0x50` (4 entries), which the game never names.
3. **Spoken first lines (shared, `ScummTextDecoder.DecodeLine`).** A talkie line starts with four `0xFF 0x0A` codes, and each carries two operand bytes (the voice's offset and size). Where an operand byte was a printable character, the decoder kept it, so first texts read ", I still think it's a fake." or "6  4 Here, my friends…". Code 10's operands now read as spaces. Every code's operands as spaces was tried first: it changed the LOOM EGA and CD lists (text counts; one more LOOM CD scene) and Monkey Island's "trial @ twice" line, so it was narrowed to code 10.
4. **Speech in a scene played from the list (shared App, `TalkieSpeechPlayer`, `MainWindow.Play.cs`, `MainWindow.PlayCutscenes.cs`).** A room opened for a room-entry scene keeps the visit's entry (the S7 MonkeyCd rule). But the play start re-attaches the speech player after that visit, as for a state preview, so a line the visit started would not be spoken. This was read from the code, not observed before the change. The play start now records the speech count before its visit, and a room-entry scene played from the list attaches the player from there. An object scene, or an entry entered again, needs no change: the player stays attached.
5. **Registry and test.** Indy4 Cutscenes – → ◐; matrix regenerated (`SCUMM_UPDATE_SUPPORT_MATRIX=1`: README and REQUIREMENTS, one cell each). `tests/SCUMMexplorer.Tests/ScummIndy4CutsceneTests.cs` has 3 tests.
6. **Harness.** `CutsceneList` (Indy3 record) gained a `skip` mode (the boot, then the game's skips until a verb is on) and prints the planner's verb source. `CutsceneWindowCheck/` is the real-window check, copied from MonkeyCd's.

## Commands run (through RTK)

```
rtk proxy dotnet build .verification/indy3-cutscenes-20261017/CutsceneList/CutsceneList.csproj
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\indy4 vga en" scan | skip | scan run (-> indy4-list.txt) | prun 8 | probe <room> 0 00
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\<loom ega mt32 en | loom vga en>" loom          (before and after)
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\<indy3 ega en | indy3 vga en | monkey1 ega en | monkey1 vga en>" scan   (before and after, timing line dropped)
rtk proxy dotnet Exe/Indy4BootTrace/... dump "D:\Juegos\Lucas\indy4 vga en" e:10 v:10:138:08
rtk proxy dotnet build SCUMMexplorer.slnx
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~ScummIndy4Cutscene"
rtk proxy dotnet test ... --filter "FullyQualifiedName~GameSupport|FullyQualifiedName~SupportConformance|FullyQualifiedName~SupportMatrix"   (SCUMM_UPDATE_SUPPORT_MATRIX=1)
rtk proxy dotnet test ... --filter "FullyQualifiedName~Loom|FullyQualifiedName~Cutscene|FullyQualifiedName~Play|FullyQualifiedName~Support|FullyQualifiedName~Indy4|FullyQualifiedName~Text"
Indy4CutsceneWindowCheck "D:\Juegos\Lucas\indy4 vga en" <scratch>\window1     (private SCUMM_EXPLORER_PROFILE_ROOT and SCUMM_ROOM_CACHE_DIR)
```

## Results (numbers)

| | Indiana Jones and the Fate of Atlantis (talkie CD) |
|---|---|
| Triggers (depth 3) | 666 |
| Groups (entries in the list) | 426 |
| Static category: Scenes / Dialogue and reactions | 182 / 244 |
| Fresh-start run began a cutscene of its own | 210 of 426 (scenes: 80 of 182) |
| Titles with a raw verb number | 4 (`verb 0x50`), before the planner change 187 |
| Planner (one boot, the skips to the verbs, the scan) | 2.1–2.8 s |
| All runs, one thread (harness) | 233 s (machine shared with the parallel S7 Monkey2 agent) |
| All runs, 8 workers, own copies (harness `prun 8`) | 185 s, results identical to one thread |
| Background derivation in the window, while play runs (first launch) | **185 s** from the play start (193 s from opening; 8 workers, own copies), with the parallel S7 Monkey2 agent's runs on the machine; the last 15 runs took about 60 s |

Full listing with every run: `indy4-list.txt`.

**Acceptance (three scenes, each begins from a fresh start headless; `ScummIndy4CutsceneTests`, 3 of 3 passed):**

| Scene | Trigger found by the scan | Code it reaches | Fresh-start run |
|---|---|---|---|
| Room 01 · Entry: Smith's scene, "I still think it's a fake." | Room 1's entry script | Room 1 locals 200 to 213 (63 lines) | began; 1846 frames; went on through Rooms 74, 8, 9, 2, 75 to New York (10): from the credits the rest of the credits plays around the scene |
| Room 11 · Entry: Sophia's lecture in New York, "Here, my friends, is ATLANTIS…" | Room 11's entry script | Room 11 local 200: `cutscene` +0x0000 to `endCutscene` +0x03CE, `loadRoom` 12 at +0x03CF; locals 201, 203 | began; 516 frames; went on backstage (Room 12, locals 204, 210, 203) |
| Room 82 · Entry: the diving suit, later in the game, "Oh boy… I've got maybe three minutes of air in my suit…" | Room 82's entry script | Room 82 local 201: `cutscene` +0x0000 to +0x01F3, `loadRoom` 42 at +0x01F4; locals 202, 205, global script 57 | began; 161 frames; went on to Room 42 (local 200, "Avast, you lubbers.  We've arrived.") |

**The scene's speech, headless** (`TheLecturePlaysWithItsSpokenLines`): from the play boot, Room 11 visited as the window does. The lecture's first line is on screen inside a cutscene, and its spoken line is recorded: sample 0x003436D0, 140 jiffies from the voice file.

**Not begun from a fresh start (the brief's examples):**
- Room 10's fire-escape climb (`Use fire escape`, object 138 verb 0x08, local 207) needs the three crates pushed first (class 6). It plays in play after the S6 route's pushes.
- Room 10's entry ("Well, here I am in New York…") depends on `bit[0x013]`, `bit[0x04F]` and `var[0x0E0]`, which the credits do not set (Room 10 entry +0x004E to +0x00F3).

Both are listed with the note that a fresh start does not begin them (Advanced mode).

**LOOM, Indy3 and Monkey lists unchanged.** `CutsceneList loom` for `loom ega mt32 en` and `loom vga en`, and `CutsceneList scan` for `indy3 ega en`, `indy3 vga en`, `monkey1 ega en` and `monkey1 vga en`, were run before and after the shared commits. All six outputs are byte-identical (SHA-256 equal, the planner's timing line dropped).

**Regression** (filter `Loom|Cutscene|Play|Support|Indy4|Text|Architecture`; LOOM EGA and CD, Indy3 EGA, sound and MT-32 roots set): 1007 passed, 5 skipped (LOOM dumps), 0 failed. The support tests (19) passed with `SCUMM_UPDATE_SUPPORT_MATRIX=1`.

**Window check** (`CutsceneWindowCheck/`, real `MainWindow`, private profile and cache; `window/cutscene-check.txt`, captures `window/cutscene-0N-*.png`):
- Play started in Room 4 (the credits) 7.9 s after opening, with the playback bar, the room button and the progress line.
- The 426 background runs finished in 185 s while play ran. Planner: 666 triggers, 426 groups, 182 scenes, 210 begun, with the verb source above.
- The picker (Normal mode) lists 210 entries.
- **`Room 11 · Entry` from the picker:** Room 11 opened, a cutscene began, and "Here, my friends, is ATLANTIS…" was on screen with Skip enabled (`window/cutscene-03-room-11-lecture.png`).
  - The speech player spoke the first line (offset 0x003436D0, 140 jiffies) and the next (0x00349C44, 143 jiffies) from `MONSTER.SOU`.
  - The scene's music, sound 41 from Room 11's entry +0x0013 `startSound 0x29` (iMUSE, AdLib, 73.3 s), was rendered and opened.
  - 6 s later the lecture was at "…as it might have appeared in its heyday…".
- **`Room 82 · Entry` from the picker:** Room 82 opened, a cutscene began, and "Hey-- what happened?" was on screen (`window/cutscene-04-room-82-diving.png`).
  - The speech player stopped the lecture's voice and spoke two lines (0x070DCE6A, 138 jiffies; 0x070E3246, 107 jiffies).
  - 6 s later: "…Atlantis is right below me!".
  - The scene-sound log shows only "stop all: play was left", logged when the room was selected for the scene. Room 82's entry started no sound of its own in those 6 s.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Source only (the bundled ScummVM was not run, per the brief): `string.cpp` `handleNextCharsetCode` (case 10 reads 14 bytes: the voice codes' operands), `script_v5.cpp` `o5_cutscene`/`o5_endCutscene`/`o5_beginOverride`, `script.cpp` `abortCutscene`, read for S6 Indy4 and S7 MonkeyCd (17–18 October 2026).

## Differences and uncertainties kept

- A scene run from the list is an **exploration action**. The room is a state preview when the session was elsewhere (Advanced note).
- **216 groups begin nothing from a fresh start.** Their code tests state the credits do not reach. No start setup is derived for this game (R-8.33).
- **The skips to the verbs press Esc as the game allows it.** That is an exploration of the game's own skip, not the user's input. If a future edition's boot showed verbs, nothing would change; if its skips never reached a verb, titles keep numbers and `VerbSource` says so.
- **Room 11's run lists Room 4** among the rooms it entered, besides 12 (the run started in the credits' room). It was not traced further; the room list counts the lecture also for Room 4.
- **Mouth sync is not modelled** (as S6).
- **First texts.** Only `print` lines are counted (as for Monkey Island), so some of Indy's `printEgo` lines give no first text. The game's ellipsis shows as `^`, as in Monkey Island's lists.

## User checks (exact steps and where; none if none)

1. Open `D:\Juegos\Lucas\indy4 vga en` in the app and select Room 4. Play starts in the credits. A gold line under the game counts the background scan (first launch only) and disappears when it is done.
2. Press the picker's `Play Cutscene` and choose `Room 11 · Entry`. The lecture hall opens and Sophia's lecture plays: every line is spoken, and the music plays. Esc skips once the scene allows it.
3. Do the same with `Room 82 · Entry`. The diving-suit scene is spoken, then the boat (Room 42) follows.
4. Switch Advanced on and open the picker. The fire escape and Room 10's entry are listed with the note that a fresh start does not begin them; Normal mode hides them.
5. Check that the titles read with the game's verbs ("Use fire escape", "Talk to ticket taker").

## Where the facts went (links to the script map / engine notes / tracker entries)

- Scene rows: [`docs/indy4/INDY4_SCRIPT_MAP.md`](../../docs/indy4/INDY4_SCRIPT_MAP.md) (Rooms 1, 11, 82, 10 (S7); "skips to the verbs").
- Tracker and decision log: [`docs/indy4/SUPPORT_STATUS.md`](../../docs/indy4/SUPPORT_STATUS.md).
- Requirements: R-8.32 and R-8.33 (Proposed) apply; no new row.
