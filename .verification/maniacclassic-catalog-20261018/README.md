# ManiacClassic catalog: close indirect helper coverage

| | |
|---|---|
| Family / stage | ManiacClassic / inventory catalog completion following admin S8 clarification |
| Fixture and edition | `Games/mmansion ega en` (content-detected DOS classic English, SCUMM v1) |
| Date and author | 2026-10-18, ManiacClassic catalog worker |
| Commits | Shared catalog milestone `b09d6bd` (local cherry-pick `094bdfe6`); Classic evidence/test commit reported to orchestrator |
| Registry change | Parent owns registry/matrices; source inventory completeness is established for the supplied copy, subject to integrated grant/UI acceptance |

## What was done

Enumerated every global, room-entry/exit and object-verb disassembly in the supplied classic copy. Classified the sole indirect global pickup by all of its incoming helper calls, the index flags and native guard execution. Added a source census regression and native guard checks. A complete-roster test exercises explicit admin grants to unselected playable character 7, all retained native verb entrypoints and exact inventory/state/actor/global Undo.

The old 49-entry catalog has one false positive: piggy bank 35. It is a named scene object, and its Open/Close/Pull/Pick up handlers call Script 116 after assigning its discriminator to 35. Script 116's guard skips the pickup instruction for that discriminator. Admin freedom does not turn a scene object that is never inventory into an inventory item; no earned prerequisite is imposed on the genuine catalog.

## Commands run (through RTK)

Cwd `.worktrees/maniacclassic-catalog`:

```powershell
rtk proxy dotnet run --project .verification/maniacclassic-catalog-20261018/Probe/Probe.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~ScummManiacClassicCatalogTests&FullyQualifiedName!~CompleteCatalog" --verbosity normal
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~ScummManiacClassicCatalogTests --verbosity quiet
```

## Results (numbers)

- **1,089 disassembled blocks**, all with complete control flow; zero undecoded offsets or decoder issue blocks.
- **49 pickup instruction sites counted per decoded block**, including aliases in verb tables; only one is global: Script 116 +0x003F, ActiveObject1. No local-script directory exists for this layout.
- Zero Pickupable or Unpickupable flag writers, and no indirect startScript/chainScript operands in the complete corpus.
- **450 code-bearing object IDs**. The index has **56 Pickupable IDs**: **48 have retained object code**; 1, 3, 4, 5, 22, 23, 24 and 27 have none and cannot be inventory resources.
- Script 116 has eight literal incoming calls, all in Room 26: hamster 34, dime 36/37/38 and piggy bank 35 (verbs 1/2/6/14). The four piggy handlers assign the discriminator to 35. The other four items are already indexed Pickupable objects.
- Native source execution from the +0x0039 discriminator guard picks up 34/36/37/38 with the other-callers' zero discriminator and skips 35 with its caller-set value. All five reach stop with no unresolved instruction.
- Initial focused run: **six tests passed**, zero failures/skips; build zero warnings/errors.
- After integrating the shared catalog analyzer: **seven tests passed**, zero failures/skips, five seconds of test cases. The source-derived catalog contains exactly **48 named items**. A single admin batch grants all 48 to unselected character 7; every retained verb entrypoint matches its own resource, and Undo restores inventory slots, retained rooms, owners, states, inventory membership, globals and actors exactly. No source pickup/story code runs for the grants.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Compared the existing v1/v2 executor/data source semantics with the standing `script_v2.cpp` pickup/discriminator comparison and `object.cpp` retained inventory resource contract (18 October 2026). This is source-resource census and source execution, not an original-game visual/timing comparison. No ScummVM executable was run.

## Differences and uncertainties kept

The first harness build used a four-level relative project reference where three levels were required. It failed before source inspection; corrected before any results were accepted. An initial `dotnet test --no-restore` invocation emitted no test results because the new worktree had not restored its test project. The explicit restore/build invocation ran six cases and is the accepted result.

The former generic Script 116 "indirect target" note describes a parameterized helper template. Complete callers and immutable native Pickupable metadata close this copy's inventory coverage; it is not evidence for an arbitrary additional object. The bank is an actual catalog false positive rather than a newly missing item. All other previous gameplay/audio/visual/edition uncertainties remain outside this bounded inventory census.

Read-only shared-analyzer review found signed casts in generic v1/v2 relational comparisons, contrary to the existing native unsigned executor rule. The exclusive Core owner was notified to add a correction/regression; parent owns that integration. Potential helper/indirect mutation invalidation was also raised as a generic review point. In this Classic helper, return writes cannot affect a later pickup in its eight object callers: they stop immediately after the call, or jump to a stop. Neither review point changes the independently executed native guard or the 48-item source census.

## User checks (exact steps and where; none if none)

Parent owns the integrated real-window inventory checks. This headless source/test worker requests no duplicate user check.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent integrates these facts into [Maniac script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [support tracker](../../docs/maniac/SUPPORT_STATUS.md) and [run checkpoint](../../docs/checkpoints/20261018-Catalog-ManiacZak.md). This worker edits none of those shared files.
