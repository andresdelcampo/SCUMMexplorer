# Checkpoint: S7 cutscenes, S9 Spanish editions, S4b/S5 MonkeyCd (17 October 2026)

Run request: "Keep going: run S7 Cutscenes for Indy3 EGA, Indy3 256 colours and Monkey Island floppy", then "can you parallelize anything else?" / "please do" (user, 17 October). Follows [the 16 October checkpoint](20261016-S6-Indy3Ega-S4b-S5-Indy3Vga.md).

## Done (all merged into master; suite at the end: 1056 tests, 1051 passed, 5 skipped, 0 failed)

| Item | Commits | Result | Record |
|---|---|---|---|
| S7 Indy3Ega + Indy3Vga, with the shared scan moved to `Core/Scumm/Analysis` (`ScummCutsceneIndex`, `ScummCutsceneTitles`, `ScummCutsceneVerbs`, `ScummCutsceneRun`, `ScummCutscenePlanner`) | 002867b..28f1122 | 153 entries per release (71 scenes); 90 begin from a fresh start (after the run fix below); three acceptance scenes (Room 73 light switch, Room 22 entry, Room 80 entry). LOOM lists byte-identical. **Cutscenes –→◐** for both | `.verification/indy3-cutscenes-20261017/` |
| S7 MonkeyFloppy | 9b625aa, 22d2835, 4990902 | 164 entries (63 scenes); 104 begin; acceptance: Room 33 poster, Room 37 entry, Room 19 entry; derivation 55 s on first launch. Shared run fix: a begin counts only when it comes from the trigger's own scripts or rooms (Indy3 95→90). **Cutscenes –→◐** | `.verification/monkey1-cutscenes-20261017/` |
| S9 Indy3 Spanish (`indy3 ega es`, `indy3 vga es`) | 3c43b1a, d7d51aa | Every stage passes; separate translations handled per copy; the 256-colour Spanish charset has no ¿ ¡ glyph (drawn as nothing, as the data says) | `.verification/indy3-editions-20261017/` |
| S4b + S5 MonkeyCd (first v5 game) | 1571451..e32fc34 | `ScummV5DataSource`; boot to Room 38 (the lookout) in 1573 frames, nothing unsupported, no assumed input; CD music timer; 32-colour costumes; 82 of 83 rooms without a gap (Rooms 88, 89, 99 written down) | `.verification/monkeycd-session-20261017/`, `monkeycd-objects-20261017/` |
| Scan text decoding + S9 MonkeyFloppy Spanish (`monkey1 ega es`) | 4fdc10b..146d70d | Cutscene titles and lines decode with `ScummTextDecoder` (English lists identical except one Monkey name gaining "Mêlée"; Spanish lists gain ¿ ¡ ñ á); the eight-disk Spanish copy passes every stage | `.verification/monkey1-editions-20261017/` |

## Not done and why

- No ScummVM runtime comparison (agents cannot drive it).
- Cutscenes: about 60 entries per game begin nothing from a fresh start; no derived start setups yet (Proposed R-8.33). Some background attribution may still be loose (`Room 28 · Use mug`, `Room 35 · Open door`).
- MonkeyCd: Rooms 88, 89, 99; the hit test's untouchable class (v5 uses 32); `BootForPlay` reads starting objects through the v4 archive only (S6 work).
- Offer (held back because it changes the Indy3 lists): count `printEgo` lines as first texts, which fills Monkey Island's text column.

## Proposed requirements to confirm or reject

R-8.32, R-8.33 (script-derived cutscene lists and fresh-start runs for the generic session); R-7.7 to R-7.18 except R-7.16 (confirmed 16 October).

## User checks

1. Indy3 EGA and 256 colours: Room 73, Play Cutscene ▴ → "Room 73 · Turn off light switch"; the picker → "Room 22 · Entry", "Room 80 · Entry" (Advanced). First launch: a gold line counts the scan for about a minute.
2. Monkey Island floppy: Room 33 → "Room 33 · Look at poster"; picker → "Room 37 · Entry", "Room 19 · Entry".
3. Spanish copies: `indy3 ega es` Room 1 choices (¿ glyph; capture in `.verification/indy3-editions-20261017/window/`), `monkey1 ega es` Room 33 in play and its cutscene list with accented titles.
4. Monkey Island CD: the room cards of Rooms 28, 30, 33, 37, 38, 70.

## Parallel orchestrator

The user may run Maniac Mansion and Zak McKracken (v1/v2) from an independent orchestrator; the coordination rules and its brief were given in chat on 17 October (v1/v2 opcodes in their own executor file, small additive shared edits, merge master then fast-forward and retry, its own checkpoint names and `NEXT_SESSION_PROMPT_MANIAC_ZAK.md`).

## Next run

`S6 MonkeyCd` (play on v5: `BootForPlay` through the data source, class 32, v5 verbs), then `S4b Monkey2` / `Indy4`; or `S7 MonkeyCd` after S6.

## Continued (17 October, evening): S6 MonkeyCd, S4b/S5 Monkey2 and Indy4, in parallel

Run request: "Keep going: run S6 for Monkey Island CD, and in parallel S4b for Monkey Island 2 and Indy 4." All three were stopped once by a rate limit and resumed. Master moved meanwhile with the separate Maniac/Zak orchestrator's S4b and S5; each branch merged master before its fast-forward.

| Item | Commits | Result | Record |
|---|---|---|---|
| S6 MonkeyCd | 73dc582, 097646e, 37b26a9, b0be9ea, fd3f758 | Route from the dock (the game reaches it after the lookout): Look at poster (verb 8, object 429) and its cutscene skipped, door 428 to the SCUMM Bar, the important-looking pirates (Script 14 input, lines 0x78–0x7A, 0x7F), back out through Script 120's flashback, Room 29's rubber chicken (inventory icon, object 961); right click default verb from `var[0x0B6]`; disc music in play, looping tracks. v5 by profile: `UntouchableClass` 32, `VerbImagesFromNamedRoom`, v5 charsets, `charsetColors`. **Cells: Objects ◐→✔, Play –→✔** | `.verification/monkeycd-play-20261017/` |
| S4b + S5 Indy4 (`indy4 vga en`, talkie CD) | fb15da8..4f5715e, 3a5f5cf, 36a2b95 | Boot: Room 68, Room 4 (Barnett College) with the player in control after 721 frames; nothing unsupported; no copy protection at boot parameter 0; one labelled pointer position in Script 79. 96 of 96 rooms without a gap; Rooms 32, 46, 50 pinned. `soundKludge` start/stop events; iMUSE lengths labelled estimates; one image per object state | `.verification/indy4-session-20261017/`, `indy4-objects-20261017/` |
| S4b + S5 Monkey2 (`mi2 talkie en`, a fan-patched talkie) | 2bd0dc9..52052f5, af4b179, 04f7e11 | Boot to Room 4 (the campfire) in 3409 frames; nothing unsupported; one assumed click choosing the full game (not Lite) through Script 131. 107 of 110 rooms without a gap (Rooms 104–106 hold images only); Rooms 39, 55, 79 pinned. Transparent v5 object strips; `roomOps` palette colour; the duplicate Indy4/Monkey2 engine work reconciled into one implementation each (04f7e11) | `.verification/monkey2-session-20261017/`, `monkey2-objects-20261017/` |

Suite on master at the end: 1178 tests (1173 passed, 5 skipped, 0 failed). My own merge slip (a stray parenthesis in `ScummProfile` while merging Indy4 with Maniac/Zak) broke one build and was fixed in 36a2b95 before the fast-forward.

**Not done:** ScummVM runtime comparison; MonkeyCd Rooms 88, 89, 99; the lookout shows for one frame in MonkeyCd play (the boot's headless frames ran its track); iMUSE markers/hooks and the music timer for Monkey2/Indy4; the play screen picks v5 object images by state only after S6 for those games; Indy4 Room 4's magenta line.

**User checks (added):** Monkey Island CD (`Games/monkey1 vga en`) from Room 38: music at the dock, Look at the poster + Esc, the door, the bar's music and dialogue, right click the poster, the chicken's inventory icon in Room 29. Indy4 `room-04-after-boot.png` (magenta line) and Rooms 4, 32, 46, 50. Monkey2 `room-04-after-boot.png` and Rooms 39, 55, 79.

**Next run:** `S6 Monkey2` and `S6 Indy4` (play on v5; both have iMUSE music in play to wire), `S7 MonkeyCd`, `S9` for the other MI2/Indy4 copies (`mi2 mt32 en`, `mi2 sb16 en`, the Spanish copies, `indy4 classic vga es`, `indy4 talkie vga es`) and `monkey1 vga es`.
