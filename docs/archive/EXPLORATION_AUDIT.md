# Interactive LOOM exploration: implementation audit

This records the implementation audit and its verification results. For current product priorities and the next bounded work session, use the [LOOM EGA plan and progress tracker](../LOOM_EGA_PLAN.md). The checks below are historical results, not a new validation run during the product assessment.

Audited 25 September 2026 against the supplied DOS LOOM files in `Games/loom`.
The game data remains local. Counts describe this release and the current parser;
they are not claims about other SCUMM versions.

Source-review clarification, 25 September 2026: statements below about reached room loads concern verb execution. The current possible-doorway buttons instead call `OpenRoomDestination` to visit a candidate directly, bypassing the originating verb. This shortcut does not establish gameplay reachability. The new product assessment did not repeat the runtime checks below or reproduce the reported Room 03/17 defects.

## What works today

- All 76 indexed room backgrounds can be inspected with decoded local object
  images and a depth-mask overlay. Image clicks cycle stored alternatives sharing
  an exact rectangle. This is an asset inspection action, not a game verb.
- Indexed sounds have an on-demand PC speaker or AdLib player. Playback can be
  started from a sound resource in the tree or from the compact room panel
  without leaving the room. Room playback offers Play, Pause, and Stop and
  never starts automatically on a visit.
- The script reader and relationship index expose old-v3 bytecode paths,
  literal resource references, and explicit decode issues. A bounded interpreter
  applies some room-entry object-state operations and marks missing information.
- A persistent session carries known object states, positions, owners, global
  variables, and script-started sounds across selected rooms. It runs known
  startup and room script prefixes, can advance a paused frame, and labels
  unresolved state. A separate asset inspection mode remains available.
- Unresolved started child scripts no longer stop the caller's known object
  commands. Session rendering enforces decoded parent-state chains. If no
  artwork is visible after an incomplete entry run, the viewer uses labelled,
  reproducible artwork estimates from that room's nonoverlapping images;
  those estimates do not change the session's object states.
- The parser reads 675 verb entries from 243 of the 832 local object code records
  in this release. In session mode, a hotspot with one verb directly runs that
  decoded object script; multiple verbs offer a choice. Room-load instructions
  found in those scripts appear as possible doorways. Room 6 object 204 verb
  `0x38` reaches `loadRoomWithEgo` for Room 5, so that action opens Room 5.
  The destination no longer comes from a room-specific catalog entry.

## Why exploration is still incomplete

- **Room state:** All 76 rooms have an embedded entry script. An isolated bounded run
  completes without a reported limitation for 13; it reports partial or
  unresolved behavior for 63. For example, Room 6 stops on an unknown bit
  variable, Room 28 on an unknown ordinary variable, and Room 33 on an
  unmodeled class test. Even a locally completed entry run does not establish
  a first-visit state: startup scripts, persistent variables, child scripts,
  actor actions, waits, and the route taken to the room are outside this run.
  The persistent session attempts startup and room transitions, but startup
  reaches unknown engine variable `var[0x028]` before the global hook variables
  are established. Catalog-selected routes are also unverified, so these session
  snapshots remain explicitly partial.
- **Doorways:** All 675 decoded object verb entries have a bounded code path
  with no reported decode issues, but a room load found by static decoding is
  only a possible destination. The session changes rooms only when direct verb
  execution reaches the load. Input dispatch, actor walking, class-based hit
  selection, and some dynamic branches remain unresolved, so this does not
  establish which doorways a player can actually reach.
- **Room music:** The session follows reached `startMusic`, `startSound`,
  `stopMusic`, and `stopSound` commands across visits, including sound IDs
  supplied through known script locals. Its latest known music start is offered
  in the room panel, while sound stops remove it. Possible tracks from room
  entry and local object scripts, including literal child-script calls, are
  shown separately and can be previewed on demand. The supplied release's
  room-entry paths often stop on unresolved state before a music start, so
  these candidates must not be called the room's actual soundtrack. Natural
  completion and the game's audio mixing are not simulated.
- **Object clicks:** In-room image clicks cycle stored alternatives in both
  modes, including a painted room background when the index shows an empty
  possibility. Clicking the same area restores a hidden image. Artwork clicks
  take priority over overlapping doorway hotspots; doorway buttons remain
  available. The hotspot list uses decoded room ownership and
  parent-state requirements to offer verb scripts, which can change the
  persistent session state. Artwork cycling remains local to the displayed
  view. The engine's verb selection and walking sequence are not reproduced,
  and class-based hit selection is unresolved.
- **Coverage:** All 113 indexed scripts with valid resource lengths, all 76
  embedded room-entry scripts, and 75 room-exit scripts have zero reported
  control-flow decode issues in
  this release. Four indexed entries (0, 51, 55, 116) declare lengths past their
  container ends. The [per-script coverage report](LOOM_SCRIPT_COVERAGE.md)
  lists bytes visited by the decoder; unvisited bytes may be data or unreachable
  code, so the byte ratio alone is not a completeness verdict.

## Technical milestone history and remaining work

1. **Done — make script evidence trustworthy.** Expanded old-v3 decoding for
   the opcodes and inline strings encountered in this release. The reader
   traverses jump targets and both conditional arms, bounds cycles and invalid
   targets, and reports issues per path. The bounded interpreter follows known
   comparisons and local object-state tests while leaving unknown state
   unresolved. Tests cover the operand lengths and branch semantics against
   [ScummVM v3](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v3.cpp),
   [v4](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v4.cpp),
   and [v5](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v5.cpp).
   Each indexed script has a byte-coverage row; script relationships are labelled
   static candidates rather than executed actions. The four invalid index entries
   remain explicit. This milestone establishes decoder evidence, not game-state
   execution or verified reachability.
2. **Done — build a conservative game-state session.** Begin with index
   owner/state data and the known prefix of startup script 1. Retain known
   globals, object states and positions, owners, room, script-started sounds,
   and scripts paused at frame boundaries across visits. Report inventory only
   when the EGO actor is known. Run global and embedded exit/entry hooks in
   engine order and reached child scripts to stop, frame pause, or an unresolved
   operation. A snapshot can be called faithful only if its path is fully
   resolved. The UI supports reset, frame advance, and isolated inspection.
   The supplied release currently remains partial because startup, routes,
   actor behavior, and sound timing are unresolved.
3. **Partly done — resolve object actions and doorways.** The old-v3 local verb
   tables are decoded, hotspot clicks offer their scripts, and direct verb
   execution updates the persistent session. Navigation occurs only after a
   reached room load. The UI shows the source object, numeric verb, code
   offset, destination, and uncertainty; Room 6 object 204 is a regression
   example rather than a hardcoded rule. Completing this milestone requires
   executing or conservatively resolving the game's input dispatch, verb
   selection, actor walk, and class-based hit testing. Until then, direct verb
   invocation is a clearly labelled exploration action, not proof that the
   player can perform it from the displayed state.
4. **Done — keep music in the room.** Reached start/stop commands update a
   route-scoped cue; known variable and local sound targets are resolved during
   bounded execution. The room player reuses the existing PC speaker and
   AdLib renderers with Play, Pause, and Stop, without automatic playback.
   Static room and child-script paths are labelled as possibilities and stay
   separate from reached cues. Technical script evidence is in tooltips and
   advanced details. Natural track completion remains outside the session.
5. **Add route inspection.** Record a reproducible breadcrumb of room visits,
   clicks, state changes, and script evidence. Let users save or compare
   snapshots so conditional rooms can be explored without implying a single
   universal first-visit appearance.

## Verification from this audit

- Core tests: 57 passed, including control-flow, old-v3 operand-length,
  small-header `drawObject`, persistent frame state, bit aliases, sparse-jump,
  state-conditional, signed relational, branch-direction, object-verb table,
  reached room-load, unresolved-branch, resolved local sound targets, and
  start/stop cue selection cases.
- Room cue release scan: static room/child-script traversal found playable
  music candidates in 27 of 76 rooms. Room 2's entry script passes `0x001F`
  to script 39, which could start Sound 31; the bounded visit stops earlier in
  script 16, so Sound 31 remains a possible cue rather than a reached one.
- Object verb release scan: all 76 rooms and 675 verb entries decoded with zero
  reported path issues; static decoding found 76 room-load instructions across
  the object verb scripts. These are possible instructions, not 76 verified
  navigable doorways. Directly invoking Room 6 object 204 verb `0x38` reached
  `loadRoomWithEgo` and moved the bounded session to Room 5; the Room 6 click
  point `(160,48)` selects that object's sole verb in the current indexed state.
- Release scan: 113/117 indexed script entries have valid resource lengths;
  all 113 decoded without a reported path error. All 76 embedded room-entry
  scripts and 75 room-exit scripts decoded without a reported path error. The
  indexed script decoder traversed 22,487 of 60,919 resource bytes after the
  v3 `drawObject` operand fix; the rest can include inline data or unreachable
  code. Of the 113 valid indexed scripts, 78 have every resource
  byte visited. These byte counts are not gameplay coverage.
- Session release check: Rooms 6 → 5 → 6 were selected without a reset. The
  session retained 1,000 indexed object states and owners, known positions,
  and accumulated uncertainty. Each snapshot remained labelled partial.
- WPF app: `dotnet build src/SCUMMexplorer.App/SCUMMexplorer.App.csproj --no-restore`
  passed with zero warnings and errors.
- Windows room-player check: opened the supplied LOOM folder and Room 2,
  selected possible Sound 31, and observed the playback timer advance to
  `0:09 / 3:00` while Room 2 stayed visible. Pause held the position and Stop
  reset it to `0:00`; visiting Room 3 showed possible Sound 25 at `0:00 / 0:00`
  without starting playback. Audible output was not independently checked.
- Executable: `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`.
- Earlier Windows click-through (before the decoded-verb change): launched the
  executable with the supplied LOOM folder, opened Room 6, and clicked its
  then-catalogued doorway. Room 5 opened and rendered. Back in Room 6,
  selecting object 208 showed its stored image and changed its control to
  `background`. Room 6's music panel showed that no `startMusic` command was
  reached in the bounded entry path. Audible playback and the new verb-choice
  UI have not been checked interactively.
