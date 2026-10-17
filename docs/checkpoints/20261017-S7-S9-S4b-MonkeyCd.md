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
