# ZakEnhanced inventory catalog: independent source census and native checks

| | |
|---|---|
| Family / stage | ZakEnhanced / S8 admin inventory catalog completion |
| Fixture and edition | `Games/zakE ega en` (English enhanced v2, external `D:/Juegos/Lucas`) |
| Date and author | 2026-10-18, ZakEnhanced catalog agent |
| Commits | Core milestone `b09d6bd`, independently cherry-picked as `59248609`; this record accompanies the separate Enhanced verification commit |
| Registry change | Parent owns registry/matrices; the supplied copy's complete 76-item roster is verified here. No registry edit in this worker. |

## What was done

Independently examined every global, entry, exit and native object-verb context in the Enhanced copy, including all previously parameterized pickup templates and their incoming contexts. Reproduced the newly found nest omission and the seven shop-header over-inferences before checking the sole Core writer's source-flow correction. The implementation uses a generic source analysis; this worker changes tests and verification harnesses only.

The letter investigation resolves the earlier uncertainty: native Read/Pickup on Room 3 object 139 acquires object 27, the fan club card. Object 139 is the room's letter container, not another inventory item. Its resident FD handler coordinates Give and does not pick up that header. Native execution verifies both normal verbs, ownership, retained room and the absence of 139 from native storage.

The newly included object 53 is named **abandoned bird nest** in the source header. A native Use sentence with a source class-2 tree branch runs the two nested FD handlers and picks up 53 while preserving ActiveObject1; it never picks up either resident handler header. The catalog retains its source header name; the pickup script's later rename is not falsely applied by an admin grant.

Seven independent native purchase-prefix/commit checks verify the shop display-header exclusion. The original helper's named `Unpickupable` branch skips pickup of the display and changes the counterpart's owner; Lotto rewrites ActiveObject1 before the helper and picks up the actual ticket. These are explicitly prepared, narrow source executions, not claimed completed or earned purchases.

## Commands run (through RTK)

Cwd `.worktrees/zakenhanced-catalog`:

```powershell
rtk proxy dotnet run --project .verification/zakenhanced-catalog-20261018/Probe/Probe.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~ScummZakEnhancedInventoryCatalogTests --verbosity minimal
rtk proxy dotnet build .verification/zakenhanced-catalog-20261018/WindowCheck/WindowCheck.csproj --no-restore -p:NuGetAudit=false --verbosity minimal
rtk proxy dotnet run --project .verification/zakenhanced-catalog-20261018/WindowCheck/WindowCheck.csproj --no-build -- .verification/zakenhanced-catalog-20261018/window-output
```

The Probe emits its complete machine-readable script-context census to ignored `Exe/Probe/Debug/net10.0/scripts.json`; console output can be redirected to ignored `audit.txt`. No original game asset is committed. The actual-window harness sets private profile/cache paths under its output directory. The parent reserved the WPF slot; no other harness window ran concurrently.

## Results (numbers)

- **1,475 complete source contexts:** 148 globals, 57 entry scripts, 45 exit scripts and 1,225 object-verb aliases. Every context has an empty decode-issue list and null decode note. There are **62 pickup occurrences including aliases**, **one Pickupable writer**, no Unpickupable writer and no variable start/chain-script calls.
- The sole Pickupable writer resolves to boom box 71 through its ordinary source object contexts; its standalone unbound helper template does not establish another item. Special handlers retain caller identity. The source analysis reports **76 candidates / 76 named catalog entries**, **761 code-bearing object identities**, no ambiguous omitted candidate and no call-depth limit finding.
- **15 new tests pass**, zero failed/skipped, **32 seconds**. They cover both letter verbs; nested-handler source offsets and the corrected roster; native nest pickup through its source tool-class guard; seven native shop-prefix/commit cases; all 76 catalog entries granting/transferring and undoing with exact native/unrelated state restoration; complete decode census; and native retained nest Use/card Read execution in Room 1, outside their source rooms.
- WindowCheck build: **zero warnings/errors**, 4.90 seconds. Actual Normal-mode window check: **PASS**, exit 0. Search for letter offers no item; search/grant/Undo for nest 53 and card 27 preserves their native source rooms 13/3 and restores exact state. Six PNG captures are retained locally; four key captures were visually inspected (no-letter result, both grants and restored catalog). Native Zak picture/name, readable source item labels and clean Normal presentation are visible.
- Earlier native 80-slot overflow, stale-batch and exact-key Undo checks are unchanged; parent owns their integrated rerun and final full suite. No shared UI or production code is changed by this worker.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Reference semantics are the existing 18 October 2026 source-reviewed ports of ScummVM `script_v2.cpp` (`o2_doSentence`, Pickupable state branches and pickup) and `object.cpp` (native inventory slot/code retention). Independent comparisons here execute the fixture's real bytecode through the bounded executor/session, rather than accepting the new analyzer's interpretation as its own oracle. The named v2 `ifState` flag is its jump case; numeric comparisons use the ordinary false jump. The native shop tests verify that distinction against the analyzer's output.

No ScummVM executable was run. These are decoded/source-execution and actual Explorer-window results, not original-game timing or audiovisual acceptance.

## Differences and uncertainties kept

The pre-fix five-test run passed three and failed two as expected: the missing 53 catalog/grant assertions. A later focused pre-fix run passed the letter/card/decode checks and still failed the 53 grant. These failures exposed real catalog coverage, not a gameplay opcode defect.

The first native nest experiment used a lighter (49). The real handler rejects it; its native class mask is zero, while the successful tree branch (52) has mask 10 including bit 2. The test now prepares the source-supported branch explicitly and does not force a flag or bypass the nested handler. Initial shop tests incorrectly expected `InstructionsVisited == 2` at a two-instruction bound; the executor increments its counter again when encountering the bound. Outcome assertions now check native slots/ownership instead. Both failed assumptions are retained here; neither required a production fix or weaker gameplay behavior.

The source header says “abandoned bird nest”; its later pickup rename says “bird nest”. An early unrun name assertion assumed the later name and was corrected to the copy's actual header. First harness compilation used an incorrect relative project path; corrected before source acceptance. The first JSON write occurred before enumeration; moved after enumeration before any census claim. No failure was used as acceptance.

Completeness applies to the supplied Enhanced fixture and the defined native inventory-item contract, not hypothetical missing releases or arbitrary corrupted runtime words. Existing earlier story, palette, timing and sound limitations are unaffected. No game/profile/save/ROM mutation, junction, worktree removal, force or stash was performed.

## User checks (exact steps and where; none if none)

Optional visual check only: in the supplied Enhanced Zak copy, open Game Status in Normal, search “bird nest”, grant **abandoned bird nest** to Zak, apply and undo. Search “fan club card” offers the actual item; “letter” does not offer its room container. No new listening/timing check is needed for this catalog-only change.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent integrates the shared family facts and support decision into [Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md), [tracker](../../docs/zak/SUPPORT_STATUS.md) and the [orchestrated checkpoint](../../docs/checkpoints/20261018-Catalog-ManiacZak.md). This worker owns the independent tests/evidence only; Core, registry, generated tables, CURRENT_WORK and handoff have exclusive separate owners.
