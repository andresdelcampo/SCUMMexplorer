# Double-click look in the verb-based games (R-1.5, R-6.25.2) - 24 October 2026

User, 24 Oct: "Should not harm to add that. As long as the game did not have another use for double click." Rule: a double click on an object runs the game's own Look at verb through its normal sentence path (the Look at verb is clicked, then the object; the first press is the normal click), only where the game's own input leaves the double click free. Per-game data is `ScummProfile.DoubleClickLookVerb` (null = off), never a game test in the UI.

## Method

`InputScripts/` (this folder) boots a game's data source, follows the input script (`VAR_VERB_SCRIPT`, script 4 in play in every game below) through `startScript`/`chainScript` calls with a literal id (depth 3), dumps every script reached (`input-chain-*.txt`) and lists every read or write of the three engine timers `var[0x00B]..var[0x00D]` (`VAR_TMR_1..3`), which is what LOOM's script 006 uses for its double-click window (R-7.1). A free double click also needs that no script compares the clicked object with the previous click (the other way a game can give a second click a meaning).

Command: `InputScripts.dll "D:\Juegos\Lucas\<folder>" <out.txt> 3 4`.

## Findings

| Game | Input chain | Timer reads | Second click on the same target | Decision |
|---|---|---|---|---|
| Indiana Jones 3 EGA / 256 colours | script 4 (+ 9, 11, 12, 15, 16, 20, 24, 119, 202-211) | 0 | **Used**: script 4 +0x018D compares the clicked object (`local[0x005]`) with the last target (`var[0x03D]`) and a second click on it runs the sentence (R-7.10). The 256-colour script 4 is the same code with one branch more. | **off** |
| Maniac Mansion, Zak McKracken (classic and enhanced) | native version 1/2 input of the engine, not a script chain | n/a | Not recorded in this repository; ScummVM's `ScummEngine_v2::checkExecVerbs` was not read this session | **off (inconclusive)** |
| Monkey Island 1 floppy (v4) | script 4 (+ 8, 9, 11, 12, 17, 20, 23, 32, 68, 92-95, 127, 130, 131, 133, 144, 163) | 0 | No: a click composes `var[0x06B]` verb / `var[0x06C]` object 1 / `var[0x06D]` object 2 and `doSentence`s it; a ground click `walkActorTo`s the pointer; nothing is compared with the previous click | **on, Look at = 0x09** |
| Monkey Island 1 CD (v5) | script 4 (+ 8, 9, 11, 12, 20, 130) | 0 | No (same structure; the sentence variables are the same) | **on, Look at = 0x08** |
| Monkey Island 2 | script 4 (+ 8, 9, 11, 12, 16, 19, 22, 42, 165) | 0 | No (`doSentence` at +0x057C, `walkActorTo` for the ground) | **on, Look at = 0x08** |
| Indiana Jones 4 | script 4 (+ 9, 10, 13, 14, 22, 59, 77, 131, 145, 150, 197) | 0 | No (`doSentence` at +0x03A3, `walkActorTo` at +0x03D5) | **on, Look at = 0x09** |

The Look at ids are the games' own verb tables (tests `ScummMonkeyFloppyCutsceneTests` 0x09, `ScummMonkeyCdCutsceneTests` and `ScummMonkey2CutsceneTests` 0x08, `ScummIndy4CutsceneTests` 0x09); the Spanish editions use the same ids.

LOOM stays as it is (single click walks and uses, double click looks, R-1.5).

## Behaviour and test

`ScummSession.DoubleClickLook(x, y)` queues a click on the game's Look at verb (it must be on, else refused with the reason) and, two frames later, the scene click on the same point, as two real presses would (the verb click starts scripts that need a frame). The App sends it for the second press of a double click (`ClickCount == 2`) when an object of the room is under the pointer, instead of the plain second click. `ScummDoubleClickLookTests`: the profile data for every family; Indy4 New York, a double click on the marquee runs `sentence script 2 runs verb 0x09 with objects 142` after the first press's `verb 0x0A`; Indiana Jones 3 refuses.

User check: in Monkey Island 1 or 2 or Indy 4, double-click an object (for example the marquee in Indy 4 Room 10): the hero walks to it (first click) and then looks at it (its description). In Indy 3, Maniac Mansion and Zak a double click is two ordinary clicks, as before.
