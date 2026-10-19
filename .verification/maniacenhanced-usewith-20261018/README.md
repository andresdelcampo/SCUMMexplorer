# ManiacEnhanced native battery / flashlight Use-with

| | |
|---|---|
| Family / stage | ManiacEnhanced / bounded Use-with engine fidelity |
| Fixture and edition | `D:/Juegos/Lucas/mmansionE ega es`, DOS v2 Spanish |
| Date and author | 18 October 2026, Enhanced shared-Core owner |
| Commits | Shared preposition implementation `3572a57`; final Enhanced regressions and this record in the follow-up commit |
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
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~ScummManiacEnhancedUseWithTests|FullyQualifiedName~ScummV2PrepositionTests" --logger "console;verbosity=normal" --verbosity quiet
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~SupportConformanceTests.FamilyIsRecognisedAndItsClaimsHold&DisplayName~ManiacEnhanced" --logger "console;verbosity=normal" --verbosity quiet
```

The baseline copied values and instructions remain locally under `output-before/`; corrected traces under `output-corrected/` and `output-corrected-dispatch/`; the successful native Use, Switch-on and dark-room route is in `output-positive/`. Large logs/JSON/captures are ignored; the reproducible harness is committed.

## Results (numbers)

Shared focused checks: **30 passed, zero failed/skipped**, including six new preposition cases, existing v1/v2 executor contracts and LOOM EGA boot/CD state safeguards. Final family run: **11 passed, zero failed/skipped**: four Enhanced route/lifecycle cases, six shared preposition cases and the Enhanced support conformance case.

Own Enhanced corrected composition reads header preposition 1, displays `Usar pilas en linterna`, and keeps object1=167/object2=84. Repeating the selected second object, as Script 4 +01F2 requires in the reached state, invokes the battery's own Use code; native word 0x032 becomes 2, owner167 becomes zero and its inventory slot disappears. The source-local observation from battery +0018 to +0026 changes only word 0x032; torch ownership/slot, held object state bits, object classes and all other owners remain unchanged. Wrong-target Use observes the actual battery +0018 instruction with ActiveObject2=142 and preserves the battery and battery word. Without batteries, genuine Switch-on reaches the source refusal at +0047 (`Necesita…`) and preserves the torch inventory/state and both battery/light words.

Native Switch-on after insertion sets word 0x09C=1. Room 5 then has lights12, a 6-by-4-strip beam and 1,115 nonzero composed background pixels at pointer179,79; moving the pointer changes the composed image. Lifecycle coverage also verifies an edited room header copied at pickup, an independent held header across room departure, an indexed but absent object returning FF, a missing setter doing nothing, preserved decoded source walk-Y/header bytes, and FF after native consumption. Parent separately reports both actual MainWindow edition routes passed, with eight captures inspected, actual bitmap matching composition, a moving beam and no frame/state change from rendering; that window evidence is owned by the parent.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Official master source read 18 October 2026: [script_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp) `o2_getObjPreposition` / `o2_setObjPreposition` reads/writes OBCD byte 12 high three bits; missing reads FF, missing writes do nothing. [string.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/string.cpp) `ScummEngine_v2::drawSentence` computes the object-dependent preposition only for DOS Maniac v1 when its current value is zero and verb prep is FF. [script.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script.cpp) `runInputScript` confirms existing global/local delivery; [verbs.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/verbs.cpp) confirms area/mode and inventory/sentence regions. No ScummVM executable was run; no upstream immutable commit is claimed.

SHA-256 of the read source copies (ignored `source/`):

| File | SHA-256 |
|---|---|
| script_v2.cpp | `09DF8B3FF96F7E586077F124FC34960AD964DB06F3C500273ABD9400A7F48013` |
| string.cpp | `F5CF135B53C1E4DA5D513D08386E07153D0997AA937DD8B17657ADC3F81E0D6A` |
| script.cpp | `353982EA0E5BD54BF3834E1C8F477DACF91550E72DDE4864D2D5FE5D064E99E6` |
| verbs.cpp | `DF516D43617E2AE08AA271AAE2C2FC27C02B2A11B466C02AB7C381AC1643B435` |

## Differences and uncertainties kept

The earlier fidelity failures remain in their original record. The fresh baseline confirms a concrete missing opcode/lookup, not wrong click geometry or ownership. A sentence-line Area 5 click only redraws in this reached script state; a repeated selected second-target click sets the native execute flag and completes the action. No speculative input-mode/global fix was added. One development compile failed on referring to an estimation set outside this helper's scope; the existing caller already propagates changed-variable estimates, so the redundant reference was removed. Final focused build/tests pass.

Two initial family-test assertions were corrected after copied evidence: remote ambient object548 changes its state while frames advance, so refusal checks constrain the held torch and source-local words rather than every remote object; room key141's own preposition is1, so lifecycle preservation compares with its actual decoded header rather than assuming zero. Neither required an engine change. The successful battery route passed its first execution.

A final combined display-name filter containing unescaped parentheses selected no tests despite exit code0; normal console output exposed that tool issue. The corrected separate filters above executed ten regression cases and one Enhanced conformance case successfully. The zero-test invocation is not counted as validation.

Direct room visits and source radio/torch pickup/Open actions are labelled exploration preparation; inventory Use itself uses native input. This is not a continuous earned puzzle route, original-game picture/timing comparison or full-game claim.

Preparation uses Room2 radio142 pickup14, then Open1 to release its native contained battery167 to ego3, 120frames; Room7 torch84 pickup14, tenframes. After each genuine input allow fiveframes: displayed Use11, held battery167, held torch84, repeat torch84. After twentyframes select displayed Switch-on4, held torch84, repeat torch84; wait twentyframes, visit Room5 and wait twentyframes. Battery167 has no pickup14 entrypoint; none is invented or forced. There are no synthetic battery/light/owner writes in this acceptance route.

## User checks (exact steps and where; none if none)

Parent owns the real-window check and final steps. No repeated subjective timing/listening check is requested.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent coordinates [Maniac script facts](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries), [engine notes](../../docs/engine/SCUMM_SCRIPT_MAP.md) and [checkpoint](../../docs/checkpoints/20261018-UseWith-ManiacZak.md). Enhanced battery Use is Room 2 object 167 +0018/+001E/+0022; native input lookup is Script 4 +0247. Classic independently supplies its own offsets/evidence.
