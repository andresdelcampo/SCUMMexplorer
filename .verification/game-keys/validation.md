# The game's keys in the interactive mode, and '.' to skip text (user reports F and G, 30 September 2026)

## The reports and the user's choices

- F: "Spells cannot be cast with keys." The user chose (30 Sep) that all the game's keys work in the interactive LOOM mode, as in ScummVM, with no option.
- G: "A key to skip to the next text during a cutscene." The user chose ScummVM's own talk-stop key, '.', only.

## What the user sees now

- In the interactive LOOM mode, typing q w e r t y u i or c d e f g a b C plays the distaff's notes (script 005 area 4, [decoded 28 Sep](../key-input/validation.md#script-005-area-4-decoded)), so a draft can be cast from the keyboard once the distaff is in hand and an object is aimed at. The other keys the game's input script reads work too: < and > (text speed, printed as a line), F5 (save screen, Room 70), F6 (music and effects), F7 (animation speed), Ctrl+V (version line), Escape (close-up off, or cancels a draft being played). Engine keys are still refused with a status line: F8 (restart), space outside a cutscene (pause). Each key sent says so in the status line ("Key 'e' sent to the game (assumed input)"). The inspection mode sends no keys. Typing in a text box never reaches the game.
- '.' ends the delay of the text shown (the talk line, in a room or in the verb screen), in a cutscene or not, so the next part or the next line comes on the next frame. It never reaches the scripts, as in ScummVM. With no text shown, the status line says so. A click on the text still does the same.
- The hidden "Keys as game input" check box (advanced view, off by default) is gone. The interactive mode's tooltip lists the keys.

## Evidence

- ScummVM `input.cpp` `processKeyboard`: '.' is the talk-stop key (v3 has no `VAR_TALKSTOP_KEY`, so it is always on): `_talkDelay = 0` and the key is not passed on. Other keys go to `checkExecVerbs` → `runInputScript(4, key, 1)` (see [key-input](../key-input/validation.md)).
- Decoded: script 005 +0x0211 prints the text speed with `print 0xFF` (a system talk line, colour 0x8F) after '<' or '>'. The Elders' trial's first line is script 046 +0x002F.

## Change

- `MainWindow.GameKeysActive` no longer needs the removed `GameKeysToggle`: keys go to the game in the interactive LOOM mode.
- `MainWindow_PreviewTextInput`: '.' calls the new `SkipTalk` (shared with the click on the text, `SkipTalkAt`) whenever the interactive session runs, also while a cutscene is loading a room.
- The interactive mode's tooltip lists the note keys, '.', Escape and space.

## Checks

- KeyInputCheck (real MainWindow, synthetic key events): 0 failures ([key-input-check.txt](key-input-check.txt)). Updated: no option exists and the interactive mode is on; the inspection mode sends nothing; '<' prints "(Velocidad texto 0.)" as talk and '.' ends its delay (240 → 0 jiffies) without running script 005, and the next frames end it; '.' with no text says so; the note 'e' is recorded after the aim (unchanged); new step: in the trial (script 046, played from its known-cutscene entry on the real clock), '.' ends the delay of "Parece que los Ancianos están ocupados." (255 → 0) and the text moves on within 3 s while the cutscene goes on.
- `dotnet build SCUMMexplorer.slnx`: 0 warnings. No Core change: unit tests not rerun (244/244 after task E).
- Not run: the other WPF harnesses (they send no key input; none reads the removed check box).

## Not checked

- The text-box exclusion was not exercised (the trace box cannot take focus in the harness; logged as INFO, as on 28 Sep).
- Casting a whole draft from the keyboard on a real target was not replayed; the note keys reach script 010 exactly as clicks on the notes do.
