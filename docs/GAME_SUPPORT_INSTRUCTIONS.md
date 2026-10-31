# Game support instructions

How work on a game is done, the same for every game: the rules, what "done" produces, how agents work, which ScummVM sources to read, and where things are written. *What* the app must do is in [REQUIREMENTS](REQUIREMENTS.md); *what is open now* is in [ROADMAP](ROADMAP.md). Rewritten 31 October 2026 for the explorer scope (R-1.14); the stage playbooks S1 to S9 of 14 October, still followed by the Maniac/Zak track, are kept in [archive/GAME_SUPPORT_INSTRUCTIONS_STAGES_2026-10-14.md](archive/GAME_SUPPORT_INSTRUCTIONS_STAGES_2026-10-14.md).

## 1. Authority

1. **Order of authority:** REQUIREMENTS (the user's decisions) > this file (how) > ROADMAP (what now). A step that would contradict a requirement is stopped and logged (§4.4); a missing requirement is written as a `Proposed` row (§4.5).
2. **Unit of work = one game × one of the ten features** of the ROADMAP (rooms, costumes, sounds, scripts, cutscenes, interactions, room sounds, hotspots/doorways/depth, game state, readable scripts). A unit ends with the feature working in the app, a test that would fail if it stopped working, an evidence record and one commit.
3. **Standing authorization:** everything this file describes (new `Proposed` rows, commits, fast-forward merges to `master`) is done without asking; decisions follow §4.5 and are reported at the end.

## 2. Ground rules

| Rule | Requirement |
|---|---|
| The product is an explorer: any room's interactions and cutscenes can be picked at any time, without playing to them; the game's rules never forbid a pick. Playing through the game's own interface comes last. | R-1.14, R-8.35 |
| Evidence exists only behind **Advanced**. Normal mode never shows status, validation or "partial" notes. | R-1.1, R-1.10, R-1.13 |
| Game data, ROMs and saves are never committed or shipped. | R-1.2 |
| Rooms show **no gaps** and their **first-visit state**; where the data does not decide, pick at random and label the pick. | R-1.4 |
| First visits and Play never force state. A **picked** scene or interaction may: teleport the hero, dress actors, give items, set the flags and object states its own code tests, bring an object into the room; derived generically from its scripts and listed in Advanced. No room-specific hacks. | R-1.9, §8.6 |
| Every exit, action, state, cue or scene is tied to a resource, script and instruction; the words *possible outcome*, *exploration action*, *state preview*, *estimate*; names carry an evidence level. | R-2.1 to R-2.3 |
| Source execution, prepared checks and original-game observation are different evidence; a scene beginning does not prove correct actors, prerequisites or timing. A feature is ✅ only when it works in the app. | R-2.4 |
| Code takes `IScummDataSource` / `ScummProfile.For(game)`, never branches on the game; code goes in the lowest layer true for it (`Scumm` cannot see `Loom`). | R-3.6, R-16.1 |
| Editions differ in **data**: derive ids, bits and offsets from the edition's own scripts; check only plausible differences, reusing `.verification/editions-20261005`. | R-14.2, R-14.4 |
| Tests filtered to the change; one targeted window check per change; harnesses use `SCUMM_EXPLORER_PROFILE_ROOT`; `%LOCALAPPDATA%\SCUMMexplorer` is never deleted; never `git stash`. | R-15.3, R-16.3 to R-16.5 |
| One commit per finished task, explicit paths, `Games/` never added. All shell commands go through RTK. | R-16.2, R-16.6 |

## 3. Reference

- **Family**: a `GameFamily` (one row of `GameSupport.Families`, the matrix of REQUIREMENTS §3.1, generated; regenerate with `SCUMM_UPDATE_SUPPORT_MATRIX=1`). **Fixture**: the local game folder its tests use; a family without one cannot raise a level.
- **ScummVM** `engines/scumm` source (GitHub, `raw.githubusercontent.com`) is the ground truth for engine behaviour; the bundled build in `D:\Juegos\scummvm` for runtime comparison. Read the source before porting behaviour, and note the date in the record.

| Feature | ScummVM files |
|---|---|
| Index, resources, rooms | `resource.cpp`, `resource_v2.cpp`, `resource_v3.cpp`, `resource_v4.cpp`, `room.cpp` |
| Room pictures | `gfx.cpp` (`GdiV1`, `GdiV2`, `Gdi`) |
| Objects, walk boxes, text | `object.cpp`, `boxes.cpp`, `charset.cpp`, `string.cpp` |
| Costumes, actors, camera | `costume.cpp`, `actor.cpp`, `palette.cpp`, `camera.cpp` |
| Scripts by version | `script.cpp`, `script_v2.cpp`, `script_v5.cpp` |
| Verbs, input, sentences | `verbs.cpp`, `input.cpp`, `cursor.cpp` |
| Boot, variables, copy protection | `scumm.cpp`, `vars.cpp` |
| Sound | `sound.cpp`, `players/player_v2.cpp`, `players/player_v3m.cpp`, `imuse/`; AdLib in `audio/adlib.cpp` |

## 4. Procedures

### 4.1 What "done" produces

1. **The feature working in the app**, seen in the real window with a harness (`.verification/scene-setup-20261031/WindowCheck` and the others listed in `NEXT_SESSION_PROMPT.md`).
2. **A test** that fails if the claim becomes false (a registry level is not raised without one).
3. **Evidence**: `.verification/<topic>-<yyyymmdd>/README.md` in the [evidence format](templates/EVIDENCE_RECORD.md): what was run, the fixture, the numbers, the comparison, differences and uncertainties kept as written. Large captures are not committed.
4. **Docs**: a `docs/CHANGELOG.md` entry linking the record; decoded facts in the script map or engine file (§5); the finished item's lines removed from `ROADMAP.md` and `NEXT_SESSION_PROMPT.md` in the same commit.
5. **Requirements**: a wrong or missing requirement becomes a `Proposed` row and a mention to the user.
6. **One commit**: explicit paths, the attribution line, `Games/` untouched.

### 4.2 Choosing what to run

The unit tests named after the change, the family's tests, **one** window check. The full suite (about 6 minutes, with `SCUMM_SOUND_TEST_ROOT` and `SCUMM_INDY3_EGA_TEST_ROOT` set) before committing engine changes; LOOM's tests as the regression when shared code changes.

### 4.3 Working with several agents

- One game per agent, one feature at a time; each in its own git worktree. The agent owns `docs/<game>/`, its `.verification/` records and its `GameSupport.Families` row.
- Shared files (`GameSupport.cs`, conformance tests, generated tables, `ScummVars`, the executor, shared readers) change in small separate commits; generated tables are regenerated on conflict. Two agents never edit the same engine file at once.
- Agents do not ask the user; they decide (§4.5), log, and report.

### 4.4 Stopping rules (hard stops only)

Stop the item (not the run), record why, move on when: a step contradicts a requirement; the only fix is a room- or game-specific rule in shared code or a forced state outside a pick (R-1.9); the step needs an irreversible or outside-the-repo action (game data, the user's profile, force-push, `git stash`, publishing, the user's running applications); data is missing; two different attempts at the same blocker failed.

### 4.5 Deciding without asking

Where nothing decides, decide and record the decision (what was open, options, choice, why, how to undo). Preferences: (1) what the requirements already say for LOOM, extended to the game; (2) what ScummVM does; (3) the smaller, more reversible change; (4) less technical text in Normal mode. New requirements are `Proposed (<date>, <family>)` rows, binding until the user changes them.

### 4.6 Orchestrated runs

An orchestrated run (several agents) plans the items in a checkpoint file `docs/checkpoints/<yyyymmdd>-<topic>.md`, gives each agent the brief of Appendix A, integrates each result as it returns (review against §4.1, tests, fast-forward to `master`), and ends at a clean checkpoint: everything finished merged with tests green, unfinished items recorded with the reason, decisions, proposed requirements and user checks listed. It never continues past its checkpoint on its own.

## 5. Where things are written

**Do not create other files or headings for these facts.** The `docs/` root holds five files: README, ROADMAP, REQUIREMENTS, CHANGELOG and this one.

| What | Where |
|---|---|
| The current state: readiness, open work, the user's checks, the rules | `docs/ROADMAP.md` (only the current state; finished work leaves it in the same commit) |
| What the app must do | `docs/REQUIREMENTS.md` |
| Finished work, about two recent days | `docs/CHANGELOG.md`; older entries in `docs/archive/CHANGELOG_THROUGH_<date>.md` |
| Index of a game | `docs/<game>/README.md` ([template](templates/GAME_README.md)) |
| Facts about a game's scripts (boot, hooks, variables, bits, scripts, verbs, objects, actors, sounds) | `docs/<game>/<GAME>_SCRIPT_MAP.md` ([template](templates/SCRIPT_MAP.md)): one table per heading, every row with an evidence level, a source and a date |
| Engine facts true of a SCUMM version | `docs/engine/` (a section per version, each statement with its ScummVM source and date) |
| Evidence of finished work | `.verification/<topic>-<yyyymmdd>/README.md` with the harness beside it |
| Plan and result of an orchestrated run | `docs/checkpoints/<yyyymmdd>-<topic>.md` |
| Anything dated or historical (old plans, reviews, handoffs) | `docs/archive/` |

Rules: decoded facts live once (in the script map or engine file) and are linked, never copied; a fact about how SCUMM v`N` works goes in `docs/engine/`, a fact about what one game does goes in its script map; game folders are one per game, lower case (`maniac` covers classic and enhanced); `docs/loom/LOOM_SCRIPT_MAP.md` keeps its own format. Paths to game data name only a game's folder under `Games` (`D:\Juegos\Lucas`, or `SCUMM_GAMES_ROOT`); never create a `Games` junction in a worktree.

## Appendix A. The brief for an agent

> You work on **`<game>`**, feature **`<n>` (`<name>`)** of the ROADMAP, fixture `Games/<folder>`, in your own worktree. Read `docs/GAME_SUPPORT_INSTRUCTIONS.md`, the ROADMAP's lines for your game and feature, and the REQUIREMENTS rows they cite. You have the standing authorization of §1: do not ask; decide by §4.5 and record decisions. Write documentation only where §5 says. Stop an item only under §4.4. Item cap: `<budget>`; at the cap, stop at your next green commit. Deliver: the feature working in the app (a window check), a test, the record under `.verification/`, the CHANGELOG entry and the ROADMAP lines removed, one commit with explicit paths ending with the attribution line. Never touch `Games/`, the user's profile or running applications. Finish with a short report: what changed, numbers, decisions, blocked items, proposed requirements, user checks.
