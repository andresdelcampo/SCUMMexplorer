# VerbAreaCheck "off again": the check was stale (29 September 2026)

## What the user sees

Nothing changes on screen. When "Show verb area" is turned off in Room 02 after a note click, the verb screen stays under the room while the game's text is there (the note letters and the name "cielo"), and the room fits the space left ([room-02-verb-area-off-again.png](room-02-verb-area-off-again.png)). That was already the app's behaviour; the check now agrees with it.

## Which side was wrong

- **The text should still be there** (ScummVM, decoded). The strings in the verb screen are drawn strings, not talk: script 007/008 print the name with `print 0xFE` and script 018 the note letters with `print 0xFE` (text slot 1, `drawString`). A drawn string has no talk delay; it stays until the room is redrawn or a `drawBox` covers it (script map, "Text slots"). So M3b's talk delay does not end it, and the note "Verb screen: the game's text there" is right. The check already passed that part.
- **The check's layout arithmetic was stale.** It failed `roomFits`: it expected the room image's 144 rows and a 17-px scroll bar under a wide room. Since the crop (29 Sep) the view shows the game's 128 rows (`ShownRoomRows`), and the pan buttons' panel (`RoomScrollButtonsPanel`, its height and margins) takes the scroll bar's place. The app's rule is `UpdateRoomViewportLayout`: scale = min(width / visible width, (height − pan panel − 36 if the verb screen shows) / (128 + 56 if it shows)). The check now computes the same: Room 02 at scale 1.657, 530.2 × 212.1 in 862.3 × 368.9.

## Evidence

- VerbAreaCheck ([verb-area-check.txt](verb-area-check.txt)): **0 failures**, twice in a row (was 1).
- No app or Core change; unit tests not affected (229/229 at task 1's commit). Other WPF checks not rerun (no code they test changed).
