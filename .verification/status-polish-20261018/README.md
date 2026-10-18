# Maniac/Zak Game Status refinement — 18 October 2026

Scope: the user's five picture/party/duplicate-item/layout corrections, all four supplied editions. Broader fidelity work is parked. Parent integrates three isolated workers: character eligibility/art, Zak source identities and serial actual-window acceptance. No game assets, profiles, ROMs or saves are modified; ScummVM is read as source only and never run. No junctions or worktree removal.

## Implementation and source evidence

- [Character API and tests](../status-character-art-20261018/README.md): native selected-party commitment, unique source costume fallback and private native frame initialization. Recipient snapshots are read-only display copies, not actor placement or earned story progress.
- [Zak identity audit](../zak-item-identities-20261018/README.md): six scene aliases excluded, 70 logical items per copy; separate native CashCards/shards/helmets/tickets/tokens remain. The preceding 76-object count was an overcount, corrected here. Canonical55's real native allocation has independent execution proof because the conservative main flow analyzer loses its asynchronous context.
- [Actual-window checks](../status-polish-window-20261018/README.md): real MainWindow/modal, native party/input routes, recipient images, compact32px controls, bottom alignment, grants/transfers/Undo/capacity and modal clock. Every WPF process runs serially with private profile/cache. Failed optional alternate-party lookups are preserved and do not establish a production failure or acceptance for untested kids.

The parent supplies the identity helper with source pickup/Pickupable targets, resolvable object verbs, positively known empty entries and a dialect-independent retained-code predicate. A candidate is removed only for a fully resolved pickup closure returning one other same-name retained item to Ego, without inventory effects or source-context changes. Unknown writes/calls, incomplete code, bounds, call recursion and identity chains/cycles fail closed. No production object-ID list or name deduplication is used. Advanced catalog notes retain alias mappings; Normal uses neutral stable ordinals for genuine homonyms.

## Verification

Run from `.worktrees/maniaczak-status-polish`. Parent focused run:82 passed/zero failed or skipped,2m24s. Final source `4294422` adds reviewed conservative source-table/complete-code guards. Matrix regeneration:3 passed/zero failed; solution build:zero warnings/errors,9.72s. Four serial actual-window processes pass; 67 captures retained locally and six inspected by parent. Required full suite: **1,474 passed, zero failed, five expected skips** (1,479 total), 17m 6s; all 49 new character/identity cases passed. No errors/timeouts/aborts. `status-full.trx` is retained locally in ignored `test-results/`. Master stayed `defbf54e` through the final result audit; only verification/evidence/records follow the tested production source.

```powershell
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~ScummInventoryCharacterArtTests|FullyQualifiedName~ScummInventoryAdminTests|FullyQualifiedName~InventoryCatalogTests|FullyQualifiedName~ScummZakInventoryIdentityTests|FullyQualifiedName~ScummManiacClassicStateTests|FullyQualifiedName~ScummManiacEnhancedStateTests" --logger "trx;LogFileName=status-focused.trx" --results-directory .verification/status-polish-20261018/test-results
$env:SCUMM_UPDATE_SUPPORT_MATRIX='1'
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~SupportMatrixTests
rtk proxy dotnet build SCUMMexplorer.slnx --no-restore -p:NuGetAudit=false --verbosity minimal
$env:SCUMM_SOUND_TEST_ROOT='D:\Juegos\Lucas\loom ega mt32 en'
$env:SCUMM_INDY3_EGA_TEST_ROOT='D:\Juegos\Lucas\indy3 ega en'
$env:SCUMM_MT32_ROM_ROOT='D:\Juegos\Lucas\mt32'
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore -p:NuGetAudit=false --logger "trx;LogFileName=status-full.trx" --results-directory .verification/status-polish-20261018/test-results
```

Registry: all four Status cells stay Full. This corrects their implementation under the user's clarified contract; it does not promote another feature or claim a full playthrough, original-game visual/timing equivalence, all-party window coverage or missing releases.
