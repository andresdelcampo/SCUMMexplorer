# LOOM EGA exploration plan

Rewritten 4 October 2026, then cleaned of finished work the same day (M5 brought back in scope; other-versions order agreed with the user). The dated checkpoints that used to fill this file are kept unchanged in [PLAN_HISTORY_2026-10.md](archive/PLAN_HISTORY_2026-10.md), including the state table. Finished work is in the [changelog](CHANGELOG.md); what the decoded scripts, variables and rooms do is in the [script map](loom/LOOM_SCRIPT_MAP.md). The test and harness audit of 4 October is in [HARNESS_REVIEW_2026-10-04.md](archive/HARNESS_REVIEW_2026-10-04.md).

## Product direction

Make the DOS *LOOM* EGA release (English data in `Games/loom ega en` is the reference; Spanish is kept for comparison) a place to explore. Rooms and their original artwork lead. Clicking things does what the game does: Bobbin walks over, looks, speaks and uses things, drafts change the world, and cutscenes play with their actors, text and music. Source details (IDs, bytecode, uncertainty labels) stay available on demand in Advanced mode. The last piece is a guide to secrets built from verified triggers (M5).

The user's three priorities of 28 September (object interactions, cutscenes and animation, on-screen text) are delivered, as are the English migration and the two demos.

## What is really left for LOOM EGA

**1. M5, the secrets guide: shelved (user, 5 October).** It would mostly be another route to things the cutscene picker and the inspections already reach; build it only if a real value that they lack is shown, never as a shortcut around the app experience. Nothing of it is built; the section below stays as the old specification.

**2. Needs the user (on-screen or ScummVM checks)**
- Staff scenes in Room 43, El Muerto and Room 17 sizes, actor 4 placement.
- Draft verbs: confirm on screen that they show their text and animation (M4 runs them through the game's scripts; the draft table is a seeded estimate until each draft is learnt through a route).
- Room 34 local200 +0035: probably dead code; only a real ScummVM save could show otherwise. May be left.

**3. Evidence-bound polish** (only when it changes what the user sees)
- Draft names: Silence (0x4E/0x4F) and Shaping (0x50/0x51) follow the manual order; Sharpening's two verbs are not told apart. Verb labels outside known ranges show as `Verb 0x..`. M5 will expose these gaps.
- Glossary growth: story bits not set by a catalog scene show as `bit[0x...]`; actor names are scoped by scene; "Used by" lists literal references only; some operations keep a raw line in the readable view.
- 17 gallery costumes point at no data (no script uses them).
- Stored-text coverage: Room 70 locals 203 and 204 are truncated; unreachable code is not scanned.
- Engine gaps, only if a route needs them: `stopObjectScript`, inventory object code, `killScriptsAndResources` ending other room-owned scripts at a room load; the Room 69 difficulty and draft answer are assumed, not simulated; the Room 03 town and cemetery labels are unidentified.

**4. Housekeeping**
- 17 historical git worktrees under `.claude/worktrees/` and the open proposals in [CLEANUP_REVIEW.md](CLEANUP_REVIEW.md).

## M5. Secrets and guide (shelved 5 October; old specification)

Build a discoverable, filterable index of secrets from verified triggers and outcomes, with source links. Raw material: the 235 audited cutscene sites (`loom-replays.json`, `LoomSceneSetupPlanner`), the 90 story-event bits and the glossary, the draft table, and the script map. Every entry carries its evidence level (`verified`, `preview`, `unresolved`).

Proposed steps:
1. **Define the kinds and classify** the 235 sites, story bits, drafts and objects into them (all kinds count; see the decisions below).
2. **Entry data** (Core, generated and tested): per entry the kind, the trigger (room, object, verb, state needed), the outcome (scene, bit, draft, item), the evidence level and the source links (resource, script, instruction). Derive from the catalog and glossary, never from byte offsets or Spanish numbering.
3. **Guide window** (App): all entries visible, grouped by room and kind, filterable by kind and by triggered / not yet triggered (read from Game Status where the bit is known); each entry offers "Go to room" and "Play cutscene" through the existing launchers. Technical source details Advanced-only.
4. **Verification**: unit tests for the entry builder; one UI harness shot of the window; record under `.verification/m5-guide/`; changelog row.

Decisions (user, 4 October):
- **Meaning of secret:** all kinds: hidden cutscenes, optional drafts, alternate outcomes, easter eggs and anything else the data shows. Each entry has a kind, and the guide can filter by kind.
- **Spoilers:** ignored. The whole app is a spoiler, so there are no spoiler levels and no reveal gating.
- **Scope:** both seen and unseen content, with a filter (all / triggered this session / not yet triggered), using Game Status bits and session history where a trigger is known.

## LOOM EGA with MT-32 music (user, 4 October; built 5 October, M5 on hold)

The user put M5 on hold on 5 October and asked for MT-32 first. Built and checked (record: [.verification/mt32/README.md](../.verification/mt32/README.md)): the Roland upgrade's sounds 33–64 are tagless MIDI-like streams (`LoomRolandSong`) that the game's scripts choose when `var[0x030]` is 4; `LoomMt32Audio` renders them through Munt (`mt32emu` via Mt32emu.net 1.0.0, LGPL-2.1+; the user's own ROMs from `Games/mt32`, `Mt32RomSet`); the app has a fourth device, "Roland MT-32", that sets the session's sound card to 4 (`LoomExplorationSession.SoundCard`, also a constructor argument) so the scripts pick the Roland sounds, as decided. Local material: the MT-32 and CM-32L ROM images in `Games/mt32/` (copyrighted, never committed or shipped; the app finds them via `SCUMM_MT32_ROM_ROOT`, the saved folder, a `mt32` folder beside the game folder, or asks).

Left, in order of value:
1. **The user listens** (confirmed 5 October: the device is in the sound dropdown, and the Roland card adds the new overture to the opening while the other cards skip it, which is how the game behaves): A/B of the rendered samples (`Exe/mt32-samples/*.wav`, or the device in the app) against the bundled ScummVM with the same ROMs. Known possible differences: ScummVM's IMuse scales channel volume and may allocate parts; the app passes the stream through unchanged, as the original driver did.
2. The Mt32emu.net dependency is accepted (user, 5 October).
3. Optional: a General MIDI fallback for users without ROMs (labelled as a different sound); the room sounds menu's possible-sound list shown per card; 48–50, 60 and 63 are longer or shorter than their AdLib twins (not examined).

## Next: LOOM EGA editions, before VGA (user, 5 October)

New order: the editions of LOOM EGA first, then LOOM VGA. Reason: the folder named `loom ega en` is the Roland-upgraded copy (see the MT-32 section), so the names should say so, and more editions are now in `Games/`.

**1. Rename the game folders (done 5 October, own commit; all references updated).**
- `Games/loom ega en` → `Games/loom ega mt32 en`, and `loom ega es` → `loom ega mt32 es` (the Roland upgrade installed).
- Then `Games/loom ega ibm en` → `Games/loom ega en`, and `loom ega ibm es` → `loom ega es` (the plain game, no upgrade).
- Do the two renames in this order (the old names are taken until the first pair has moved). Games/ is not in git, so the rename is a filesystem move that the user or the session does; everything that names the folders changes in the same step: `tests/SCUMMexplorer.Tests/TestGames.cs` (lookup list `"loom ega en", "loom ega es", "loom"`), `SCUMM_SOUND_TEST_ROOT` and `SCUMM_LOOM_TEST_ROOT` in CLAUDE.md, `.verification/run-verify.cmd`, `.verification/harness-review-20261004/*.ps1`, harness drivers and `loom-replays.json` sources, README, docs, and the memory note `english-reference-edition`. Run `git grep -n "loom ega"` for the full list (about 20 tracked files outside the archives).
- **Reference edition (decided by the user, 5 October): `loom ega mt32 en` stays the reference.** The tests, harness drivers, replay catalog and glossary point at it by that name; the plain `loom ega en`/`es` are checked against it afterwards (they are other releases: `00.LFL` and most room files differ, and files 82–85 are absent).

**2. Try the plain editions and the new languages**, each as English was tried (open it, then compare with `GlossaryCheck` and `ScriptDump`; never key code on offsets, bits or object ids; record under `.verification/editions-<date>/`):
- the new `loom ega en` and `loom ega es` (the plain game, the former `ibm` folders; their indexes still point sounds 33–64 at files 82–85 that are not there, so sounds must degrade cleanly);
- `loom ega fr` and `loom ega de` (already in `Games/`). `loom ega hebrew` is **dropped** (user, 5 October): no Hebrew support, the folder stays untouched in `Games/`; the first-pass findings are in `.verification/editions-20261005/README.md`;
- `loom ega v1.2 es` and `loom ega v1.2 de` (version 1.2 of the game; check what 1.2 changed in scripts, text and room files against the plain editions).
None of these has the Roland files (no `82.LFL`), so they are plain-sound editions. For each: does the folder open and get identified, are the rooms right, do the cutscene catalog, Game Status and glossary hold or need per-edition entries, do the texts extract. Fix what breaks only where it changes what the user sees.

**Result (5 October, [record](../.verification/editions-20261005/README.md)):** the plain en/es, fr, de and v1.2 es/de editions open with no room gaps and the glossary holds against `loom ega es`; v1.2 de equals plain de and v1.2 es differs in two files (the disk table and two bytes of room 15, no visible change); Hebrew dropped (user). **Decided (user, 5 October): the cutscene catalog and replay tests stay on `loom ega mt32 en` only; no pipeline rerun for the others.**

**3. Then LOOM VGA** (next section).

## Then: LOOM VGA (CD, SCUMM v4) (user, 5 October; Indy3 deferred, M5 shelved, MT-32 built)

**What is in `Games/loom vga en`** (read from the files on 5 October, nothing decoded yet): `Loom.cue` + `Loom.bin`, 630 MB, `MODE1/2352` raw sectors with an ISO 9660 volume "LOOM" (268,245 sectors). Track 1 is the data track; track 2 is one CD audio track that starts at 04:57:60 (pregap 2 s) and fills the rest of the disc, about 54 minutes. The data track's root holds only `000.LFL` (index, 8 KB), `901.LFL`–`904.LFL` (2–4 KB each, probably charsets), `DISK01.LEC` (4 MB, the resource file) and `LOOM.EXE`. That is the SCUMM v4 layout: an index plus one `.LEC` container instead of the per-room `NN.LFL` files of the EGA game. The audio track probably carries the spoken dialogue and the music that the scripts start by position (not checked; ScummVM's Loom CD support is the reference).

**How to read it (not decided).** Options:
- **A. Read the image itself.** A small ISO 9660 reader for 2352-byte raw sectors (and plain `.iso` at 2048), driven by the `.cue`; the audio track is read straight from the same `.bin` by sector range. The user's files work as they are, and later formats (`.iso`, split `track01.bin`/`track02.bin` sets, `.img/.ccd/.sub`, `.mdf/.mds`; `.chd` only if a decoder is worth it) become more readers behind one interface.
- **B. Require an extracted folder** (files copied out, audio ripped to `.wav`/`.ogg`). Least code, most work for users, loses the exact audio positions.
- **C. Both:** a `game file source` abstraction (folder or disc image), the folder as the first implementation (what exists today), the image reader second. Recommended: it keeps the current code working, avoids extraction, and gives every later edition the same entry point.

**Update (5 October, night, very last): the CD music timer, the intro as a scene and voice timing are in** (record `.verification/loom-vga/README.md` step 16). `var[0x0E]` follows the CD cue like ScummVM's CD timer; "The intro" plays from Play a cutscene (skill screen with an assumed answer after 3 s, the overture cue, logo and copyright, the fairy, Bobbin on the cliff, Room 02); a click's voice lines start at the game time of their prints, gaps labelled as estimates. Not heard by ear. **Update (5 October, night, very very very last): the CD game's other cutscenes are in the picker, Escape and the held-staff Game Status are checked** (record step 17): 204 derived scenes (79 scenes, 125 dialogue and reactions) from the CD scripts, a note for each, an honest status when none begins from the stated start (24). **Next:** the user's listening and viewing check (intro, click voices, the new scenes: the picture against the audio); two scenes that never end headless without Escape or a line (Room 9 object 199 verb 0x38 "feather", Room 24 object 359 verb 0x3E "sheep": find what they wait for); the 24 scenes that need another state (Game Status or a derived start setup, as the EGA `LoomSceneSetupPlanner` does: rederive for the CD scripts); CD music in `SO` blocks stays archaeology; then the next game or what the user chooses.

**Update (5 October, night, the next session): LOOM CD start setups are derived and three engine gaps are closed** (record `.verification/loom-vga/README.md` step 18). The feather and sheep scenes waited for the CD music timer, which a session that skipped the intro never started (now running from the session's start, labelled); the CD game's bits are a separate array as in ScummVM (no aliasing onto variable words, which let the camera and `var[0]` flip `bit[0x025]`/`bit[0x00E]`); script 001's tail runs after a skipped intro. Of the 24 scenes with no cutscene from a fresh state, 6 begin by themselves now, 16 from a derived setup (`LoomCdSceneSetupPlanner`, shown in the picker note and run by `PlayCdCutscene`), 2 say why not. **Next:** the user's viewing and listening check; the Room 41 sword scene (stalls after its first line; ambient script 206's voice cue resets the CD timer); the two scenes without a setup (Room 34 entry: a crossing from box 6 to box 8; Room 34 object 485 verb 0x38: `bit[0x038]`); CD music in `SO` blocks stays archaeology; then the next game or what the user chooses.

**Update (5 October, night, the shepherd): Room 25 object 372 is enabled; 203 of 204 CD scenes (99.5 %)** (record `.verification/loom-vga/README.md` step 20). The scene needs bits 0x032, 0x033, 0x046 set and 0x05B clear; the room's walk-box monitor (local 200) sets them as the ego enters boxes 4, 3, 2 in that order, and the planner now derives such monitor orders (estimates: box placement and order). The Room 34 entry is the one scene left without a start: the only script that puts the ego at box 6's line (global 97) leaves him in box 5, and a walk cannot skip box 7 (looks unreachable; not proven). The user's check found the walk-triggered scenes and the CD music in Advanced fine and said they missed the per-room "Play Cutscene" button: it is in the build and shows in 44 of 78 CD rooms (34 rooms have no scene triggered from them). **Next, if the user wants it:** also list a scene in the rooms it plays out in (the EGA button does, through catalog membership; for the CD game derive the rooms a scene visits from a headless trial). Not heard by ear.

**Update (5 October, night, the sword): the three open CD scenes are answered; 202 of 204 scenes (99.0 %) are enabled** (record `.verification/loom-vga/README.md` step 19). The sword scene stalls from a fresh start because the room's hammer loop (206, freeze-resistant, a voice cue every 27 frames) keeps zeroing the CD timer that the scene's waits read; the scene stops the talk loop 207 and expects it, and runs all 50 lines when the foreman scene (object 557's look, script 210) was played first; how the real game reaches that state was not found (estimate, labelled in the picker note). The planner (`LoomCdSceneSetupPlanner`) now also handles a scene that begins but never ends, a writer inside the scene's own toggle script run by another trigger, and a scene played twice: Room 34 object 485 (the disguise door) comes from object 487's draft verb 0x4C run twice. Room 34 entry needs a change from box 6 straight to box 8, which are not neighbours (walks change one neighbour at a time): not enabled, the note says why. Not enabled now: Room 25 object 372 verb 0x37 and Room 34 entry. **Next:** the user's viewing and listening check of the scenes (nothing here was heard); what the user chooses (the next game, or the CD game's remaining gaps: how the real game reaches the sword scene, a teleport that crosses boxes 6 to 8, Room 25 object 372); CD music in `SO` blocks stays archaeology.

**Update (5 October, night, last): CD sounds, the CD audio list and Game Status artwork are in** (record `.verification/loom-vga/README.md` step 15). **Next, in this order:** (1) model `VAR_MUSIC_TIMER` (`var[0x0E]`) for CD cues, as ScummVM does (ticks every 4th timer tick from the cue's start, reset to 0 when a `sayVoice` starts or stops), so script 001 and script 044 can run; then the intro as a playable scene from "Play a cutscene" (charset 4, overture cue, room 69's skill screen assumed answer, `playCdIntro` / `CdIntroExperiment` are the start); (2) voice timing of a click's lines (they play back to back; the session runs a click's whole interaction at once; the gaps are an estimate); (3) hear the CD audio list and the voice player by ear and compare with the bundled ScummVM; (4) more CD room checks (`CdCheck`, only for what needs the window; use unit tests for data checks).

**Update (5 October, night, final): CD voices and the CD status schema are in** (record `.verification/loom-vga/README.md` step 14): the audio track plays a message's voice cue (queued per click, Voices checkbox), Game Status serves the CD game (staff, notes, 16 numbered drafts). `soundKludge` appears in no script. Open: the CD music (30 `SO` blocks, `startSound` in global 37), voice timing between a click's lines, staff/note artwork and draft names for the CD game, the intro as a playable scene (charset 4), listening check by the user.

**Update (5 October, night, last): the CD verb strip, per-message charset and voice cues are in** (record `.verification/loom-vga/README.md` step 13). Open for the CD game: a staff/note/draft state schema derived from CD scripts 017-026 and 034 (the EGA one does not fit), CD audio (track, `soundKludge`, voice cues already read), the intro as a playable scene (charset 4).

**Update (5 October, night): the data-source seam is in and the CD game shows first-visit object states** (`IScummDataSource`, `LoomCdRoomScene`; item (a) object images and (b) scripts below are done for rooms; see the changelog and [.verification/loom-vga](../.verification/loom-vga/README.md) step 6). **Update (5 October, late night): walk boxes, the arrival and state scanners behind the seam, and Bobbin on screen in the CD room view are done** (record step 7). Next: object verbs and clicks for the CD game (walk to the object, verb 0x38 / 0x36 as on EGA; needs the session in the app for CD), a depth mask for CD actors, `matrixOps` in the executor, the view following the camera, charsets `901`-`904.LFL`, CD audio last.

**Status (5 October, evening): steps 1 to 3a done, plus the v4 script dialect (all 793 scripts), the object records and the texts.** Probe, source abstraction and disc reader (Core, tested), and the room backgrounds in the app; see the [changelog](CHANGELOG.md) and [.verification/loom-vga](../.verification/loom-vga/README.md). **Next, in order of what the user sees:** (a) object images (`OI` blocks, an id then a strip table like `BM`) with the object records (`OC`: id, x, y, width, parent, walk point, direction, in the same layout as the EGA `LoomRoomObjectDirectory`), drawn with the states of the `0O` index table; (b) the v4 script dialect (`EN`/`EX` entry and exit scripts, `LS` local scripts, `SC` global scripts) for first-visit states and object verbs; (c) costumes (`CO`) and Bobbin; (d) texts; (e) CD audio last. The Game Status, glossary and replay catalog stay EGA-only until they are rederived.

**Steps once the way is chosen**
1. **Feasibility probe** (read-only harness, record under `.verification/loom-vga/`): file list, `000.LFL` index structure, the `LECF`/`LFLF` block tree in `DISK01.LEC`, room and sound counts, the opcode set against the v3 reader (v4 differences: containers, room image format and palette, object and costume formats, the script dialect), and the audio track's role. Compare with ScummVM (`scummvm/`, same disc).
2. **Source abstraction and image reader** (Core), tested against the user's `Loom.cue`.
3. **Rooms and backgrounds first** (the VGA artwork is the visible gain), then scripts, objects and Bobbin, then cutscenes, and speech and CD audio last, each checked in the real window as for EGA.
4. Separate the LOOM-specific from the generic as VGA needs it: the ~40 `GameId.Loom` gates in `MainWindow.xaml.cs` (propose before changing), and the `Loom*` Core types (`LoomEgaStateFlags`, `loom-glossary.json`, `loom-replays.json`, `LoomDraftNames`, the catalog and setup planner). The VGA scripts differ from EGA, so the glossary, bits and ids must be rederived, never copied.

Decided (user, 5 October): **C**, a game file source that is a folder or a disc image. **CD audio comes last**, after picture, scripts and cutscenes. The user picks a folder (or image) and the app identifies the game and edition itself, as it does today; VGA is a different edition, so detection must recognise it (internal game id and edition naming are an implementation detail, not a new menu choice).

**Also in `Games/`:** `loom vga en kixx` (`LOOMCD.BIN`/`LOOMCD.CUE`, another dump of the CD, probably the budget re-release; compare it with `loom vga en` in the probe). **Deferred:** Indy3 EGA (until the user moves its data from `Games/FUTURE/` to `Games/`; [INDY3_CONVERSATIONS_PLAN.md](indy3/INDY3_CONVERSATIONS_PLAN.md)), then Monkey Island and Maniac Mansion.

## Evidence rules

- Tie every exit, action, state change, message, cue, animation and scene to the resource, script and instruction behind it.
- A decoded branch or room load is a **possible** outcome. Calling a verb directly is an **exploration action**. A partial session is a **state preview**. Label estimates, assumed input and timing assumptions.
- Messages, actors and playback are game behaviour when reached events drive them. A substitute (a UI font, a placeholder actor, a skipped wait) is labelled as presentation, not the game's behaviour, and is optional.
- No room-specific hacks or invented states. Rooms show no gaps and their first-visit state. When in doubt, pick at random and label the pick. Room hotspots navigate between rooms.

Status words: `verified` means source-backed and demonstrated in the app; `preview` is intentionally partial; `unresolved` means missing evidence or execution; `deferred` is later scope.

## Records

- [Changelog](CHANGELOG.md): every finished step, with its validation.
- [Plan history](archive/PLAN_HISTORY_2026-10.md): the dated checkpoints moved out of this file.
- [Script map](loom/LOOM_SCRIPT_MAP.md): the reverse-engineering reference.
- [Object states](engine/SCUMM_OBJECT_STATES.md): the rendering and state contract.
- [Harness review](archive/HARNESS_REVIEW_2026-10-04.md): what passes, what is stale, what can go.
- Historical: [research](engine/RESEARCH.md) (23 Sep), [script coverage](archive/LOOM_SCRIPT_COVERAGE.md) (25 Sep), [exploration audit](archive/EXPLORATION_AUDIT.md) (25–26 Sep), [rendering and Indy3 audit](archive/AUDIT_2026-09-27.md) (27 Sep), [cutscene discovery](loom/LOOM_CUTSCENE_DISCOVERY.md), [readable scripts plan](archive/READABLE_SCRIPTS_PLAN.md).
