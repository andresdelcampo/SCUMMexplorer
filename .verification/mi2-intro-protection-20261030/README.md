# MI2 floppy intro: the copy protection could not be answered (30 October 2026)

| | |
|---|---|
| Item | User report, 28 October: "the copy protection blocks play of the game's initial cutscene, and the protection screen itself is not shown" ([CURRENT_WORK](../../docs/CURRENT_WORK.md), [ROADMAP](../../docs/ROADMAP.md) gate A) |
| Fixtures | `D:\Juegos\Lucas\mi2 mt32 en` (floppy, English), `mi2 classic es` (floppy, Spanish); the talkies ask no protection |
| Base | master `5e6763e3` |
| ScummVM | not run; the behaviour is read from ScummVM's source (`string.cpp`: `convertStringMessage`, `displayDialog`, `CharsetRenderer::addLinebreaks`) |

## Finding

The headless play boot answers the protection itself (labelled assumed input, `ScummProfile.BootInput`), so the catalog never showed it. **Play Game Intro** (`ScummGameSession.StartIntro`) answers nothing for the player (R-7.20), so there the player has to read the recipe and type the number from the Mix'n'Mojo wheel. Two engine gaps made that impossible:

1. **The recipe screen was blank where it matters.** Script 128 loads string 31 = `"RECIPE FOR \xFF\x07+\x00:"` (code 7 naming string 43) and string 45 = `"MIX \xFF\x07)\x00 \xFF\x07 \x00 …"` (naming strings 41, 32, 46, 33); Script 130 copies the recipe's name into string 43 (`copyString 0x2B, var[0x064]`) and fills 41 and 46 with `_`. `ScummTextDecoder.ConvertMessage` converted a fetched string again but without string support, so every string inside a string was dropped: the screen read "RECIPE FOR :" / "MIX   WITH  ." ([before](window/before-2-recipe-blank.png)). ScummVM's `convertStringMessage` passes the stored string to `convertMessageToString`, which resolves its codes.
2. **Talk lines were never wrapped.** Script 128 +0x0022 centres "PRESS ENTER TO BEGIN LAME-O COPY PROTECTION." at x 160 in charset 7, wider than 320 pixels; the window cut it to "…COPY PRE" ([before](window/before-1-prompt-cut.png)). From version 4 on, ScummVM's `displayDialog` calls `addLinebreaks` before each chunk: the last space before the line passes `maxwidth` becomes a new line; `maxwidth` = slot right − x − 1, for centred text twice the smaller of that and x (316 here).

## Change

- `ScummTextDecoder.ConvertMessage`: code 7 converts the stored string with string support, depth-bounded (8) so a string that names itself cannot loop.
- `ScummMessageTable.AddLineBreaks`, called by `DisplayDialog` before each chunk; enabled by `ScummSession` for engine version 4 and later through `LineBreakCharset` (the session's `PlayCharset`). Versions 1 to 3 are unchanged. Read against ScummVM master on 30 October (`string.cpp` `displayDialog`: `if (_game.version > 3)`, `maxWidth = _charset->_right - _string[0].xpos - 1`, centred `min(maxWidth, _nextLeft) * 2`; `charset.cpp` `addLinebreaks`: codes 10, 21, 12, 13 skip two bytes, 14 changes the charset, 1 resets the width, 2 and 3 stop). Not modelled: code 8 turning the following spaces into '@'; `_nextLeft` of a kept text (the slot's x is used).

## Checks

| Check | Result |
|---|---|
| `IntroWindowCheck mi2 mt32 en` (real `MainWindow`, private profile; Play Game Intro, then Enter as a key event, the game's own answer typed as text input, a click on the full-game box) | prompt on two lines ([1](window/1-intro-waiting.png)); "RECIPE FOR GOUT: / MIX __ GRAVE DIGGER'S SOCKS / WITH __ WADS OF PRE-CHEWED GUM." ([2](window/2-after-enter.png)); answer 1412 accepted, the choice screen ([3](window/3-after-answer.png)); the opening plays in Room 87 to "Well, it all started on Scabb Island." ([4](window/4-after-choice.png)); log `window/report.txt` |
| `ScummMonkey2IntroTests` (new) | nested string unit test; floppy English and Spanish: the prompt wraps within 316 pixels, the recipe names its disease and ingredients, the typed answer reaches the choice |
| Full suite | 1827 passed, 0 failed, 5 skipped. Three LOOM CD tests compared the credit "LucasArts Entertainment Company" (charset 4, centred at 160) as one line; it is wider than 316 pixels with the CD's charset widths, so ScummVM's rule wraps it too: the tests now compare the words regardless of line breaks (`LoomVgaTests`, `LoomCdCutsceneTests`). |

`IntroProbe/` traces the explicit intro headless (room, input, pending scripts, texts) for any game.

## Open

- The player still needs the code wheel; the app does not show the answer (it would be a shortcut, R-1.12). Advanced could show it as the game's own value; not done.
- Line wrapping now applies to every version 4 and 5 game (LOOM CD, Monkey Island 1 floppy and CD, MI2, Indy 4). Long talk lines there were cut before; the user's look at a few long lines is in [USER_CHECKS](../../docs/USER_CHECKS.md).
