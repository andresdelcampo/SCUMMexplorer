# LOOM EGA exploration plan

Rewritten 4 October 2026, then cleaned of finished work the same day (M5 brought back in scope; other-versions order agreed with the user). The dated checkpoints that used to fill this file are kept unchanged in [PLAN_HISTORY_2026-10.md](PLAN_HISTORY_2026-10.md), including the state table. Finished work is in the [changelog](CHANGELOG.md); what the decoded scripts, variables and rooms do is in the [script map](LOOM_SCRIPT_MAP.md). The test and harness audit of 4 October is in [HARNESS_REVIEW_2026-10-04.md](HARNESS_REVIEW_2026-10-04.md).

## Product direction

Make the DOS *LOOM* EGA release (English data in `Games/loom ega en` is the reference; Spanish is kept for comparison) a place to explore. Rooms and their original artwork lead. Clicking things does what the game does: Bobbin walks over, looks, speaks and uses things, drafts change the world, and cutscenes play with their actors, text and music. Source details (IDs, bytecode, uncertainty labels) stay available on demand in Advanced mode. The last piece is a spoiler-aware guide to secrets built from verified triggers (M5).

The user's three priorities of 28 September (object interactions, cutscenes and animation, on-screen text) are delivered, as are the English migration and the two demos.

## What is really left for LOOM EGA

**1. M5, the secrets guide** (back in scope, 4 October). The only unbuilt feature. See below.

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

## M5. Secrets and guide (in scope again, 4 October)

Build a discoverable, spoiler-aware index of secrets from verified triggers and outcomes, with source links. Raw material: the 235 audited cutscene sites (`loom-replays.json`, `LoomSceneSetupPlanner`), the 90 story-event bits and the glossary, the draft table, and the script map. Every entry carries its evidence level (`verified`, `preview`, `unresolved`).

Proposed steps:
1. **Decide what counts as a secret** (user decision, below) and which of the 235 sites, story bits and objects qualify.
2. **Entry data** (Core, generated and tested): per entry the trigger (room, object, verb, state needed), the outcome (scene, bit, draft, item), a spoiler level, the evidence level and the source links (resource, script, instruction). Derive from the catalog and glossary, never from byte offsets or Spanish numbering.
3. **Guide window** (App): entries hidden by default and revealed one by one, grouped by room and kind; progress read from Game Status where the bit is known; each entry offers "Go to room" and "Play cutscene" through the existing launchers. Technical source details Advanced-only.
4. **Verification**: unit tests for the entry builder; one UI harness shot of the window; record under `.verification/m5-guide/`; changelog row.

Open decisions for the user: what "secret" means (hidden cutscene, optional draft, alternate outcome, easter egg, or all of these); whether the guide shows spoiler levels or only reveals on request; whether it lists unseen content or only what the session has already triggered.

## Next phase: other versions (agreed 4 October; starts after M5)

Done: English LOOM EGA as the reference edition, and the two LOOM demos. Local data: `Games/indy3 ega en` (the tests expect it), `indy3 ega es`, `indy3 vga es`, `indy3 demo en`, `monkey1`, `mmansion`, plus `Games/FUTURE/`. There is no LOOM VGA data yet.

1. **Separate the LOOM-specific from the generic.** About 40 `GameId.Loom` gates in `MainWindow.xaml.cs`; propose which become generic before changing them. `Loom*` Core types hold LOOM-only parts (`LoomEgaStateFlags`, `loom-glossary.json`, `loom-replays.json`, `LoomDraftNames`, the catalog and setup planner) and generic ones (executor, actors, costumes, camera, walk boxes, text extraction, audio). No behaviour change; check on LOOM.
2. **Indy3 EGA as the second explorable game**, scoped to backgrounds, texts and dialogue choices (verbs 120–125), using [INDY3_CONVERSATIONS_PLAN.md](INDY3_CONVERSATIONS_PLAN.md).
3. **LOOM VGA (CD, SCUMM v4) and later containers** (Monkey Island, Maniac Mansion): needs the data in `Games/` first, then a feasibility probe of the resource format and opcode set. None of it is decoded.

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
