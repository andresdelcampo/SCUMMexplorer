# Maniac Mansion enhanced S8: explicit inventory ownership choices

| | |
|---|---|
| Family / stage | ManiacEnhanced / S8 |
| Fixture and edition | `D:\Juegos\Lucas\mmansionE ega es`, Spanish DOS v2 |
| Date and author | 2026-10-18, Codex |
| Commits | API `30b2ccd`; attribution/round-trip tests `e25aca2`; running-action guard `2d74a55` |
| Registry change | Parent owns Status promotion after real-window Apply/Cancel/Undo acceptance. No other cell changes. |

## What was done

Implemented the generic v1/v2 inventory status API in a separate session partial, with source-derived selected characters and reached actor names. A captured immutable view exposes the current character/room and held items across the selected party. Explicit owner-only transfers validate the whole batch before writing; opaque session-bound tokens prevent forged snapshots, cross-session undo and stale overwrites. Apply and Undo produce separate Advanced choice attribution, never played-scene events. Cancel discards a view without an API mutation.

The new Proposed R-12.7–12.10 (`d3103ed`) precede implementation. This is an explorer choice, not native Give gameplay or a simulated pickup. Slots, retained rooms/code, intrinsic states, classes, actors and unrelated words remain unchanged. It cannot create, destroy or drop an item, select an unchosen kid, or transfer to an NPC.

## Commands run (through RTK)

```powershell
rtk proxy dotnet build src/SCUMMexplorer.Core
rtk proxy dotnet test tests/SCUMMexplorer.Tests --no-restore --filter "FullyQualifiedName~ScummManiacEnhancedStateTests|FullyQualifiedName~LoomBootTests|FullyQualifiedName~LoomCdBootTests"
rtk proxy dotnet test tests/SCUMMexplorer.Tests --no-restore --filter FullyQualifiedName~ScummManiacEnhancedStateTests
rtk proxy dotnet run --project .verification/maniacenhanced-state-20261018/StateCheck -- "D:\Juegos\Lucas\mmansionE ega es"
```

The same bounded harness was rerun only to verify the later running-action guard and final explicit attribution prefix. `result.txt` contains the final route and own-script facts; `tests.txt` the final focused result. Parent owns WPF, family conformance and the full suite.

## Results (numbers)

- Shared prerequisite: Core build, zero warnings/errors; seven focused tests passed (initial selected-party test and six LOOM EGA/CD boot regressions).
- Final enhanced tests: three passed, zero failed, zero skipped.
- Native route: 1553 frames after boot, 12 queued native inputs, plus the genuine Esc cutscene skip. Room 1, Dave (actor 3), key 55 `llave`, native slot 0, retained room 1.
- Round trip: key owner 3 → 1 → 3. All global words, object states/classes, actors, room and scene count preserved; slot, retained room and name preserved. Only the requested owner changes and two explicit-choice trace records occur.
- Rejection tests: mixed valid/invalid batch (no partial write), duplicate item, room-owned mat, NPC, unselected kid, stale snapshot after frame or edit, cross-session tokens, reused undo, queued input, menu and cutscene. Empty/no-op batches preserve the valid prior undo token.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Official master source reviewed 18 October 2026:

- [script_v2.cpp](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/script_v2.cpp), `o2_actorOps` lines 686–725: suboperation 3 retains an actor-name resource; implemented only in the existing v1/v2 executor partial.
- The same source, `o2_waitForSentence` lines 678–683: both queued sentences and the running sentence script represent unfinished work. The edit guard also refuses queued input, active input/object scripts, any executing script, movement, dialogue, cutscenes, boot and disabled user control.
- [object.cpp](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/object.cpp), `putOwner` lines 281–285: ownership is a separate table. This API performs only that component as explicitly required by R-12.8, without running native inventory callbacks or pickup effects.

The copy's Script 4 assigns ego from the selected-party variables and immediately follows that actor with the camera. Those instruction pairs derive the eligible values from live data; no party-variable or actor-ID table is hard-coded. Reached Script 1 actor-name instructions supply their labels. The harness logs exact own-copy offsets; canonical facts go to the Maniac script map. No ScummVM executable was run, and no original-game runtime comparison is claimed.

## Differences and uncertainties kept

This inventory reassignment is an explicit exploration choice, not the game's Give action; distance, puzzles or story progress are not simulated. Undo is deliberately conservative: any intervening frame invalidates it, even if the inventory appears unchanged. It is intended for the paused modal window. Unresolved names/recipient contracts fail closed; absent or unreviewed font executables retain the earlier fallback documented in S9.

Prior S5 visual comparisons, darkness/flashlight fidelity and bounded S6/S7 execution uncertainties remain open. No alternative enhanced edition is available; S9's blocked comparison remains. The initial no-restore build needed a normal restore in the new worktree; a tuple-return compile typo while adding the guard was fixed before any checkpoint. No failed gameplay approach was hidden.

Integration correction: guard `2d74a55` rejected every pending object script. Zak's genuine bedroom retains special FD animation handlers forever (objects 101/95/107), so its four transfer tests failed and waiting 3000 frames did not help. These failures remain in the Zak/parent evidence. The correction uses the existing native slot flags from `o2_doSentence`: FA is background+special, FD special, ordinary foreground neither. Pending special/background handlers do not mean a player action is active; ordinary pending object scripts, native input/sentence scripts and all executing code still block. A slot-lifecycle regression checks both sides, including an ordinary and special instance of the same object. Twelve focused tests passed after this correction (three enhanced state, three slot lifecycle, six LOOM boot).

## User checks (exact steps and where; none if none)

Parent verifies the real window from the native S6 route: choose Syd and Bernard, start, Esc after the override is available, exit the driveway, Pick up the mat then key. Open Game Status, reassign `llave` from Dave to Syd, Apply, Undo; change the pending recipient and Cancel to confirm no edit. The normal window shows the explicit-choice label; raw source and edit provenance appear only in Advanced. Parent records the exact WPF result in the combined S8 checkpoint.

## Where the facts went (links to the script map / engine notes / tracker entries)

The family worker owns [Maniac script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md) and [tracker](../../docs/maniac/SUPPORT_STATUS.md). Parent owns shared engine notes, requirements, registry/matrix, App, final checkpoint and handoff. Tests: `ScummManiacEnhancedStateTests`; API: `ScummSessionInventoryStatus`.
