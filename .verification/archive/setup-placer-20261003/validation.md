# Per-scene start setup placer (3 October 2026)

Code: `LoomSceneStateScanner` (static scan of a scene's scripts, the scripts and room entries they reach by constant call, and
`FindConstantWriters`), `LoomSceneSetupPlanner` (derives the setup), App wiring in `MainWindow.PlayKnownCutscene` and the picker note.
Tests: `LoomSceneSetupPlannerTests`, `LoomSceneSetupSceneTests` (relevant tests only).

## Method
1. Scan: the scene's own code is everything except system scripts 000-043 (cutscene hooks, drafts, distaff display, which read bit D61 for every scene).
2. Staff (bit D61): a scene whose own *global script* clears D61 presumes Bobbin holds the distaff. Unless the scene sets it first, the real
   earlier scene is found in the game's code: object verbs that `move D61 = 1` are Room 09 object 271 verb 0x38 (first in room order),
   Room 12/322, Room 38/611 (Look, scene 087), Room 45/656; entries Room 13, 26, 46, 47, 66. Room 09/271 runs unseen, then a catalog visit of the scene's room.
   Clears inside the entry/local scripts of rooms a scene passes through (Room 10 local 203 look reset, local 200 restore) are not presumptions and are ignored.
3. Reads of D61, the ego costume variable var[0x07D] and story bits >= D72 in the scene's own code keep their fresh (first-visit) value; each is listed in the picker note.
   No flag is written by the explorer; no `staff`/`place` edit is used for the derived scenes.

## Result over the 84 catalog scenes (this tool's inputs: 41 scenes carry a note; the others read none of these)
- Setup steps (real earlier scene run unseen): Room 43 object 643 Verb 0x3A, Room 43 object 650 Verb 0x38.
  Both reach their own `endCutscene` (Script 91 +0x0E83, Script 93 +0x05FB) from the derived start with no unsupported execution; the start differs by
  Bobbin holding the distaff (the fresh start does not).
- Scenes whose own code reads D61 (state kept, labelled): Room 02 object 156 Verb 0x3A, Room 09 object 270 Verb 0x3A, Room 13 object 324 Verb 0x36, Room 18 object 369 Verb 0x36, Room 18 object 371 Verb 0x36, Room 20 object 389 Verb 0x36, Room 63 object 821 Verb 0x40, Room 63 object 822 Verb 0x40.
- Scenes testing var[0x07D] (kept, labelled): Room 02 object 156 Verb 0x3A, Room 13 object 324 Verb 0x36.
- Not changed: scenes with explicit sequences (052-053, 086-087, 087-098, the lookout) keep their hand-written steps.

## Not done / open
- The ego's costume: the only own-code read is Room 10 entry (+0x0050, costume 0x11); the scan found no other costume dependency, so no costume setup is derived (not proven: a scene may branch on an actor look the scan does not name).
- Arrival through a real neighbour room and `place` are not derived: no scanned scene reads state that a visit chain or placement would change.
  The building blocks stay in Core.
- The staff pickup is an exploration action (object 271 is untouchable in the preview); the chosen writer is the first in room order, an estimate.
- The scene list is not complete for story order: scenes after Room 9's pickup in the real game but read-only for D61 keep the fresh without-distaff branch.
