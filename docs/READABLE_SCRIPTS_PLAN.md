# Readable scripts, traces and assumptions (plan, 3 October 2026)

Why: Advanced mode shows raw bytecode, raw event lines and the explorer's own assumption log. The user "would not even dare to enter there": nothing explains what a script is, what a variable means or why a line matters. The reverse-engineering we already did (`docs/LOOM_SCRIPT_MAP.md`, the cutscene catalog, the story-bit names in Game Status, the verb and speaker names) should reach the screen.

Rules that still hold: technical views are Advanced-only; label every estimate; no invented meanings (a name needs evidence in the script map, the game's text or play, and says which); one subtitle, no repeated sentences; keep the raw view one click away. Do not run the whole test suite routinely; use the harnesses in `.verification` (`UiShots`, `SceneShots`, `StatusShots`) for on-screen checks.

## Order of work (each step is one commit)

1. **Glossary (Core).** `LoomGlossary`: a small data file plus a reader, with `Variable(name)`, `Bit(name)`, `Script(id)`, `Object(room, id)`, `Actor(n)`, `Room(n)`, `Sound(id)`. Every entry carries its evidence (`decoded`, `reading`, `play`). Seed it from:
   - the variable and bit tables in `docs/LOOM_SCRIPT_MAP.md` (for example `var[0x07C]` = ego size, `var[0x07D]` = saved ego costume, `bit[0xD61]` = staff held, `bit[0xD62]` ..., `var[0x031]` = video mode, `var[0x070]`...);
   - `LoomStoryEvents` (90 story bits, 27 named after catalog scenes) and the script table in the map (044 opening, 046 the trial, 086 firewood scene, 091/093/095/096 the Room 43-45 chain...);
   - `LoomSpeakerNames`, `LoomVerbNames`, `LoomDraftNames`, the room object names, the catalog titles.
   Unknown things stay as they are (`var[0x0D1]`), never guessed.
2. **Names in the existing views.** Script view header ("Script 91 · cage scene, Room 43"), tree labels, the "starts Script 28" rows in Room entry, the Assumptions list. Cheap, visible, no new structure.
3. **Readable view of a script (the main step).** `LoomScriptPresenter` turns a `LoomScriptDisassembly` into lines of plain language:
   - operand names from the glossary (`bit[0xD96]` -> `cage scene happened`), text as quotes with the speaker named, actors and objects by name, rooms as `Room 43`;
   - one phrase per opcode family (a table: `startScript` -> "start Script 92 (...)", `loadRoom`, `putActor`, `walkActorTo`, `print`, `actorOps`, `cutscene`/`endCutscene`...), unknown opcodes fall back to the raw line;
   - structure: forward jumps recovered into indented `if ... / else ...` blocks (a branch whose target is after a block and no back edge); loops and anything not recoverable stay as labelled jumps. Offsets stay in a left column (hidden with the bytecode column).
   The script view gets a Readable / Raw toggle (Readable default, remembered in `view-options.txt` like the bytecode choice). Raw remains the evidence.
   Check with golden outputs under `.verification/readable-scripts/` for scripts 91, 17, 6 and Room 02's entry, plus a unit test per opcode phrase.
4. **A trace that tells the story.** Last action as a timeline built from the session's events: "clicked hoja -> Look at -> script 5 started -> Bobbin: ... -> camera moved", with who started what. The raw trace stays below.
5. **Assumptions in plain language.** Group the list into three kinds (arrived by a shortcut, an estimate such as camera or position, an effect not drawn), one sentence each on whether it changes what is on screen. The details stay in tooltips.
6. **"Used by".** On every script and object: which scripts and verbs start it (the relationship index behind "Explore links" already has this).

## Not in scope yet
- Editing scripts, running a script from its view with arguments (the Play-as-cutscene button already covers catalog scenes).
- Verifying the draft names beyond 0x4C/0x4D (see the next-session prompt).
