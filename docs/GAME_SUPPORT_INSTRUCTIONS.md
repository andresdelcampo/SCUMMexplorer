# Game support instructions

Written 14 October 2026. The roadmap and the exact, repeatable procedure for raising what the explorer supports for **one game family**. A person or an agent given a family and a stage follows this file and nothing else is needed to know *what* to do; *what the app must do* stays in [REQUIREMENTS.md](REQUIREMENTS.md).

## 0. Authority and how to use this file

1. **Order of authority:** [REQUIREMENTS.md](REQUIREMENTS.md) (what the app must do; the user's decisions) > this file (how to get there) > [CURRENT_WORK.md](CURRENT_WORK.md) (what is being done now). If a step here would contradict an existing requirement, **stop that item and log it** (§4.4); never change the requirement or skip the step on your own. A stage may need a *new* requirement (for example what Play means for a verb-based game): write it into REQUIREMENTS.md as a **Proposed** row (§4.5) and go on; the user reviews proposals at the checkpoint, not before.
2. **Unit of work = one family × one stage.** A family is a row of the support matrix ([`GameSupport.Families`](../src/SCUMMexplorer.Core/Domain/GameSupport.cs); REQUIREMENTS §3.1). A stage is one section of §3 below. Every stage ends with a registry level raised (or an honest "not raised, because…"), a test that would fail if the claim became false, an evidence record, and one commit.
3. **Do the stages of a family in order** (§2 shows which ones are independent). Do not start a stage whose prerequisites are not met.
4. **This file is the user's standing authorization** for everything it describes, including the shared-engine refactor of S4a, new `Proposed` requirement rows, commits and fast-forward merges to `master`. Nobody asks for approval of a step this file describes; they follow §4.5 (decide, log, continue) and report at the checkpoint (§4.7).
5. Anything not covered here: apply the rules of §1, decide by §4.5, log the decision.

## 1. Ground rules (each one is a requirement; the id says where)

| Rule | Requirement |
|---|---|
| The product is an audio-visual explorer, not a technical viewer. Evidence exists, but only behind **Advanced**. Normal mode never shows status, validation, "not ready" or "partial" notes; content that is not ready is simply not offered. A caveat of a new feature goes in Advanced. | R-1.1, R-1.10, R-1.13 |
| Game data, ROMs and saves are never committed or shipped. | R-1.2 |
| Rooms show **no gaps** and their **first-visit state**; where the data does not decide, pick at random and **label the pick**. | R-1.4 |
| Objects respond as in the game, with the game's hero on screen; a stored-image cycling gesture belongs to the inspection mode only; no hidden gestures. Room hotspots navigate between rooms. | R-1.5, R-1.6 |
| The running session carries only changes caused by real scenes; a scene adds only its minimum prerequisites. Game state shown to the user is under the user's control; the app never edits it as a shortcut. | R-1.7, R-1.8 |
| **No room-specific hacks, no invented states, no forced costumes/flags/boxes, no teleports.** Look, placement and scene starts come from the game's own scripts. Estimates are labelled and never change gameplay. | R-1.9, R-1.11 |
| Build only what adds real value beyond cutscenes and inspections. | R-1.12 |
| Every exit, action, state, cue or scene is tied to a resource, script and instruction. Use the words *possible outcome*, *exploration action*, *state preview*, *estimate*; names carry an evidence level (`decoded`, `engine`, `reading`, `play`); unnamed things keep their raw form. | R-2.1 to R-2.3 |
| Source execution, prepared checks and original-game observation are different evidence; a scene completing does not prove correct actors, prerequisites or timing. | R-2.4 |
| Code takes `IScummDataSource` / `ScummProfile.For(game)`, never branches on the game; put code in the lowest layer true for it (`Scumm` cannot see `Loom`). | R-3.6, R-16.1 |
| Editions differ in **data**, not only text: derive ids, bits, offsets and availability from the edition's own scripts; never key shared code on a byte offset or a language. Check only the plausible differences for the feature and reuse `.verification/editions-20261005`. | R-14.2, R-14.4 |
| Tests are filtered to the change; one targeted harness check per change; the user retests at the end. Harnesses use `SCUMM_EXPLORER_PROFILE_ROOT` for a private profile; `%LOCALAPPDATA%\SCUMMexplorer` is never deleted. Reproduce differences through the real window and compare with the bundled ScummVM; never `git stash`. | R-15.3, R-16.3 to R-16.5 |
| One commit per finished task, explicit paths, `Games/` never added. At session end update `CURRENT_WORK.md` and `CHANGELOG.md`. | R-16.6, R-16.7 |
| All shell commands go through RTK (see `CLAUDE.md`). | R-16.2 |

## 2. Vocabulary and the roadmap

- **Family**: a `GameFamily` (one matrix row). **Cell**: a family × feature level (✔ Full, ◐ Partial, – None). The meaning of each level per column is the table in REQUIREMENTS §3.1; this file does not redefine it.
- **Fixture**: the local game folder the conformance test uses for the family (`SupportConformanceTests.Fixtures`). A family without a fixture cannot raise a level.
- **Reference**: ScummVM `engines/scumm` source is the ground truth for engine behaviour (§5 lists the file per feature); the bundled `scummvm/` build is the ground truth for runtime comparison.

### Roadmap

| Stage | What it delivers | Matrix column(s) | Needs | Done once or per game |
|---|---|---|---|---|
| **S1 Intake** | The family is recognised, has a fixture, a reference folder and a baseline record | (all, Rooms) | – | per game (done for all current families; re-run as an audit) |
| **S2 Reading** | Costumes and scripts are read right, in the correct palette and dialect | Costumes, Scripts, Texts | S1 | per game (open: classic v1 costume colours only) |
| **S3 Sound** | Every sound kind plays on every device the game has | Sound | S1 | per game (open: v5 script hooks, after S6) |
| **S4 Session** | A live session that boots the game, runs entry scripts and tracks child scripts | (prerequisite of S5–S8) | S1, S2 | **S4a once** (generic session), **S4b per game** (boot, data source, dialect) |
| **S5 Objects** | First-visit object state from the game's own scripts, rooms without gaps | Objects | S4b | per game |
| **S6 Play** | Actors, camera, walking, verbs and clicks run the game's scripts | Play (and Objects ✔) | S5 + the Play requirement | per game |
| **S7 Cutscenes** | A list of the game's cutscenes, derived from its scripts, playable | Cutscenes (◐) | S6 | per game |
| **S8 State** | A window for the game's state, **only if the run request asks for it** (default: skipped, offered at the checkpoint) | Status | S6 | per game |
| **S9 Editions** | The other releases/languages/demos of the family behave | (notes) | the stage being widened | per family, after each stage |

S2 and S3 do not need a session. **They are done for every cell of the matrix except two**: the actor colours of the classic Maniac Mansion and Zak McKracken costumes (S2) and the v5 sound hooks that wait for a script (S3, which can only be finished after S6, because they need running scripts). Everything else listed as "polish" below (AdLib tempo checked by ear, PCjr/Tandy not yet listened to, a few percent of v5 scripts unresolved) does not change a cell; do it only when a run asks for it.

### Where each current family stands (14 October)

| Family | S1 | S2 | S3 | S4 | S5 | S6 | S7 | S8 | Pending in the matrix |
|---|---|---|---|---|---|---|---|---|---|
| LoomEgaReference, LoomEga, LoomEgaDemo, LoomCd | done | done | done | done (LOOM-coupled) | done | done | LoomEga/Demo/Cd ◐ by design | done | only the LOOM items in CURRENT_WORK; the *generalisation* of S4a is the work that serves the other games |
| Indy3Ega | done | done | done (polish: AdLib tempo unchecked by ear) | – | stored boxes ◐ | – | – | – | Objects, Play, Cutscenes, Status |
| Indy3Vga, MonkeyFloppy | done | done (polish: costumes use the palette of the storing room) | done (polish: AdLib music at half tempo, set by ear) | – | ◐ | – | – | – | Objects, Play, Cutscenes, Status |
| MonkeyCd, Monkey2, Indy4 | done | done (polish: ~3% of v5 script blocks unresolved) | **◐ iMUSE: script-driven hooks, loops; two MI1 tracks unnamed; Indy 4 CD has no tracks** | – | ◐ | – | – | – | Sound (needs S6), Objects, Play, Cutscenes, Status |
| ManiacEnhanced, ZakEnhanced | done | done (polish: Maniac scripts 00/01/06 decode little) | done (polish: PCjr/Tandy not listened) | – | ◐ | – | – | – | Objects, Play, Cutscenes, Status |
| ManiacClassic, ZakClassic | done | **Costumes ◐: actor colours estimated** (polish: Zak Script 39) | done (polish: PCjr/Tandy not listened) | – | ◐ | – | – | – | Costumes, Objects, Play, Cutscenes, Status |

Facts behind S4 and S5 (so nobody rediscovers them): `LoomExplorationSession` only accepts old-v3 LOOM and LOOM CD (`GameId.Loom`/`LoomVga` checks); `ScummDataSources.For` has an `IScummDataSource` for old-v3 (`ScummV3DataSource`) and v4 (`ScummV4DataSource`) only, so v1/v2/v5 games are decoded ad hoc in the App (`DecodeBrowsedV4Script`, `ScummOldScripts`); a probe on 12 October showed the real starting objects of Indy3 and Monkey Island come from child and local scripts (`startScript`, `isScriptRunning`, `getObjectOwner`, `roomOps`) that only a script-tracking session can follow (`.verification` / CHANGELOG 12 October, "finding: real starting objects").

## 3. Stage playbooks

Every stage uses the same template: **Goal, Anchors, Prerequisites, Steps, Acceptance, Do not, Record.** "Record" is always §4.1.

### S1. Intake (a new game, or an audit of a current one)

- **Goal.** The folder or disc image is recognised as exactly one family, has a fixture, and the facts needed later are written down.
- **Anchors.** R-3.1 to R-3.14, R-3.13 (evidence = the format reading only), R-14.2.
- **Prerequisites.** A local copy under `Games/` (never committed) and, for the comparison, the bundled `scummvm/`.
- **Steps.**
  1. Run the scanner (`GameFolderScanner.Scan`) on the folder. Add the family to `GameFamily`, its published title to `KnownGameTitles` and its mapping to `GameSupport.FamilyOf` if missing. Detection is by file bytes and indexes, never names or sizes (R-3.1).
  2. Add the family row to `GameSupport.Families` with **only the levels the code already delivers**; add its fixture folder to `SupportConformanceTests.Fixtures`; run the support tests with `SCUMM_UPDATE_SUPPORT_MATRIX=1` so the README and REQUIREMENTS tables follow.
  3. Write or extend the format notes in [`engine/SCUMM_RESOURCE_FORMATS.md`](engine/SCUMM_RESOURCE_FORMATS.md) (layout, room picture codec, costume, sound), labelled with the SCUMM version they apply to.
  4. Create `docs/<game>/` (for example `monkey2/`) with `<GAME>_SCRIPT_MAP.md`: script ids, variables, verbs and rooms found, each with source and date (R-9.20). Engine rules go in `engine/`, game facts in `docs/<game>/`, never mixed.
  5. List the **edition differences** that could matter (languages, floppy/CD/talkie, EGA/VGA, demos) with the local copies of each; mark which will be checked in S9.
- **Acceptance.** `SupportConformanceTests` passes for the family (recognised; rooms, costumes, sounds, scripts and texts exist for every level above None); the baseline record exists.
- **Do not.** Claim a level the test cannot show; use a folder name or file size to identify a game; leave an unrecognised game titled with its folder name in the matrix (it belongs in `OtherBrowsed`).

### S2. Reading (Costumes, Scripts, Texts)

- **Goal.** Costumes drawn with the colours the game gives them; scripts decoded in the right dialect with no silent loss; texts complete.
- **Anchors.** R-3.12, R-3.13, R-5.4, R-9.5 to R-9.8, R-9.18, R-11.1 to R-11.3, R-2.5.
- **Prerequisites.** S1.
- **Steps.**
  1. **Costumes ◐ → ✔.** Find, in ScummVM's source (`costume.cpp`, `actor.cpp`, `palette.cpp`, and the version's script file for `setPalColor` and the actor's colour/palette sub-opcodes), where the colours of a costume come from for this version. Where the colours come from a script (classic Maniac Mansion/Zak: the actor's colour table; v4/v5: an actor palette set by scripts), derive them **from the game's own scripts or tables**, with evidence, and not by hand. If a colour cannot yet be derived, keep the estimate **labelled** (R-1.11) and the cell ◐. A palette taken from the room that stores the costume is an estimate, not the game's colours.
  2. **Scripts.** For each block that stops on an unresolved path, classify it: data table, code reached only at run time, an opcode not modelled, a script that continues after a stop. Fix the opcode table from the version's script source file (`script_v2.cpp`, `script_v5.cpp`, …, fetched with the date noted). Report what remains; the Raw view never hides an undecoded byte (R-9.7).
  3. **Texts.** Check kinds (spoken lines, object names, labels, choices) against the script source; talkie speech offsets are kept (`ScummSpeechBank`).
- **Acceptance.** For Costumes: a harness picture of each costume side by side with the same costume in the bundled ScummVM, in the same room/actor state; differences listed. For Scripts: decode coverage numbers before/after on all blocks of the fixture; a unit test pins the opcode fix. The conformance test is extended with the new floor (count of costumes/scripts/texts).
- **Do not.** Guess an operand; name a script/object without evidence (names are LOOM-only data until the game's glossary exists, R-10); change a cell to ✔ because it "looks right".

### S3. Sound

- **Goal.** Every sound kind the game has plays on each device it has, matching ScummVM.
- **Anchors.** R-13.1 to R-13.8, R-7.6.
- **Prerequisites.** S1.
- **Steps.**
  1. List the sound kinds and devices of the family (AdLib, PC speaker, PCjr/Tandy, Roland MT-32, Sound Blaster samples, speech, CD tracks). The registry's Partial note says what is missing.
  2. For each missing or unverified kind, port the behaviour from ScummVM's driver (`sound.cpp`, `player_*.cpp`, `imuse/`), keeping the third-party notices current (R-13.6). Tempo, loops and instrument tables come from the source, not from the ear; where the current value was "set by ear", replace it with the source value or keep it labelled.
  3. Script-driven hooks (a song that waits for a script's id) are done in S6, when scripts run; until then the sound plays its default path, and Advanced says so.
- **Acceptance.** A unit test per kind that renders and checks signal (as `V5ImuseAudioTests`); then a **listening check by the user** (an agent cannot hear). It does not block anything: list the exact sounds in the checkpoint's *user checks* (§4.7) and go on. The cell stays ◐ until the user reports the result; a later run flips it in a one-line commit.
- **Do not.** Mark ✔ from signal tests alone; change a device the user already confirmed.

### S4. Session (the gate to Objects, Play, Cutscenes, State)

- **S4a. The generic session (done once, by one agent).**
  - **Goal.** Split the game-independent part of `LoomExplorationSession` (script runner with child-script tracking, variables, actors, camera, verbs, messages, object states, room loading) from LOOM's part (story bits, drafts, staff, Game Status, catalog recipes), so a second game can use it. This is the only structural change; behaviour of LOOM must not change.
  - **Anchors.** R-3.6, R-16.1, R-1.7, R-15.1, R-9.19.
  - **Steps.** (1) Write the target class names and their `Scumm/` folder in a short plan at the top of the S4a record and start (no approval needed; the plan is reviewed at the checkpoint). (2) Move code in small commits, LOOM tests green after each. (3) Rename a `Loom*` class to a generic name only when it moves to `Scumm/` (`LoomCutsceneIndex`, `LoomScriptPresenter`, `LoomTextExtractor`, `LoomSpeakerNames` are candidates; their LOOM tables stay data). (4) Replace every `GameId` test in the session by a `ScummProfile` property or by data.
  - **Acceptance (automatic, replaces any approval).** Before the first move, record a **baseline** of the LOOM EGA (reference English) and CD results: the unit tests for boot, first-visit rooms, clicks, actors, scenes and the replay catalog, plus the room audit and `WindowCheck` numbers. After the last move the same checks give identical results; no new `GameId` branch in `Scumm/`. A difference that cannot be explained as a bug fixed on purpose means **revert that commit** and try a smaller move. Each move is its own commit, so a revert is cheap.
- **S4b. Per game.**
  - **Goal.** The session boots the game to its first room.
  - **Prerequisites.** S4a, S2 (the dialect is read right).
  - **Steps.** (1) Make sure `ScummProfile.For(game)` returns the right dialect and `ScummDataSources.For(game)` returns an `IScummDataSource` for the family's format. v1/v2/v5 have none today: write it beside the format readers (`Scumm/V1`, `V2`, `V5`), implementing the same interface. (2) Find the boot sequence in ScummVM (`scumm.cpp`, the version's `setupScummVars`, the boot script number from the index/script source) and add the version's engine variables to `ScummVars` with sources. (3) Run the boot in the bounded executor; every unsupported opcode is reported as unsupported (R-9.19), never skipped silently; fix the executor from the version's script file. (4) Record the boot trace under `.verification/<game>-session-<date>/`.
  - **Acceptance.** Headless: the boot reaches the first playable room in a bounded number of frames with a list of unsupported items that is empty or written down; a test pins it. The first-room entry script runs to a stable state.
  - **Do not.** Hard-code a room, script number or flag in shared code; edit variables to make the boot pass (R-1.9).

### S5. Objects (first-visit state)

- **Goal.** Every room of the game shows its first-visit state with no gaps, from the game's own entry scripts.
- **Anchors.** R-1.4, R-1.9, R-5.3, R-6.4, R-6.14, R-6.15, [object states](engine/SCUMM_OBJECT_STATES.md).
- **Prerequisites.** S4b.
- **Steps.**
  1. The room's first visit = the settled state after the earliest normal route: global entry script, room entry script, child scripts, until the frame it settles (the rule in `SCUMM_OBJECT_STATES.md`; do not sample at an arbitrary frame).
  2. The room thumbnail and the room view are produced by the session (R-5.3), falling back to the plain background only if the run cannot finish; the fallback is Advanced-visible, never a Normal-mode caveat.
  3. Paint order, `drawObject` queue, `setState` and parent-state rules are the shared ones (R-6.4). A room that still shows a gap after a correct run: look for the missing script behaviour; do **not** add a room-specific fill. Where the data genuinely does not decide, pick at random and label the pick (R-1.4).
  4. Apply R-6.14/R-6.15 equivalents: objects at off-scene positions and untouchable (class) objects are not hotspots; read the family's class bits from its source.
  5. Remove the stored-object box mode's limitation note once real state exists; stored boxes remain as the Advanced list.
- **Acceptance.** (a) Every room opens without a gap (run the room audit harness for the family, `RoomAudit`-style); (b) a **sample of at least ten rooms**, chosen to include the most script-heavy ones, compared by picture with the bundled ScummVM at the first visit, with differences listed in the record; (c) a unit test that pins the settled object states for three named rooms. The Objects cell moves to ✔ only together with S6 (REQUIREMENTS §3.1 defines ✔ as state **and** click responses); until then the tracker records "S5 done" and the cell stays ◐.
- **Do not.** Initialise states from the index and call it first-visit; fill gaps by pixels; invent a state a room does not reach.

### S6. Play

- **Goal.** The user clicks in a live room and the game's scripts respond, with the game's hero on screen.
- **Anchors.** R-1.5, R-1.6, R-1.13, R-6.1 to R-6.11, R-7.1 to R-7.6.
- **Prerequisites.** S5 **and a Play requirement for the family.** REQUIREMENTS §7 is the LOOM binding (single click walks and uses, double click looks, input scripts 005–007). A verb-based game (sentence line, verb pick, inventory, its own input scripts) needs its own rows. Write them as `Proposed` rows (§4.5) using the generic mechanism (the game's input script chain, `VAR_VERB_SCRIPT`, the playback clock, skip/pause), following the game's own interface as ScummVM implements it, and build on them. Put the proposal rows in their own commit so the user can reject them at the checkpoint and the Play work can be reverted as a unit.
- **Steps.** (1) Actors, walk boxes, camera, costumes and messages from the shared runtime. (2) Input: deliver clicks and keys to the game's own input scripts as the version does (ScummVM `input.cpp`, `verbs.cpp`, `script.cpp`); the verb strip (R-6.6) is drawn from the game's verbs. (3) Inventory and dialogue choices as the game's scripts draw them (Indy3's dialogue verbs 120–125 are the existing example, R-11.6). (4) The hint line, the Interact menu and all technical text follow R-6.8, R-6.9 and R-1.13 (Advanced only). (5) Script-driven sound hooks of S3 are connected here. (6) Hotspots: doorways are not object hotspots (R-6.13a).
- **Acceptance.** A scripted headless walk-through of a bounded route written in the record (for example: leave the first room, take one object, open one dialogue) with each step tied to a script and instruction (R-2.1); the same route in the bundled ScummVM described side by side; a WPF check through the real window. Perceived timing and audiovisual sync are the user's manual check (R-2.4) and are asked for once.
- **Do not.** Add a modifier-key gesture; force a verb; present a static doorway guess as a certain exit (R-7.2).

### S7. Cutscenes

- **Goal.** A list of the game's cutscenes derived from its scripts, playable from the picker and the room's button.
- **Anchors.** R-8.1 to R-8.5, R-8.10, R-8.13, R-8.21 to R-8.25, R-8.17, R-1.13.
- **Prerequisites.** S6.
- **Steps.** (1) Use the shared scan (`LoomCutsceneIndex`; move it to `Scumm/` when it first serves another game, K-06 in the findings): `cutscene`/`endCutscene` spans, triggers grouped by the scripts they reach, helpers reached from more than 20 triggers dropped. (2) Titles come from the single builder (`LoomCutsceneTitles`; the format is REQUIREMENTS §8.2), with the game's own verb names in place of LOOM's draft names. (3) Start setups come from the planner that runs the game's own prerequisites (R-8.5, R-8.26 apply to the cutscene button only). (4) The room list and the picker work as for LOOM CD, including the background derivation with its progress line. (5) A shipped seed (R-8.25) only if the user asks.
- **Acceptance.** The scan finds the triggers of at least three scenes the user names; each begins from a fresh start in a headless run (as `LoomCutsceneRun`); counts recorded. The registry level is ◐ (list derived, not hand-validated); ✔ is reserved for a hand-validated catalog, which exists only for the LOOM reference (R-8.18).
- **Do not.** Show "needs validation" in Normal mode (R-1.13, R-8.17); hand-write a catalog for a family without the user's approval.

### S8. State

- **Goal.** If the game has state worth showing (inventory, key variables), a window for it.
- **Anchors.** R-12.1 to R-12.6, R-1.8, R-1.12, R-1.13.
- **Default: skip.** Build S8 only when the run request names it; otherwise list it in the checkpoint as an offer. The LOOM Game Status is specific to staff, notes and drafts. For another game it is a new design that must add value beyond play and cutscenes (R-1.12). When the run request asks for it, write the requirement rows as `Proposed` and build: editing goes through supported session edits, never silent (R-1.8); the user's choices are labelled as choices, not played scenes.
- **Acceptance.** As S6, with the edit/undo round-trip checked.

### S9. Editions

- **Goal.** The other releases of a family (languages, floppy/CD/talkie, demos, patched versions) behave as the reference one.
- **Anchors.** R-14.2 to R-14.4, R-3.9, R-3.3.
- **Steps.** After a stage is done on the reference copy of the family, list the plausible differences the stage can meet (opening/detection, format/scripts, text/fonts, graphics, audio), take the relevant checks from `.verification/editions-20261005`, and run them only on the copies where the difference is plausible. Derive per-edition numbering from the edition's own scripts (R-14.2). A genuine difference gets an isolated, documented branch **in data**; a language never selects code.
- **Acceptance.** A table in the record: edition × check × result. An edition that fails keeps its registry level and gets a note in the tracker.

## 4. Procedures common to all stages

### 4.1 The record (what "done" produces)

Each finished stage leaves, in this order:

1. **Registry**: the level changed in `GameSupport.Families` (or an explicit "unchanged, because …" in the tracker); tables regenerated with `SCUMM_UPDATE_SUPPORT_MATRIX=1`.
2. **Test**: the conformance test extended (or a unit test) so the claim fails if false. A level without a test is not raised.
3. **Evidence**: `.verification/<game>-<stage>-<yyyymmdd>/README.md` with: what was run (commands via RTK), the fixture folder and edition, the numbers, the comparison with ScummVM, the list of differences and **uncertainties kept as written** (failed evidence is kept, R-2.4). Large captures are not committed.
4. **Docs**: `docs/<game>/` script map and engine notes updated (facts with source and date); `docs/CHANGELOG.md` entry; the family's tracker (§6) updated; `docs/CURRENT_WORK.md` if the plan changed.
5. **Requirements**: if the stage found that a requirement is wrong or missing, a proposed row (not a silent edit) and a mention to the user.
6. **One commit**: explicit paths, message ends with the attribution line, `Games/` untouched.

### 4.2 Choosing what to run

Run the unit tests whose names match the change, the conformance test for the family, and **one** harness for the stage (room audit for S5, WindowCheck for UI, a listening request for S3). Do not run the full audit driver. When shared code changes, run the LOOM EGA and CD tests as the regression check, and say so in the record.

### 4.3 Working with several agents

- **One family per agent; one stage at a time.** The agent owns `docs/<game>/`, `.verification/<game>-*`, its fixture row and its `GameSupport.Families` row.
- **Shared files** (`GameSupport.cs`, `SupportConformanceTests.cs`, `KnownGameTitles.cs`, README and REQUIREMENTS tables, `ScummVars`, the executor, shared readers) are edited in **small, separate commits** that touch only what the stage needs. The README and REQUIREMENTS tables are generated: on a merge conflict, take either side and re-run the tests with `SCUMM_UPDATE_SUPPORT_MATRIX=1`.
- **Engine-wide changes** (S4a, executor opcodes, `ScummVars`, new `IScummDataSource` members) are announced in `CURRENT_WORK.md` before starting; two agents never edit the same engine file at once. An agent that needs a change in shared engine code for its game writes the need down and waits for the owner, unless the change is additive and small.
- **Handoff**: the family's tracker (§6) is the handoff. It says the last stage done, the next, what is blocked and on whom.
- **Asking the user**: agents do not ask; they decide by §4.5 and log. Questions, proposals and user-only checks (listening, timing) go in the checkpoint (§4.7).

### 4.4 Stopping rules (hard stops only)

An agent stops **the item it is on** (not the whole run), writes the reason in its tracker under *Blocked on*, and moves to the next item when:

1. a step would **contradict an existing requirement** (a missing requirement is not a contradiction: use §4.5);
2. the only fix would be a **room- or game-specific rule in shared code**, a forced state, or a pixel fill (R-1.9);
3. the step needs an **irreversible or outside-the-repo action**: deleting or moving game data or the user's profile, force-pushing, `git stash`, publishing, anything on the user's running applications;
4. the needed **data is missing** (no fixture, no ROMs);
5. **two attempts** at the same blocker have failed (the second with a different approach): record both and move on.

Everything else is decided, not asked (§4.5).

### 4.5 Deciding without asking

Where this file or REQUIREMENTS does not decide, the agent decides and writes a **decision-log entry** in the family's tracker (what was open, the options, the choice, why, how to undo). Preferences, in order: (1) what the existing requirements already say for LOOM, extended to the new game; (2) what ScummVM does; (3) the smaller, more reversible change; (4) the choice that shows the user less technical text in Normal mode. Where the data does not decide a visible state, pick at random and label the pick (R-1.4).

A **new requirement** is written into REQUIREMENTS.md in the proper section with `Basis` = `Proposed (<date>, <family>)`, in its own commit. Proposed rows are binding on the agents that follow, like any other row, until the user rejects or edits them at a checkpoint; a rejection is handled in the next run by reverting the dependent commits.

### 4.6 Running a stage with agents (what the orchestrator does)

The user starts a run with a request like *"Run stage S2 for all pending families, budget about X"*. The orchestrator (the main Claude session) then:

1. **Plan the run.** Read this file, `CURRENT_WORK.md` and every family's tracker; list the (family, stage) items that are pending and whose prerequisites are met; drop items without a fixture; order them by §2 (S4a first and alone). Estimate the cost per item from the previous checkpoints (the first run has none: start with the smallest family) and cut the list to the budget. Write the plan at the top of a new `docs/checkpoints/<yyyymmdd>-<stage>.md`.
2. **One agent per item**, each in its own git worktree (`isolation: "worktree"`), started in the same message when the items are independent. S4a, and any item that changes shared engine files (§4.3), run alone or in a fixed order, never in parallel with each other.
3. **Give each agent the brief of Appendix A** and nothing more: the family, the stage, the fixture path, the budget for the item, and the instruction to read this file and the REQUIREMENTS sections it names.
4. **Integrate as each agent returns**, not at the end: review its diff against §4.1 (registry, test, record, docs, one commit) and the stopping rules; run the family's conformance test and, if shared code changed, the LOOM regression of §4.2; fast-forward the worktree branch into `master` (R-16.6); regenerate the matrix tables if they conflict. An agent whose result fails review gets one correction round, then is logged as blocked.
5. **Close with the checkpoint (§4.7)** and stop. The run never continues past its checkpoint on its own, and never creates follow-on tasks by itself.

### 4.7 Checkpoints and budget

A run always ends at a **clean checkpoint**: everything finished is merged into `master` with the tests green, nothing is half-done in the working tree, and unfinished items are recorded in their trackers as *not started* or *blocked* with the reason. Budget rules:

- Each item has a cap (set by the orchestrator from the plan). An agent that reaches the cap stops at its next clean point (a commit that leaves the tree green), records the state and returns. Never leave an uncommitted partial refactor at the end of a run; S4a in particular proceeds in small green commits so any prefix is a valid checkpoint.
- Before spawning each new item the orchestrator checks the remaining budget and does not start an item it cannot finish; it stops early at a checkpoint instead.
- The checkpoint file `docs/checkpoints/<yyyymmdd>-<stage>.md` holds: **Done** (family, stage, cell changes, commit ids), **Not done and why**, **Decisions made** (the logs), **Proposed requirements to confirm or reject**, **User checks** (listening, timing, a look at a real window: each with the exact steps and where), **Offers** (S8 designs, shipped seeds), **Cost** (what each item took, to size the next run) and **Next run**: the exact request to paste.
- `CURRENT_WORK.md` and `CHANGELOG.md` get a short pointer to the checkpoint file.

## 5. ScummVM sources to read, by feature

All under `engines/scumm` of ScummVM (fetch the version you read and note the date in the record).

| Feature | Files |
|---|---|
| Index, resources, rooms | `resource.cpp`, `resource_v2.cpp`, `resource_v3.cpp`, `resource_v4.cpp`, `room.cpp` |
| Room pictures | `gfx.cpp` (`GdiV1`, `GdiV2`, `Gdi`) |
| Objects | `object.cpp`, `boxes.cpp` (walk boxes), `charset.cpp` (message rendering) |
| Costumes, actors | `costume.cpp`, `actor.cpp`, `palette.cpp` |
| Scripts by version | `script.cpp`, `script_v2.cpp`, `script_v5.cpp`, `script_v6.cpp` (out of scope today) |
| Verbs, input | `verbs.cpp`, `input.cpp`, `cursor.cpp` |
| Boot, variables | `scumm.cpp`, `vars.cpp` |
| Sound | `sound.cpp`, `players/player_v2.cpp` (PC speaker/PCjr), `players/player_v3m.cpp`, `imuse/`; the AdLib/OPL driver is in ScummVM's `audio/adlib.cpp`, outside `engines/scumm` |

## 6. Per-family tracker (copy into `docs/<game>/SUPPORT_STATUS.md`)

```markdown
# <Game> support status

Family: <GameFamily> | Fixture: Games/<folder> | Last updated: <date> | Owner: <agent/person>

| Stage | State (not started / in progress / done / blocked) | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | | Rooms | .verification/<game>-intake-<date> | |
| S2 Reading | | Costumes, Scripts, Texts | | |
| S3 Sound | | Sound | | listening check listed in checkpoint: yes/no; result: |
| S4b Session | | – | | boot trace |
| S5 Objects | | Objects (◐ until S6) | | rooms compared: n/10 |
| S6 Play | | Play, Objects | | Play requirement: Proposed row id / confirmed by user: yes/no |
| S7 Cutscenes | | Cutscenes | | |
| S8 State | | Status | | requested in a run: yes/no |
| S9 Editions | | – | | editions checked: |

Blocked on: …
Next: …
Open uncertainties: …

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
```

Create the file at the family's S1 and update it in the commit of every stage.

## Appendix A. The brief for an agent

> You work on **family `<GameFamily>`**, **stage `<Sx>`**, fixture `Games/<folder>`, in your own worktree. Read `docs/GAME_SUPPORT_INSTRUCTIONS.md` (the whole file; section 3 for your stage and section 4 for the procedures) and the REQUIREMENTS sections it cites. Follow the stage playbook exactly. You have the standing authorization of section 0.4: do not ask for approval; decide by section 4.5 and log decisions in `docs/<game>/SUPPORT_STATUS.md`. Stop an item only under the hard stops of section 4.4. Item cap: `<budget>`; at the cap, stop at your next green commit. Deliver: the registry level (or an explicit "unchanged because"), the conformance/unit test, the record under `.verification/<game>-<stage>-<date>/`, doc updates, and one commit with explicit paths ending with the attribution line. Run only the tests your change needs plus the regression of section 4.2 if you touched shared code. Never touch `Games/`, the user's profile, or the running applications. Finish with a short report: what changed, numbers, decisions, blocked items, proposed requirements, user checks.
