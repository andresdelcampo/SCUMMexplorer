# Object names: one at a time, under the close-up (28 September 2026)

The user reported two problems after M1c:

- the verb screen showed an object's name without its close-up;
- names piled up when the user clicked one object after another.

## What the game does (decoded, script 008 and 007; ScummVM `o5_drawBox`)

- Script 008 runs when the chosen object changes. If an object was chosen before (`var[0x066]` ≠ 0), it:
  1. turns verb 0x36 off;
  2. runs `drawBox 232, 152, 312, 192, colour 0`, a black box over the close-up and name area of the verb screen;
  3. then, for the new object, turns verb 0x36 on at (248, 152) with the close-up image from the object's verb 0x39 (`var[0x068]`).
- Script 007 +0x001E (or script 008 +0x004E) prints the object's name with `print 0xFE`, centred at (272, 181), over the close-up's lower edge.
- `drawBox` paints the rectangle on its virtual screen, so a name drawn earlier is gone.

## What the user now sees

- A click on the leaf (object 155) shows its close-up, the red leaf, with "hoja" over its lower edge. See [room02-click-155-name-app.png](room02-click-155-name-app.png).
- A click on the sky then shows the starry close-up with "cielo" ([room02-click-156-name-app.png](room02-click-156-name-app.png)). Only one name shows at a time.
- An object with no close-up (157) clears the area.
- With the verb strip off, the verb screen still appears while it holds the game's text, and it now draws the verb slots with it: the close-up, and the distaff when it is on. The note says the slots are a presentation.

## Changes

- Core:
  - `LoomMessageTable.DrawBox` records each box on the drawn strings drawn before it (`LoomMessage.ErasedBy`). It drops a string the box surely covers, allowing 8 pixels per glyph.
  - The session now applies `drawBox` when it reaches it, instead of skipping it as a presentation operation.
  - `LoomMessageRenderer` does not draw a string's pixels inside the boxes drawn after it.
  - The box's fill is not painted over the room image. In the verb screen, colour 0 is that screen's black.
- App: the text-only verb screen draws `session.VerbArea()` under the text, instead of plain black.

## Verification

- Tests: **207/207**. The new test, `ADrawnBoxCoversTheNamesUnderIt`, checks that:
  - two names are covered by the box;
  - a string the box only partly covers keeps the box on it.
- MessageAppCheck: **0 failures** ([message-app-check.txt](message-app-check.txt)). It now clicks objects 155, 157 and 156 in turn, and after each click checks:
  - that at most one name is at (272, 181);
  - that the verb screen draws the verb slots, so the close-up is above the name.
- VerbAreaCheck: 0 failures, with the "off again" wording updated ([verb-area-check.txt](verb-area-check.txt)). ClickInputCheck: 0 failures ([click-input-check.txt](click-input-check.txt)).
- DoorwayClickCheck on Rooms 02, 03, 04, 08 and 10: 13/13, identical to the targeted run before this change ([doorway-clicks-targeted.txt](doorway-clicks-targeted.txt)).
- Not rerun:
  - the full DoorwayClickCheck (the click path is unchanged; `drawBox` only changes the message table);
  - RoomAudit (room renders do not use messages);
  - Blockers: `drawBox` no longer passes as a presentation operation, so the count of "presentation operation not applied" lines may drop.
