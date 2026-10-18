# ManiacEnhanced native battery / flashlight Use-with

| | |
|---|---|
| Family / stage | ManiacEnhanced / bounded Use-with engine fidelity |
| Fixture and edition | `D:/Juegos/Lucas/mmansionE ega es`, DOS v2 Spanish |
| Date and author | 18 October 2026, Enhanced shared-Core owner |
| Commits | Shared preposition implementation commit; family acceptance follows |
| Registry change | Unchanged: bounded Play/Objects/Status already Full, Cutscenes still Partial |

## What was done

Reproduced the retained native inventory failure with copied pre-instruction observations, rather than repeating guessed inputs. All expected input tuples were delivered. Script 4 +0247 selected held battery 167 and then stopped on unsupported `getObjPreposition`; its preposition remained zero, so the next torch click replaced the first target. Independent Classic observation finds the corresponding missing implicit lookup inside `drawSentence`. These are version-engine omissions, not evidence that an extra story flag or battery should be assigned.

Implemented the native old object-header high-three-bit field, `getObjPreposition`, `setObjPreposition` and the existing Classic-only object-dependent verb fallback. Header queries resolve current room objects or retained inventory code, return native FF when nowhere, and fail explicitly on unreadable code. Set operations preserve the low-five-bit walk-Y field by storing only the independent preposition. Room-header edits and picked-up inventory copies have separate session state; picking up copies the current room value. No file bytes, puzzle flags, item ownership or actor state are assigned by this engine query.

Native input dispatch remains unchanged. Official `runInputScript` writes click area/verb/object and passes mode as a local; game variable 0x022 is owned by Script 4 and is not an engine mode destination.

## Commands run (through RTK)

Working directory: `D:/Documents/Personal/Code Projects/SCUMMexplorer/.worktrees/maniac-enhanced-usewith`.

```powershell
rtk proxy dotnet run --project .verification/maniacenhanced-usewith-20261018/UseWithCheck/UseWithCheck.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter "FullyQualifiedName~ScummV2PrepositionTests|FullyQualifiedName~ScummV2ExecutorTests|FullyQualifiedName~LoomActorLookTests.AfterTheBoot|FullyQualifiedName~LoomCdRunningSetupTests" -p:NuGetAudit=false --verbosity quiet
```

The baseline copied values and instructions remain locally under `output-before/`; corrected traces under `output-corrected/` and `output-corrected-dispatch/`. Large logs/JSON/captures are ignored; the reproducible harness is committed.

## Results (numbers)

Shared focused checks: **30 passed, zero failed/skipped**, including six new preposition cases, existing v1/v2 executor contracts and LOOM EGA boot/CD state safeguards. Own Enhanced observed corrected composition reads header preposition 1, displays `Usar pilas en linterna`, and keeps object1=167/object2=84. Repeating the selected second object, as Script 4 +01F2 requires in the reached state, invokes the battery's own Use code; native word 0x032 becomes 2 and the sentence returns to `Ir a`. Full family refusal/consumption/beam regressions and the parent real-window check follow separately; no premature window or complete-game acceptance is claimed here.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Official master source read 18 October 2026: [script_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp) `o2_getObjPreposition` / `o2_setObjPreposition` reads/writes OBCD byte 12 high three bits; missing reads FF, missing writes do nothing. [string.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/string.cpp) `ScummEngine_v2::drawSentence` computes the object-dependent preposition only for DOS Maniac v1 when its current value is zero and verb prep is FF. [script.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script.cpp) `runInputScript` confirms existing global/local delivery; [verbs.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/verbs.cpp) confirms area/mode and inventory/sentence regions. No ScummVM executable was run; no upstream immutable commit is claimed.

## Differences and uncertainties kept

The earlier fidelity failures remain in their original record. The fresh baseline confirms a concrete missing opcode/lookup, not wrong click geometry or ownership. A sentence-line Area 5 click only redraws in this reached script state; a repeated selected second-target click sets the native execute flag and completes the action. No speculative input-mode/global fix was added. One development compile failed on referring to an estimation set outside this helper's scope; the existing caller already propagates changed-variable estimates, so the redundant reference was removed. Final focused build/tests pass.

Direct room visits and source radio/torch pickup/Open actions are labelled exploration preparation; inventory Use itself uses native input. This is not a continuous earned puzzle route, original-game picture/timing comparison or full-game claim.

## User checks (exact steps and where; none if none)

Parent owns the real-window check and final steps. No repeated subjective timing/listening check is requested.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent coordinates [Maniac script facts](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [tracker](../../docs/maniac/SUPPORT_STATUS.md), [engine notes](../../docs/engine/SCUMM_SCRIPT_MAP.md) and [checkpoint](../../docs/checkpoints/20261018-UseWith-ManiacZak.md). Enhanced battery Use is Room 2 object 167 +0018/+001E/+0022; native input lookup is Script 4 +0247. Classic independently supplies its own offsets/evidence.
