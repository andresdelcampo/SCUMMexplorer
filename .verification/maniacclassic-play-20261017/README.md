# Maniac Mansion Classic S6: native input route

| | |
|---|---|
| Family / stage | ManiacClassic / S6 |
| Fixture and edition | `D:/Juegos/Lucas/mmansion ega en`, DOS v1 English |
| Date and author | 2026-10-17, Codex family agent |
| Commits | Charset prerequisite `efe8ed5`; route result below |
| Registry change | Parent integration owns the matrix; bounded native route verified, S5 original-game visual limits retained |

## What was done

Requirements: section 7.1, especially the native old-engine interface and startup/choice adaptations proposed as R-7.19 to R-7.21 in this run. The remaining sentence, exit, pickup, displayed inventory, override skip and real-window criteria remain required.

The native interface has fifteen verbs, including Read, What is, Unlock, Fix and New kid. It has no Talk to verb. The actual character-selection menu supplies the player's choice for the early-engine acceptance adaptation, rather than adding an invented dialogue interface. All route inputs start from boot; there are no catalog visits, direct object-verb calls, or story-variable writes.

The separate `ScummV2Charset` prerequisite supplies the source's built-in 8-by-8 DOS font, inventory arrows and native ASCII-slot accent replacements. The English v1 apostrophe differs from English v2; native Spanish substitutions are separate font data. Rendering reuses the existing one-bit drawing, as ScummVM's v2 renderer inherits v3.

## Commands run (through RTK)

```powershell
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter FullyQualifiedName~ScummManiacClassicPlayTests
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~ScummV2CharsetTests
rtk proxy dotnet run --project .verification/maniacclassic-play-20261017/Route/ManiacClassicPlayRoute.csproj --no-restore
```

## Results (numbers)

Final focused checks after the shared direction and inventory correction `e156b55`: eight tests pass, zero failures and zero skips (two classic route tests, two charset tests, four retained S5 first-visit cases, including Room 2). Earlier, the six-test play/charset/old-interface set also passed. The second route uses the same session for party choice, introduction skip, source exit, camera movement, sentence composition, mat lift, key pickup and inventory selection. The final standalone route takes 1993 frames after the constructor, fifteen queued clicks/keys plus Escape. No source operation on that route is reported as unsupported v2.

The displayed sentence is `Pick up door mat` before execution, with colour-13 pixels on the native sentence line at y=152. The acquired key paints 63 pixels in the first inventory slot at y=184..191. A click on that slot is delivered as inventory area 3, object 55, and displays `Walk to key`; the test checks the pixels and the native hit dispatch, not only the owner table.

| Input | Observed result |
|---|---|
| Boot; screen clicks `(76,120)`, `(196,120)`, `(292,76)`, 100 frames each | Native party selects Syd and Bernard; Start begins the introduction |
| Wait for Room 44 and its skip point; Escape; wait for control | Script 131 resumes at its skip jump and restores control |
| Screen `(8,136)`; wait for Room 1; 120 frames | Native exit reaches Room 1 at the right edge, view-left 960 |
| Four screen `(8,136)` clicks, 200 frames after each | View-left 808, 656, 504, 352; ego x=512 |
| `d`; screen `(112,108)` once | Native Pick up sentence targets object 54 (mat) |
| Same point again; wait for mat state 8 | Source response lifts the mat and exposes its child key |
| `d`; screen `(152,108)` twice; wait for owner 3 | Object 55 is picked up by Dave |
| Screen `(8,188)` | The displayed inventory key is selected and the sentence reads `Walk to key` |

Coordinates use the current native screen top of 16. One frame follows each key/press; a second press is delivered separately, never synthesized by the app as a double click. The source input script composes on the first object press and executes on the second.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Source reference: upstream ScummVM master `engines/scumm/charset-fontdata.cpp`, `charset.h`, `charset.cpp`, read 17 October 2026. Font and language tables are GPL-3.0-or-later, attributed in `THIRD_PARTY_NOTICES.md`. The shared adapter follows `verbs.cpp`, `string.cpp`, `actor.cpp`, `boxes.cpp` and `script_v2.cpp`; its engine owner records those changes. Source-only comparison: native party clicks select Syd and Bernard, Start plays the opening, and Esc at Script 131's override ends the outdoor conversation and returns control. Walking left leaves the gate screen; at the front door, Pick up door mat reveals the key and Pick up key adds it to Dave's inventory. Clicking that inventory entry composes Walk to key. These are the decoded original flow and the app's source execution; no ScummVM executable was launched, and original-game visual and timing acceptance remain open.

## Differences and uncertainties kept

- The first exit probe (before shared S6 fixes): party preparation, room click `(8,120)`, 500 frames left ego 3 at `(192,118)` in Room 44. Script 2 reached its `I can't reach it` branch. The engine owner identified the old room reader discarding the native walkbox matrix; this failed evidence is retained here.
- The first pickup probe used only one object press. It composed the sentence but did not execute it; the next verb key executed the pending sentence and the following room click interrupted that walk. Delivering the native second press completed the route, without an engine workaround.
- Read-only regression diagnosis after the walking change found both editions' Room 2 previews stuck in turn state 4: `(144,16)`, walkbox 7, facing 0 versus target 180 for 300 frames. The box constrained the visible direction while completion compared the unconstrained target. The source-native direction mapping in `e156b55` resolves it; the original classic S5 test passes without a weakened baseline.
- A first diagnostic source dump requested an object verb absent from the object's table and raised `InvalidDataException`; the diagnostic was changed to enumerate actual entries. It was a harness mistake, not an unsupported game instruction.
- The first sandboxed test host stalled; the focused tests passed through the Windows host with the required tool permission.
- S5 source previews, native black rectangles, shown-lit dark rooms, seeded-route labels and unperformed original-game comparisons are unchanged.

## User checks (exact steps and where; none if none)

In the app, open the classic English fixture and enter Play. Choose Syd, Bernard and Start; when the outdoor introduction allows Skip, press Escape. Walk off the left edge, then walk left across the next scrolling room to the front door. Press `d`, click the doormat once to compose the sentence and again to lift it; repeat Pick up on the revealed key. Click the key in the inventory. The exact native screen points and waits are in the route table above. Check the visible walking/turning, sentence and inventory legibility, sound and perceived timing. These perceptual checks are not established by source execution. The parent real-window result is recorded in the run checkpoint.

## Where the facts went (links to the script map / engine notes / tracker entries)

Script facts belong in [the Maniac script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md); stage status in [the tracker](../../docs/maniac/SUPPORT_STATUS.md). This record distinguishes source execution and app tests from original-game observation.
