# Choosing the language of a game that holds several (19 October 2026)

Requirement: [R-3.16](../../docs/REQUIREMENTS.md) (Rule, confirmed by the user on 19 October: "yes, we should allow choosing a language in those games that have multiple").
Before: the S9 run ([monkeycd-editions-20261018](../monkeycd-editions-20261018/README.md)) found five language folders on the Monkey Island 1 CD and read SPANISH through `GameFolderScanner.DiscFolder`, but nothing in the app set it.

## Survey: which games hold several languages

`LanguageSurvey` (this folder) opens every game folder under `D:\Juegos\Lucas` and lists, from the data only, the v5 candidates (`X.000` with `X.001` whose index parses), the top folders of each disc image and `GameFolderScanner.ListLanguages`. Run on 19 October:

| Folder | What the data holds | Languages |
|---|---|---|
| `monkey1 vga en` (`The_Secret_of_Monkey_Island.cue`) | `ENGLISH/`, `FRENCH/`, `GERMAN/`, `ITALIAN/`, `SPANISH/`, each `MONKEY.000/.001` with 100 rooms | **five**: English, French, German, Italian, Spanish |
| `indy4 vga en` (`ATLANTIS.cue/.img`) | `ATLANTIS/ATLANTIS` (99 rooms) beside the demos `INDYDEMO/FATE` (99), `DOTTDEMO` (78), `SAMDEMO` (7) | one (the demos have other file names: not languages) |
| `loom vga en`, `loom vga en kixx` (`.cue/.bin`) | 7 files at the top of the disc (`000.LFL`, `DISK01.LEC`, ...), no folders | one |
| `indy4 classic/talkie vga es`, `mi2 *` (classic es, mt32 en, sb16 en, talkie en/es) | one `NAME.000/.001` each (the talkies one `MONSTER.SOU`) | one |
| every LOOM EGA, Indy3, Monkey Island floppy, Maniac and Zak folder | one LFL or DISKnn.LEC set each (one language per folder; LOOM editions named by their hashes) | one |

So the Monkey Island 1 CD is the only local game with several languages. The talkie folders (Indy 4, MI2) are each one language; their speech files are per folder too.

## What was built

- **Core.** `ScummV5Archive.FindGames` (every candidate), `FindGameFolders` (the folders that each hold the same full game: same file names, same room count; fewer than two is no choice) and `FolderOf`. `GameFolderScanner.ListLanguages(path)` returns `GameLanguage(Key, Label)`: the key is the disc folder, the label its name in title case (the disc names the folder; nothing is guessed). `Scan` sets `GameLibrary.Languages`, `Language` (the folder read) and the detection note (`The data holds 5 languages, one folder each (...); Spanish is read (chosen).`). `GameLibrary.Key` is the folder plus the language when there are several; `GameFolderScanner.Reopen(game)` gives a fresh library of the same data set. A remembered folder the disc lacks falls back to the default and the note says so.
- **Default.** As before: ENGLISH, the folder that wins the tie of equal room counts (R-3.14), stated in the detection note.
- **App.** Under the game's name (EXPLORE column) a language list, only for a game with more than one; Normal mode, no technical text (tooltip "Play the game in another of its languages."). Choosing one stops play and reopens the game in it (`OpenGameFolder`), remembered per game folder in `game-languages.txt` in the profile (`SCUMM_EXPLORER_PROFILE_ROOT` for harnesses). Keyed by `GameLibrary.Key` (or with the language): the play session (`_playFolder`), room thumbnails, the CD room-list cache of play cutscenes, the workers' own copies of the game (`Reopen`), `ScummRoomScene` first-visit scenes and `ScummEgoArrivals`. Games with one language keep their old keys.

## Checks

- Unit tests `GameLanguageChoiceTests` (4): the MI1 CD lists the five languages and opens ENGLISH by default; SPANISH gives the Spanish texts ("Re-eliga a la Gobernadora Marley.", "cartel") and no English line; an unknown folder falls back; LOOM and the Indy 4 disc offer no choice.
- Regression: discovery/scanner/detection, MonkeyCd (session, play, cutscenes, objects), support matrix and architecture tests: 78 passed, 0 failed. LOOM (`FullyQualifiedName~Loom`): 836 passed, 0 failed, 5 skipped.
- WPF through the real window, `LanguageWindowCheck` (this folder; private profile and room cache in `window/`), all PASS (`window/language-check.txt`, captures `window/language-0*.png`, not committed):
  - MI1 CD opened: list visible, `English, French, German, Italian, Spanish`, English selected.
  - Spanish chosen in the list: game reopened in SPANISH; `game-languages.txt` holds the folder and `SPANISH`.
  - Room 38 through the tree, play runs to the dock (Room 33) on the Spanish data: verbs drawn `Dar, Abrir, Cerrar, Coger, Mirar, Hablar a, Usar, Empujar, Tirar, Caminar a`; "Mirar" on the poster gives the sentence line `Mirar cartel`.
  - LOOM opened: no list. MI1 CD opened again: Spanish remembered.

Run: `LanguageWindowCheck "<Games>\monkey1 vga en" "<Games>\loom ega mt32 en" <output>` and `LanguageSurvey [<Games>]` (rebuild first).

## User checks

1. Open the `monkey1 vga en` folder with **Open Game Folder**. Under "The Secret of Monkey Island" a list shows **English**.
2. Choose **Spanish** in it. The room cards rebuild; open a room (the dock, Room 33, or Room 38 and let it run): the verbs read *Dar, Coger, Mirar...* and pointing at the poster with *Mirar* reads *Mirar cartel*.
3. Try **French**, **German** or **Italian**: the game reopens in that language (these three have had no edition run; tell us what looks wrong).
4. Open a LOOM folder: no language list. Close the app and start it again on the Monkey Island CD: the last chosen language comes back.

## Open

- FRENCH, GERMAN and ITALIAN folders open and play, but had no boot/route/cutscene run as SPANISH had in S9.
- Another format with languages in one container (a v4 disc with language folders) would add its candidates in `GameFolderScanner.ListLanguages`; none exists locally.
