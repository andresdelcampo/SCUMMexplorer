# Monkey2 S9 Editions: the floppy releases and the Spanish talkie (18 October 2026)

| | |
|---|---|
| Family / stage | Monkey2 / S9 |
| Copies checked | `Games/mi2 mt32 en` (DOS floppy, English, Roland data), `Games/mi2 sb16 en` (DOS floppy, English, Sound Blaster data), `Games/mi2 classic es` (DOS floppy, Spanish), `Games/mi2 talkie es` (Spanish with the fan talkie patch, `monster.sou`), all read from `D:\Juegos\Lucas` |
| Reference | `Games/mi2 talkie en` (English, fan "Talkie CD 0.2 beta" patch); its records: `monkey2-session-20261017`, `monkey2-objects-20261017`, `monkey2-play-20261017`, `monkey2-cutscenes-20261018` |
| Model | `.verification/indy3-editions-20261017`, `.verification/monkey1-editions-20261017` (texts per copy, offsets from each copy's scripts) |
| Registry change | unchanged: S9 raises no cell; no copy fails |
| Build | worktree branch `worktree-agent-a47aea5921c3bc41e` after merging master `defbf54e` |

ScummVM was not run (brief).

## What was run (all through RTK, `rtk proxy dotnet ...`)

| Check | Harness / test | Output |
|---|---|---|
| Boot (S4b), choice click, copy protection | `.verification/monkey2-session-20261017/BootTrace <folder> <out> 0`; `ScummMonkey2SessionTests.TheBootTakesTheFullGameAndReachesTheCampfireWithThePlayerInControl` (theory over the five copies) | `boot/boot-*.txt` (the talkies' traces cut to their head) |
| Room audit (S5) | `.verification/monkey2-objects-20261017/RoomAudit <folder> <out>` on the five copies with the same build; `ScummMonkey2FirstVisitTests.ARoomSettlesWithTheObjectsItsScriptsDraw` (Rooms 39, 55, 79 × five copies) | `room-audit/room-audit-*.tsv` |
| Play route (S6), right click, play screen | `ScummMonkey2PlayTests.TheAcceptanceRouteFromWoodtickRunsThroughTheGamesOwnInterface`, `ARightClickOnTheSignRunsItsDefaultVerb` (theories, texts per copy in `RouteTexts`) | test output |
| iMUSE music in play and the beat answer | `ScummMonkey2CutsceneTests.LeChucksSceneStartsItsSongAndFollowsItsBeat` (theory): Script 76 starts sound 176 at +0x0019 and passes the beat wait (+0x0090..+0x00E6) | test output |
| Cutscene scan and fresh-start runs (S7) | `.verification/indy3-cutscenes-20261017/CutsceneList <folder> scan` and `prun 8`; `ScummMonkey2CutsceneTests.ThreeScenesBeginFromAFreshStart` (theory) | `cutscenes/scan-*.txt`, `cutscenes/prun-*.txt` (mt32 en, classic es, talkie es; sb16 en equals mt32 en) |
| Accents and charsets | `ScummMonkey2PlayTests.EveryTextByteAboveAsciiHasAGlyphInTheCopysCharsets` (theory) | test output |
| Window (one look) | `.verification/monkey2-play-20261017/PlayWindowCheck` on `mi2 classic es` (real `MainWindow`, private `SCUMM_EXPLORER_PROFILE_ROOT` in the scratch output) | `window/play-check.txt`, `window/play-02-sentence.png`, `window/play-05-dialogue.png` |

## Edition × check × result

| Check | `mi2 mt32 en` | `mi2 sb16 en` | `mi2 classic es` | `mi2 talkie es` |
|---|---|---|---|---|
| Detection / opening | pass: Monkey Island 2, v5 resource file, family Monkey2 | pass | pass | pass |
| Boot (S4b) | pass after the fix: 3657 frames, rooms 108, 0, 87, 0, 3, 103, 109, 0, 5, 4, player in control at the campfire, unsupported 0 (before: frame limit in Room 108) | pass: 3657 frames, same rooms | pass: 3699 frames, same rooms | pass: 2883 frames (reference 2841), same rooms |
| Copy protection | asked (Script 128/130, see below); answered: Enter, then the digits of its own `var[0x206]` = 1412 for the seeded run, accepted; labelled assumed input; no quit | asked; answered the same way (1412) | asked; answered the same way (1412) | not asked (Script 130 skips it for boot parameter 0, as the reference) |
| Full/Lite choice click | pass: one click on object 1156 through Script 131 after the protection | pass | pass | pass (the only assumed input, as the reference) |
| Room audit (S5) | pass: 110 rooms, 107 without a gap (104–106, image holders, as the reference), 0 could not run. Same outcome, active images, gaps, actors in 108 rooms; Rooms 45 and 50 differ in data (below) | as mt32 en | as mt32 en; frames differ in 9 rooms (text lengths) | pass: every room's state equal to the reference; frames differ in 8 rooms (text lengths) |
| Play route (S6) | pass: Woodtick at (696, 53), "Look at sign" (no cutscene in this copy's code: both lines are said, then quiet), the shovel, Largo's two lists through Scripts 14 and 206, the robbery, the door to Room 8 at (51, 112) | pass, as mt32 en | pass: "Mirar a" / "Mirar a rótulo" / "Coger rótulo", "¿Es esto un soborno?", "¿Entonces quién cuida de la ley y del orden público?", "Ir a puerta" (no sign cutscene) | pass: "Mirar" / "Mirar rótulo"; the sign's cutscene and its Esc skip as the reference; dialogue names padded with '@' drawn as nothing |
| Right click default verb | pass: `runInputScript(2, 0, 2)` looks at the sign | pass | pass ("Vienbenido a Woodtick") | pass |
| iMUSE in play, beat answer | pass: sound 176 starts at Script 76 +0x0019 and the beat wait ends (estimate from the AdLib timeline, as the reference) | pass | pass | pass |
| Cutscene scan (S7) | pass: 450 triggers, 190 groups, 76 scenes, **97 begin** from a fresh start (157 s on 8 workers); 189 groups shared with the reference with the same begun/not begun but one (Room 50 Close coffin); 1 group of its own (Room 85 Use ocean); the reference has 71 more (the talkie patch's cutscene-wrapped descriptions, 67 begin) | equal to mt32 en (keys and runs) | equal to mt32 en (keys and runs); verb words "Abrir … Mirar, Coger, Hablar, Ir a" | equal to the reference: 536 triggers, 260 groups, 165 begin; the same keys, begun and rooms per run; Spanish verb words (after the '@' fix) |
| Acceptance scenes (Room 9 barkeeper, Room 13 entry, Room 74 key) | pass (3 of 3; Room 13 goes on to Room 12) | pass | pass | pass |
| Accents and charsets | pass: texts write 0x82 é, 0x83 â, 0x88 ê | pass | pass: CP437 0x81 ü, 0x82 é, 0x88 ê, 0x89 ë, 0xA0 á, 0xA1 í, 0xA2 ó, 0xA3 ú, 0xA4 ñ, 0xA5 Ñ, 0xA8 ¿, 0xAD ¡; all drawn by charsets 1, 2, 4, 5 except 0x83 (1 use) in charset 1 | pass: the same codes plus 0x92 Æ (2 uses, also in the English talkie); charsets 1, 2, 4, 5 draw all but 0x83/0x92 |
| Talkie speech | none (no `monster.sou`) | none | none | speech codes (`\xFF\x0A`) left out of the first texts as in the reference; speech is not played by the session (as the reference) |
| Window (one look) | – | – | pass: the real window boots through the protection, plays to Woodtick, the sentence line "Mirar a rótulo" and the dialogue lines with ¿, ó, ¡ drawn (`window/play-02-sentence.png`, `window/play-05-dialogue.png`) | – |

## Differences found and how they are handled

1. **Copy protection in the floppy releases** (genuine difference in data): Script 128 and Script 130 have no skip for boot parameter 0. Script 128 asks for Enter, Script 130 draws the recipe and keeps the expected answer in `var[0x206]` (+0x0134) before it reads four digits through input script 131 (three wrong answers end in `systemOps quit`). Handled by the profile's plan, now `ScummBootInputPlan("var[0x206]", ClickObject: 0x484, AnswerDigits: 4)`: the boot presses Enter at the first stall, types the digits the game itself derived (read, never written; zero-padded to four), and clicks the full game once Script 130 has made it touchable. The talkies never write `var[0x206]` and keep their single click. Rows in [MONKEY2_SCRIPT_MAP.md](../../docs/monkey2/MONKEY2_SCRIPT_MAP.md) (Boot and hooks).
2. **Engine change (small, additive, shared code):** `ScummGameSession.PressBootKeys` clicks the choice object only when it is not untouchable (class 32) and only once; before that a plan with an answer variable answers with keys. The stall test counts script numbers instead of offsets only while a fixed-length answer the game wrote waits (Script 136 blinks the input field on a `delay` loop, so the offsets never stood still). `ScummBootInputPlan.AnswerDigits` is new (default 0: no padding). Monkey Island floppy's plan is unchanged in behaviour.
3. **Texts differ per copy, not per language:** the two Spanish copies are different interface translations ("Mirar a" in the floppy's sentence line, "Mirar" in the talkie's). The tests keep `RouteTexts` per copy.
4. **The talkie patch wraps multi-line descriptions in cutscenes** (the sign in Room 7 and 70 others: 71 groups, 67 begin). The floppy sign prints both lines without a cutscene; the play test reads this from the copy's own object code (`SignOpensCutscene`).
5. **Rooms 45 and 50 are patched in the talkies** (one more object image in Room 45; Room 50's scripts keep running, so the first visit reaches the frame limit, with the same active object). Data; nothing to change.
6. **'@' filler** in the Spanish talkie's verb and dialogue names: the charset already draws it as nothing; the cutscene planner's verb words now drop it (`ScummCutscenePlanner.VerbsOf`, one line; before: "Mirar@@@…" in the titles).
7. **Charsets 3, 6 and 7** lack most accents in every copy (English too): fonts the game uses for other screens; not an edition matter.

## Tests and regression

- `ScummMonkey2SessionTests` 9 passed (the boot theory over the five copies; the profile test now pins the plan with the answer variable).
- `ScummMonkey2PlayTests` 16 passed (route and right click × 5, glyph check × 5, play screen).
- `ScummMonkey2FirstVisitTests` 17 passed (Rooms 39, 55, 79 × 5).
- `ScummMonkey2CutsceneTests` 11 passed (fresh-start scenes and LeChuck's beat × 5, titles).
- Neighbour filter `Monkey|Support|Cutscene|Indy4|Profile|ScummGameSession`: 248 passed, 0 failed.
- LOOM regression (filter `Loom`, shared session code changed): 841 total, 836 passed, 5 skipped (the usual dumps), 0 failed.
- Code and tests: commit e8a80047.

## Uncertainties kept

- ScummVM was not run: the recipe screen, the Spanish pictures, costumes and timing are not compared with the original; the window look is one screen of one copy.
- The protection answer 1412 is the one the seeded run's random picks lead to; another seed draws other recipe ingredients and another answer, always read from `var[0x206]`.
- The Roland and Sound Blaster copies differ only in their driver files (`ROLAND.IMS`, `SOUNBLAS.IMS`); the session's sound clock reads the songs' AdLib parts in every copy. Listening on each device is a user check.
- Which charset each screen uses was not traced; the window shows the sentence line and dialogue with accents.

## User checks

- Optional: open `Games/mi2 classic es` in the app, select Room 4: the play boot passes the recipe screen by itself (Advanced lists the assumed Enter, digits and click) and the game goes on to Woodtick; the verbs and sentence line are Spanish with accents ("Mirar a rótulo").
- Optional: compare the recipe screen's answer with the original (ScummVM, `mi2 mt32 en`): the expected four digits are those of `var[0x206]`.
