# Zak inventory identities — 18 October 2026

User report: the Game Status catalog repeats several item names. Both supplied copies (`zak ega en`, Classic v1; `zakE ega en`, Enhanced v2) were audited independently from their own data under `D:/Juegos/Lucas`. No ScummVM executable was run and no game/profile/save data was changed.

## Finding

The previous 76-entry catalogs contained **six scene representations** in addition to their actual inventory identities. They must not be offered as extra items. This corrects the prior catalog checkpoint's claim that all 76 names were distinct inventory items; proving that an arbitrary header could be administratively granted did not establish that it was a native inventory identity. The corrected catalog is **70 native items per supplied Zak copy**. Letter139 remains a scene container; its native contents are card27, unrelated to these six aliases.

| Native scene header / source | Native inventory identity / retained source | Source proof in both copies |
|---|---|---|
| blue crystal431 / Room25 | blue crystal55 / Room14 | Pickup14 +001F hides431; +0025 changes owner of55 to Ego. No pickup of431. |
| yellow crystal429 / Room25 | yellow crystal81 / Room44 | Pickup14 +0023 hides429; +0029 changes owner of81 to Ego. No pickup of429. |
| fuse471 / Room28 | fuse73 / Room30 | Pickup14 +0017 changes owner of73 to Ego, +001B/+001D hides the scene object, +001F queues Pull13 on470. It never allocates471. |
| ladder535 / Room33 | ladder70 / Room28 | Pickup14 +001F calls537's FD handler while preserving the original sentence identity. |
| ladder536 / Room33 | ladder70 / Room28 | Same source relay as535. |
| ladder537 / Room33 | ladder70 / Room28 | Native FD +0025 checks the canonical ladder's lock; +002D/+002F hides the original scene object; +0031 returns owner of70 to Ego. |

The index's Pickupable bit is insufficient: these scene headers have that flag and executable pickup handlers, but the handlers return a different retained item. The generic filter therefore requires a resolved pickup closure, an actual owner-to-Ego return, one unambiguous canonical target with the same native name, retained item code and no resolved pickup/positive-Pickupable target for the alias anywhere in the existing source census. It does not deduplicate names or use a room/ID denylist.

The fuse's queued action is explicitly **sub=0x00**, not an immediate handler. The proof follows the native engine's fixed sentence Script2 with fresh sentence bindings. Room28 object470 has no Pull13 or fallback entry, so native `VerbAllowed=0` is known; Script2/default Script3 are examined rather than ignored. Unknown object tables, calls, owners, missing offsets, room/restart changes, recursive calls and analysis bounds keep the candidate. The source worklist explores unknown branch edges and discards stale values after unknown/indirect/range/bit/query writes. Queued calls never inherit unrelated caller globals.

Canonical allocation is also verified independently of the catalog analyzer: Script119's real pickup instruction allocates55 (Classic +0053, Enhanced +0006); Room30 object491 verb33 +0077 allocates73. Prepared single-instruction executions verify native slots, retained source room and ownership. They are narrow source-contract checks, not earned puzzle routes. The analyzer can lose known55 at the delayed147/148 child invalidation before119; that conservative precision limit is preserved rather than described as a resolved effect target or an original-game preload. Alias-return executions start with explicitly prepared retained canonical storage and verify that it remains the same storage/source while the scene header stays absent.

## Genuine repeated names

These entries stay separate. Native scripts establish independent state, not merely distinct numeric headers.

| Native name | IDs and source | Meaning established by native code |
|---|---|---|
| CashCard | 18/Room1, 32/Room6, 75/Room30 | Their Read verbs select source character1/2/4; Script53 prints the character's name and separate balance. Give/Use respect those identities. |
| crystal shard | 33/Room6, 56/Room14 | Separate pickup paths and object identities. Script119 distinguishes the two halves and explicitly discusses acquiring/fusing the other half. |
| helmet | 65/66, Room26 | Separate air variables CE/CF. Helmet66's fallback forwards physical actions to65 while preserving its owner/actor4 context. Sharing action code does not merge the two inventories. |
| ticket | 82/83, Room46 | Script58 preserves separate character1/2 ticket records, rejects a second ticket for the same person, and transfers route state between the two records. |
| token | 61/62/63, Room27 | Script145 selects separate canonical token holders for characters1/3/4 and maintains each character's token count. These are three native inventory/count records, not three fabricated individual coins. |

A test grants these twelve real same-name entries simultaneously to their source-established recipients, checks independent slots/owners, and restores exact original slots/owners through Undo in each edition. This preserves both real shard halves, both helmets/tickets and all native card/token identities. Normal UI can distinguish remaining repeated names with stable neutral numbered choices; it need not invent locations or claim a fixed story destination for a mutable ticket.

## Reproduction and validation

Worktree `.worktrees/zak-item-identities`, branch `codex/zak-item-identities`.

```powershell
rtk proxy dotnet run --project .verification/zak-item-identities-20261018/Probe/Probe.csproj -p:NuGetAudit=false --verbosity quiet -- helpers
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter "FullyQualifiedName~ScummZakInventoryIdentityTests|FullyQualifiedName~ScummInventoryIdentityClosureTests" --logger trx --results-directory .verification/zak-item-identities-20261018/test-results --verbosity minimal
```

The read-only Probe prints both duplicate-name groups, verb disassemblies, indexed owners/states and selected native helper contexts. Its output `audit.txt` and TRXs remain ignored locally; source data assets are not committed. No WPF windows were required.

Final focused result: **43 passed, zero failed/skipped**, with **zero build warnings/errors**, in40 seconds; TRX `test-results/adelc_DARKHERO_2026-10-18_22_34_33.trx` is retained locally. This includes two complete source maps, twelve native alias-return checks, four native canonical-allocation checks, two independent-homonym grant/Undo checks and23 conservative synthetic cases. A completed earlier predicate-based run passed42 cases in39 seconds; the final added regression rejects mutual alias endpoints. Earlier failed harness/closure attempts are retained in separate TRXs: an off-room resolver initially indexed room -1; the overly strict canonical-effect test missed55; and the initial fuse closure lacked the native VerbAllowed input. Those runs are not acceptance. The final parent owns application wiring, updated old count assertions, real-window acceptance, matrices, full-suite integration and promotion.
