# Zak flight preparation: integrated verification

| | |
|---|---|
| Families / fixtures | ZakClassic `D:/Juegos/Lucas/zak ega en`; ZakEnhanced `D:/Juegos/Lucas/zakE ega en`, supplied English v1/v2 |
| Date / owner | 18 October 2026, parent Maniac/Zak orchestrator |
| Baseline | Master `7b6b4009`, including prior Maniac Use-with and other orchestrator's Indy4 S7 work |
| Shared corrections | `07d9eb1` / integrated `af019f6` class masks; `a425b0a` / `2aed0bb` actor room identity; `7d4a2cb` / `0d58274` same-resource follow dispatch |
| Family proofs | Classic `65db70f` + `571b865` / integrated `8f1fe00` + `c6b351e`; enhanced `45d522d` / `32b56fb` |
| Registry | Unchanged: bounded native preparation does not provide a hand-validated catalog or settle Classic costume estimates |

## What was done

Three isolated workers independently traced both editions, supplied a sole shared-Core owner and reviewed the result. Object51 is peanuts, not a ticket; tickets82/83 are retained from Room46. The old wait observation remains valid as an unprepared context sample, but its ticket interpretation was wrong. Persistent scripts06/09 did not establish an unfinished boot; that early inference was explicitly retracted in the [review history](../zak-flight-review-20261018/README.md).

Native knife/card Use exposed an unimplemented old class-mask predicate. It now reads OBCD byte6 from current or retained held code, using masked equality rather than later class numbers. Missing code always jumps; unreadable code remains unsupported. Native actor logical room numbers now survive physical resource mapping and scene changes, including camera follows between aliases sharing one resource. The rejected idea of changing Zak's saved cutscene room was not implemented: its end operation follows ego. One review finding in the follow guards was corrected and independently rechecked.

Two final private real `MainWindow` processes start the actual Play session and verify its timer advances. They then drive its actual frame callback deterministically. Profiles, cache and failure logs are private. The harness does not assign actor coordinates, owners, money, ticket words or puzzle flags; no shipping App source changed.

## Checked route and exact commands

Preparation is disclosed exploration: Room2 preview, native alien-override Escape, direct native knife26 Pick up, Room1 preview, native Use of held knife26 with plastic card106, then Room8 airport preview and the second native alien skip. It is not an earned bus/airport journey. The interface recovers CashCard18 and retains logical airport144 across the scene; physical resource8 is distinct from that native number.

The actual reservations choices are visible: Use terminal 194, select the rendered **San Francisco** option 663, then **OK** (native confirmation/Buy 657). Refunding the old $260 ticket raises the balance from 12250 to 12510; the new 260 fare leaves 12250, ticket 82 held, departure 144 and price 260. Native Walk to gate 190 consumes ticket 82 to owner 14 and clears departure. Within 2500 more frames, safety/meal service finishes in Room 11 with control, peanuts 51 held, and no pending Script 104 guard. This does not establish landing: Room 11 object 241 retains its native 28800-jiffy cruising timer. No production cap was increased or timer bypassed.

Working directory: `D:/Documents/Personal/Code Projects/SCUMMexplorer/.worktrees/zak-flight`.

```powershell
rtk proxy dotnet build .verification/zak-flight-20261018/WindowCheck/WindowCheck.csproj --verbosity quiet -p:NuGetAudit=false
rtk proxy Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.exe "D:\Juegos\Lucas\zak ega en" ".verification/zak-flight-20261018/window-classic-flight" ".verification/zak-flight-20261018/classic-flight.json"
rtk proxy Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.exe "D:\Juegos\Lucas\zakE ega en" ".verification/zak-flight-20261018/window-enhanced-flight" ".verification/zak-flight-20261018/enhanced-flight.json"
```

Final harness build: zero warnings/errors,8.13s. Both final processes exit0: Classic window frame3754, enhanced4076, eleven delivered native inputs each. These callback counters exclude some boot/preparation work and are not original-game timing measurements. Both check returned control, native ticket/peanut/departure values, Normal tooltip/note cleanliness and exact equality of actual displayed bitmap with composed Play frame. Rendering/capture changes neither serialized snapshot nor frame counter.

Ten final captures were inspected: each edition's `airport-restored`, `reservations`, `destination-selected`, `ticket-received`, `boarded-meal-served`. Both show the native choices, visible hero at the airport and in the cabin, ticket/inventory changes and peanuts. Local outputs, including private cache/profile and `result.txt`, are in the two `window-*-flight` directories beside this file and ignored. Original-game picture/placement/timing acceptance is still separate.

The earlier enhanced Card-only window also passes (frame945); its two inspected captures establish the native sentence/recovered card prefix, not the entire flight route. An initial Classic Card-only window fails because the harness hard-coded sentence y156 and hit Push. It is retained under `window-classic-card`; the final recipe uses dynamic `MainScreen.Bottom+4` and passes. No Core change was made for that harness error. The final harness says “configured Zak actions” so a prefix cannot be mistaken for full route acceptance.

## Regressions, differences and limits

Classic final family/Play/Fidelity/conformance: 15 passed, 0 failed/skipped. Enhanced family/class-mask/actor-room/executor/LOOM safeguards/conformance: 55 passed, 0 failed/skipped. Counts overlap and are not additive. Both own-source routes pin the actual class predicate, wrong tools, native ticket refund/Buy/Cancel, gate guards and peanut service. Enhanced also checks missing CashCard. Native Cancel leaves balance 12510 after the old-ticket refund, ticket 82 owner 14, departure 0 and price 0; candidate 663 remains but the gate says `I need a ticket first.` It is not turned into a valid ticket by the harness.

Both workers retain two failed bus input/preparation approaches and stop that item. The successful airport preview is a separate disclosed preparation supported by corrected native room identity; no third guessed bus route is accepted. Corrected test assumptions/filter mistakes and the reviewer's source-inference correction remain in their records. A Classic unverified airport city label was removed during integration; the verified state uses native room identity and rendered choices.

No ScummVM executable ran; no game/profile/ROM/save files changed, no game assets committed, no Games junction/worktree removal, no forced state or automatic choices. Full timed travel/arrival, an earned bus journey, other destinations/puzzles, original-game visual/timing/listening and unavailable-release checks remain. No catalog, shipped seed or new requirement proposal.

The merged solution builds with zero warnings/errors (4.84s), and regeneration with `SCUMM_UPDATE_SUPPORT_MATRIX=1` passes all three matrix checks. Required full suite: **1339 passed, 0 failed, 5 skipped (1344 total)**, exit 0, reported 12m43s; all 26 new cases pass. Sound, Indy3 EGA and MT-32 roots are set under `D:/Juegos/Lucas`. Local ignored result is `test-results/zak-flight-final.trx`; exact command and clean integration record are in the [checkpoint](../../docs/checkpoints/20261018-Flight-ManiacZak.md). Facts are in the [game map](../../docs/zak/ZAK_SCRIPT_MAP.md), [engine notes](../../docs/engine/SCUMM_SCRIPT_MAP.md), [support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries) and dedicated handoff. The commands and JSON above reproduce the bounded check; no repeated subjective user check is requested.
