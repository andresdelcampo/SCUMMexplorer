# Maniac/Zak inventory catalog completion: integration

| | |
|---|---|
| Families | ManiacClassic, ManiacEnhanced, ZakClassic, ZakEnhanced |
| Fixtures | `mmansion ega en`, `mmansionE ega es`, `zak ega en`, `zakE ega en` under external `D:/Juegos/Lucas` |
| Date | 18 October 2026 |
| Registry | Four Status cells Partial→Full under the clarified admin inventory contract; no other cell changed |

## What was done

Independent own-edition source censuses classify the parameterized helpers and special-handler contexts that blocked exhaustive catalog acceptance. The source-established rosters are 48 inventory items per Maniac copy and 76 per Zak copy. Maniac's piggy bank35 and Zak's letter139 are scene containers. The latter yields fan club card27. Zak nest53 was missing because its nested FD handlers preserve the caller's ActiveObject1; seven shop display headers were false positives. Generic data-flow and native flag semantics resolve these cases without item-ID exceptions in production.

The exclusive Core writer owns the shared analyzer. Independent review required native unsigned/reversed relational comparisons, source-alias reads, mutation invalidation and preservation of finite alternate targets at joins. Synthetic regressions verify those rules separately from the fixture rosters. Bounds remain visible and are not themselves completeness evidence. Independent source classification closes the supplied Zak copies' bounded Script55: its only pickup/flag sites precede the recording loop, all seven callers supply object71, it never rewrites ActiveObject1, and the later helper closure adds no inventory target. These are source analysis and explicit admin operations, never earned gameplay.

## Commands run

Working directory `.worktrees/maniaczak-catalog`, all shell commands through RTK. Final integration sequence:

```powershell
rtk git merge master
$env:SCUMM_UPDATE_SUPPORT_MATRIX = '1'
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter FullyQualifiedName~SupportMatrixTests --verbosity minimal
rtk proxy dotnet build SCUMMexplorer.slnx -p:NuGetAudit=false --verbosity minimal
$env:SCUMM_SOUND_TEST_ROOT = 'D:\Juegos\Lucas\loom ega mt32 en'
$env:SCUMM_INDY3_EGA_TEST_ROOT = 'D:\Juegos\Lucas\indy3 ega en'
$env:SCUMM_MT32_ROM_ROOT = 'D:\Juegos\Lucas\mt32'
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore --logger 'trx;LogFileName=full-suite.trx' --results-directory .verification/maniaczak-catalog-20261018/test-results --verbosity minimal
```

Per-family source/native tests and reproducible window harness commands are in the linked records below. Window processes ran sequentially with private profiles/caches. Master promotion uses only `git merge --ff-only codex/maniaczak-catalog`; moving master requires repeating the integration sequence.

## Results

Final solution build: **zero warnings/errors**. Both generated matrices: **three checks passed**. Required full suite: **1,422 passed, zero failed, five skipped** (1,427 total), **12m03s**, local20:31:50–20:43:54. TRX confirms **all 65 new cases passed**: the four family catalog classes plus generic analyzer regressions. Sound, Indy3 EGA and MT-32 roots were set as shown above; no test filter was used.

The integration merge was already up to date against master `170842d9`; master remained unchanged throughout the full run. Tested source is `2739aea`; only final records follow. Promotion uses a clean `--ff-only` merge after rechecking master. The final root App build refreshes `Exe/Debug/SCUMMexplorer.exe`. No remote push or history rewrite.

Four actual-window routes pass: 48/48/76/76 catalog entries; Maniac pictures7/7 in each edition; grant/transfer/Undo and native capacity/modal restoration. The dedicated Enhanced Zak route searches for letter (no grantable result) and grants/undoes nest53 and card27 retaining rooms13/3. Forty local PNG captures are retained across the four routes; nine key captures were visually inspected. No overlapping WPF windows or failures occurred in this run.

The parent repeats the dedicated Enhanced Zak window against frozen final Core `e8d70a4f`: build zero warnings/errors, process exit0, catalog76, no letter grant, both nest/card retained-room/exact-state Undo checks pass. Six additional captures and four visual inspections confirm readable choices and restored state. Overall: **five sequential window processes, 46 local captures, 13 inspected**. This last output is `window-final-zakenhanced/window-check.txt`; its harness creates a private profile/cache and closes itself.

## Comparison with ScummVM

Existing native source rules from `engines/scumm/script_v2.cpp` and `object.cpp` govern operands, sentence context, flags, inventory storage and retained code. Independent fixture-bytecode execution is the native branch oracle, rather than comparing the catalog against itself. No ScummVM executable was run. Actual-window results are Explorer UI acceptance, not original-game timing/audiovisual acceptance.

## Differences and uncertainties kept

Pre-fix missing-nest assertions and rejected wrong-tool/instruction-counter assumptions are retained in the Enhanced record. The independent reviewer reproduced generic join, comparison and alias/bit-write defects before correction. The previous Admin checkpoint's concurrent pointer/state assertion remains unreproduced and unresolved; sequential success here does not claim a production fix. The source contract applies to the supplied copies, not unavailable releases or arbitrary corrupted runtime state. Earlier story, palette, sound and visual limitations are unchanged.

The first integrated focused run had 44 passes and four conformance failures: the new test searched for the substring `bound`, inadvertently matching the diagnostic text `unbound-only writer: False`. The predicate now checks only the two actual bound-message prefixes; production code and roster expectations were unchanged. That failed TRX is retained as `test-results/catalog-focused.trx`; the corrected run uses a separate filename.

The first solution build used `--no-restore` before this fresh worktree's App assets existed and failed with NETSDK1004. The explicit restoring build succeeded with zero warnings/errors; this was a setup failure, not source acceptance.

## User checks

Optional: open any supplied Maniac/Zak copy and Game Status. Search an item, choose a recipient, Add item, Apply Changes and Undo. Maniac offers seven pictured children, including unselected ones. Zak offers abandoned bird nest and fan club card; letter is a scene container and absent from inventory choices. No new listening/timing check is needed.

## Where the facts went

[Checkpoint](../../docs/archive/checkpoints/20261018-Catalog-ManiacZak.md), [Maniac map](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [Zak map](../../docs/zak/ZAK_SCRIPT_MAP.md), [engine notes](../../docs/engine/SCUMM_SCRIPT_MAP.md), [Maniac and Zak support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries). Independent evidence: [ManiacClassic](../maniacclassic-catalog-20261018/README.md), [ManiacEnhanced](../maniacenhanced-catalog-20261018/README.md), [ZakClassic](../zakclassic-catalog-20261018/README.md), [ZakEnhanced and shared windows](../zakenhanced-catalog-20261018/README.md).
