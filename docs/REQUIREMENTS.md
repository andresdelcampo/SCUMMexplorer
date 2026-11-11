# SCUMMexplorer requirements

What the app must do: the rules the user decided, one or two sentences each. Ids are the ones the code and the records cite. The full wording of every row (exact captions and tooltips, implementation names, dates, the rows that describe the code as it is, and the Play proposals) is frozen in [the detailed reference of 10 November 2026](archive/REQUIREMENTS_DETAIL_2026-11-10.md); when the two differ, this file wins. A new rule is added here, short, with the next free id of its section. What is open is in [ROADMAP](ROADMAP.md).

**Scope tags** say how widely a rule holds, and so where its code goes (the lowest layer that is true for it): `SCUMM` any game (`Core/Scumm`), `SCUMM vN` one engine version (`Core/Scumm/VN`), `LOOM` every LOOM copy (`Core/Loom`), `LOOM EGA` / `LOOM CD` / `LOOM EGA ref` one edition, `APP` the window. The tag belongs to the rule, not to today's code: a rule for every game stays `SCUMM` even while only LOOM meets it.

## 1. The product

| ID | Tag | Rule |
|---|---|---|
| R-1.1 | APP | An audio-visual, interactive **explorer**, not a technical SCUMM viewer: walk through rooms, trigger cutscenes, change objects, find secrets. |
| R-1.14 | APP | **Scope** (31 Oct). The user picks any room and triggers its interactions and cutscenes at any time, without playing to them; teleports and magic costume changes are fine. Priority: (1) every room opens in its first-visit state, no gaps; (2) every object's interactions can be triggered; (3) every listed cutscene plays when picked; (4) picture and sound match the game. Playing through the game's own interface comes last: no new Play features. No session save or load. |
| R-1.13 | APP | **Normal and Advanced.** Normal mode is playful: the game's art, text, sound and the controls to play with them. It never shows status, confidence or evidence, and never explains why something is absent: content that is not ready is simply not offered. Advanced is for the researcher: evidence, bytecode, traces, assumptions, coverage. A Normal-mode note needs the user's approval. |
| R-1.4 | APP | **Rooms show no gaps and their first-visit state.** Where the data does not decide, pick at random and label the pick. |
| R-1.5 | APP | **Objects respond as in the game, with the hero on screen.** LOOM: one click walks Bobbin to the object and uses it, a double click also looks at it. A verb game is played through its own verbs. Cycling an object's stored images belongs to the inspection mode only. No hidden modifier gestures. An object may be hidden only if the background behind it has no gap. |
| R-1.6 | APP | Room hotspots navigate between rooms. |
| R-1.7 | APP | Rooms begin in their earliest state; the session carries the changes of real scenes and of the user's Game Status edits. A scene adds only what it needs and keeps later state. Restart returns to the boot. |
| R-1.8 | APP | **Game Status is an admin editor under the user's control**: it may grant items, abilities and story state without earning them. |
| R-1.9 | SCUMM | **No room-specific engine or renderer hacks; no invented state in a first visit or in Play.** Exception (31 Oct): a scene or interaction the user picks may get what its own code tests (actors placed and dressed, items, flags, object states), derived generically from its scripts, only on the pick, listed in Advanced. |
| R-1.11 | APP | **Estimates never change gameplay.** An assumption (a click point, a random pick) is labelled and does not alter state; a picked scene's setup is the one exception and labels the values it had to choose. |
| R-1.2 | APP | Game data is never shipped or committed. |
| R-1.12 | APP | Build only what adds real value beyond cutscenes and inspections. |

## 2. Evidence

| ID | Tag | Rule |
|---|---|---|
| R-2.1 | SCUMM | Every exit, action, state change, cue, scene or secret is tied to the resource, script and instruction that support it. |
| R-2.2 | SCUMM | A decoded branch or room load is a **possible** outcome; calling a verb directly is an **exploration action**; a partial session is a **state preview**; estimates are labelled **estimate**. |
| R-2.3 | SCUMM | A name carries its evidence level (`decoded`, `engine`, `reading`, `play`); what has no name keeps its raw form (`var[0x0D1]`), never a guess. |
| R-2.4 | APP | Running the source, a prepared check and watching the original game are different evidence. A scene completing does not prove its actors, prerequisites or timing. |
| R-2.5 | SCUMM | Names judged unreliable stay out (sounds are `Sound NN`); uncertain names stay labelled uncertain. |

## 3. Games and editions

The matrix is generated from `GameSupport.Families` (`Core/Domain/GameSupport.cs`); edit the registry, not the table (`SupportMatrixTests`; `SCUMM_UPDATE_SUPPORT_MATRIX=1` rewrites it). A level is raised only when the feature works for that family and its conformance check passes. How ready each game is, in percent, is in [ROADMAP](ROADMAP.md).

<!-- support-matrix:start -->
| Game | Rooms | Objects | Walk boxes | Costumes | Sound | Texts | Scripts | Play | Cutscenes | Status |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| **LOOM** EGA English (reference) | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| **LOOM** EGA other editions | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ✔ |
| **LOOM** demos | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ✔ |
| **LOOM** CD | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ✔ |
| **Indiana Jones 3** EGA | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ◐ |
| **Indiana Jones 3** 256 colours | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ◐ |
| **Monkey Island 1** floppy | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | – |
| **Monkey Island 1** CD | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | – |
| **Monkey Island 2** | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | – |
| **Indiana Jones 4** | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | – |
| **Maniac Mansion** enhanced | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ✔ |
| **Maniac Mansion** classic | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ✔ |
| **Zak McKracken** enhanced | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ✔ |
| **Zak McKracken** classic | ✔ | ✔ | ✔ | ◐ | ✔ | ✔ | ✔ | ✔ | ◐ | ✔ |
<!-- support-matrix:end -->

✔ works (Full), ◐ partly, – not supported.

| Column | ◐ Partial | ✔ Full | Checked by |
|---|---|---|---|
| Rooms | – | every room picture decodes and is offered; LOOM also shows the first-visit state (R-1.4) | rooms offered, picture decodes |
| Objects | stored object boxes and names from the room data (no script run) | first-visit state from the entry scripts, and click responses through the game's own scripts (R-1.5) | LOOM tests; per family a room audit and an S6 route test (`Scumm<Family>PlayTests`) |
| Walk boxes | – | read for the room and drawn in Advanced | – |
| Costumes | drawn, but in an estimated palette (Zak McKracken classic: 11 of 35 actor colours depend on what ran and stay labelled estimates; Maniac Mansion classic is derived from the scripts and counts as full) | drawn in the correct palette | costumes offered |
| Sound | some devices or kinds not heard or not played (e.g. PCjr/Tandy not listened to, MT-32 needs the user's ROMs) | every kind plays on every device the game has, as listened to by the user; the v5 hooks that wait for a script id (S6) are a known limit and do not hold the cell back (decision of 14 October) | sounds offered; ✔ only after the user's listening check ([ROADMAP (Your checks)](ROADMAP.md)) |
| Texts | – | Game Texts extracts spoken lines, names and labels | extractor finds entries |
| Scripts | – | raw disassembly in the family's dialect, and the Readable view | scripts offered |
| Play | – | live room: actors, camera, clicks run the game's scripts | LOOM; every other family through its own verbs (§7.1), the R-7.18 route per family |
| Cutscenes | list derived from the scripts, not hand-validated | hand-validated catalog (English Roland LOOM only) | – |
| Status | View/limited edits, incomplete item catalog or missing required native artwork | Admin Game Status editor (§12): LOOM fields; inventory catalog grants and recipient selection for inventory games | LOOM status tests; inventory catalog/capacity/edit/undo and window checks, including native artwork |

| ID | Tag | Rule |
|---|---|---|
| R-3.1 | SCUMM | A game is identified from its bytes (known hashes, then the files its editions share, then what its index names), never from executables, file or folder names. |
| R-3.6, R-15.6, R-16.1 | SCUMM | **Layering.** `Core/Scumm` cannot see `Core/Loom`. One place maps a game to engine variants (`ScummProfile.For`); code that needs scripts, objects or costumes takes `IScummDataSource`. Shared code never tests the game, a room number or a script number. |
| R-14.2 | SCUMM | **Editions differ in data, not only text.** Never key shared code on a byte offset or let a language decide numbering; derive bits, ids and offsets from each edition's own scripts. |
| R-14.1, R-8.19 | LOOM EGA | The English Roland copy is the reference for tests, harnesses and the replay catalog; the catalog is not rebuilt for other editions. |
| R-14.4 | APP | For a change, check only the edition differences it could plausibly touch. |
| R-3.15 | SCUMM | A game on several disks runs as if every disk were inserted (a labelled assumption). |
| R-3.16 | SCUMM, APP | A game whose data holds several languages offers the choice, detected from the data and remembered per folder. |
| R-15.5, R-15.7, R-15.8 | SCUMM | Every game but LOOM runs in the generic session, booted by its own script 1 until the player has control. A boot that waits for the player (a key, a copy-protection answer the script itself holds) is answered through the game's input script as labelled assumed input. |
| R-3.5, R-13.12 | APP | **Opening another game starts clean**: playback, sound, speech and every piece of the previous game's state end; late background work must not publish into the new game. |

## 4. Rooms and the room selector

| ID | Tag | Rule |
|---|---|---|
| R-5.1 to R-5.3b | SCUMM | Room cards show the plain background first and keep it; first-visit states are prepared in the background and cached (by game, build and language). Only LOOM EGA fills its cards with first-visit detail. Loading never blocks the window. |
| R-5.3a, R-6.12a | SCUMM | A picked room is entered without playing the route to it, from the state the boot left, with the hero brought in as the game's own code would bring him (a labelled pick), in his usual outfit. Every game that has a first-visit state shows it. |
| R-8.5a | SCUMM, APP | Picking a room cancels the scene that was playing, its queued continuation and its audio; it never resumes later. |
| R-8.5b | SCUMM, APP | A room picked from the selector is prepared in its entry state without playing its entry cutscene; a real doorway walk plays it. No opcode is suppressed and no flag invented to do so. |
| R-6.18 | SCUMM, APP | When the game itself changes room, the selector, the controls and the Advanced panel follow, without re-entering the room. |
| R-6.52 | SCUMM, APP | A room that cannot be made live says why; a Maniac Mansion room picked before the kids are chosen uses an assumed party. |

## 5. Room controls

| ID | Tag | Rule |
|---|---|---|
| R-16.9, R-6.30, R-6.21 | SCUMM, APP | **One shared set of room controls with one behaviour; only their content varies by game.** LOOM is the reference for how each control looks, is worded and behaves. A game supplies data through `IRoomControlProvider`; it never gets its own control. |
| R-6.31 | SCUMM, APP | One control bar under the scene. A control that cannot act now is disabled; a control with nothing to offer is hidden. |
| R-6.32 to R-6.35 | SCUMM, APP | Pause/Play is for cutscenes (background animation runs on). Step is Advanced only, one frame while paused. Skip (Esc) works once the cutscene has set its skip point. Skip line (`.`) is enabled only while a line shows. |
| R-6.45 | SCUMM, APP | Space, Esc, `.`, F11 and Left/Right are the app's in every game; the game's own keys reach it only while Interactive is on. |
| R-6.41, R-6.42, R-6.19 | APP | Options: Original framing; Hotspots and Doorways, each Never / On hover / Always (Never by default), in both modes and every game; depth areas and walk boxes in Advanced. Overlays follow the live scene's objects, camera and state. |
| R-6.13b | SCUMM, APP | **A doorway** is an object whose walk-to code can load a room. Every other object with a box is a hotspot. |
| R-6.48 | SCUMM, APP | Hovering stays responsive: bounded work per pointer move. |
| R-6.49, R-6.50 | SCUMM, APP | The room view (framing, scrolling, full screen), Interactive, Restart game state and Connections are LOOM's in every game. |
| R-6.43, R-6.20, R-6.51 | SCUMM, APP | The Advanced right panel is LOOM's in every game: doorways, hotspots, the room's scripts and object interactions as links to the script page, with game-neutral wording. |

## 6. Interact

| ID | Tag | Rule |
|---|---|---|
| R-6.39, R-8.38 | SCUMM, APP | **Interact offers every object that belongs to the room, present or not yet, with every verb that has code**, named by the game's own verb word, in both modes, as an exploration action. Picking one brings the object and the hero to it and runs the verb. It does not replace the game's own interface. |
| R-7.2 | SCUMM | Walk, use and look go through the game's scripts; a static guess is never shown as a certain exit. |
| R-7.4 | LOOM | Drafts can be spun with keyboard and mouse in EGA and CD. |
| R-7.16 | SCUMM | The right button goes to the game only where the game's own interface reads it. |

## 7. Cutscenes

| ID | Tag | Rule |
|---|---|---|
| R-8.10 | SCUMM | **What a cutscene is** (9 Nov): anything that changes room, even if it comes back, or that animates objects or characters; not lines of text with nothing else changing. One rule for every game. |
| R-8.35 | SCUMM | **Everything is offered; the game's rules never forbid a pick.** Every scene and interaction of a room is offered in both modes, including those that need earlier story. |
| R-8.36, R-8.37, R-8.39 | SCUMM | A picked scene's setup may set what the scene's code tests: bits, variables, object states and owners, items, actors placed and dressed. It never lowers existing state and runs only on the pick. Normal mode shows the scene and nothing about the setup; Advanced lists each value with the instruction that tests it. |
| R-8.5 | APP | A single LOOM scene keeps the running story; explicit sequences and the intro start fresh. In the games played through their verbs, a scene picked from a list begins from the boot. |
| R-8.2a, R-6.36 | SCUMM, APP | Each room has a Play Cutscene list in both modes, by room membership (not only where the scene starts), titled in game text and the game's verb names. |
| R-8.2b | SCUMM, APP | The trigger is in Interact and the scene in Play Cutscene: both are kept. |
| R-8.2c, R-6.37 | APP | Advanced also has the whole game's scenes: the picker in the header and a Cutscenes branch in the tree, grouped by room. |
| R-8.5c, R-6.38, R-7.20 | SCUMM, APP | Intros are deliberate: opening a game or its first room never plays the intro; Play Game Intro is offered whenever the game has one and keeps the player's own choices (Maniac Mansion's kids). |
| R-8.40, R-8.41 | SCUMM | The scan lists a room's entry, each object verb, the arrival scene a loading script starts, and the scene started on leaving a room. |
| R-8.9, R-8.20 | LOOM EGA ref | The English catalog's 328 reviewed replays stand as data. Its late-room starts (Room 46) run their reviewed real prerequisites only when that state is missing. |
| R-8.30, R-8.31 | LOOM | A launched scene's state is derived from the edition's own scripts, and should show the same lines, actors, camera and rooms as the scene reached by playing (a target). |
| R-8.42 | SCUMM | **Which scenes are worked on** (11 Nov). A scene that plays but looks wrong is fixed through its cause, shared with other scenes and games; no setup rule is added for a single scene. An entry that is not a scene (a refusal, blank prints) leaves the lists. A scene the user names as one that matters may get a **reviewed start**: hand-checked data for that copy (its fingerprint), as LOOM's Room 46; it is data, never engine code, and Advanced says so. A scene that still shows nothing or shows wrong is not offered in Normal mode (R-1.13) and stays in Advanced, labelled. |

## 8. Game Status

| ID | Tag | Rule |
|---|---|---|
| R-6.44, R-12.14 | SCUMM, APP | Offered only when the game has a Status editor and a live session. **Apply and close**: one click validates and applies the whole batch or changes nothing. |
| R-12.7 to R-12.10 | SCUMM | **Inventory games**: a catalog of all the game's inventory items; any item can be given to a chosen character or moved, with no earned condition, within the game's own capacity. Names and identities come from that copy's data. Undo restores the previous inventory while the session still matches. |
| R-12.11 to R-12.13 | SCUMM | Recipients show their own full-body picture and name; items show their inventory picture where the game has one. Equal names do not make two items the same: qualify them from the scripts (whose item it is), else number them. |
| R-12.15 | SCUMM v3 | Indy 3: inventory for the one hero, health and outfit, IQ points, story flags, each found by its role in the scripts. |
| §12.1 | LOOM | LOOM: staff, notes, drafts and story, in its own window ([detail](archive/REQUIREMENTS_DETAIL_2026-11-10.md)). |

## 9. Sound

| ID | Tag | Rule |
|---|---|---|
| R-13.11, R-6.40, R-6.24 | SCUMM, APP | A room's Sounds button lists the sounds and music its scripts use, in both modes and every game: `Music N · m:ss`, `Sound effect N`; source and uncertainty in Advanced. A conditional use is not a confirmed soundtrack. |
| R-13.10 | APP | The sound-device selector is shown in Normal mode for every game with live scenes. |
| R-13.9 | SCUMM v3 | The music timer the scripts read follows the chosen device's driver, as in ScummVM. |
| R-5.10 | APP | CD audio is its own tree group, never mixed with sounds and music. |

## 10. Scripts, names and texts

Scripts open raw and Readable (plain language, the default), with names from the game's own data and `Used by` links, in Advanced. Game Texts lists spoken lines, names and labels. The rows are in the detailed reference (§9 to §11); decoded findings go in `docs/<game>/<GAME>_SCRIPT_MAP.md` (R-9.20).

## 11. How the work is done

| ID | Rule |
|---|---|
| R-16.3, R-16.5 | Tests and harnesses are chosen by the change, never "run everything". Reproduce a reported difference through the real window and compare with ScummVM. |
| R-16.6, R-16.7 | One commit per finished task on `master`, explicit paths. At session end: ROADMAP, CHANGELOG, the evidence record, and a fresh next prompt. |
| R-15.3 | `%LOCALAPPDATA%\SCUMMexplorer` is never deleted by the app or a harness; harnesses use `SCUMM_EXPLORER_PROFILE_ROOT`. |
| R-16.8 | Raising a game's support follows [GAME_SUPPORT_INSTRUCTIONS](GAME_SUPPORT_INSTRUCTIONS.md). |

## 12. Out of scope

Games after SCUMM v5; the Hebrew LOOM; a secrets guide; replay catalogs for the non-reference LOOM editions; General MIDI; session save and load; new Play features.
