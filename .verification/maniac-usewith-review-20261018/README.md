# Maniac native Use-with: independent retained-failure review

| | |
|---|---|
| Family / stage | ManiacClassic and ManiacEnhanced / bounded S6 fidelity review |
| Fixture and edition | `Games/mmansion ega en` (DOS v1 English); `Games/mmansionE ega es` (DOS v2 Spanish), external root `D:/Juegos/Lucas` |
| Date and author | 2026-10-18, independent read-only reviewer |
| Commits | Baseline `ab10eb4`; this record's evidence commit |
| Registry change | None: analysis does not establish a new support level |

## What was done

Independently read the retained `NativeManiacTorch` diagnostic, both editions' Script 4 and Script 2, and the current v1/v2 input delivery. Reproduced the retained preparation and the same four native clicks with copied instruction observations. No production Core/App file or shared document was edited. The diagnostic uses the existing disclosed party preview, direct source radio pickup/Open and flashlight pickup, then genuine queued interface inputs. Those preparation actions are exploration actions and room previews; this is not an earned route or original-game observation.

Both reproductions establish missing native object-preposition handling as the immediate composition blocker. Enhanced stops on its explicit `getObjPreposition`; Classic stops on implicit object-header preposition lookup during `drawSentence`. The shared Core owner and parent received exact observations before implementation. Different source offsets and paths are recorded independently; no language or offset switch is proposed.

## Commands run (through RTK)

Working directory: `.worktrees/maniac-usewith-review`.

```powershell
rtk proxy dotnet build .verification/ScriptDump/ScriptDump.csproj --verbosity quiet -p:NuGetAudit=false
rtk proxy dotnet Exe/ScriptDump/Debug/net10.0/ScriptDump.dll "D:\Juegos\Lucas\mmansionE ega es" 4 2
rtk proxy dotnet Exe/ScriptDump/Debug/net10.0/ScriptDump.dll "D:\Juegos\Lucas\mmansion ega en" 4
rtk proxy dotnet build .verification/maniac-usewith-review-20261018/ReviewProbe.csproj --verbosity quiet -p:NuGetAudit=false
rtk proxy dotnet Exe/ReviewProbe/Debug/net10.0/ReviewProbe.dll "D:\Juegos\Lucas\mmansionE ega es"
rtk proxy dotnet Exe/ReviewProbe/Debug/net10.0/ReviewProbe.dll "D:\Juegos\Lucas\mmansion ega en"
```

Output was retained locally as `enhanced-baseline.log` and `classic-baseline.log` using `rtk proxy powershell -NoProfile -Command` with `Set-Content -Encoding UTF8`. Raw instruction/resource dumps are not committed. `ReviewProbe` deliberately catches and labels the old diagnostic's expected failure so both the cause and late unrelated continuation can be inspected; exit zero is **not** a gameplay acceptance result. No broad audits or suite were run by this reviewer.

## Results (numbers)

Both small harness builds: **zero warnings/errors**. Two retained-failure reproductions, each with all four expected input tuples delivered:

| Click | Area | Value | Mode |
|---|---:|---:|---:|
| Displayed Use verb | 1 | 11 | 1 |
| Inventory batteries | 3 | 167 | 0 |
| Inventory flashlight | 3 | 84 | 0 |
| Sentence line | 5 | 0 | 0 |

Both batteries and flashlight are owned by the current selected actor before composition; the held targets occupy displayed slots. The retained inventory coordinates use row top `MainScreen.Bottom + 32` plus four pixels and the proper left/right column. Accepted and delivered object values are 167 and 84, so this result does not support a wrong-coordinate, scroll-slot, missing-owner or dropped-input explanation.

At the first battery click, both own-source runs select sentence verb 11 and object 1 = 167 while object 2/preposition are still zero. Enhanced's copied observation seq 261 then ends with `unsupported v2 getObjPreposition (var[0x01D], var[0x01B]) at +0x0247`. Classic's seq 240 instead ends with `unsupported v2 drawSentence (var[0x01A], var[0x01B], var[0x01C], sub=0x02) at +0x0242`. These are native script execution stops, before insertion code can run.

The next flashlight click replaces the first object with 84 because the missing operation left preposition zero, then stops at the same edition-specific instruction. The final sentence click also hits that stop. Battery mode remains zero. Waiting longer and observing a later capture continuation cannot resolve the earlier unsupported instruction or prove that insertion's reached story conditions were invalid.

Final independent read-only review of shared Core correction **`3572a57`** (parent integration **`01eb536`**) found **no actionable correctness issues**. Reviewed the object header field/decoding, session callbacks and new preposition partial, pickup-copy timing, native get/set handlers and Classic implicit draw fallback. Native room lookup stays limited to loaded records; non-room ownership additionally requires an actual retained inventory slot/source room, including container owners. Room and inventory header edits remain separate, pickup copies the reached room header before changing ownership, missing reads return FF, missing writes are no-ops and low walk-Y bits are preserved. Classic implicit preposition computation retains the native family/profile gate; Enhanced continues using its own explicit query. Later engines gain only unused default metadata/callbacks, and the new opcode handling stays inside the existing v1/v2 gate.

The reviewer changed no Core/App/shared document and ran no tests during this final review. Lifecycle, consumption, refusal, other-family regressions, actual-window checks and full integration validation are delegated to the implementing family workers and parent. The review compared the patch with official source semantics, not an original-game runtime; the two baseline failures above remain independent evidence.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Official ScummVM master source read 18 October 2026; no fixed upstream commit claimed, no executable run:

- [`verbs.cpp`, `ScummEngine_v2::checkExecVerbs`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/verbs.cpp): normal verb/scene clicks use native mouse code; inventory and sentence clicks carry mode zero. Inventory keys resolve displayed native inventory positions too.
- [`script.cpp`, `ScummEngine_v2::runInputScript`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script.cpp): fixed script 4; native click-area, clicked-verb or clicked-object globals are supplied. The baseline session matches these dispatch semantics for the reproduced clicks.
- [`script_v2.cpp`, `o2_getObjPreposition` / `o2_setObjPreposition` / `o2_doSentence`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp): object preposition is the high three bits of OBCD header byte 12; missing objects return FF; writes retain the low five bits. Drawing through sentence subopcode 2 delegates to native sentence drawing. Retained inventory code must remain the source after leaving the storing room.
- [`string.cpp`, `ScummEngine_v2::drawSentence`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/string.cpp): DOS Maniac v1 computes the preposition only with a selected first object and an existing zero preposition, taking verb preposition FF from the object's retained header; other checked versions leave that computation to their scripts.
- [`object.cpp`, `whereIsObject` / `getOBCDFromObject`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/object.cpp): object lookup is bounded by native room versus retained-inventory membership, not an arbitrary indexed resource in another room.

The Classic baseline explicitly refuses verb preposition FF in `drawSentence`, rather than guessing. Enhanced decodes `getObjPreposition` but has no executor implementation. Both fail-closed behaviors correctly disclosed unsupported work, and the missing native semantics are a version-level correction rather than a battery/room exception.

## Differences and uncertainties kept

This analysis pinpoints the immediate unsupported operations, not every possible later blocker. Successful owner/state changes, native rejection, cancellation, scrolling and actual-window behavior require the implementing workers' separate acceptance. No preposition, battery, ownership, room, actor, puzzle bit or session variable is assigned by this reviewer. No game/profile/ROM/save data was changed, no ScummVM executed, no junction created or worktree removed.

The checked Classic/Enhanced path difference is own-edition evidence; it is not a general claim about every v1/v2 game or language. Existing R-7.19 remains Proposed and binding; no new requirement is introduced.

## User checks (exact steps and where; none if none)

None from this diagnostic. Parent owns the integrated real-window route and user-facing checkpoint.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent owns coordinated source rows in [Maniac script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [engine script notes](../../docs/engine/SCUMM_SCRIPT_MAP.md), [support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries) and [Use-with checkpoint](../../docs/archive/checkpoints/20261018-UseWith-ManiacZak.md). This record retains the independent reproduction and review boundary.
