# Cutscene discovery

The session executes reached script/object/room instructions independently of replay discovery. Until 1 October, replay used only `KnownCutscenes` (046, 052, 086) plus the opening (044), further filtered to the current room. This explains why a triggered scene could work yet be absent from Play Cutscene: it needed no new executor implementation, but had no catalog recipe.

## Current replay coverage (1 October)

Play Cutscene now opens a global searchable picker: **75 scene starts and 216 dialogue/reaction previews, 291 starts total**. These are explorer start recipes, not 291 distinct narrative scenes. The original four starts retain their validated setup. Another 287 use the same generic fresh-session catalog/direct-verb playback path. A direct draft response does not simulate casting that draft or establish its original-game prerequisites. The same plain descriptor links uniquely reached story scripts in Advanced Game Texts; shared helpers and ambiguous multi-start scripts do not inherit an arbitrary replay.

The [audit](../.verification/replay-coverage-20261001/audit.md) probes 363 source-derived object/entry roots: 289 completed previews (two already in the old catalog), 19 without a cutscene reached, 45 incomplete and 10 brief input-suppression scopes. Completed previews reach **139/235 cutscene sites**. **79 candidate owners have outstanding sites**, shown in Needs validation with disabled Play. The original manually validated firewood start requires its subsequent click and remains available separately; the automatic verb-only probe does not replace it. Full-game replay is not complete.

`loom-replays.json` is an embedded, generated catalog, not an interpreter. Its per-LFL SHA256 fingerprints must match the loaded game before expanded recipes appear. Other editions keep the four established descriptors and show that expanded validation is required. No game files are bundled in the catalog. Titles use stored object names (CP437) plus plain action/room descriptors; absent names retain an explicit object identifier rather than inventing a story title.

### Reproducible promotion pipeline

From the main checkout, use a fresh ignored output folder and RTK for each command:

1. Create an output folder, then run `.verification/fallback-evidence/CodeDump/CodeDump.csproj` against current game files to regenerate `code.tsv`. Global/local scope comes from the declared index boundary used by CodeDump; no fixed local-number rule is needed.
2. `python .verification/replay-coverage-20261001/discover_replays.py <code.tsv> <roots.json>` resolves literal start/chain callers transitively, preserving local room scope, cycles, unresolved calls and possible sites. Arguments remain decoded source operands; the executor binds them on the tested route. Object/entry roots are candidates only.
3. `dotnet run --project .verification/replay-coverage-20261001/ReplayAudit/ReplayAudit.csproj -c Release -- <game> <roots.json> <audit-folder>` creates a fresh post-boot seed-0 session for every root, visits its room and invokes only the decoded object verb or entry. No variable, class, inventory or position writes. The automatic probe requires a start within the normal 32-frame input/arrival window, matching reached start/end boundaries, no remaining scene/talk/ego/camera activity and no unsupported execution, bounded to 3000 frames. It logs assumptions and executed scene sources. It does not prove a full game route.
4. `python .verification/replay-coverage-20261001/export_catalog.py <roots.json> <audit-folder>/audit.json <game> src/SCUMMexplorer.App/loom-replays.json <report.md>` exports completed recipes, fingerprints and unresolved owner/site coverage. Validate the picker and representative replays in WPF before committing.

Current regenerated input hash: `0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569`. Historical discovery hash differs only in six `print height` labels previously called `left`; root, candidate/site and unresolved-edge definitions compare identical. The current executor was used for every probe.

### What is automatic, and what needs individual validation

| Case | Automatic result / remaining work |
| --- | --- |
| Literal object verb → global/local call chain | Discover and test the actual decoded verb with its own argument binding. Promote only the completed explorer preview. Includes the sky response / 049 and many later dialogue, draft and room-transition scenes. |
| Room entry reaching a scene in the arrival window | Probe automatically. None of the 19 no-start results is promoted solely because its owner contains an opcode. |
| Several callers / branches of one script | Keep distinct recipes and reached site coverage; do not equate one successful branch with every site. |
| Shared note/draft/input helper | Do not promote its opcode as an independent story scene. Brief scopes are excluded; longer object-specific responses go in Dialogue and reactions. |
| Position/watch-box trigger | Requires a source-backed input route and reached boxes. Room 13 local 201 changes walk-box → box 2 + touchability of object 325 → script 053; the catalog entry alone does not reach it. Firewood 086 keeps its individually validated verb/arrival/click recipe. |
| Dynamic script/argument/object dispatch or earlier inventory/choice | 123 static unresolved edges remain. Resolve caller scope and values on a reached route, then validate the resulting setup individually. A trace by itself cannot reconstruct a reset recipe. |
| Scene starts but does not finish | 45 bounded failures remain disabled. Investigate waits, actor movement, sound assumptions and earlier state separately. Example: Room 13 object 323 use starts 054 in Room 15, but the fresh preview does not complete; do not inject a position or prerequisite bit to force it. |

Replays deliberately retain the existing explorer policy: fresh state, estimated catalog arrival, direct verb activation and seed-0 choices. They are not labelled as input-accurate ScummVM gameplay. Core execution/rendering was unchanged. [Visible validation](../.verification/replay-coverage-20261001/validation.md).

## Source-derived candidate inventory

The reproducible [discovery tool](../.verification/cutscene-discovery-20260930/discover.py) reads decoded `code.tsv`, indexes `cutscene` sites, literal `startScript`/`chainScript` callers, owner room commands and state reads. It keeps dynamic targets and unresolved local scope explicit. Run from the repository root:

```powershell
python .verification/cutscene-discovery-20260930/discover.py .verification/fallback-evidence/code.tsv .verification/cutscene-discovery-20260930/candidates.json
```

This snapshot has 235 sites across 216 owners. The [compact inventory](../.verification/cutscene-discovery-20260930/candidates.json.md) records its input hash. It is a discovery index, not reachability analysis: owner-wide room commands are possibilities, state reads are requirements to investigate, and transitive call chains do not establish a playable start. Dynamic script numbers, script arguments, object dispatch and caller room scope can require further resolution.

| Candidate | Decoded trigger | Membership / required state |
|---|---|---|
| 044, opening | Script 001 starts 044 after the intro answer | Existing validated boot stop; difficulty/draft answer assumed. Rooms 01, 02, 53. |
| 046, trial | Room 08 object 255 verb 0x38 starts 046 | Existing validated fresh-boot catalog/direct-verb start; scene rooms recorded in KnownCutscenes. |
| 052, transition | Room 10 object 295 verb 0x38 starts 052 | Existing validated start; Rooms 10, 11, 13. |
| 086, firewood | Room 37 local 200 +0x001D chains 086 after a walk-box change into box 8 | Existing reached arrival through Room 36 object 599, then assumed game click; catalog arrival into 37 alone remains estimated. Rooms 37, 58, 38. |
| 049, swans/draft reaction | Room 02 object 156 verb 0x3A starts 049 | Draft dispatch, object target, distaff and literal draft state matter. A direct invocation must not imply the draft was played. Existing draft evidence establishes one route; no new standalone descriptor added. |
| Room 02 local watcher / 045 | Frame watcher and actor walk-box conditions | Multiple transitions requiring reached position and input; not a fresh standalone scene per opcode. |
| 011 and similar helpers | Many callers, including note/draft scripts | Brief cutscene scopes around reactions/input suppression; not automatically independent menu scenes. |

## Scalable promotion

Generate candidates from current decoded resources, resolve literal/global/local call scope and argument bindings, then trace each candidate backwards to an object verb, room entry/watcher or boot instruction. Record the exact trigger, required state, assumptions, and reached room membership. Validate the start through a real session and focused playback before assigning a plain title and enabling replay. Runtime traces can record newly reached candidates for review, but cannot infer a safe reset/replay recipe alone.

Store promoted descriptors together with their evidence and validated setup. The global picker and Game Texts consume shared descriptors. The 30 September investigation below was discovery only; 1 October adds the validated previews described above. Uncertain sites remain visible in Needs validation, with no fabricated start.
