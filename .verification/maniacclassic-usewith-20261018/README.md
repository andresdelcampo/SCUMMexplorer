# ManiacClassic Use-with: native held battery / flashlight composition

| | |
|---|---|
| Family / stage | ManiacClassic / explicitly requested Use-with follow-up |
| Fixture and edition | `Games/mmansion ega en`, supplied English v1, `TestGames.Root=D:/Juegos/Lucas` |
| Date and author | 2026-10-18, Codex Classic family worker |
| Commits | Base `ab10eb4`; shared hook `3572a57` (own cherry-pick `fe704410`); family proof commit follows |
| Registry change | Unchanged: bounded Play/Objects/Status already Full; Cutscenes Partial requires a hand-validated catalog. This closes a concrete native inventory-composition gap, not the complete game. |

## What was done

The retained battery diagnostic's native acquisition/coordinates were reproduced once with a copied instruction observer. Radio142 Pick up/Open and flashlight84 Pick up are actual direct source actions after disclosed room-state previews; these do not establish an earned mansion route. Subsequent Use, held item selection and execution use the native displayed interface and its input/sentence scripts only. The harness never assigns a global, state, owner, actor, room or inventory slot.

Classic's native input receives battery167 then flashlight84 correctly. Script4 `+0226` assigns the selected first object; `+0242 doSentence(...,sub02)` stops before the following instruction, leaving preposition0 and drawn sentence `Use`. The next held click therefore replaces the first object. The existing Classic drawSentence path explicitly refuses a verb's preposition255 because its object-header lookup was absent. This separates input/retained-code semantics from a missing story prerequisite. Shared Core owner implements the native hook; this worker changes no Core/App/shared docs.

Independent positive and wrong-held-target regressions use the Classic fixture's own code. The positive shows two native held objects in the sentence, dispatches through Script2, executes the actual battery code, consumes batteries, then native Switch on and dark-room beam. The negative leaves batteries held and word50 unchanged when the target is the radio.

## Commands run (through RTK)

Worktree: `D:/Documents/Personal/Code Projects/SCUMMexplorer/.worktrees/maniac-classic-usewith`.

```powershell
rtk git worktree add .worktrees/maniac-classic-usewith -b codex/maniac-classic-usewith codex/maniac-usewith
rtk proxy dotnet run --project .verification/maniacclassic-usewith-20261018/UseWithCheck -p:NuGetAudit=false
rtk proxy dotnet build tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --verbosity quiet
rtk git cherry-pick 3572a57
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~ScummManiacClassicUseWithTests|FullyQualifiedName~ScummManiacClassicPlayTests|FullyQualifiedName~ScummManiacClassicStateTests|(FullyQualifiedName~SupportConformanceTests&DisplayName~ManiacClassic)" --logger "console;verbosity=normal"
rtk proxy dotnet run --project .verification/maniacclassic-usewith-20261018/UseWithCheck -p:NuGetAudit=false -- --verify
```

Creation ran at repository root. The first harness output, retained as [observed-before.md](observed-before.md), includes actual input/variable/instruction chronology and Classic source dumps. It is diagnostic evidence, not an acceptance pass. Final passing output is [observed-after.md](observed-after.md).

The same harness's `--source` mode reads source only, with no session boot: [source.md](source.md). Native header metadata is battery167 preposition1 (`in`), flashlight84 preposition2 (`with`), radio142 preposition0. Radio142 Pick up14 `+001E/+0020` picks up radio and batteries, `+0023` sets batteries owner14 (container); Open1 `+0044/+0048` checks that owner and `+005A` transfers them to ego. Source-derived acquisition never writes a replacement story flag.

## Results (numbers)

Baseline: current actor3, owners142/167/84 all3, native slots0/1/2. Displayed Use click `(196,179)` delivers area1/verb11/mode1; `(184,188)` delivers inventory167/mode0; `(8,196)` delivers inventory84/mode0; native sentence `(8,156)` delivers area5/mode0. All accepted. The first object becomes167 then84 instead of a pair, preposition remains0, word50 remains0, batteries remain owner3; 120 continuation frames do not resolve it. No acquisition/capture/story shortcut was applied.

Family project build: zero warnings/errors. After the shared hook: **8 focused tests passed, zero failed/skipped**, about eight seconds (two new positive/invalid-target cases, two retained Play cases, three State cases and one family conformance row). The bounded harness exits0 with PASS.

Native sentence result: `Use batteries in` (B167/C0/preposition1) → `Use batteries in flashlight` (B167/C84/preposition1), word50 still0 and batteries owner3 during composition. Repeating the selected second item executes Script4's native sentence logic and Script2 `+019F` dispatches battery167 Use0B with active object2=84. Actual `+0022` writes word50=2, `+0026` consumes owner167=0; sentence resets to `Walk to`. The wrong target radio142 takes the battery verb's own rejection branch, with word50=0 and owner167=3 preserved.

Genuine Switch on4 followed by two held flashlight clicks runs the source switch code. After the disclosed Room5 source-state preview: **lights4, 6×4 strips**, room pointer `(179,63)` from screen `(179,79)`. Native darkness bit2 remains off while flashlight bit4 is on; no unsupported-v2 uncertainty remains. The final transcript contains the actual battery source execution.

Exact bounded interface sequence after preparation: displayed Use11, held batteries167, held flashlight84, repeated held flashlight84 (execution), five frames after each click, then40 frames; displayed Switch on4, held flashlight84 twice, five frames after each click, then20 frames; source-state preview Room5, then20 frames. Held slot coordinates are derived afresh from native slot/owner state after consumption, not hard-coded inventory ownership. Parent verifies the same sequence in the actual WPF window independently.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Shared Core owner independently reads official `engines/scumm/script_v2.cpp`, native `drawSentence`, object-header/preposition and held-object lookup behavior (18 October 2026). Its record owns engine rules. This worker reads Classic's own Script4/2 and object167/84 instruction bytes; relevant rows go to the parent-owned game map. ScummVM execution remains prohibited. Explorer source execution/scene previews and original-game observation remain distinct.

## Differences and uncertainties kept

The prior fidelity follow-up's two failed acquisition/input approaches remain in [the earlier lighting record](../maniacenhanced-fidelity-20261018/README.md). This new run resumes through source observation, not another guessed click sequence. The new Classic baseline is a retained failed composition, not an acceptance result; the native header/held-code correction fixes the demonstrated defect and needs no story/context workaround. No new functional test or final harness failed. No complete-story, original-game visual/timing/audio acceptance or additional-edition support is claimed. The selected Syd/Bernard preview is disclosed, and native randomness/arrivals retain estimates. Earlier S5 geometry/camera, walking metric, listening and unavailable-copy gaps remain.

Game/profile/ROM/save files were not changed; no Games junction was created, no worktree removed, no ScummVM executed, no broad full suite run by this worker. Parent owns the actual-window check, matrices and required integration suite.

## User checks (exact steps and where; none if none)

Parent supplies the final actual-window route and build path. A direct exploration preparation is not an earned puzzle playthrough; no optional timing check is used as automated acceptance.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent owns [Maniac script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries), checkpoint/current work/changelog/matrices. Suggested Classic source rows: battery167 Use0B `+001C` tests active object2=84, `+0022` assigns word032=2, `+0026` owner167=0; flashlight84 Switch on04 `+002C` battery check, `+0032` word09C=1 and `+0036` starts Script82. Source evidence decoded + play, own Classic fixture and native observed input, 2026-10-18. New requirements are not needed: existing R7.7/R7.12/R7.19 require native sentence/data-driven composition.
