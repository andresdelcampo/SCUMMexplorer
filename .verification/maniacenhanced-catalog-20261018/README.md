# Enhanced Maniac inventory catalog audit

| | |
|---|---|
| Family / item | ManiacEnhanced / remaining inventory catalog coverage |
| Fixture | `mmansionE ega es` under `D:/Juegos/Lucas` |
| Date | 18 October 2026 |
| Registry | Parent records final level after shared fix and UI acceptance |

## What was done

Independent own-edition census covers every global, entry/exit, local and object-verb block offered by the data source. Native Pickupable metadata and all pickup/flag-writer sites are compared with the generic helper's incoming contexts. No item-ID or room exception is added to production code.

## Commands run

Working directory `.worktrees/maniaczak-catalog`; all commands through RTK:

```powershell
rtk proxy dotnet build .verification/maniacenhanced-catalog-20261018/Probe/CatalogProbe.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet Exe/CatalogProbe/Debug/net10.0/CatalogProbe.dll "D:\Juegos\Lucas\mmansionE ega es"
rtk proxy dotnet Exe/CatalogProbe/Debug/net10.0/CatalogProbe.dll "D:\Juegos\Lucas\mmansionE ega es" calls
rtk proxy dotnet Exe/CatalogProbe/Debug/net10.0/CatalogProbe.dll "D:\Juegos\Lucas\mmansionE ega es" script 116
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~ScummManiacEnhancedCatalogTests.CompleteOwnEditionCodeCensus|FullyQualifiedName~ScummManiacEnhancedCatalogTests.NativeCallerGuard" --verbosity quiet
```

## Results

Own edition: **1092 blocks**, all complete control-flow decodes; **49 pickup occurrences**, **zero positive or negative Pickupable-bit writers**, **eight literal calls** to helper116, no dynamic start/chain operands. The only global pickup is Script116 +0x0045, reading ActiveObject1. All helper callers are ordinary Room26 object verbs for34/35/36/37/38.

Piggy bank35 has index state0 and source name `hucha`. Its Open1/Close2/Use6/Pick up14 calls write `var[0x07B]=35` at +0x0054/+0x0072/+0x002A before calling116. The helper's +0x003F guard skips its +0x0045 pickup when that argument is35. A native executor check from the guard confirms hamster34 and dimes36/37/38 can be picked up, while the bank is not. Earlier break/text/Ed branches do not bypass the guard.

The old49-entry catalog therefore contains one false positive; the source-established inventory roster is **48 code-bearing Pickupable items**. Index-only entry3 has no retained object code and is not an inventory resource. All source pickup sites are literal/self-context targets or the bounded helper above; no unresolved dynamic target remains in this fixture's decoded source.

Initial census/native guard tests: **six passed, zero failed/skipped**; build zero warnings/errors. After integrating the shared Core milestone, **all seven tests pass**, zero failed/skipped, five seconds. The complete 48-item roster grants to an unselected child with every native retained verb entrypoint intact. A mixed batch containing the scene bank fails atomically, and Undo restores slots, retained rooms, owners, states, classes, variables, inventory membership and actors exactly. This is an explicit admin edit, not earned acquisition. Parent's final required full suite covers the final analyzer and these same tests together.

## Comparison with ScummVM

The existing native v1/v2 opcode executor and data-source decoder implement source version rules. Native helper execution here means Explorer execution of the supplied bytecode; no ScummVM executable or original-game run is claimed. Runtime scene progress, flags and inventory acquisitions are not fabricated for the census. Startup preparation uses the existing disclosed native-input setup for a test session.

## Differences and uncertainties kept

The previous source-insensitive helper scan ignored caller assignments and the pickup guard. This pass corrects that analysis rather than adding a denied-item list. Counts are specific to the supplied enhanced Spanish data; another release requires its own source census. Broader story, timing, audio, palettes and other families are unchanged.

## User checks

The integrated sequential real-window checks are recorded in the [enhanced Zak worker's shared-window addendum](../zakenhanced-catalog-20261018/README.md) and parent checkpoint. Each uses a private profile/cache, independently rendered seven-character recipient pictures, native capacity and exact grant/transfer/Undo. No repeated subjective listening check is requested.

## Where the facts went

[Catalog checkpoint](../../docs/checkpoints/20261018-Catalog-ManiacZak.md), [Maniac script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [support tracker](../../docs/maniac/SUPPORT_STATUS.md). The parent owns shared registry/matrix and final full-suite integration.
