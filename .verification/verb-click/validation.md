# Verb-area clicks as game input (28 September 2026)

## What ScummVM does (ScummVM, `verbs.cpp` `checkExecVerbs`, `findVerbAtPos`, `runInputScript`)

- A mouse click with `_userPut` > 0: `findVerbAtPos(mouse)` looks from the last verb slot down for a slot in mode 1 (on) whose `curRect` holds the point. On a verb: `runInputScript(kVerbClickArea = 1, verbid, code)`. Elsewhere: `runInputScript(zone == main ? kSceneClickArea = 2 : kVerbClickArea, 0, code)`.
- `code` is the button only for versions above 3 (`_game.version > 3 ? code : 0`), so LOOM's input script always gets 0 as its third argument. The session's room click used to send `[2, 0, 1]`; it now sends `[2, 0, 0]`. Script 005 +0x0000 sets `local[2]` = 0 first, so nothing reached changes; only the labels and trace do. (The click-input validation and the WIP entry of that task still say `[2, 0, 1]`; they record what was done then.)
- The main loop keeps the pointer's screen point in `VAR_MOUSE_X`/`Y` (v3 vars 44, 45 = `var[0x02C]`/`var[0x02D]`); script 005 reads `var[0x02C]` for verb 0x31. With the pointer in the verb screen the virtual mouse is below the room picture.

## What the session does

- `LoomExplorationSession.VerbArea()` composes the verb area from the session's verb slots, with each image's size from the room the verb took it from (the same picture the app's strip shows, so the rectangles a click tests are the drawn ones).
- `ClickVerbArea(x, y)` (screen coordinates, rows 144–199): `FindVerbAt` over those items; refused outside the verb area, while `_userPut` ≤ 0, or when `var[0x020]` is 0 (each refusal labelled). Otherwise it takes the room pointer off, sets `var[0x02C]`/`var[0x02D]` to the point (assumed input) and runs `var[0x020]`'s script with `[1, verb, 0]`, or `[1, 0, 0]` off every verb. Loads it reaches are dispatched as a room click's are. Labels: the input, the ScummVM call, a verb slot that follows an estimate, and verbs the session cannot draw (they cannot be found).
- Limits: text verbs and verbs with an unknown mode, image or position are not found; a room click does not test `findVerbAtPos` first (no LOOM verb is placed over the room picture after the boot); `VAR_MOUSE_X/Y` are set only by a verb-area click.

## App

With "Show verb area" and "Click as game input" both on, the strip takes clicks: only a point on a verb whose slot is on (findVerbAtPos) sends anything (hand cursor and a tooltip there); the note under the strip says verb clicks are assumed input; the status line says what was sent. The strip now draws `session.VerbArea()`.

## Checks

- Tests (`LoomVerbClickTests`, 3 new; 191/191 pass): after script 018 through the seam (`var[0x064]` = 2), a click on note 0x03 runs script 5 with `[1, 3, 0]`, and script 010 chains to 013 (nothing aimed at), `var[0x02C]/[0x02D]` hold the point. After a room click on Room 02 object 156 (class 1, `var[0x067]` = 156), the same click makes script 010 record note 3 in `var[0x056]`, `var[0x062]` = 1, and chain to 027. After the boot (every verb off) a click sends `[1, 0, 0]`; refusals outside the area and with user input off are labelled.
- VerbAreaCheck 15/15 (was 10): with click input off a note click sends nothing; the note mentions assumed input; an empty point sends nothing; note 0x03 reaches script 013; after `SendClickToGame(700, 40)` note 0x03 is recorded and script 027 runs. [Report](wpf-run1/VerbAreaCheck/verb-area-check.txt).
- Every other WPF check passes (ClickInputCheck 11/11 with its trace expectation now `[2, 0, 0]`, DoorwayClickCheck 75/75, Room03ActionCheck 46, PointerInputCheck 13/13, ReverseHoverCheck 22/22, HighlightCheck 19/19, MusicRowCheck 5/5, CameraPanCheck 11/11, PresentationCheck 39/39).
- RoomAudit: 0 gaps, 0 changed renders. Blockers: unchanged ([after](blockers-after.txt), 5994 over the same 136 reasons as the loadRoom-arrivals baseline): Blockers visits rooms and calls verbs directly, and sends no input.
