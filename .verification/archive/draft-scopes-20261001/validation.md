# Nested scopes and remaining routes, 1 October 2026

Main checkout, continued after sequence checkpoint09074e7. No game files, actor positions, class flags or draft prerequisites written by the route probes. `DraftProbe` adds generic directory-centre targeting and native verb-area note inputs to the read-only route diagnostic actions. Full traces/images and decoded source excerpts remain ignored under `Exe/DraftScopeSession-20261001/`; compact result digests and reproducible helpers are tracked here.

## Source and generic correction

Room41 local204 +0x0000 starts local213, then opens its own scope at +0x0006. Local213 opens at +0x0000, waits on D67/D66, and ends at +0x00A9. The prior executor resumed204 even though213's nested start froze it;204 then froze213 and eventually removed it on departure. This produced unmatched boundaries. ScummVM [`runScriptNested`](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/script.cpp) resumes a caller only while its slot remains the same and unfrozen. `runAllScripts` keeps all slots accessible to freeze/unfreeze and running queries.

Core now suspends the caller after a nested event freezes it, retaining its next instruction. Slots still awaiting their turn in the current frame remain visible to freeze/unfreeze and `isScriptRunning`; they previously disappeared from `_pending` until executed. Both changes are generic. No boundary, unfreeze or completion is inserted. `LoomNestedFreezeTests` checks that a parent's sentinel cannot execute until the real source hook unfreezes it.

Fresh direct633/0x41 now ends213 before204 starts its scope, then ends204 +0x031E and090 +0x0730, final43. `routes-2.json` preserves the prior source-staff/direct-response mismatch; `routes-4.json` records the corrected four-begin/four-end route including staff acquisition. Intermediate `routes-3` fixed the executing parent but left upcoming frozen slots invisible; it is not authoritative. `source.tsv` records decoded instructions for the draft dependencies and local scopes.

## Promotion and fresh audits

All363 literal roots rerun against the same `native-roots-1.json`/code hash0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569. Authoritative ignored packet: `final-audits-1/literal/audit.json`. Results330 validated,4 incomplete,19 no-start,10 brief scopes. `audit-comparison.json`: no old completed root regressed; all old memberships unchanged;133 frame counts changed under correct nested scheduling.

Three newly completed previews:

| Start | Frames | Reached rooms | Scope order |
| --- | --- | --- | --- |
| Room24 object432 look0x36 |421|24|204 opens,215 twice opens/ends,204 ends +0x0080|
| Room41 object633 response0x41 |1729|41,42,43,60|213 opens/ends,204 opens/ends,090 opens/ends|
| Room41 object633 response0x4B |1723|41,42,43,60|same order|

Established four memberships rerun, then literal catalog exported using the existing pipeline; `promote.py` asserts identical source fingerprints, existing titles and memberships. SequenceAudit rerun; `export_sequences.py` merges the two sequence recipes with generated counts. Final shared catalog **334 starts =84 scenes/250 reactions**; literal audit **168/235 sites,50 outstanding owners,123 unresolved edges**. Two completed literal roots overlap established starts; the two longer sequences remain distinct audited recipes. Fresh053/087 entry gaps remain visible.

Sequences still complete:052/053701 frames;086/0872930 frames (formerly2237); boundaries and memberships unchanged. `sequence-audit-summary.json` hashes the fresh full packet. The generator derives group counts rather than relying on the prior fixed inventory.

Core256/256 before the separately implemented status feature;260/260 including it. RoomAudit76 zero gaps (previously three under the incorrect nested ordering), all76 historical pixel hashes identical; no PNG refresh. Fresh shared WPF: global21/21, room20/20, doorways16/16, controls19/19, Input18/18, audio10/10. Blockers zero blocked random sites. Both full sequences rerun on normal clock12/12 (including departure/skip), screenshots inspected. Room41 seven assertions passed before the later instant-view harness failure; separate corrected Room24 check2/2. `check-summary.json` records report hashes, including the partial failed harness report explicitly.

## Actual input and late routes remain unresolved

Source Room9 object271 use acquires the staff through the object's code; no forced D61 write. Native Room41 target633 and draft inputs still select no aimed object because633 is Untouchable/class24; var067 remains0 and script013 supplies the aim-first response. `routes-4.json` keeps both attempted draft slots and the successful **direct response** separate. This promotion does not claim actual draft casting or original-game prerequisites.

`late-1.json` rechecks the source095/staff656-look/655-use/092/639-use route after the scheduler fix. It reaches096 but actor4's seeded movement still leaves Room45 local continuation +0x0049 possible after +0x0046; scopes four/three, final43, no promotion. Fresh43 use and staff-without095 controls still differ. No movement/class/prerequisite injection. Remaining fresh bounded failures are18/370look,20/390response0x48,20/391response0x48,38/610look. The longer087 sequence does not establish its fresh-entry route.

## Visible checks and reproduction

`ScopeUiCheck` runs Room41/0x41 on the normal Dispatcher clock and the other two new previews instantly. It checks literal boundary balance,213-before204 ordering, visible final room and restored controls. `ui-1` passed seven Room41 assertions (both responses/final43 screenshots), then timed out on the Room24 **view**: instant mode never shows an intermediate frame, and a recipe ending in its start room does not ask a different previously displayed room to load. The corrected instant harness first displays Room24, matching historical instant checks. It waits for the production start guard, which permits only idle arrival polling, and still checks balanced scopes. No production completion is forced. `ui-2` caught a reflection typo (guard is a property); `ui-3` confirmed the same instant-view mismatch and was stopped by the agent after diagnosis (only its verified ScopeUiCheck process). `ui-4` rebuild hit that own harness lock; `ui-5` is the fresh Room24 check. No user app stopped.

Run from the main checkout with RTK:

```powershell
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/draft-scopes-20261001/run-audits.ps1 -Output <fresh-output>
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/draft-scopes-20261001/run-ui.ps1 -Output <fresh-ui>
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/draft-scopes-20261001/run-regressions.ps1 -Output <fresh-regressions>
```

Global/room inventory expectations334/84/250/50. Doorway rooms2/3/4/8/10/37, controls, input, audio and Blockers rebuilt before use. Synthetic WPF input/layout/media validation; no physical-input or independent-listening claim. Historical PNGs change only if the actual pixels change. Unrelated audit work retained.
