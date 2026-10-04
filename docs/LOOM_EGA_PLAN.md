# LOOM EGA exploration plan

Rewritten 4 October 2026 (M5 parked and the other-versions order agreed with the user the same day). The dated checkpoints that used to fill this file (route coverage, certainty and observer work, completed UI specifications) are kept unchanged in [PLAN_HISTORY_2026-10.md](PLAN_HISTORY_2026-10.md). Finished work is in the [changelog](CHANGELOG.md); what the decoded scripts, variables and rooms do is in the [script map](LOOM_SCRIPT_MAP.md). The test and harness audit of 4 October is in [HARNESS_REVIEW_2026-10-04.md](HARNESS_REVIEW_2026-10-04.md).

## Product direction

Make the DOS *LOOM* EGA release (Spanish data in `Games/loom`) a place to explore. Rooms and their original artwork lead. Clicking things does what the game does: Bobbin walks over, looks, speaks and uses things, drafts change the world, and cutscenes play with their actors, text and music. Source details (IDs, bytecode, uncertainty labels) stay available on demand in Advanced mode, but they are not the experience. Later, a spoiler-aware guide to secrets is built from verified triggers.

**User priorities (28 September), all delivered:**

1. Object interactions do something visible: a click walks Bobbin to the object and runs the game's own verb scripts (005–007, verb 0x38); a double click also looks (0x36). Doorways lead on.
2. Cutscenes and animations play: actors drawn with costume animation, walking in steps with the camera following, real `delay` timing, pause, step and skip.
3. Text appears where the game shows it, in the game's font: descriptions, dialogue, cutscene lines.

## State of the app (4 October 2026)

| Area | State |
| --- | --- |
| Rooms and objects | 76 rooms render with no gaps in their first-visit state (RoomAudit: 0 rooms with gaps). Object images, depth masks, walk boxes and doorway navigation work; DoorwayClickCheck 16/16 (rooms 2, 3, 4, 8, 10, 37). |
| Interaction | Game input, notes and drafts go through the game's own scripts. Game Status lets the user set staff, notes, learned drafts and the 90 story events; the app only raises the state to the minimum an action needs. |
| Cutscenes | Play Cutscene (global in Advanced, per room beside Interact) offers about 335 catalog starts (330 generated in `loom-replays.json` plus the hand-built opening, trial, transition, firewood and bonus scenes) built from the game's own code; each start gets a derived setup (staff, costume, speakers, camera) and every assumption is shown in the picker note. Sequences (052 → input → 053, 086 → look → 087) replay as stated chains. |
| Audio | PC speaker, AdLib (NukedOPL3Sharp) and No Sound; music and effects follow the selector. Room Sounds menu plays what the room may start. |
| Texts | Game Texts (Advanced-only) browses extracted dialogue with room pictures and Play Cutscene links. |
| Advanced inspector | Readable/Raw script view with a glossary, Last action timeline, grouped assumptions and "Used by" (readable-scripts plan complete). |
| Coverage of the game's 235 cutscene sites | 234/235 audited as reached by some route; 169/235 by a single action. The remaining site is Room 34 local200 +0035, which looks unreachable in play. |
| LOOM demos (4 October, late) | `Games/loom ega short demo en` and `long demo en` open: a different build, not a subset of the full game (own rooms, scripts, stale index entries dropped), rooms explorable in their first-visit state; "The preview" (the demo's self-running director, script 045 there) is one more cutscene in the existing launchers. No glossary script/bit names, replay catalog or Game Texts cutscene links for a demo. Evidence: `.verification/demos-20261004/README.md`. |
| Quality | Build: 0 errors. Unit tests: 703 passed, 5 skipped diagnostic dumps. Harnesses: 129 run, none of the failures reflected game behaviour; 35 obsolete ones removed, 3 repaired (see the review). |

## What is still open for LOOM

Needing the user (on-screen or ScummVM checks):
- Staff scenes in Room 43, El Muerto and Room 17 sizes, actor 4 placement. (Room 02 mountain-side depth mask: closed 4 October; ScummVM hides Bobbin at the same place, see [the record](../.verification/room02-mountain-20261004/README.md).)
- Room 34 local200 +0035: probably dead code; only a real ScummVM save could show otherwise.
- Verify on screen that the draft verbs show their text and animation (M4 below is implemented through the game's scripts; the draft table is a seeded estimate until each draft is learnt through a route).

Evidence-bound work (do only when it changes what the user sees):
- Draft names: Silence (0x4E/0x4F) and Shaping (0x50/0x51) follow the manual order; Sharpening's two verbs are not told apart. Verb labels outside known ranges show as `Verb 0x..`.
- Glossary growth: story bits not set by a catalog scene show as `bit[0x...]`; actor names are scoped by scene; "Used by" lists literal references only; some operations keep a raw line in the readable view.
- 17 gallery costumes point at no data (no script uses them); they need a story order the data does not give.
- Stored-text coverage: Room 70 locals 203 and 204 are truncated; unreachable code is not scanned.
- Engine gaps, only if a route needs them: `stopObjectScript`, inventory object code, `killScriptsAndResources` ending other room-owned scripts at a room load; the Room 69 difficulty and draft answer are assumed, not simulated; the Room 03 town and cemetery labels are unidentified. (The earlier `expression` blockers are gone: Blockers reports 0 blocked random sites.)

Housekeeping:
- Done 4 October: stale harnesses removed (35) and three repaired, `Exe/` shrunk from 27.6 GB to about 1.4 GB (evidence packed into `Exe/_evidence-archive/`). See the harness review. The 5 skipped diagnostic unit tests stay as audit utilities.
- 2 nullable build warnings at `MainWindow.xaml.cs:3691-3692`.
- `README.md` still describes the earlier scope and UI; refresh when the other-versions work starts.
- 17 historical git worktrees under `.claude/worktrees/` and the open proposals in [CLEANUP_REVIEW.md](CLEANUP_REVIEW.md).

## M5. Secrets and guide (parked by the user, 4 October)

Build a discoverable, spoiler-aware index of secrets from verified triggers and outcomes, with source links. The 235 audited cutscene sites, the story-event bits and the draft table are the raw material; every entry must carry its evidence level.

## Next phase: support for other versions (agreed with the user, 4 October)

LOOM EGA Spanish is the working reference, and the old-v3 dialect switch already exists (`ScummScriptDialects.For(GameId)`; Indy3 EGA differs in four opcodes). Local data for other games sits in `Games/` (`IndyCru/EGA` English, `indy3/EGA` Spanish, `monkey1`, `mmansion`); only LOOM and Indy3 EGA are supported today (Indy3: backgrounds, texts and dialogue choices). A sensible order, each step small and visible:

1. **Separate the LOOM-specific from the generic.** Core types named `Loom*` hold both. Mark which parts depend on LOOM data (`LoomEgaStateFlags`, `loom-glossary.json`, `loom-replays.json`, `LoomDraftNames`, the Game Status flag list, the catalog and its setup planner) and which are generic SCUMM (the executor, expression evaluator, actors, costumes, camera, walk boxes, text extraction, audio). Do this by documentation and small renames only where it pays; no behaviour change.
2. **Second language/edition of the same game.** Open an English EGA LOOM (and Indy3 English, which the tests already expect in `Games/IndyCru/EGA`) with the existing code and see what breaks: the catalog, glossary and tests carry Spanish names.
3. **Indy3 EGA as the second explorable game:** object art, room scripts and the interaction path on top of the existing dialect support, using the Indy3 conversations plan ([INDY3_CONVERSATIONS_PLAN.md](INDY3_CONVERSATIONS_PLAN.md)).
4. **Later containers and dialects** (other LOOM editions, Monkey Island, Maniac Mansion): the resource format and opcode set must be assessed from the data first; none of that is decoded yet.

## Evidence rules

- Tie every exit, action, state change, message, cue, animation and scene to the resource, script and instruction behind it.
- A decoded branch or room load is a **possible** outcome. Calling a verb directly is an **exploration action**. A partial session is a **state preview**. Label estimates, assumed input and timing assumptions.
- Messages, actors and playback are game behaviour when reached events drive them. A substitute (a UI font, a placeholder actor, a skipped wait) is labelled as presentation, not the game's behaviour, and is optional.
- No room-specific hacks or invented states. Rooms show no gaps and their first-visit state. When in doubt, pick at random and label the pick. Room hotspots navigate between rooms.

Status words: `verified` means source-backed and demonstrated in the app; `preview` is intentionally partial; `unresolved` means missing evidence or execution; `deferred` is later scope.

## Records

- [Changelog](CHANGELOG.md): every finished step, with its validation.
- [Plan history](PLAN_HISTORY_2026-10.md): the dated checkpoints moved out of this file.
- [Script map](LOOM_SCRIPT_MAP.md): the reverse-engineering reference.
- [Object states](SCUMM_OBJECT_STATES.md): the rendering and state contract.
- [Harness review](HARNESS_REVIEW_2026-10-04.md): what passes, what is stale, what can go.
- Historical: [research](RESEARCH.md) (23 Sep), [script coverage](LOOM_SCRIPT_COVERAGE.md) (25 Sep), [exploration audit](EXPLORATION_AUDIT.md) (25–26 Sep), [rendering and Indy3 audit](AUDIT_2026-09-27.md) (27 Sep), [cutscene discovery](LOOM_CUTSCENE_DISCOVERY.md), [readable scripts plan](READABLE_SCRIPTS_PLAN.md).
