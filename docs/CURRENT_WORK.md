# Current work

Rewritten 4 October 2026 (the old route-priority and observer-hardening text, and an RTK command block that duplicated the global instructions, are gone; the route/observer history is in [PLAN_HISTORY_2026-10.md](PLAN_HISTORY_2026-10.md)). CLAUDE.md points here. The hand-off for the next session is [NEXT_SESSION_PROMPT.md](NEXT_SESSION_PROMPT.md); priorities, state and open items are in [LOOM_EGA_PLAN.md](LOOM_EGA_PLAN.md).

## Where the work stands

LOOM EGA exploration is functionally complete for the user's three priorities (object interactions, cutscenes and animation, on-screen text). 234 of 235 cutscene sites are audited as reached; the one left (Room 34 local200 +0035) looks unreachable. The next phase is support for other versions (see the plan). The checks of 4 October are in [HARNESS_REVIEW_2026-10-04.md](HARNESS_REVIEW_2026-10-04.md).

## Rules the user has set

Room state (3 October). Rooms start in the earliest game state. A room moves to a late state only after the user triggers the late cutscene that causes it (the session then carries that state on). No scene is chained after other scenes to show a later branch (the Room 44/652, Room 45/655, Room 41/633 question is closed: fresh branch). The staff rule for scenes outside the island (decided 3 October) is its own explicit exception: scenes whose code clears or reads bit D61 start with the distaff held through the game's own pickup (Room 09 object 271), run unseen, when the scene is outside the LOOM island (Room 11 on); island scenes keep the fresh state. Every assumption is shown in the picker note.

State control (3 October). The user controls the game state (Game Status: staff, notes, drafts, story events) and can change it freely. The app only raises the state to the minimum the chosen room or cutscene needs, and keeps a later state if the session already has one. A single cutscene picked from the menu keeps the running session (`PlayKnownCutscene`); `LoomSceneSetupPlanner.Adapt` drops the staff pickup when the session already holds it; stated sequences start fresh; Restart returns to the boot. Story-event checkboxes need no presetting: the real pickups and scenes set their bits. The LOOM-specific flag list is `LoomEgaStateFlags` (App); the window is generic.

Costumes (3 October). The ego keeps whatever costume a scene gave him when he moves to another room, unless that room's own code needs a different one. A catalog start has no previous scene, so it takes the costume the room's entry gives (audit: `.verification/setup-placer-20261003/audit-costume-entries.md`).

Cutscene starts. The picker derives a per-scene start setup from the scene's own code (`LoomSceneStateScanner`, `LoomSceneSetupPlanner`). No one-off teleports and no invented story flags.

Object interaction (28 September). A single click walks Bobbin to the object and uses it (scripts 005–007, verb 0x38; exits lead on); a double click also looks (0x36). Cycling an object's stored images belongs only to the inspection mode. Hiding an object is allowed only if the background behind it has no gap. Room hotspots navigate between rooms.

Interface (3 October). Technical text (traces, assumptions, raw bytecode, source details) belongs to Advanced mode and must be digestible there; normal mode shows the scene, the game's own text and a few clear controls. One short subtitle per window; no sentence that repeats another.

Testing. Run only the checks a change needs (filtered unit tests; UiShots, SceneShots or StatusShots for UI); the user retests at the end. The 4 October full run is the exception, done on request. Do not delete `%LOCALAPPDATA%\SCUMMexplorer` (saved game state) after harness runs unless asked.

Workflow. Commit each finished task separately, no push. Another session may work in the same tree: stage files by explicit path. Ask before editing CLAUDE.md.
