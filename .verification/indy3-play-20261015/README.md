# S6 Play, Indiana Jones and the Last Crusade EGA (15–16 October 2026)

Family `Indy3Ega`, fixture `D:\Juegos\Lucas\indy3 ega en` (English EGA; `TestGames.Root`). Requirement: REQUIREMENTS §7.1, R-7.7 to R-7.18 (Proposed 15 October). Reference: ScummVM `engines/scumm` master, read 15 October 2026 (`verbs.cpp` drawVerb, findVerbAtPos, verbMouseOver, checkExecVerbs; `cursor.cpp` setBuiltinCursor, animateCursor; `script.cpp` checkAndRunSentenceScript; `actor.cpp` getActorFromPos; `object.cpp` findObject). The bundled ScummVM was not run (brief).

## What was built

| Piece | Where | ScummVM / game source |
|---|---|---|
| Verb slots keep colour, highlight, dim colour, key, centring and the creating charset | `Runtime/ScummVerbs.cs` | `o5_verbOps` 3, 4, 0x10, 0x12, 0x13, `new` defaults 2/0/8 |
| Text verbs drawn with the charset, dim/highlight colours, centring; their rectangles for `findVerbAtPos` | `Rendering/ScummVerbArea.cs`, `V3/ScummV3Charset.cs` (`DrawPlainLine`) | `drawVerb` text branch |
| Verb names resolve code 5 (verb name) and code 6 (object name, inventory objects keep their room's name) | `Scripts/ScummTextDecoder.cs`, `Session/ScummSessionPlay.cs`, `Session/ScummSessionRooms.cs` | `convertVerbMessage`, `getObjOrActorName` |
| Pointer: `VAR_MOUSE_X/Y` (screen) and `VAR_VIRT_MOUSE_X/Y` (screen + xstart, − topline) every frame | `ScummSessionRooms.PlaceVirtualMouse`, `ScummSession.PointAtScreen` | `scummLoop` |
| One press = one click, queued and delivered after the frame's scripts: verb `(1, verb, mode)`, other non-room point `(1, 0, mode)`, room `(2, 0, mode)`; mode 0 in v3 (`ScummProfile.ClickInputMode`) | `ScummSessionPlay.ClickScreen`, `ScummSessionFrames` (`DeliverQueuedInput` before `RunSentenceScript`) | `checkExecVerbs` |
| Keys: verb shortcut `(1, verb, 1)`, else `(4, key, 1)`; pause key, '.', Esc, restart key and the profile's save/load key (Indy3 F5 = 319) are not delivered | `ScummSessionPlay.PressPlayKey`, `ScummProfile.UndeliveredKeys` | `checkExecVerbs`, Script 24 |
| `cursorCommand cursorOn/Off/SoftOn/SoftOff` kept as `_cursor.state`; `roomOps setScreen` kept as the screen split | executor, `ScummScriptRuntimeState.CursorState`, `MainScreen` | `o5_cursorCommand`, `initScreens` |
| `actorFromPos` (generic session with `PlaysThroughVerbs` only; LOOM unchanged) | executor + `ScummGameSession.ActorFromPosition` | `getActorFromPos` (strip and drawn top/bottom; the drawing is the session's costume renderer) |
| `ifClassOfIs` / `setClass` with a variable class (`[local[0x000]]`) | executor `TryReadClassOperands` | — (a parser bug: the list ended at the first `]`) |
| Game screen: text screen rows 0–15, room rows 16–143 from xstart, verb screen 144–199, messages, crosshair (arms ±5..11 across, ±3..10 down, colours 15, 15, 7, 8 every two frames) | `Rendering/ScummPlayScreen.cs` | `setBuiltinCursor` (labelled `engine`) |
| App: the room view of a `PlaysThroughVerbs` game is the live game screen (`PlayScreenImage`), its own frame timer, the system pointer hidden over it | `App/MainWindow.Play.cs`, two hooks in `MainWindow.xaml(.cs)` | — |

`ScummProfile.PlaysThroughVerbs` is true only for `Indy3Ega` (R-7.17: no other non-LOOM family delivers clicks).

## The acceptance route (R-7.18), headless

`tests/SCUMMexplorer.Tests/ScummIndy3PlayTests.cs`, run with `SCUMM_INDY3_EGA_TEST_ROOT=D:\Juegos\Lucas\indy3 ega en`:

```
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter FullyQualifiedName~ScummIndy3PlayTests
```

| Step | Input (screen point) | What the game's scripts did | Source |
|---|---|---|---|
| boot | – | Room 73, input script 4, sentence line "Walk to", 12 verbs on, Talk/Travel dim, screen rows 16–143 | Script 1 +0x021B setScreen 0x10, 0x90; Script 96; Script 16 |
| (1) compose a sentence | click "Pick up" (verb 0x0B), click the mallet (room 176, 112) | sentence line "Pick up mallet" | Script 4 +0x0114 (var[0x03B]), Script 20 findObject, Script 16 +0x0021 |
| (1) run it, (3) take an object | second click on the mallet | `doSentence 0x0B, 836, 0`; sentence script 2 walks Indy and runs object 836 verb 0x0B: `pickupObject`; Script 12 names verb 0x65 "mallet" | Script 4 +0x01B9, +0x025D; Room 73 object 836 verb 0x0B; executor runInventoryScript; Script 12 +0x0081 |
| the game's interruption | "Close" the door | Room 73 local 205 (started by the pick-up) waits until Indy is near the bell, says "The mallet's chained to the bell.", gives the mallet back (setOwnerOf 0x344, 0x0F) and its endCutscene stops the Close sentence (Script 23 +0x0017); the Close is clicked again | Room 73 local 205 +0x000F..+0x0065 |
| (5) skip a cutscene | "Close" the door (object 830), "Push" the switch (object 837, child of the door, findable only while the door is closed) | cutscene, lights off, `beginOverride` +0x0008; Esc → abortCutscene → skip jump to +0x005F, lights back on | Room 73 object 837 verb 0x06; ScummVM processKeyboard/abortCutscene |
| (2) leave the room | "Open" the door, then a click on it ("Walk to" runs on one click) | object 830 verb 0x0A walks out and `loadRoomWithEgo 0x1C, 0x01` → Room 1 | Script 4 +0x0123; Room 73 object 830 verb 0x0A +0x0025 |
| (4) a dialogue | "Talk" (on in Room 1: entry +0x00A4 var[0x032] = 29, Script 113), click on Marcus (actor 4) twice | Script 20 `actorFromPos` finds actor 4; sentence `0x0D, 0, 4`; Script 2 +0x0041 starts Script 29; it sets var[0x020] = 18 and offers verbs 0x78–0x7A | Script 2 +0x0041; Script 29 +0x0039, +0x01B6 |
| (4) the choice | click on verb 0x79 | Script 18: Indy says "Why Marcus, have you lost your sense of humor?" (printEgo code 5 of the verb), var[0x072] = 0x79, Marcus answers "You'll have to be the judge of that." | Script 18 +0x0068, +0x0077; Script 29 +0x025E |

All five steps pass; the app chose no verb, object, sentence or answer; no key reached area 4 on the route.

**The same route in ScummVM (described, not run):** in ScummVM the boot leaves Indy in the gym with the same verb screen; Pick up + the mallet composes "Pick up mallet"; the second click takes it and walking towards the door makes Indy say that the mallet is chained to the bell; the light switch beside the door can be used only once the door is closed (it is a child object of the closed door) and Esc ends its cutscene; Walk to the open door leads to the corridor (Room 1); Talk to Marcus opens his conversation. This is the game's own script flow as decoded above; no runtime comparison was made.

## WPF check through the real window

`.verification/indy3-play-20261015/PlayWindowCheck` (private profile through `SCUMM_EXPLORER_PROFILE_ROOT` in the output folder), run 16 October:

```
rtk proxy dotnet build .verification/indy3-play-20261015/PlayWindowCheck/Indy3PlayWindowCheck.csproj
rtk proxy <Exe>\Indy3PlayWindowCheck.exe "D:\Juegos\Lucas\indy3 ega en" .verification\indy3-play-20261015\window
```

Result (`window/play-check.txt`, captures `window/play-0*.png`): play started in Room 73 with the room view replaced by the game screen and the system pointer hidden, Normal-mode note empty; Pick up → mallet → sentence line "Pick up mallet"; second click → inventory [836], verb 0x65 "mallet" (drawn in its highlight colour under the pointer); key 'x' through the window's text-input handler → `runInputScript(1, 0x09, 1)`, sentence line "Look"; 65 frames played by the window's own timer. The clicks were given to the window's session at screen points (the call the image's mouse handler makes after mapping the pointer); the mouse mapping itself is the user's check.

## Regression

LOOM and the generic-session tests (filter `Loom|FirstVisit|ScummGameSession|MonkeyFloppy|Indy3|ScummEngineOpcode|Support|Verb`, `SCUMM_SOUND_TEST_ROOT=D:\Juegos\Lucas\loom ega mt32 en`): **886 passed, 5 skipped, 0 failed** (before the cell change); support tests 32 of 32 after it. LOOM paths are unchanged by construction: `actorFromPos` and code 5 in prints are wired only where `PlaysThroughVerbs`; verb colours/keys are kept but LOOM's image verbs ignore them; `PressKey` is unchanged.

## Differences and uncertainties (kept)

- The pointer, the clicks' timing and the keys are the player's (assumed input when driven headless).
- `getActorFromPos` uses the session's costume drawing for the actor's strips and top/bottom, not ScummVM's screen usage bits.
- Objects held in the inventory run no verb code of their own yet (`StartObjectScript` does not model held objects): "Look at" an inventory item does nothing.
- The talk text position follows the shared message table (LOOM's layout, screen top 0); with Indy3's screen top 16 a line may sit up to 16 pixels off; not compared.
- The app's control bar (Pause/Skip buttons) is not shown for Indy3 play; Space, Esc and '.' do the same from the keyboard. Right click is not delivered (R-7.16) and opens nothing in play.
- Picking another room from the list enters it from the session's state (state preview), as the first visit does; the room list does not follow a room change the game makes.
- Dark rooms are shown lit (S5 decision); the flashlight is not drawn.
- Perceived timing and audio are the user's checks (no sound is played during Indy3 play yet).
