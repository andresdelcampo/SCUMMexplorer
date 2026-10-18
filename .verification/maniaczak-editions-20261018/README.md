# Maniac/Zak S9: available-edition checks and explicit missing-copy limits

| | |
|---|---|
| Family / stage | ManiacClassic, ManiacEnhanced, ZakClassic, ZakEnhanced / S9 |
| Fixture and edition | `mmansion ega en`, `mmansionE ega es`, `zak ega en`, `zakE ega en` under `D:/Juegos/Lucas` |
| Date and author | 2026-10-18, Maniac/Zak orchestrator and three family workers |
| Commits | See checkpoint and family records |
| Registry change | Unchanged: no new family feature or additional release is established by the available-copy checks |

## What was done

Inventory precedes testing. The root has exactly one copy per family; classic and enhanced are different matrix families, not alternate editions of one family. Additional language/revision/demo checks are blocked by missing fixtures. The independent workers perform bounded checks of the available copies and reuse the S4b–S7 evidence where no plausible difference warrants a repeat.

Shared-file ownership: parent only. A candidate raw-executable font loader was committed as `a8542c4` after existing font/LOOM tests passed, then immediately reverted by `1ead78e` when the enhanced worker's full-glyph check disproved the whole-font slice assumption. The earlier ASCII/accent samples are valid, but the low-glyph span contains encoded instructions. The rejected implementation remains in Git evidence and is not shipped. Passing the older tests did not prove correctness of the new loader.

## Commands run (through RTK)

The corrected loader uses ScummVM's preserved executable-font extraction loop and checks the decoded font hash before rendering. The raw slice is not used. Only the reviewed local executable enables this data descriptor; all other v1/v2 executables retain the existing fallback. Source attribution is in THIRD_PARTY_NOTICES; the enhanced family record retains exact offsets and hash evidence. Two obsolete v1/v2 detection notes were also replaced with facts about the directories read, without a new runtime claim.

From `.worktrees/maniaczak-s9`; all data reads are read-only and profiles/captures private.

```powershell
rtk git merge master
$env:SCUMM_UPDATE_SUPPORT_MATRIX="1"
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~SupportMatrixTests" --verbosity quiet
Remove-Item Env:SCUMM_UPDATE_SUPPORT_MATRIX
rtk proxy dotnet build SCUMMexplorer.slnx --no-restore -p:NuGetAudit=false --verbosity quiet
$env:SCUMM_SOUND_TEST_ROOT="D:\Juegos\Lucas\loom ega mt32 en"
$env:SCUMM_INDY3_EGA_TEST_ROOT="D:\Juegos\Lucas\indy3 ega en"
$env:SCUMM_MT32_ROM_ROOT="D:\Juegos\Lucas\mt32"
rtk proxy dotnet test SCUMMexplorer.slnx --no-build --no-restore --verbosity quiet --logger "trx;LogFileName=maniaczak-s9-full.trx" --results-directory ".verification/maniaczak-editions-20261018/test-results"
```

## Results (numbers)

Available-copy inventory: ManiacClassic 52 rooms / 23 costumes / 173 scripts / 61 sounds; ManiacEnhanced 53 rooms; ZakClassic 58 rooms / 35 costumes / 151 scripts / 110 sounds; ZakEnhanced 58 rooms / 39 costumes / 148 scripts / 112 sounds. Classic Maniac's own startup reaches Room 44; its source scan remains 59 triggers / 27 groups / 25 scenes. Prior S4b–S7 boot/play/scene evidence is reused for the unchanged copies.

Thirteen integrated edition/conformance/old-charset/affected-enhanced-Play tests pass. Corrected-font window check passes with the unchanged enhanced census of 59 triggers / 27 groups / 25 scenes / seven begun. Room-list TV playback reaches Script 112's first line at window frame 51 in Room 40; raw `m}sicos y dise{adores` paints as `músicos y diseñadores`. The actual-window capture was inspected. The harness uses the real timer, then drives its same handler deterministically with a private profile; it does not establish physical input feel or original-game timing.

Window build/run (same cwd):

```powershell
rtk proxy dotnet build .verification/maniaczak-editions-20261018/WindowCheck/ManiacZakEditionWindowCheck.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy Exe/ManiacZakEditionWindowCheck/Debug/net10.0-windows/ManiacZakEditionWindowCheck.exe "D:\Juegos\Lucas\mmansionE ega es" ".verification/maniaczak-editions-20261018/window-enhanced-native-font" ".verification/maniaczak-editions-20261018/WindowCheck/enhanced-native-font.json"
```

Build zero warnings/errors; window result PASS. Private captures/logs are retained but ignored. Final matrix/build/full-suite results pending.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Prior edition workflow `.verification/editions-20261005` and S4b–S7 source records reused. No ScummVM executable was run; prepared Explorer/Core/WPF results remain distinct from original-game observation.

## Differences and uncertainties kept

Missing alternate copies cannot pass by inference. Existing S5 visual acceptance, native darkness/flashlight, nearest-box walking approximation, Zak's 11 estimated costume palettes, Classic Maniac's post-doorbell Script 174 and state-gated/capped cutscenes remain unchanged. No full-game, timing or audio acceptance follows from a bounded edition check. S8, catalogs and seeds remain outside this run.

## User checks (exact steps and where; none if none)

Optional look at the corrected supplied Spanish copy: run the built app, open `D:/Juegos/Lucas/mmansionE ega es`, choose Room 17, Play Cutscene → `Encender TV`; the first TV line should show `músicos y diseñadores` with ú/ñ. The exact executable must be present for this native font. Earlier scene timing/listening checks remain in S7 and are not requested twice. Additional releases must be supplied before their compatibility can be tested.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Checkpoint](../../docs/checkpoints/20261018-S9-ManiacZak.md), [Maniac tracker](../../docs/maniac/SUPPORT_STATUS.md), [Zak tracker](../../docs/zak/SUPPORT_STATUS.md). Family records retain the edition × check × result tables.
