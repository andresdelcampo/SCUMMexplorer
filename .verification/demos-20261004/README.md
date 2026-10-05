# LOOM demos (4 October 2026)

`Games/loom demo short ega en` (18 `.LFL` files, 16 rooms) and `Games/loom demo long ega en` (34 files, 32 rooms): LOOM's self-running previews (`LOOMDEMO.EXE`, "Demo IBM - 03/20/90" and "Demo - 02/01/90", copyright 1990). Tools: `.verification/DemoProbe` (`<folder>` summary, `index`, `texts`, `play <frames>`, `idle <frames>`), `ScriptDump`, `WindowCheck` (through `run-windowcheck.ps1`) and `DemoPreviewCheck` (this folder's `preview-short/`, `preview-long/`).

## What the demos are

- **Not a byte subset of the full game.** No room file is identical to the English release's (rooms 53 and 42 are a quarter of the size). Rooms 72 and 80 (short) and 72, 73, 74, 76, 77 and 80 (long) do not exist in the full game; the 82–84 sound-bank rooms are in the long one. The demo has its own `00.LFL` (the short one has the full game's size, 5748 bytes, but other content; the long one 6108).
- **The index is stale for what the folder lacks.** Entries that point at a missing container file, at no resource start, or at a start another kind also claims are not resources of the folder. Short demo: scripts 68 → 19, costumes 122 → 36, sounds 41 → 20; long: 68 → 20, 122 → 52, 39 → 27. `GameFolderScanner` keeps only the entries that start a resource (XOR 0xFF chain) and that read as their kind (script decodes without issues, costume loads; where a script and a costume or sound claim one start the costume or sound wins). Sounds are checked only for their start: half of the full game's sounds have no AdLib or PC speaker bundle either (33 of 64 on the English release).
- **Boot and preview.** Script 001 loads Room 01; Room 01's entry starts local 200 (verbs), script 046 (a music loop) and script 045, the **director**: it starts script 015 (the title and copyright text), then loads Rooms 03, 09, 62, 10 (short) with `loadRoomWithEgo` and waits for `var[0x0E0] = 1` between them. The rooms then chain on their own (the engine plays 1, 2, 53, 2, 3, 9, 62, 10, 16, 24, 28, 34, 35, 42, 44 and back to 1 in about 2,100 frames, about 3.5 minutes; the long one adds 5, 4, 17, 22, 20, 31, 36, 43 and takes about 3,450 frames). The existing session runs it unchanged (`DemoProbe play`).
- Script and room-local numbers are the demo's own. Script 045 there is the director, not the full game's 045; the glossary's script, bit, actor and sound names are not applied to a demo (variable and room names are kept; object names come from the demo's own data).

## What was wrong, and what changed

1. The scanner listed the full game's resources: 30 of 68 scripts failed to decode; the tree, counts and text extractor claimed the rest. Fixed (see above); both demos now decode every script, the text extractor finds 23 / 28 texts with 0 unresolved (script 027 was a sound, not a script).
2. The real window did not open a demo: `LoomStatusSchema` (the Game Status contract, read from full-game scripts 017, 010, 022–026) throws `InvalidOperationException` where script 044 and others are missing, and `LoomGlossary.Create` did not catch it. `LoomStoryEvents.FirstBitFor` now catches it, and a demo's glossary leaves script, bit, actor and sound names out.
3. A fresh demo session started the preview's director on its own (the idle clock then walked the view from room to room under the user's feet). A demo session now leaves it stopped (`LoomExplorationSession(…, demoPreview: false)`), with an uncertainty saying so, so rooms can be explored as in the full game: first-visit state, the same rules.
4. **Decided with the user:** both ways. Rooms are explorable from the picker; the self-running preview is one more cutscene in the existing launchers (the room's "Play Cutscene" button in Room 01 and the Advanced cutscene picker), not a new button. It runs on the playback clock until another room is opened (a fresh session then replaces it). A demo has no replay catalog and no Game Texts "Play this cutscene" (those name the full game's rooms and objects).

## Seen in the window (WindowCheck, DemoPreviewCheck)

- Every room opens in the real window and draws without gaps at the picker's size (`rooms-short.png`, `rooms-long.png`, from `WindowCheck`; Bobbin appears where the explorer's arrival puts him). The demo-only rooms (72, 80; long: 72–74, 76, 77, 80, 81) have no background pixels and draw black: unexamined (script-driven screens or banks; the director never enters them).
- Preview: rooms entered 1, 2, 53 (the Elder's cloak: "Rise, son of Cygna.") in the first 30 s; the lines are the full game's opening texts.
- Idle: a fresh demo stays in Room 01 (the session reports room 1 after 4.5 s).
- `WindowCheck` over all rooms of each demo passes (`window-check-short.txt`, `window-check-long.txt`). Before item 3 it failed on the short demo with "Selected Room 09 is not highlighted" after rooms 1, 2, 3: the director running in the background had moved the session.

## Not checked

Object names and verbs of the demo rooms against the full game's; costume details; sounds (music is script 046's loop, a sound in the demo's own numbering); the long demo's rooms 72–77, 80, 81 (never entered by the director); whether the demo's rooms have gaps (the thumbnails show none at picker size).
