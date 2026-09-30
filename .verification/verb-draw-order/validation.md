# The verb screen drawn in the game's order (user report E, 30 September 2026)

## The report

"After the distaff shows the open spell, the notes keep their shine and colour in the verb area."

## What the user sees now

After a note is played on the distaff (by the player, by a draft playing back or by a scene), the note returns to its plain picture on the verb screen once its glow animation ends, as in the game. Before, the brightest glow frame stayed on the note for good.

## Cause

Script 031 (decoded) animates a played note: with the distaff in hand it turns on ten verbs in turn at the note's place, one a frame: five glow verbs (note N: verbs 9+5(N−1)…13+5(N−1), images 101+6(N−1)…105+6(N−1) and back), and last note N's own verb with its plain image (Room 01 object 100+6(N−1)). Nothing turns the glow verbs off: after a note, six verbs are on at the same place.

ScummVM (`script_v5.cpp` `o5_verbOps`, `verbs.cpp` `drawVerb`, `restoreVerbBG`, `killVerb`, `redrawVerbs`, `scumm.cpp`): every `verbOps` ends with `drawVerb(slot, 0)`, which draws a verb that is on at once, or clears the rectangle a verb that is off was last drawn in. The whole verb list is redrawn in slot order only on a complete screen redraw (a load or restart). So the game shows the verb drawn last: the plain note.

The app composed the verb screen from the verb slots in slot order. The glow verbs are later slots than the notes, so the last glow verb (image 105+6(N−1), the brightest) covered the plain note.

## Change

- `LoomVerbTable` (Core): each `verbOps` numbers its draw (`DrawOrder` on the verb, new optional field of `LoomVerbSnapshot`); a verb turned off, created again or deleted clears the rectangle it was last drawn in (`Erases()`, pruned when no verb that is on is older). `ImageSize` (set by the session) gives the rectangle of an image verb.
- `LoomVerbArea.Compose` draws the verbs and the clears in that order (slot order within the same order, and for verbs never drawn).
- A verb drawn or cleared over a string printed on the verb screen covers it (`LoomMessageTable.VerbDrawn`, the same bookkeeping as `drawBox`). Script 027 prints the draft's note letters at y 188 and turns verb 0x35 on over them at (96, 184) when a new draft starts; script 028 turns it off, which clears them. The covered-string boxes are now kept only for strings they may touch.

What a click finds is unchanged and follows ScummVM `findVerbAtPos` (the highest slot that is on): after a note was played, a click on it finds the last glow verb, and script 005 maps it back to the note ((verb − 9) / 5 + 1, +0x008B).

## Checks

- Unit tests: 244/244 (two new in `LoomVerbAreaTests`: after script 030 [3] and 40 frames, the six verbs on at note 3 are 0x03 and 0x13–0x17, 0x03 was drawn last and the verb area shows its plain image 112 pixel for pixel; composed in slot order the same verbs show a different picture. A verb turned off clears its rectangle).
- VerbAreaCheck (real MainWindow): 0 failures ([verb-area-check.txt](verb-area-check.txt)). New step: after script 030 [3] and 40 frames, the strip shows Room 01 object 112 at x 72 (0 pixels differ); [room-02-verb-area-note-played.png](room-02-verb-area-note-played.png). The note-click step now expects verb 0x17 (the last glow verb, highest slot), which script 005 maps to note 3; it still reaches script 013.
- Not run: RoomAudit (room images unchanged), the playback and doorway harnesses (they do not look at the verb screen), CutsceneLayoutCheck.

## Not checked

- The report's own route ("the distaff shows the open spell") was not replayed; the fix applies to every note script 031 animates (draft playback, script 020; scenes through 030). The letters' colour under the notes (scripts 018, 027, 036 print them; each print covers the one before) was not compared with ScummVM. Image 101+6(N−1), the first glow frame, differs from the plain note by five white pixels.
- Not compared with the running ScummVM.
