# Indiana Jones and the Fate of Atlantis S6: play through the game's own interface, with its spoken lines

| | |
|---|---|
| Family / stage | Indy4 / S6 |
| Fixture and edition | `Games/indy4 vga en` (`ATLANTIS.cue`/`.img`, English talkie CD, SCUMM v5; speech bank `ATLANTIS/MONSTER.SOU` on the image, 7029 samples) |
| Date and author | 17–18 October 2026, S6 Indy4 agent |
| Requirements | REQUIREMENTS §7.1 R-7.7 to R-7.18 (R-7.16 confirmed), R-1.13, R-15.7 |
| Registry change | Indy4 Objects ◐ → ✔ and Play – → ✔ (`GameSupport.Families`; README and REQUIREMENTS tables regenerated); subtitle Browsed → Explore |

## What was done

- **The route of R-7.18** (`tests/SCUMMexplorer.Tests/ScummIndy4PlayTests.cs`, `TheAcceptanceRouteFromBarnettCollegeRunsThroughTheGamesOwnInterface`). The play boot (`ScummGameSession.BootForPlay`) ends in Room 4 (Barnett College) after Marcus' line, with Script 78 as the input script (Room 4 local 206 +0x019D) and the whole screen as the room (Room 4 entry +0x0032 `setScreen 0, 200`): the game's playable credits, with no verbs. Every step is a click (mode 1) or Esc:
  1. **Leave a room through the prologue.** The pointer over the peculiar statue (object 63): Script 79 prints the object's name at the pointer. The click runs Script 78 (`runInputScript(2, 0, 1)`), which walks Indy to the object and runs its verb 0x0A (+0x0061). Room 4 local 200's cutscene drops Indy into Room 5 (frame 239).
  2. **Skip.** Esc takes the override of Room 5 local 214 (+0x000D `beginOverride`, skip jump to +0x001D): the rest of the credits is skipped, Script 4 becomes the input script (+0x008B), and the game goes on through Room 1 (Smith's scene, which plays) to New York, Room 10 (frame 572). Script 19 (`local[0]` 2) restores the full interface: nine text verbs, the sentence line (verb 0x64), the inventory slots 0x65 to 0x6E and arrows 0x6F/0x70, room screen rows 0 to 143.
  3. **Compose and run a sentence.** "Look at" (verb 9) gives "Look at"; the pointer on the marquee (object 142) gives "Look at marquee"; the click makes sentence Script 2 run verb 0x09 on 142, and Indy reads the marquee.
  4. **Take an object.** "Pick up" (0x0B) on the newspaper (141): it reaches the inventory, and the second slot (verb 0x66) gets its icon from Room 98 (object 1358). The first slot holds object 23, the magazine from Smith's scene.
  5. **Hold a dialogue.** "Talk to" (0x0C) on the ticket taker (143) chains to Room 10 local 208. It loads the lines as strings 0x29 up, each with its speech codes (+0x0113 up), and turns on verbs 0x78 to 0x7B, whose names are a bullet and code 7 of a string. Script 17 becomes the input script. The lines are "But--", "Now wait--", "Hold on--" and "Um--", drawn from row 152 down. The route picks 0x78. Script 17 +0x00D9 prints the verb's name through code 5, so Indy says "But--" with its spoken line (sample 0x040B3E4B, 13 jiffies). The ticket taker answers "No seats, no standing room, no exceptions.", and Script 4 is the input script again.
  6. **Leave the room and skip a cutscene.**
     - "Push" (6) on the crates, in the order 145, 137, 139. The push of 139 (verb 6 +0x0118) clears the fire escape's class 6.
     - "Use" (8) on the fire escape (138) chains to Room 10 local 207, a cutscene with an override.
     - Esc skips the climb, and `loadRoomWithEgo` (+0x008A) enters Room 12, backstage (frame 1012).
- **Speech (the talkie).**
  - A text's code 10 (four `0xFF 0x0A` pairs: offset bytes 0, 1, 4, 5; VCTL size bytes 8, 9, 12, 13; ScummVM `handleNextCharsetCode`) starts a spoken line. `ScummMessageTable.SpeechEvents` records it, and `stopTalk` records a stop of a line still speaking.
  - From the end of the boot, a message waits for its line: ScummVM ends it when the sample has finished and the text delay is 0 (`processSfxQueues`, `displayDialog`). The line's length comes from its voice file (samples over rate), not an estimate.
  - The window's `TalkieSpeechPlayer` plays each new line from `MONSTER.SOU` at the frame its text appears. It stops the line when the game does, and pauses with play. Lines from the boot and from a room visited through the list are not spoken.
- **Engine changes (shared, small, each from ScummVM):**
  - Prints convert code 7 strings (version 5) and the code 7 inside a code 5 verb name.
  - `loadString` stores its literal also on a seeded-pick path. Room 10 local 208 fills the dialogue strings after a `getRandomNr`.
  - `DrawPlainLine` skips the two operand bytes of codes 9, 10, 12, 13 and 14, so a dialogue line's speech codes draw nothing.
  - A deferred `actorFollowCamera` does not enter a room the session is already in (`setCameraFollows` starts a scene only for an actor elsewhere). Script 21, run nested from Room 4 local 200's `endCutscene`, entered Room 5. The caller's own follow at +0x0093 then entered it a second time and re-ran Rooms 5's cutscenes, which left user input off.
  - `PlaysThroughVerbs` is true for Indy4.
- Harness changes: `Indy4BootTrace dump objs:<room>` lists a room's objects with names, places and verbs. `PlayWindowCheck/` is the real-window check, copied from the MonkeyCd record.

## Commands run (through RTK)

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx
& '...rtk.exe' proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~Indy4|FullyQualifiedName~Monkey2"
# with SCUMM_INDY3_EGA_TEST_ROOT, SCUMM_SOUND_TEST_ROOT, SCUMM_MT32_ROM_ROOT set:
& '...rtk.exe' proxy dotnet test ... --filter "...~Loom|...~Monkey|...~Indy|...~Scumm|...~Maniac|...~Zak|...~Play|...~Support|...~Architecture|...~Cutscene|...~Text"
# SCUMM_UPDATE_SUPPORT_MATRIX=1 for the matrix, then the support tests again without it
& '...rtk.exe' proxy dotnet build .verification/indy4-play-20261017/PlayWindowCheck/Indy4PlayWindowCheck.csproj
Exe\Indy4PlayWindowCheck\Debug\net10.0-windows\Indy4PlayWindowCheck.exe "D:\Juegos\Lucas\indy4 vga en" <scratch folder>   # private SCUMM_EXPLORER_PROFILE_ROOT inside it
```

## Results (numbers)

- `ScummIndy4PlayTests`: 3 of 3 pass (route; main-menu key and a verb's key; a line lasts as its sample). Indy4 and Monkey2 filter: 22 of 22.
- Regression filter before the last master merge: **1222 passed, 5 skipped (LOOM dumps), 0 failed**. After merging master `b1174cd`: **1289 passed, 5 skipped, 0 failed**. After merging master `8fa733e` (S6 Monkey2), with the iMUSE filter added: **1298 passed, 5 skipped, 0 failed**.
- Route frames: Room 5 at 239, New York at 572, newspaper taken at 670, dialogue choices at 743, climb at 1011, Room 12 at 1012.
- Spoken lines measured from the samples: Marcus 0x0507BB6E 215 jiffies; "But--" 0x040B3E4B 13 jiffies; the boot and the route start 16 lines up to the dialogue.
- The real window (`window/play-check.txt`, `window/play-0*.png`):
  - Play started in Room 4. The system pointer is hidden, and Normal mode shows no note and no tooltip.
  - The prologue names "peculiar statue" at the pointer.
  - Esc through `PlayKeyDown` takes Room 4 local 221's override. That is the boot room's own skip; the test's skip is from Room 5.
  - Room 10 came after 340 window frames, with nine verbs drawn and the sentence line "Look at marquee".
  - The four dialogue lines are drawn with the bullet and their back colour (`play-03-dialogue.png`). Indy says "But--".
  - The speech player started 15 lines in the window, all from MONSTER.SOU.

## Comparison with ScummVM (source files and date; runtime comparison if any)

ScummVM `engines/scumm` master, read 17–18 October 2026 (raw GitHub):

- `string.cpp`: `handleNextCharsetCode` case 10, `displayDialog`'s `_haveMsg == 1` and `DIGI_SND_MODE_TALKIE`.
- `sound.cpp`: `processSfxQueues`, which calls `stopTalk` when `finished && (!subtitles || _talkDelay == 0)`.
- `camera.cpp`: `setCameraFollows`, which calls `startScene` only if the actor is not in the current room.
- `script.cpp`: `killScriptsAndResources`, which kills room, object and local slots.
- `input.cpp`, `verbs.cpp`, `script_v5.cpp` and `cursor.cpp`: the S6 MonkeyCd reading applies unchanged.

The bundled ScummVM was not run (brief). The same route side by side in ScummVM is a user check.

## Differences and uncertainties kept

- **Script 21 in ScummVM kills its caller.** `killScriptsAndResources` kills the local and room scripts when Script 21's `startScene` runs inside the nested call, and Room 4 local 200 is one of them. The session instead stops the caller at its own follow. A first attempt that stopped the caller at once changed Monkey Island 2's pinned boot: Room 5 local 202's `cursorOn`/`userInputOn` after its `endCutscene` no longer ran, and the boot hit the 5000-frame limit. The narrower rule was kept, and the difference is left to the engine/Monkey2 owner.
- **Speech timing applies after the boot only.** The headless boot keeps the text's own timing, so the S4b frame counts of Indy4 and Monkey Island 2 (talkie) stay as pinned. With speech timing the MI2 talkie boot exceeds 5000 frames.
- **Mouth sync is not modelled.** That is the VCTL words and the talk frames during speech.
- **The first inventory slot (object 23, the magazine) draws no icon.** Script 10 gave it image object 0. It was taken during Smith's cutscene in Room 1; not investigated. *Fixed 19 October: see the addendum below.*
- **Shared play additions from S6 Monkey2** (current-state object images, iMUSE music with loops and jump hooks, text verbs skipping code operands) were merged from master `8fa733e` and are used as they are. The route and the window check were re-run on them. The window check does not log music; listening is a user check.
- **Not covered.** The prologue's other rooms, the Biff conversation's way in, and the fist fight.
- **Timing.** Perceived timing and audio-visual sync are the user's check (R-2.4).

## User checks (exact steps and where; none if none)

1. **Speech and timing:**
   - Open `D:\Juegos\Lucas\indy4 vga en` and select Room 4.
   - Play starts in the credits sequence. Press Esc to skip it, and listen to Smith's scene and "Well, here I am in New York…".
   - In New York, "Talk to" the ticket taker, then pick "But--". Each line should be spoken once, and the text should stay until the voice ends.
2. **Look:** compare the verb area, the dialogue lines and the newspaper's inventory icon with ScummVM at the same places.
3. **Route side by side:**
   - In ScummVM, at the start, click the peculiar statue, then press Esc in the next room.
   - In New York, push the crates and use the fire escape, then press Esc during the climb.
   - Compare with the explorer.

## Addendum, 19 October 2026: the credits' headings and the magazine's icon

Two play leftovers fixed in shared code (ScummVM `engines/scumm` read on GitHub master on 19 October 2026: `string.cpp` `drawString`/`printString`, `gfx.cpp` `Gdi::drawBitmap`/`restoreCharsetBg`, `script_v5.cpp` `decodeParseString`).

- **Credits drawn on top of one another.** Cause: each heading (Room 4 locals 208 to 211 +0x0008, Room 5 likewise) is `print 0xFE` at (160, 150), centred. `drawString` draws it with `printChar(c, true)` straight into the room's front buffer, so it is not cleared by the next message; the game erases it by drawing an object over it: Script 94 +0x0063 `drawObject var[0x134]` (object 73, Room 4 entry +0x0051) after the heading's last name. `Gdi::drawBitmap` copies every strip it draws from the back buffer to the front (`copy8Col`), which removes the heading's pixels. The session ignored object draws for drawn strings, so all headings stayed. Fix: `ScummMessageTable.ObjectDrawn` covers drawn strings under a drawn object's strips (8-pixel columns, rows of the image, in screen coordinates from the session's `RoomOnScreen`), as `drawBox` already did; the executor's `drawObject` calls it (one line, in the globals-tracking agent's file). Position, centring and the overhead flag were right; v4 and later's `print` subop 6 (`SO_LEFT`: centring and overhead off) is now applied instead of being reported as not modelled.
- **The magazine (object 23) has no icon.** Cause: Script 10 +0x0131 runs each held object's verb 0x5B (`startObject`) for its icon in `var[0x0A1]`. The magazine was taken in Room 1 (Room 1 local 205 +0x012F `pickupObject 23, 0`), and in New York the session's `startObject` found no object 23 in the room and ran nothing (held objects' code was modelled for v1/v2 only), so the slot got image object 0. In ScummVM an inventory object's code is the copy `addObjectToInventory` takes from its room (`whereIsObject` WIO_INVENTORY), as the session's `getVerbEntrypoint` already modelled. Fix: `ScummSession.StartObjectScript` runs a held object that is not in the room from the room it was taken from (`InventoryRooms`). The slot now shows image object 1260 of Room 98.

Tests (`ScummIndy4PlayTests`, English talkie and Spanish floppy): `EachCreditHeadingIsErasedByTheObjectDrawnBeforeTheNext` (four headings in the talkie's boot, two in the floppy's, none left), `AnObjectDrawnOverADrawnStringErasesIt` (no game data), `TheMagazineTakenInSmithsSceneHasItsIconInNewYork`. Regression: every `*PlayTests` class, LOOM messages and display operations (112 passed); every LOOM test (837 passed, 5 skips); `CutsceneList` before (master `b1f231c5`) and after for LOOM EGA and CD, Indy3 EGA and VGA, Monkey Island 1 floppy and CD, Monkey Island 2 talkie and Indy 4: equal line for line apart from timings (Indy 4 re-run against master `7417ce43` after merging the sound-polish and talkie-speech work: 431 lines, equal; play, Indy 4, message and architecture tests after that merge: 134 passed).

Window (`PlayWindowCheck`, private profile, `indy4 vga en`; captures in `window-20261019/`, not committed): with `credits` the statue's click leads to Room 5, where one heading ("background art by:") stays under the changing names and is gone after the group; the route run shows the magazine's icon in the first slot (`inventory [23]; slot 0x65: image object 1260 of Room 98, drawn True`).

User check: play from Room 4, click the statue and watch the credits in Room 5 (one heading at a time); after Esc, the first inventory slot in New York shows the magazine. Compare with ScummVM.

## Where the facts went

[Script map](../../docs/indy4/INDY4_SCRIPT_MAP.md) (input chain, verbs, objects, rooms), [tracker and decision log](../../docs/archive/indy4/SUPPORT_STATUS.md), [engine notes](../../docs/engine/SCUMM_SCRIPT_MAP.md) (talkie speech, v5), CHANGELOG 18 October.
