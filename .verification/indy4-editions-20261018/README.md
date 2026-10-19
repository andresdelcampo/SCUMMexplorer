# Indiana Jones and the Fate of Atlantis S9 Editions: the Spanish talkie and the Spanish floppy (18 October 2026)

Scope: stage S9 for the family Indy4. Copies checked against the reference `Games/indy4 vga en` (English talkie CD image, `ATLANTIS.cue`/`.img`):

- `Games/indy4 talkie vga es`: a folder with `ATLANTIS.000` (12035 bytes, as the English disc's), `ATLANTIS.001` (9903486; English 9823920), `MONSTER.SOU` (149867609 bytes, the same size as the English disc's) and `ATLANTIS.EXE` (128283, same size). Spanish texts, the English talkie's speech codes (same offsets in `MONSTER.SOU`): Spanish subtitles over the English voices.
- `Games/indy4 classic vga es`: the Spanish floppy release (`ATLANTIS.000` 12030, `.001` 9677678, `ATLANTIS.EXE` 112565, no `MONSTER.SOU`).

Stages compared: S4b boot (and the floppy's copy protection), S5 room audit, S6 play route with right click/keys, music (iMUSE) and speech, S7 cutscene scan and fresh-start runs, accents and charsets. Model: `.verification/monkey1-editions-20261017`. ScummVM was not run (brief). Build: worktree branch `worktree-agent-acff2f51ccaba0606` on master defbf54e.

## What was run (all through RTK, `rtk proxy dotnet ...`)

| Check | Harness / test | Output (local, `.verification/**/*.txt`/`*.png` are git-ignored) |
|---|---|---|
| Boot | `.verification/indy4-session-20261017/Indy4BootTrace <folder> <out> 0`; `dump <folder> g:1 e:81 l:81:205 l:81:207`; `ScummIndy4SessionTests.TheSpanishTalkieBootsTheSameWayWithoutInput`, `TheSpanishFloppysStoneDiskIsAnsweredWithTheArgumentItsOwnRoomCodePasses` | scratch boot traces |
| Room audit (S5) | `.verification/indy4-objects-20261017/RoomAudit` on all three copies with the same build | scratch `room-audit.tsv` per copy |
| Play route of R-7.18, keys, speech | `ScummIndy4PlayTests` (route, main-menu key and verb key, talkie line length) as theories over the three copies (`RouteTexts` per copy) | test output |
| Cutscene scan and fresh-start runs (S7) | `.verification/indy3-cutscenes-20261017/CutsceneList <folder> scan` and `prun 8`; `ScummIndy4CutsceneTests.TheSpanishCopiesListTheSameScenesWithTheirOwnTexts` | scratch lists |
| Accents and charsets | `ScummIndy4PlayTests.EveryTextByteAboveAsciiHasAGlyphInTheCopysCharsets` (three copies; shared collector `tests/.../ScummCopyTextBytes.cs`) | test output |
| Window | `.verification/indy4-play-20261017/PlayWindowCheck` on `indy4 talkie vga es` (real `MainWindow`, private `SCUMM_EXPLORER_PROFILE_ROOT` under the scratch output); the English disc once for comparison | `window/play-check.txt`, `window/play-01-prologue.png` to `play-04-said.png` |

## Edition × check × result

| Check | `indy4 talkie vga es` | `indy4 classic vga es` |
|---|---|---|
| Detection / opening | pass: Fate of Atlantis, v5 resource file, family Indy4, speech bank found | pass: the same, no speech bank (`game.Speech` null) |
| Boot (S4b) | pass: 717 frames (English 721), rooms 68, 0, 4, player in control in Room 4, unsupported 0, no assumed input | pass after the fix below: 727 frames, rooms **81**, 68, 0, 4, player in control, unsupported 0. Before the fix: 5000-frame limit in Room 81 |
| Copy protection | none (Script 1 as the English disc's) | the stone-disk screen (Room 81) answered as labelled assumed input: Room 81 local script 207 started with local 0 = 1, the argument the room's own submit passes (local 205 +0x017A); see "Differences" 1 |
| Room audit (S5) | pass: 96 rooms, 96 without a gap, 0 that could not run; outcome and active images equal to English in every room; Rooms 92 and 97 (both at the frame limit) differ in the number of actors drawn (4/6, 4/8): the frame the limit cuts | pass: 96 rooms, 96 without a gap, 0 that could not run; active images equal except Rooms 27, 29 (fewer at the moment the room is left), 68 (0/1, English 1/1) and 79 (1/4, English 0/4); 16 rooms end differently (RoomLeft/Settled/FrameLimit: 10, 17, 20, 27, 29, 41, 45, 46, 49, 55, 63, 64, 66, 71, 76, 95) and six draw a different number of actors at their end (16, 23, 65, 77, 92, 97): the floppy's scripts run without the talkie's speech waits, so its timing differs (data) |
| Play route of R-7.18 (S6) | pass: same ids, positions, branches and offsets (the statue click +0x0061, Room 5's skip +0x000F); texts its own ("estatua rara", "Mirar luminoso", "Coger periódico", "Hablar taquillera", "Empujar cajón", "Usar salida de incendios", choices "Pero…", "Espere…", "Alto…", "Mmm…"); 18 clicks and two Esc, as English | pass: the same route and inputs; "Hablar **con** taquillera" (the floppy's sentence script adds the preposition, the talkie's does not: data) |
| Keys and right click | pass: Esc skips, the main-menu key is not delivered, verb keys are the copy's ('m' for Mirar; English 'l'); right click reaches the game (profile `RightClickReachesGame`, shared with English; not separately clicked) | pass: the same |
| Music in play (iMUSE) | pass (route): the same sound ids as English; iMUSE lengths are the S3 default-path estimates, as English | pass (route): the same; the floppy's Room 81 plays sound 205 with `soundKludge` player commands, reported as estimates |
| Talkie speech | pass: the lines carry the English disc's offsets (Marcus 0x0507BB6E, "Pero…" 0x040B3E4B); a line lasts as long as its sample (`ATalkiesLineLastsAsLongAsItsSpokenSample`); the window started 16 spoken lines on the route | – (no speech) |
| Cutscene scan (S7) | pass: 666 triggers, 426 entries, 182 scenes, **210 begin** from a fresh start: every entry and every run result equal to English (keys, categories, scripts, begun/not begun, rooms entered) | pass: 666 / 426 / 182, **210 begin**; three entries differ in data: Room 2's entry chains to local 228 (talkie 227), Room 4's to 220 (talkie 216), Room 72's object is 985 (talkie 984); run results equal per entry |
| Acceptance scenes | pass: Room 1 (Smith, "Sigo pensando que es falsa."), Room 11 (the lecture, "Amigos, aquí está la ATLANTIDA…", goes on to Room 12), Room 82 (the diving suit, goes on to Room 42) begin | pass: the same three |
| Accents and charsets | pass: the texts write ü é á í ó ú ñ Ñ ¿ ¡ as CP437 codes (0x81, 0x82, 0xA0 to 0xA5, 0xA8, 0xAD; e.g. 0xA1 í 1702 times); charsets 1 to 3 draw all of them, charset 4 all but Ñ; charset 5 (talkie only) no accent | pass: the same in charsets 1 to 4 (the floppy has no charset 5) |
| Window (one look) | pass: Room 4 and New York in play in the real window: the Spanish verbs, "Mirar luminoso" on the sentence line, "Imagina quién paga…" said, the four Spanish dialogue lines, the credits with Ó (`window/`); the speech player followed the session and played 16 lines | not looked at in the window (headless only) |

No check failed after the fix; the registry level is unchanged (S9 raises no cell).

## Differences found and how they are handled

1. **The floppy asks the stone-disk question; the talkie does not.** Floppy Script 1: `+0x0646 isEqual local[0], 0x1EC1` (boot parameter 7873 skips the screen), `+0x06A1 loadRoom 0x51`, `+0x06A3 isEqual local[0], 0xE13F` (-7873 skips the wait), `+0x06AE notEqualZero var[0x1BC]` (the wait). Only Room 81 local script 207 sets `var[0x1BC]` (+0x005A). The room's input script (local 205) starts 207 with 0 for Esc (+0x0026) and other clicks (+0x0055, +0x018A, +0x0193) and with 1 for the submit (+0x017A); with 0, 207 jumps to its end (+0x000C → +0x0203). The generic boot answer (R-15.5) started 207 with every local 0 and so never reached the write (24 answers, 5000 frames). **Fix (engine, small, additive):** `ScummSessionBoot.NextStartArgument`: an answering script whose answer the game does not compute is started with 0 first (unchanged), and only when that did not release the boot, with the next nonzero literal the room's own code passes when it starts it; the note says so (*Player input assumed: starting Room 81 local script 207 with local 0 = 0 did not release the boot; the room's own code also starts it with local 0 = 1 (local 205 +0x017A)…*). The copy's own 207 compares the three wheel positions (`var[0x0F4]`..`[0x0F6]` with `var[0x0D3]`..`[0x0D5]`, +0x0040 to +0x004E) with branch offsets 0, so any submitted wheel passes in this copy (its data; a copy whose branches are not 0 would need the wheel's answer, not modelled). Other games' boots are unchanged (Indy3, Monkey, Monkey2, Maniac, Zak, LOOM tests: see below).
2. **Texts differ in data only** (CP437 accents and inverted marks, '^' drawn as an ellipsis); the tests keep them per copy (`RouteTexts`, keyed by the folder), never per language. The look key is the copy's ('m').
3. **Sentence preposition:** the floppy names "Hablar con taquillera", the talkie "Hablar taquillera": each copy's own sentence script.
4. **Floppy script numbering and an object id** (Room 2 entry → local 228/227, Room 4 → 220/216, Room 72 object 985/984): the floppy is not the talkie's data; read from each copy, nothing to change.
5. **Speech:** the Spanish talkie's `MONSTER.SOU` has the English disc's size and its lines carry the English offsets: the voices are the English recording (not listened to here).

## Not an edition difference (found on the way)

- The playable credits in Room 4 draw several credit lines on top of one another (`window/play-01-prologue.png`; the English disc does the same in the same harness). Not investigated; for the S6 owner.
- The English disc image also carries `DOTTDEMO`, `INDYDEMO` and `SAMDEMO` folders (`BootTrace files`); not checked.

## Commands

```powershell
rtk proxy dotnet Exe\Indy4BootTrace\Debug\net10.0-windows\Indy4BootTrace.dll "D:\Juegos\Lucas\<copy>" <out> 0
rtk proxy dotnet Exe\Indy4BootTrace\Debug\net10.0-windows\Indy4BootTrace.dll dump "D:\Juegos\Lucas\indy4 classic vga es" g:1 e:81 l:81:205 l:81:207
rtk proxy dotnet Exe\Indy4RoomAudit\Debug\net10.0-windows\Indy4RoomAudit.dll "D:\Juegos\Lucas\<copy>" <out>
rtk proxy dotnet Exe\CutsceneList\Debug\net10.0\CutsceneList.dll "D:\Juegos\Lucas\<copy>" scan | prun 8
rtk proxy Exe\Indy4PlayWindowCheck\Debug\net10.0-windows\Indy4PlayWindowCheck.exe "D:\Juegos\Lucas\indy4 talkie vga es" <out>
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~Indy4"
```

Timings on this machine: the cutscene `prun 8` takes about 285 s per copy (426 runs, 8 workers), the room audit 80 to 560 s (the floppy's ran beside other jobs).

## Tests

Regression on this build (after merging master defbf54e): the Indy4, Monkey (floppy, CD, 2), Indy3, Maniac, Zak, support, architecture, scanner, discovery, session and V5 tests 363 passed, 0 failed; LOOM (filter `Loom`) 836 passed, 5 skipped, 0 failed.

## Uncertainties kept

- ScummVM was not run: pictures, costumes and timing of the Spanish copies are compared with the English disc only.
- The floppy's stone disk is passed because its own check accepts any wheel; which symbols the screen asks for was not simulated.
- The floppy was not looked at in the window; the talkie's look is one route.
- Whether the floppy's other data (rooms that leave earlier in the audit) changes any first-visit picture was judged from the audit's active images and gaps only.
