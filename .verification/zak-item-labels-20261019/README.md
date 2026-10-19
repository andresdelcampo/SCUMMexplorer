# Source character qualifiers for Zak inventory choices — 19 October 2026

| | |
|---|---|
| Scope | ZakClassic and ZakEnhanced, Game Status repeated-name labels |
| Fixtures | `Games/zak ega en`, `Games/zakE ega en`, read from external `D:/Juegos/Lucas` |
| Baseline | `ce405e7b`, isolated `.worktrees/zak-item-labels-20261019` |
| Support levels | Unchanged; this improves readable labels and preserves70 logical inventory identities per copy |
| Parent ownership | App presentation, real-window checks, requirements/maps/checkpoint/matrices and integration |

## What changed

The catalog now exposes `Context: ScummInventoryItemContext?` with `ActorId`, nullable `Text` and `Evidence`. The actor association comes from this edition's code; each captured snapshot resolves `Text` through this session's native actor-name table. It is a **source character association**, not the current inventory owner, a declaration of original physical ownership or a restriction on admin transfers. Current held ownership remains a separate field. Missing names remain null so the UI can retain a neutral choice; no English name is fabricated or frozen in the cached catalog.

The new source analyzer reuses the existing bounded inventory-flow analysis. Its optional owner-write callback resolves both target and recipient from the same source state. Callback-null behavior remains unchanged. Literal native recipient writes establish fixed source associations; recipient-dependent selection establishes distinct per-character inventory records. The latter is accepted only when compared native player arguments select unambiguous different members of the same name group. Unknown writes, missing source, incomplete decode, ambiguous assignments and bounded dynamic paths do not establish that mapping. Only completely and uniquely distinguished same-name groups receive qualifiers; a lone handoff is insufficient.

Both copies independently produce these ten qualifiers:

| Native group | Source character association |
|---|---|
| CashCard18 /32 /75 | Zak / Annie / Leslie, from each native Give response's literal return recipient |
| helmet65 /66 | Melissa / Leslie, from Script81's native initial allocations/owner assignments |
| ticket82 /83 | Zak / Annie, from Script58's separate native ticket-record assignments |
| token61 /62 /63 | Zak / Melissa / Leslie, from Script145's recipient argument selecting a distinct token-count inventory record before +0085 `setOwnerOf` |

These names in the table are observed outputs of each copy's native actor-name table, not production labels. Ticket qualifiers identify source ticket records; they do not assert a fixed destination or prohibit changing the held owner. Token qualifiers identify the game's per-character count records, not arbitrary individual coins. Both shard33 and shard56 keep neutral fallback: one scripted Zak handoff does not establish a distinct two-character role for the halves. The six previously excluded scene aliases remain excluded and the logical roster stays70. See the preceding [identity evidence](../zak-item-identities-20261018/README.md) for those source identities.

No gameplay bytecode runs to produce labels. No runtime owner, variable, actor, slot or story flag is edited by the analysis or snapshot resolution. Tests deliberately reassign all ten contextual items to other characters and verify that source qualifiers stay unchanged, then Undo the exact inventory/unrelated state. The analyzer has no item/room/actor-ID label table and no hardcoded character names. Synthetic shifted actor/item IDs exercise the same mechanism.

## Commands and results

All commands use RTK, from the isolated worktree:

```powershell
rtk proxy dotnet run --project .verification/zak-item-labels-20261019/Probe/Probe.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter FullyQualifiedName~ScummInventoryItemContextTests --logger trx --results-directory .verification/zak-item-labels-20261019/test-results --verbosity minimal
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter "FullyQualifiedName~ScummInventoryItemContextTests|FullyQualifiedName~ScummInventoryCatalogAnalysisTests|FullyQualifiedName~ScummInventoryAdminTests|FullyQualifiedName~ScummZakInventoryIdentityTests|FullyQualifiedName~ScummInventoryIdentityClosureTests|FullyQualifiedName~LoomGameStatusTests" --logger trx --results-directory .verification/zak-item-labels-20261019/test-results --verbosity minimal
```

- Own-copy read-only probe: both catalogs70; exactly ten contexts each; four native names resolved; both shards unqualified. Outputs `audit.txt` and `after.txt` are ignored locally. Probe build zero warnings/errors.
- New dedicated tests: **21 passed, zero failed/skipped**,45 seconds; build zero warnings/errors. Two real copies independently verify exact identities/source actors, names, neutral shards, unrelated-state preservation, transfer/Undo stability and late native-name refresh. Synthetic positives/negatives cover shifted IDs, ambiguity, incomplete/unknown source and optional owner-mode parity.
- Broader focused regression bundle: **121 passed, zero failed/skipped**,1 minute58 seconds; build zero warnings/errors. It includes the21 new tests plus existing catalog flow, all-four-family admin contracts, Zak native identity and LOOM EGA/CD status. Counts overlap and are not additive. Final local TRX: `test-results/adelc_DARKHERO_2026-10-19_10_10_40.trx`.

No WPF window, ScummVM executable, game/profile/save/ROM mutation, junction, worktree removal, stash, force or external publication was used. Original-game observation and subjective audiovisual checks are outside this read-only label task. This is decoded/source-flow and native Core-contract evidence, not an earned acquisition journey or complete story-state proof. No new engine opcode or gameplay execution behavior is changed. Parent performs App/full-suite integration.

## Independent review: called-source proof barrier

Read-only review found that a complete mapping root did not certify its called source. The prior inventory census could skip a missing or partially decoded helper and resume with stale symbolic record/recipient bindings. That behavior cannot justify a character qualifier. In the optional owner-observer mode, dynamic/missing/null/incomplete global or object calls now emit `Unresolved owner-flow:` and stop that proof path. Nested failures, repeated/recursive contexts and analysis bounds propagate to the caller before any later owner event. Incomplete helpers cannot contribute their parsed prefix writes. An ordinary immediate sentence with an unknown new other-object argument cannot retain the old argument. The conditional mapper rejects an affected prepared argument; a fixed literal recipient also cannot use a stale symbolic item after an unresolved call. Earlier fully decoded prefix writes remain source evidence.

The callback-null inventory census retains its existing behavior, with explicit missing/incomplete/nested/recursive parity regressions; no gameplay executor is changed. The first guard run passed33/35 and exposed two additional new nested-missing regression failures: relevance pruning still omitted a missing-only helper. That observer-only pruning edge was corrected. Failed TRX `test-results/adelc_DARKHERO_2026-10-19_10_28_52.trx` is retained locally and is not acceptance.

Final dedicated guarded run: **37 passed, zero failed/skipped**,35 seconds, build zero warnings/errors; TRX `test-results/adelc_DARKHERO_2026-10-19_10_30_27.trx`. All ten native qualifiers remain in each70-item roster; no guard was loosened to preserve them. Final existing-flow/all-four-family-admin/Zak-identity/LOOM-EGA-CD-status bundle: **137 passed, zero failed/skipped**,1 minute20 seconds, build zero warnings/errors; TRX `test-results/adelc_DARKHERO_2026-10-19_10_32_07.trx`. It includes the37 dedicated tests, so counts are not additive. Independent reviewer reports no remaining actionable issue in this scoped proof path. Parent retains final full-suite/UI/integration ownership.
