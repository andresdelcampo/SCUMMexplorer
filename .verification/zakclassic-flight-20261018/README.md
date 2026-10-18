# ZakClassic flight preparation and ticket state, 18 October 2026

Fixture: supplied English SCUMM v1 `Games/zak ega en`, resolved through `TestGames.Root` to `D:/Juegos/Lucas/zak ega en`. Worker branch `codex/zak-classic-flight`, worktree `.worktrees/zak-classic-flight`, from the parent's `9d366733` plan. Parent owns shared support notes, matrices, actual-window evidence and integration. Enhanced worker exclusively owns shared Core changes; this worker applies their commits as prerequisites, without editing Core or App.

## Scope and current result

Bounded own-edition source reading, source preparation and native player actions; no full-story route, catalog/seed, invented currency/ticket/owner/flag, actor placement or automatic choices. Registry levels remain unchanged: a bounded flight check does not supply Full Cutscenes or resolve Classic costume estimates. Source-backed preparation is explicitly exploration; native interface composition is separately identified. Original ScummVM execution is prohibited and was not performed.

The retained Room 11 Script104 guard reads **peanuts**, object51. Tickets are objects82/83 in Room46; the boot supplies ticket82 to Zak (owner1), with departure word155=145, destination word79=662 (Seattle), price word82=260. Script116 serves the meal and picks up51 at +0025; Script104 +01C5/+01C9 waits for that native service lifecycle. Earlier records called51 a ticket; they are retained as historical evidence, and the parent's current notes correct the interpretation.

Classic's native CashCard recovery independently reproduced the shared omission. After source pickup of butter knife26 and disclosed room previews, the displayed interface composes `Use butter knife with plastic card`. Its own desk helper93 stops at **+0026 ifClassOfIs(var[0x00A],1)**, where Enhanced has +0013. Shared prerequisite `07d9eb1` (this branch `6d51bad5`) reads native OBCD byte6 from current/held code. The same inputs recover CashCard18 to owner1; using initial ticket82 instead keeps18 room-owned, preserves cash and both held objects, and reaches the same actual class predicate. The ticket supplies its own preposition (`Use ticket in plastic card`), which is deliberately retained.

Shared prerequisites `a425b0a` / `7d4a2cb` (this branch `402628f` / `59fc33f`) preserve native logical actor rooms and compare camera-follow guards in that domain. Classic independently verifies the originally failing airport return: physical Room8 is logical airport144 before and after the actual alien-scene Escape. Actor rendering snapshots intentionally keep physical room8; script word4 keeps144. Neither test nor harness assigns either value.

With that correction, the airport preview supports its own genuine reservation transaction. Native Use on terminal194 opens Room45; selecting displayed San Francisco choice663 then Buy657 refunds the original $260 ticket and purchases a $260 replacement. Cash moves 12250 -> 12510 -> 12250; ticket82 is removed during refund then reissued to owner1, departure becomes144, destination663 and price260. Native Walk to gate190 consumes82 to owner14 and clears departure. Within the retained **2500-frame bound**, boarding, the safety speech and meal service finish in Room11 with control restored: Script116 +0025 picks up peanuts51 to owner1 and Script104 passes +01CF and ends. This is not completed destination arrival: Room11 object241/verb253 +003D still waits its native 28800 jiffies (17454 remained in the final harness). No cap was raised or timer bypassed.

Native Cancel658 after selecting the same candidate retains the original ticket refund: cash12510, owner82=14, departure0, price0. The game's candidate destination663 remains, but gate190 says `I need a ticket first.` and does not board. That candidate is not silently converted into a ticket. Wrong-airport refusal and wrong held-tool recovery checks independently preserve their unaffected state. The successful route starts from disclosed logical airport144 backed by physical Room8, not an earned bus journey. An unverified Miami label in the initial record/comment was removed during parent review; the tested claim uses native room identity, not an assumed city.

## Commands and evidence

All commands run through RTK from this worktree:

```powershell
rtk proxy dotnet run --project .verification/zakclassic-flight-20261018/FlightCheck -- "D:/Juegos/Lucas/zak ega en" source
rtk proxy dotnet run --project .verification/zakclassic-flight-20261018/FlightCheck -- "D:/Juegos/Lucas/zak ega en" find
rtk proxy dotnet run --project .verification/zakclassic-flight-20261018/FlightCheck -- "D:/Juegos/Lucas/zak ega en" room 46
rtk proxy dotnet run --project .verification/zakclassic-flight-20261018/FlightCheck -- "D:/Juegos/Lucas/zak ega en" actions .verification/zakclassic-flight-20261018/cash-recovery.steps
rtk proxy dotnet run --project .verification/zakclassic-flight-20261018/FlightCheck -- "D:/Juegos/Lucas/zak ega en" actions .verification/zakclassic-flight-20261018/flight-positive.steps
rtk proxy dotnet run --no-build --project .verification/zakclassic-flight-20261018/FlightCheck -- "D:/Juegos/Lucas/zak ega en" actions .verification/zakclassic-flight-20261018/ticket-cancel.steps
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~ScummZakClassicFlightTests" --logger "console;verbosity=normal"
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter "FullyQualifiedName~ScummZakClassicFlightTests|FullyQualifiedName~ScummZakClassicPlayTests|FullyQualifiedName~ScummZakClassicFidelityTests|(FullyQualifiedName~SupportConformanceTests&DisplayName~ZakClassic)" --logger "console;verbosity=normal" --logger "trx;LogFileName=zakclassic-flight.trx" --results-directory .verification/zakclassic-flight-20261018/test-results
```

Local diagnostic text is ignored, retained in this worktree: `source.txt`, `globals.txt`, `room1.txt`, `room3.txt`, `room8.txt`, `room46.txt`, `cash-before.txt`, `run-before.txt`, `bus-attempt.txt`, `bus-center.txt`, `alias-before.txt`, `terminal-after.txt`, `flight-positive.txt`, `ticket-cancel.txt`, `flight-final.txt`. Harness code and input steps are committed. Final recipes explicitly assert owner/variable/room/control results and reject unsupported v2 operations. No game bytes or generated captures are committed.

## Failed approaches retained

The first diagnostic omitted its fixture argument and correctly failed to find a folder named `global`. A fresh prepared airport followed by `Settle` left the room before direct activation, so that diagnostic was not gameplay acceptance. A subsequent source observer read a null lifecycle site; filtering null sites corrects the diagnostic, without a runtime change. Initial inventory code mistakenly used the later-engine ego key and item property names; the corrected harness uses `CaptureInventoryStatus.CurrentActorId`, `OwnerId` and `ObjectId`.

Two native bus input approaches are retained and parked: a top-left object rectangle point selected overlapping `14th Avenue`, and a center retry retained an existing `Use bus with ...` sentence before clearing its selected second object. Neither paid a fare or earned airport arrival; no engine defect is inferred from them. A fresh source-derived bus-wake preparation, if independently demonstrated, is separate new evidence. No blind third geometry/gesture retry is authorized by this record.

Classic independently observes the source alias issue reported by Enhanced: after CashCard preparation, a Room8 preview has physical8/logical144; the second alien cutscene's genuine Escape returns physical8/logical8. That sample cannot establish a valid initial-ticket departure or genuine flight. The shared owner investigates any engine correction, and final acceptance must use a source-backed airport context.

The initial new test assumed `with` for the ticket's preposition and failed on the real `Use ticket in plastic card`; only the expectation was corrected. No game language/offset exception was added.

The two later transaction tests initially expected a rendering actor snapshot to expose logical144. Its documented rendering room is physical8; word4 already correctly retained144. Correcting that assertion made both tests pass, without an engine change. A staging attempt found that global generated-file rules ignore CSV; the small replay inputs use `.steps`, and their tracked names are recorded here. No ignored game/generated data was force-added.

## Validation and limits

First corrected focused run: **4 passed, 0 failed**, 18.00 seconds: source identities/boot ticket, CashCard positive/negative, wrong-airport refusal preserving ticket/departure/destination. The initial scoped run added the actual class-query observer assertion and included Classic Play/Fidelity and its family conformance row: **13 passed, zero failed/skipped**, 43.79 seconds, zero build warnings/errors. The successful `cash-recovery.steps` harness independently reaches owner18=1 and `Yay, it's my CashCard.`; `cash-after.txt` retains the local output. Parent owns final WPF/build/matrix/full-suite results. No ScummVM execution, data/profile/ROM/save modification, junction, stash, force or worktree removal. Subjective timing/listening and complete-game acceptance remain separate.

After the logical-room correction, all **6 Classic flight tests passed**, 59.33 seconds. This adds the actual Script115 ticket-consumption, Script116 pickup and Script104 post-wait observations, plus native Cancel/refund/no-ticket refusal. The final positive and Cancel harness recipes with all three shared prerequisites and explicit state assertions both exit0 (`flight-final.txt`, `cancel-final.txt`); Cancel retains the exact no-ticket refusal. The final expanded Classic flight/Play/Fidelity/conformance run is **15 passed, zero failed/skipped**, 79.98 seconds, zero build warnings/errors. Local ignored TRX: `test-results/zakclassic-flight.trx`. Shared engine/LOOM safeguards and actual-window evidence belong to the Core owner/parent, avoiding a duplicate full suite.

ScummVM comparison is source only: native OBCD masks and logical actor-room semantics follow the Core owner's official `script_v2.cpp`/actor/camera source review of 18 October 2026. The Classic instruction anchors above and the terminal choice geometry (Buy657 rectangle48,104,64,16; Cancel658 rectangle208,104,64,16) were independently decoded from this fixture. Source execution does not establish original-game pictures, physical event delivery, timing or audio synchronization. The harness retains a source `I can't reach it.` line before boarding; it does not suppress messages or turn the bounded result into a full-fidelity claim.

Game facts belong in the parent's [Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md); tracker/checkpoint links are updated by the parent. No proposed requirement is needed for native header predicates, payment or player-chosen terminal actions.
