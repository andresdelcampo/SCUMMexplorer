# Cutscene discovery

Reconciled 8 October 2026. Session execution and replay discovery are separate: a reached source scene can work without a reviewed catalog recipe. Play Cutscene uses shared, fingerprinted descriptors and validated setup metadata. Current capability and remaining comparisons are in [CURRENT_WORK](../CURRENT_WORK.md), [the plan](../LOOM_EGA_PLAN.md) and [the gap dossier](FINAL_GAPS_HANDOFF.md). Earlier route-by-route logs are preserved in [the historical discovery record](../archive/LOOM_CUTSCENE_DISCOVERY_BEFORE_RECONCILIATION_2026-10-08.md).

## Established coverage and limits

The [8 October correctness audit](../../.verification/correctness-20261008/README.md) established 328 English EGA recipes and 222/223 VGA groups completing normally/with Escape. Those are dated execution results, not proof of every branch, actor picture, original-game route or audiovisual detail. The remaining standalone Room 34 local-200 fortress reveal is parked by the user.

Single scene selections preserve the running session; stated sequences start fresh. Catalog arrival, direct verbs, explicit staff/note preparation and seeded choices stay disclosed as preview assumptions. The user approved reviewed source prerequisites for direct late-room catalog selection on 8 October. The English Room 46 recipe now runs missing real scenes to establish Rusty's ghost, preserves existing ghost/restored state and cancels the visit if preparation fails. It is bound to the English source fingerprint. [Correction and validation](../../.verification/catalog-return-20261008/README.md).

## Reproducible promotion pipeline

Use a fresh ignored output directory and prefix every shell/child command with RTK. Recheck the current edition's resources and source boundaries; do not reuse historical hashes as current proof.

1. Create a fresh ignored output directory, then run `rtk proxy dotnet run --project .verification/fallback-evidence/CodeDump/CodeDump.csproj -- <game> <output>` to regenerate `code.tsv` and `objects.tsv`. Its declared resource boundary determines global/local scope.
2. `rtk proxy python .verification/replay-coverage-20261001/discover_replays.py <code.tsv> <roots.json>` follows literal callers transitively and preserves unresolved dynamic calls, local scope and cycles. Object/entry roots remain candidates.
3. `rtk proxy dotnet run --project .verification/replay-coverage-20261001/ReplayAudit/ReplayAudit.csproj -c Release -- <game> <roots.json> <audit-folder>` probes fresh post-boot seed-0 sessions through decoded verbs/entries. Require matching reached start/end boundaries, settled actor/camera/talk state and no unsupported execution. Its bounded preview is not a full game route.
4. `rtk proxy dotnet run --project .verification/room-replays-20261001/MembershipAudit/MembershipAudit.csproj -c Release -- <game> .verification/room-replays-20261001/established-starts.json <established-membership.json>` records observed room membership for the reviewed historical setup recipes.
5. `rtk proxy python .verification/replay-coverage-20261001/export_catalog.py <roots.json> <audit-folder>/audit.json <game> src/SCUMMexplorer.App/loom-replays.json <report.md> <code.tsv> <established-membership.json>` exports reviewed completed recipes. Compare source fingerprints, metadata and coverage; validate representative global/room picks in WPF before committing.

**Catalog maintenance:** the historical exporter does not generate the new reviewed `catalogStarts` metadata. Preserve and revalidate that metadata when regenerating the catalog; replacing the JSON blindly would remove the Room 46 correction. Prerequisite recipes need edition fingerprints, reached source boundaries, later-state preservation, failure cancellation and visible validation.

Literal object/entry callers can be probed automatically. Position watchers, dynamic dispatch, prior inventory/choices and scopes that start but fail to finish require individual source-backed routes. Do not promote brief shared input helpers as independent story scenes, or a site merely because its owner contains a cutscene opcode.

## Historical discovery method

The following inventory records the original 30 September discovery snapshot. Its counts and assumptions describe that audit only; the current results above supersede its progress status.

### Original source-derived candidate inventory

The reproducible [discovery tool](../../.verification/archive/cutscene-discovery-20260930/discover.py) reads decoded `code.tsv`, indexes `cutscene` sites, literal `startScript`/`chainScript` callers, owner room commands and state reads. It keeps dynamic targets and unresolved local scope explicit. Run from the repository root:

```powershell
rtk proxy python .verification/archive/cutscene-discovery-20260930/discover.py .verification/fallback-evidence/code.tsv .verification/archive/cutscene-discovery-20260930/candidates.json
```

This snapshot has 235 sites across 216 owners. The [compact inventory](../../.verification/archive/cutscene-discovery-20260930/candidates.json.md) records its input hash. It is a discovery index, not reachability analysis: owner-wide room commands are possibilities, state reads are requirements to investigate, and transitive call chains do not establish a playable start. Dynamic script numbers, script arguments, object dispatch and caller room scope can require further resolution.

| Candidate | Decoded trigger | Membership / required state |
|---|---|---|
| 044, opening | Script 001 starts 044 after the intro answer | Existing validated boot stop; difficulty/draft answer assumed. Rooms 01, 02, 53. |
| 046, trial | Room 08 object 255 verb 0x38 starts 046 | Existing validated fresh-boot catalog/direct-verb start; scene rooms recorded in KnownCutscenes. |
| 052, transition | Room 10 object 295 verb 0x38 starts 052 | Existing validated start; Rooms 10, 11, 13. |
| 086, firewood | Room 37 local 200 +0x001D chains 086 after a walk-box change into box 8 | Existing reached arrival through Room 36 object 599, then assumed game click; catalog arrival into 37 alone remains estimated. Rooms 37, 58, 38. |
| 049, swans/draft reaction | Room 02 object 156 verb 0x3A starts 049 | Draft dispatch, object target, distaff and literal draft state matter. A direct invocation must not imply the draft was played. Existing draft evidence establishes one route; no new standalone descriptor added. |
| Room 02 local watcher / 045 | Frame watcher and actor walk-box conditions | Multiple transitions requiring reached position and input; not a fresh standalone scene per opcode. |
| 011 and similar helpers | Many callers, including note/draft scripts | Brief cutscene scopes around reactions/input suppression; not automatically independent menu scenes. |

### Promotion method

Generate candidates from current decoded resources, resolve literal/global/local call scope and argument bindings, then trace each candidate backwards to an object verb, room entry/watcher or boot instruction. Record the exact trigger, required state, assumptions, and reached room membership. Validate the start through a real session and focused playback before assigning a plain title and enabling replay. Runtime traces can record newly reached candidates for review, but cannot infer a safe reset/replay recipe alone.

Store promoted descriptors together with their evidence and validated setup. The global picker and Game Texts consume shared descriptors. The 30 September inventory is discovery evidence; subsequent dated audit packets record validated previews. Uncertain sites remain visible in Needs validation, with no fabricated start.
