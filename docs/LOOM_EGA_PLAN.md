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

## Next: LOOM VGA (CD, SCUMM v4), then more languages (user, 5 October; Indy3 deferred, M5 shelved, MT-32 built)

**What is in `Games/loom vga en`** (read from the files on 5 October, nothing decoded yet): `Loom.cue` + `Loom.bin`, 630 MB, `MODE1/2352` raw sectors with an ISO 9660 volume "LOOM" (268,245 sectors). Track 1 is the data track; track 2 is one CD audio track that starts at 04:57:60 (pregap 2 s) and fills the rest of the disc, about 54 minutes. The data track's root holds only `000.LFL` (index, 8 KB), `901.LFL`–`904.LFL` (2–4 KB each, probably charsets), `DISK01.LEC` (4 MB, the resource file) and `LOOM.EXE`. That is the SCUMM v4 layout: an index plus one `.LEC` container instead of the per-room `NN.LFL` files of the EGA game. The audio track probably carries the spoken dialogue and the music that the scripts start by position (not checked; ScummVM's Loom CD support is the reference).

**How to read it (not decided).** Options:
- **A. Read the image itself.** A small ISO 9660 reader for 2352-byte raw sectors (and plain `.iso` at 2048), driven by the `.cue`; the audio track is read straight from the same `.bin` by sector range. The user's files work as they are, and later formats (`.iso`, split `track01.bin`/`track02.bin` sets, `.img/.ccd/.sub`, `.mdf/.mds`; `.chd` only if a decoder is worth it) become more readers behind one interface.
- **B. Require an extracted folder** (files copied out, audio ripped to `.wav`/`.ogg`). Least code, most work for users, loses the exact audio positions.
- **C. Both:** a `game file source` abstraction (folder or disc image), the folder as the first implementation (what exists today), the image reader second. Recommended: it keeps the current code working, avoids extraction, and gives every later edition the same entry point.

**Steps once the way is chosen**
1. **Feasibility probe** (read-only harness, record under `.verification/loom-vga/`): file list, `000.LFL` index structure, the `LECF`/`LFLF` block tree in `DISK01.LEC`, room and sound counts, the opcode set against the v3 reader (v4 differences: containers, room image format and palette, object and costume formats, the script dialect), and the audio track's role. Compare with ScummVM (`scummvm/`, same disc).
2. **Source abstraction and image reader** (Core), tested against the user's `Loom.cue`.
3. **Rooms and backgrounds first** (the VGA artwork is the visible gain), then scripts, objects and Bobbin, then cutscenes, and speech and CD audio last, each checked in the real window as for EGA.
4. Separate the LOOM-specific from the generic as VGA needs it: the ~40 `GameId.Loom` gates in `MainWindow.xaml.cs` (propose before changing), and the `Loom*` Core types (`LoomEgaStateFlags`, `loom-glossary.json`, `loom-replays.json`, `LoomDraftNames`, the catalog and setup planner). The VGA scripts differ from EGA, so the glossary, bits and ids must be rederived, never copied.

Decided (user, 5 October): **C**, a game file source that is a folder or a disc image. **CD audio comes last**, after picture, scripts and cutscenes. The user picks a folder (or image) and the app identifies the game and edition itself, as it does today; VGA is a different edition, so detection must recognise it (internal game id and edition naming are an implementation detail, not a new menu choice).

**After that:** more languages of LOOM EGA (later; Spanish and English work, others when the data arrives; each checked as English was, with `GlossaryCheck` and `ScriptDump`, never keyed on offsets). **Deferred:** Indy3 EGA (until the user moves its data from `Games/FUTURE/` to `Games/`; [INDY3_CONVERSATIONS_PLAN.md](indy3/INDY3_CONVERSATIONS_PLAN.md)), then Monkey Island and Maniac Mansion.

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
