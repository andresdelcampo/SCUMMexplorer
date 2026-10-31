# User checks

Written 30 October 2026 (roadmap E2). The checks only you can do, one short list per game, gathered from about 115 evidence records that each ended with their own "user checks". Each line links the record that has the exact steps. When you do one, write the date and the result in its last column (or tell the session); a passing listening check raises that game's Sound cell back to ✔ in `GameSupport.Families`, a failing one becomes a Gate A item in [ROADMAP](ROADMAP.md).

Game folders are under `D:\Juegos\Lucas`. Roland needs the ROMs in `mt32`. If the app crashes, the log is in `%LOCALAPPDATA%\SCUMMexplorer\crash-logs` (a forced close or a crash no handler saw is logged as `…-unclean-exit.log` at the next start).

## Decisions waiting for you (gate F)

| # | Question | Where it comes from | Answer |
|---|---|---|---|
| 1 | The Play rules for verb games, R-7.7 to R-7.18 (and R-7.19 to R-7.21 for Maniac/Zak), R-8.32 to R-8.34 (cutscene lists): confirm, or say what to change. | REQUIREMENTS §7.1, §8 (Proposed) | 31 Oct: not needed. Play is the lowest priority and not a sign-off condition (R-1.14); R-8.32 to R-8.34 give way to §8.6. |
| 2 | A picked room whose entry leaves it (MI2's campfire, Room 4): a Normal-mode hint pointing to Play Game Intro, or let the boot room play its settled boot state? | R-8.5b vs R-5.3a | |
| 3 | Restart and save/load keys (Indy 3 F5, `VAR_MAINMENU_KEY`): refused as now, or working? | NEXT_SESSION_PROMPT | 31 Oct: moot, nothing is saved or loaded (R-1.14). |
| 4 | Indy 3's cursor: ScummVM's 16 × 16 cross-hair is used; the game draws a different one. Acceptable? | R-1.5, Indy 3 tracker | |
| 5 | Indy 3: count `printEgo` lines as first texts (changes the text columns)? | CURRENT_WORK | |
| 6 | Is a script-derived cutscene list (◐) the finish line for every game except LOOM English, or should some get a hand-checked list? | ROADMAP gate D | 31 Oct: every listed scene should play when picked, with its state set up (§8.6); no hand-checked lists. |
| 7 | Indy 3 castle Rooms 20, 28, 32: guards can start a confrontation at any moment, so the catalog shows a still picture (R-8.5b). Make such rooms live anyway? | [record](../.verification/indy3-castle-20261030/README.md) | |

## LOOM

| Check | Steps | Result |
|---|---|---|
| Room 2: skip the cutscene, is Bobbin the right size afterwards? | CURRENT_WORK, known gaps | |
| Rooms 15, 18, 20: staff notes after the 11 October fix | CURRENT_WORK | |
| Each other EGA copy once in the window: Italian (accents), plain English, French, German, v1.2, both demos | CURRENT_WORK, release list | |
| LOOM CD: picture, voice and music timing in a few scenes | CURRENT_WORK | |
| LOOM CD intro: "LucasArts Entertainment Company" now shows on two lines (ScummVM's v4 wrapping rule); same as the real game? | [record](../.verification/mi2-intro-protection-20261030/README.md) | |
| First run on a clean profile; Roland with no ROM folder | CURRENT_WORK, release list | |

## Indiana Jones 3 (EGA and 256 colours)

| Check | Steps | Result |
|---|---|---|
| Listen in play: Room 73 onwards, AdLib then PC speaker | [play record](../.verification/indy3-play-20261015/README.md), [sound polish](../.verification/sound-polish-20261019/README.md) | |
| Room 73 · Turn off light switch, Room 22 · Entry, Room 80 · Entry: start as in the game? Esc and Space work? | [cutscene record](../.verification/indy3-cutscenes-20261017/README.md) | |
| Rooms 20, 28, 32: the castle's cut-away plan shows only the section around Indy (decoded, Script 25). Does the real game look the same? | [record](../.verification/indy3-castle-20261030/README.md) | |
| Rooms 6, 18, 78, 80: their pictures now come from the moment their scene leaves them (the game follows Indy into the next room). Do they look right? | [record](../.verification/indy3-rooms-20261030/README.md) | |
| The same in the 256-colour copy, and the Spanish copies' accents | [editions](../.verification/indy3-editions-20261017/README.md) | |

## Monkey Island 1 floppy

| Check | Steps | Result |
|---|---|---|
| Listen and time play from the dock (Room 33): music per room, door sounds, credits music | [play record](../.verification/monkey1-play-20261016/README.md) | |
| Room 33 · Look at poster, Room 37 · Entry, Room 19 · Entry | [cutscene record](../.verification/monkey1-cutscenes-20261017/README.md) | |
| Spanish eight-disk copy in play | [editions](../.verification/monkey1-editions-20261017/README.md) | |

## Monkey Island 1 CD

| Check | Steps | Result |
|---|---|---|
| Room 38 to the dock: CD track 22 loops, each music stops when you leave, door creak plays | [play record](../.verification/monkeycd-play-20261017/README.md) | |
| Verb colours, rubber-chicken icon (Room 29) and cursor next to ScummVM; right click on the poster | [play record](../.verification/monkeycd-play-20261017/README.md) | |
| Room 19 · Entry (spoken text, disc music), Room 37 · Entry | [cutscene record](../.verification/monkeycd-cutscenes-20261017/README.md) | |
| French, German, Italian, Spanish in the window (language picker) | [editions](../.verification/monkeycd-editions-20261019/README.md) | |

## Monkey Island 2

| Check | Steps | Result |
|---|---|---|
| **Retest (31 Oct):** a floppy copy (`mi2 mt32 en`), Play Game Intro: the protection screen passes by itself (no code wheel), you choose Monkey Island 2 or Lite, the opening plays | [record](../.verification/mi2-intro-protection-20261030/README.md) | |
| Long talk lines in LOOM CD, Monkey Island and Indy 4 now wrap as in the game (version 4 and later): any line that looks wrongly broken? | [record](../.verification/mi2-intro-protection-20261030/README.md) | |
| Roland and AdLib: Woodtick music (107) loops, pauses with Space; Largo's theme (151) at the bridge | [play record](../.verification/monkey2-play-20261017/README.md) | |
| Route next to ScummVM: sign, dialogue order, inventory icon, verb colours; right click on the sign | [play record](../.verification/monkey2-play-20261017/README.md) | |
| Room 74 · voodoo key (LeChuck on the beat?), Room 13 · Entry, Room 09 · Talk to barkeeper | [cutscene record](../.verification/monkey2-cutscenes-20261018/README.md) | |
| Spanish floppy: recipe screen passes, Spanish verbs with accents | [editions](../.verification/monkey2-editions-20261018/README.md) | |

## Indiana Jones 4

| Check | Steps | Result |
|---|---|---|
| Speech: skip the credits, Smith's scene, the ticket taker; each line spoken once, text stays until the voice ends | [play record](../.verification/indy4-play-20261017/README.md) | |
| Verb area, dialogue lines and newspaper icon next to ScummVM; statue and fire-escape route side by side | [play record](../.verification/indy4-play-20261017/README.md) | |
| Room 11 · Entry (Sophia's lecture), Room 82 · Entry; titles use the game's verbs | [cutscene record](../.verification/indy4-cutscenes-20261018/README.md) | |
| Room 4 after the boot: is the magenta line there? | [Indy 4 tracker](indy4/SUPPORT_STATUS.md) | |
| Spanish talkie and floppy in the window | [editions](../.verification/indy4-editions-20261018/README.md) | |

## Maniac Mansion and Zak McKracken

| Check | Steps | Result |
|---|---|---|
| Maniac classic: Syd, Bernard, Start, skip the intro, front door and doormat route | [play record](../.verification/maniacclassic-play-20261017/README.md) | |
| Maniac and Zak: the PCjr/Tandy sounds (never listened to) | Maniac/Zak S3 notes, README | |
| Classic Maniac Room 18 (six black rectangles), Zak classic Room 44, enhanced 13, 16, 44: black in the real game too? | [S5 checkpoint](checkpoints/20261017-S5-ManiacZak.md) | |
| Zak Rooms 3 and 33: camera position; Room 19: still picture after about 25 s | [S5 checkpoint](checkpoints/20261017-S5-ManiacZak.md), NEXT_SESSION_PROMPT | |
| Zak classic costumes with estimated colours: do they look right? | [palette checkpoint](checkpoints/20261019-Palettes-ManiacZak.md) | |
| Advanced scrolling in Maniac enhanced: is it sluggish? | NEXT_SESSION_PROMPT | |
