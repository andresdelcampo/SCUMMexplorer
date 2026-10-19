# Monkey Island 1 CD S9 Editions: the disc's FRENCH, GERMAN and ITALIAN folders (19 October 2026)

Scope: stage S9 for the family MonkeyCd, the three language folders of `Games/monkey1 vga en` (`The_Secret_of_Monkey_Island.cue`/`.bin`) that the SPANISH run of 18 October left open ([monkeycd-editions-20261018](../monkeycd-editions-20261018/README.md)), against the disc's ENGLISH folder with the same build. Each folder holds its own `MONKEY.000`/`MONKEY.001` and `MONKEY.EXE`; the 24 CD audio tracks are shared. Since the language choice ([language-choice-20261019](../language-choice-20261019/README.md), R-3.16) the window opens each of them through the list under the game's name.

Model: the SPANISH run. ScummVM was not run (brief). Build: worktree branch `worktree-agent-a6f68273f090d589e` on master (merged 19 October). Evidence files under `.verification/**` (`*.txt`, `*.bmp`, `*.png`, `*.tsv`) are not committed; `runs/` and `window/` are kept locally.

## What was run (all through RTK)

| Check | Harness / test |
|---|---|
| Boot | `.verification/monkeycd-session-20261017/BootTrace "<disc>|<FOLDER>"` (`runs/boot-*.log`); `ScummMonkeyCdSessionTests.TheDiscsOtherLanguageFoldersBootTheSameWay` (SPANISH, FRENCH, GERMAN, ITALIAN) |
| Room audit (S5) | `MonkeyCdRoomAudit "<disc>|<FOLDER>"` for ENGLISH and the three folders (`runs/audit-*/room-audit.tsv`), compared per room on outcome, active/images, gaps, actors, not followed, lit, script instructions, named, named shown |
| Play route of R-7.18, right click, disc music | `ScummMonkeyCdPlayTests.TheAcceptanceRouteFromTheDockRunsThroughTheGamesOwnInterface` and `ARightClickOnAnObjectRunsItsDefaultVerb`, theories over null (ENGLISH), SPANISH, FRENCH, GERMAN, ITALIAN with per-copy `RouteTexts` (read from a probe run of the route, then pinned) |
| Cutscenes (S7) | `ScummMonkeyCdCutsceneTests.TheDiscsOtherLanguageFoldersListTheSameScenesWithTheirOwnTexts` (four folders); `CutsceneList "<disc>|<FOLDER>" prun 8` (`runs/prun-*.txt`) |
| Accents and charsets | `ScummMonkeyCdPlayTests.EveryTextByteAboveAsciiHasAGlyphInTheCopysCharsets` (five folders; per-language letters added) |
| Window | `EditionsWindowCheck` (this folder; real `MainWindow`, private `SCUMM_EXPLORER_PROFILE_ROOT` and room cache in `window/`) |

## Folder × check × result

| Check | FRENCH | GERMAN | ITALIAN |
|---|---|---|---|
| Detection / opening | pass: v5, MonkeyCd, disc tracks attached; chosen in the window's list | pass (same) | pass (same) |
| Boot (S4b) | pass: 1601 frames (ENGLISH 1582), rooms 0, 10, 0, 0, 38, player in control, unsupported 0, no assumed input | pass: 1602 frames, same rooms, unsupported 0 | pass: 1590 frames, same rooms, unsupported 0 |
| Room audit (S5) | pass: 83 rooms, 82 without a gap, Room 99 cannot run (as ENGLISH); every per-room column equal to ENGLISH (0 rows differ) | pass (0 rows differ) | pass (0 rows differ) |
| Play route of R-7.18 (S6) | pass: same ids, positions and branches; verbs "Donne, Prends, Utilise, Ouvre, Regarde, Parle, Ferme, Pousse, Tire", walk "Marchez", "Regarde affiche", "Réélisez le Gouverneur Marley.", "Parle conseil des pirates", choices "Je veux devenir pompier." / "Je veux tous vous tuer!" / "Je veux devenir pirate.", "trois épreuves", "Bon et bien je dois y aller maintenant.", "Prends poulet" | pass: "Gebe, Nimm, Benutze, Öffne, Schau, Rede, Schließe, Drücke, Ziehe", "Gehe zu", "Schau an Plakat", "Wählen Sie Gouverneurin Marley:", "Rede mit schrecklich wichtige Piraten", "Ich will Feuerwehrmann werden." / "Ich werde euch alle töten!" / "Ich will Pirat werden.", "Drei Prüfungen", "Ich geh dann mal wieder.", "Nimm Huhn" | pass: "Dai, Prendi, Usa, Apri, Esamina, Parla, Chiudi, Premi, Tira", "Vai", "Esamina il manifesto", "Rieleggete il Governatore Marley.", "Parla i pirati dall'aspetto importante", "Voglio diventare un pompiere." / "Vi ammazzerò tutti!" / "Voglio diventare un pirata.", "tre prove", "Beh, sarà meglio che vada via adesso.", "Prendi il pollo" |
| Right click | pass: the poster's default verb 8 runs, "Réélisez le Gouverneur Marley." | pass ("Wählen Sie Gouverneurin Marley:") | pass ("Rieleggete il Governatore Marley.") |
| Music in play (disc tracks) | pass: the route's sound ids and sources are ENGLISH's (110, 108, 104, 122, 2, 101, 100, 3, 115), the music ids play disc tracks; offsets not pinned (they move with the texts, as SPANISH) | pass | pass |
| Talkie speech | – (the CD has no speech) | – | – |
| Cutscene scan (S7) | pass: 286 triggers, 160 entries, 62 scenes, every key, scene flag, script list and group equal to ENGLISH; title "Room 33 · Regarde affiche" | pass; title "Room 33 · Schau Plakat" (see differences, 2) | pass; title "Room 33 · Esamina il manifesto" |
| Fresh-start runs (`prun 8`) | 101 of 160 begin (ENGLISH 101); begun / not begun and rooms entered equal for every entry (0 lines differ once frame counts, which follow text lengths, are removed) | 101 of 160, 0 lines differ | 101 of 160, 0 lines differ |
| The three acceptance scenes | the poster's scene begins (cutscene test); Room 37's and Room 19's entries begin in `prun` as in ENGLISH | same | same |
| Accents and charsets | pass: é è ê à ç ô û written (also Ç â ë ï î ù) and drawn by charsets 1, 2, 4 and 6; charset 3 lacks à ç ô û and most others, charset 4 Ç (the font's own fact, as SPANISH) | pass: ä ö ü ß Ä Ö Ü (and é ê) drawn by 1, 2, 4, 6; charset 3 lacks all but ü | pass: à è é ì ò ù (and ê á í ó) drawn by 1, 2, 4, 6; charset 3 lacks à ì ò ù í |
| Window (one look) | pass: French chosen in the list, game reopened in FRENCH, Room 38 played to the dock; verbs "Donne … Tire", "Marchez"; sentence line "Regarde affiche" (`window/edition-01-french-play.png`) | pass: "Gebe, Öffne, Schließe … Drücke, Ziehe", "Gehe zu", "Schau an Plakat", Ö ß ü drawn (`window/edition-02-german-play.png`) | pass: "Dai … Tira", "Vai", "Esamina il manifesto" (`window/edition-03-italian-play.png`); profile remembers ITALIAN |

0xFA (a middle dot in CP437) has no glyph in charset 1 of any folder, as ENGLISH and SPANISH: data, drawn with the copy's width table.

## Differences found and how they are handled

1. **Texts and verb names** are each copy's own: `RouteTexts` per copy folder in `ScummMonkeyCdPlayTests` (never per language), and the cutscene test's verb word and poster title per folder. Italian "Parla" takes no preposition ("Parla i pirati dall'aspetto importante"), German "Rede mit schrecklich wichtige Piraten": the copies' own lines.
2. **German sentence words differ from the verb buttons** (GERMAN `Script 12` +0x0055 `stringOps loadString 0x31, " an "` for verb 8 and +0x0067 `" mit "` for verb 0x0A; ENGLISH and the others load `" "`): the sentence line reads "Schau an Plakat", the verb button "Schau" (Script 22 +0x0080). The cutscene titles use the verb's own name ("Room 33 · Schau Plakat"), as for every game; the data, not a defect. Script map row added.
3. **Charset 3 lacks most accents** in every non-English folder (and charset 4 lacks Ç in FRENCH): the font's own fact; the dialogue (1), message (2) and verb (6) charsets draw every letter the copies write.
4. Nothing else differs: boot rooms, room audit, ids, positions, branches, sound ids, cutscene keys and fresh-start outcomes are ENGLISH's. No code changed; the registry level stays.

## Tests

On this build: `MonkeyCd`, `GameLanguageChoice`, `Support`, `Architecture`, `Scanner` filters: 93 passed, 0 failed.

## User checks

1. Open `monkey1 vga en`, choose **French**, **German** and **Italian** in the list under the game's name; open Room 38 and let it run to the dock: the verbs and the sentence line are in the chosen language, with Ö, ß, ü (German) and è, ò (Italian, in the pirates' dialogue) drawn.
2. In German, "Schau" on the poster reads "Schau an Plakat"; in the cutscene list the same scene is "Schau Plakat" (the button's word).
3. Play a scene from the list in each language (the poster, Room 37's entry, Room 19's ship).

## Open

- ScummVM was not run.
