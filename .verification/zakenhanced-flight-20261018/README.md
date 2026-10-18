# ZakEnhanced flight preparation and ticket state — 18 October 2026

| | |
|---|---|
| Family / scope | ZakEnhanced; requested flight preparation and ticket-state follow-up |
| Fixture | `Games/zakE ega en`, supplied English v2; `TestGames.Root` = `D:/Juegos/Lucas` |
| Worker / worktree | Codex; `.worktrees/zak-enhanced-flight`, branch `codex/zak-enhanced-flight` |
| Registry | Unchanged. Bounded native reservation/boarding/safety/service proof does not establish a hand-validated catalog, earned full puzzle route or full-game support. |
| Prerequisite commits | `07d9eb1` native OBCD class masks; `a425b0a` native actor room identity; `7d4a2cb` same-resource logical room dispatch. Parent owns shared maps, tracker, support notes, matrices and final integration. |

## What was done

The old Script 104 owner-51 guard is **peanut meal service**, not a ticket check. Native names and own source establish ticket objects 82/83 in Room 46. The opening already gives Zak ticket 82, with departure 145, destination 662 and value $260. The earlier fresh Room 11 preview did not earn boarding or establish the stewardess context; its retained wait remains evidence of that preview, not a demonstrated ticket-engine defect.

This pass first establishes a genuine CashCard acquisition response: explicit Room 2 preview and native butterknife pickup code (object 26 / verb 14), explicit Room 1 preview, then actual displayed Use → held butter knife → plastic card → plastic card inputs. The retained knife header is read from Room 2 while Room 1 is current. The same native sentence using an unsuitable held ticket refuses without acquiring the CashCard. These are exploration preparation followed by native puzzle responses, not a fully walked route from boot.

Next, explicit Room 8 exploration preparation reaches its own alien cutscene; native Escape skips that reached scene. The logical airport number is preserved. Actual Use of reservations terminal 194, actual destination click and Buy/Cancel clicks execute the game's purchase/refund state. Buy followed by native Walk to gate 190 runs Script 115, Room 11 entry/Script 121, Script 104's safety presentation and Script 116's actual meal pickup. By the bounded 2500-frame post-gate observation, Zak has control in Room 11, ticket 82 is consumed, departure is cleared and peanuts 51 are held. No owner, flag, currency, destination, actor placement or timing prerequisite is assigned.

## Commands run (through RTK)

From this worktree:

```powershell
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~ScummZakEnhancedFlightTests|FullyQualifiedName~ScummV2ActorRoomTests|FullyQualifiedName~ScummV2ClassMaskTests|FullyQualifiedName~LoomBootTests|FullyQualifiedName~LoomClickInputTests|FullyQualifiedName~LoomCdRunningSetupTests|FullyQualifiedName~ScummV2ExecutorTests|(FullyQualifiedName~SupportConformanceTests&DisplayName~ZakEnhanced)" --verbosity quiet
rtk proxy dotnet run --project .verification/zakenhanced-flight-20261018/FlightCheck -p:NuGetAudit=false -- "D:/Juegos/Lucas/zakE ega en" route .verification/zakenhanced-flight-20261018/board.csv
```

Source modes `dump`, `room`, `object` and `find` in the same harness read the supplied edition through its data source. Generated `.txt` logs remain local/ignored. The earlier `SceneCheck one 11 0 0` command reproduced the historical unprepared preview; no full scene census or full test suite was run by this worker. Parent owns actual-window and integration checks.

## Results (numbers)

- Final scoped filter: **55 passed, zero failed/skipped**, 1 minute 15 seconds. This includes four Enhanced route cases, seven native room-identity cases (two full-session alias dispatch cases), nine mask cases, the existing v2 executor and named LOOM EGA boot/click plus CD running-setup safeguards, and one Enhanced conformance row.
- Earlier non-overlapping prefixes are not added to that count: 15 mask/preposition cases, 34 executor/LOOM safeguards and 48 room/mask/executor/LOOM cases all passed.
- Wrong held ticket cannot retrieve CashCard; correct knife acquires owner 18 = 1 with its own class mask. Ticket 82, knife 26 and $12,250 stay intact.
- Without CashCard, native terminal refuses; boot ticket 82 remains owner 1 / departure 145 / destination 662 / value 260 / cash 12,250.
- Native terminal initially refunds the old $260 ticket: cash 12,510; owner 82 = 14; departure and ticket price cleared. Cancel after selecting candidate 663 keeps that refund, does not issue a ticket, and native gate says `I need a ticket first.` The candidate destination remains as the source leaves it; Cancel is not falsely called an unchanged-state operation.
- Buy candidate 663: cash 12,250, ticket 82 owner 1, departure 144, destination 663, price 260. Native gate consumes the ticket (owner 14) and clears departure; no ticket is fabricated.
- Native Script 116 supplies peanuts 51, owner 1 / retained Room 11. Script 104's guard releases and its cutscene ends. No flight Escape is sent; user control is on, no Script 104 remains pending.
- The dedicated assertion-bearing `board.csv` harness passes with exit 0: Room 11, departure 0, ticket 82 owner 14, peanuts 51 owner 1 and control all match. Generated output is retained locally as `board-final.txt`.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Official source read 18 October 2026: [script_v2.cpp](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/script_v2.cpp), `o2_ifClassOfIs`: read OBCD byte 6, test `(classByte & operand) == operand`, unconditionally jump when no object header exists. This is a bit mask, not later numbered classes. The session resolves current room or retained inventory code; unreadable code fails closed. Missing-object mask zero still jumps. Synthetic guards pin high bits/subsets, both versions, missing/unreadable headers and independence from the later class table.

The native actor keeps its logical room number; [camera.cpp](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/camera.cpp) `setCameraFollows` starts that actor's scene. Both Zak editions' `o2_endCutscene` follow ego; the saved-room restoration branch is only classic Maniac. Prior physical normalization lost an airport alias when the camera left it, so return changed `VAR_ROOM` from 144 to 8 and the terminal could not build the native choice table. Per-actor logical identity now survives separate from the physical room key used by rendering/walking. Queries, follows, with-ego loads and same-resource alias changes preserve the native number. Later engines retain their existing behavior. Synthetic tests cover alias changes, per-actor isolation, coordinate-only placement, physical mutation invalidation, zero room, later versions and full-session dispatch.

Independent reviewer verified both source rules and caught the two same-resource dispatch guards before final integration; `7d4a2cb` addresses them. No ScummVM executable was run. Source execution, prepared Explorer checks, synthetic rule tests and original-game observation remain distinct.

## Differences and uncertainties kept

Initial diagnostic object 82 / verb 17 was absent; actual ticket verbs are 3/11/12. One diagnostic entered the airport before the reached alien cutscene finished and tried an action in Room 16; rejected. The first held-inventory harness used the later engine's ego slot; corrected to `CaptureInventoryStatus.CurrentActorId`. Native card response then reached the demonstrated unsupported `ifClassOfIs`; this is the before-fix failure, retained separately from the passing result.

Two distinct bus-input preparations did not establish paid transport: the first composed a retained `Use bus with CashCard` sentence; after source helper preparation and clearing the earlier sentence, the second composed `Use CashCard in CashCard reader` but did not charge a fare or leave Room 3 in its bound. Bus work stopped; no guessed retry, forced fare or state repair followed. The passing reservation/boarding route instead discloses explicit Room 8 exploration preparation. The final source trace of the failed bus preparation remains local. No earned bus journey is claimed.

Before actor-room retention, a reached airport alien skip returned physical 8 in `VAR_ROOM`; a later reservation click had no source destination and only retained the source refund/no-ticket result. After the source correction, the same preparation retains alias 144 and actual reservation choices work. Two initial synthetic camera tests omitted the event handler and failed as unapplied presentation; corrected. Two initial session-dispatch tests had no loaded room and could not advance; corrected with an explicit initial room preview. These were test setup errors, not accepted engine behavior.

This proof ends after boarding, safety and meal service. The native Room 11 object 241 / verb 253 has a 28,800-jiffy in-flight delay; at the 2500-frame gate bound, 17,484 jiffies remained in the observed run. Flight landing/arrival, other airports/destinations/characters, original-interpreter visual/timing/listening acceptance and broader puzzle routes are not claimed. No catalog, seed, prerequisite solver or terminal restart is introduced. Existing Classic palette, walking and S5 visual limits remain. Game/profile/ROM/save data were untouched; no junction or worktree was created in game data, and no worktree was removed.

## User checks (exact steps and where; none if none)

Parent's actual-window recipe follows the same labelled preparation and native interactions; see the coordinated checkpoint. No repeated subjective timing/listening request is made by this worker.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent integrates own-edition rows in [Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md), [tracker](../../docs/zak/SUPPORT_STATUS.md) and engine notes. Useful Enhanced anchors: Room 1 helper 93 / verb 253 `+0013/+001E` class masks, `+0029` pickup 18; Script 30 refund `+0043..005B`, choice `+00D3..01AE`, payment/departure/price `+01D2..01F6`, ticket owner `+023C`; Script 115 guard `+0000..0026`, ticket consume `+007E`, departure clear `+0089..0090`, boarding `+0147..014E`; Script 104 meal guard `+01C5/+01C9`, end `+020B`; Script 116 actual peanut pickup `+0018..0025`. Room 45 object 657 Buy sets choice 1, object 658 Cancel sets 0; native candidates carry their own object number. These offsets are decoded + Explorer play evidence from this Enhanced copy; Classic offsets are independently derived by its worker.
