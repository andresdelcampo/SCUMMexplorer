# ZakEnhanced S8: guarded inventory reassignment

| | |
|---|---|
| Family / stage | ZakEnhanced / S8 |
| Fixture and edition | `D:/Juegos/Lucas/zakE ega en`, English enhanced v2 |
| Date and author | 2026-10-18, Codex Zak agent |
| Commits | Shared API `30b2ccd`; family commit containing this record |
| Registry change | Unchanged here; parent owns UI acceptance and registry |

## What was done

Booted normally, opened drawer 92 and picked up phone bill 15 with native keys/clicks. Captured inventory, explicitly reassigned the held bill from Zak to Annie, then undid the edit. This is an exploration choice, not a claim that the game's Give verb permits this transfer across rooms. No item or story flag was manufactured.

## Commands run (through RTK)

From this worktree: `rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter FullyQualifiedName~ScummZakEnhancedStateTests -p:RestoreIgnoreFailedSources=true -p:NuGetAudit=false`.

`rtk proxy dotnet run --project .verification/zakenhanced-state-20261018/StateCheck/StateCheck.csproj -- "D:\Juegos\Lucas\zakE ega en"`.

## Results (numbers)

Final revalidation after shared `f119be4`: three tests pass, zero failed/skipped, and the bounded harness passes. At the end of the existing genuine pickup route, InitialRefusal is null and SettlingFrames is 0, despite the still-resident native background handlers. Apply and Undo both succeed, owner returns to 1; no extra game action or story-state assignment was needed. The explicit 3000-frame CanEdit cap remains as a readiness guard for future timing changes.

Three tests passed, zero failed/skipped. One bounded harness completed: Room 1, control on, cutscene false; player range 1..4; bill owner 1 → 2 → 1, inventory slot 2 and retained room 1 unchanged. Native inventory also contained ticket 82 (owner 1, slot 1, retained room 46) and NPC ticket 83 (owner 14, slot 0, retained room 46); neither changed.

All object states/classes/positions, global variables, actors and pending script descriptions remained unchanged across Apply. Undo restored all owners; replayed Undo was rejected. Native Read after Undo displayed `$1138!`. Cancel-by-discard had no writes. NPC recipient 7, unheld drawer 92, an invalid mixed batch and a stale snapshot were rejected. A genuinely reached first-exit cutscene refused editing.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Local source reviewed 2026-10-18: `engines/scumm/actor.cpp`, `Actor_v2::isPlayer`, uses variables 42..43 for Zak's player range; `script_v2.cpp` supplies native actor naming and pickup semantics. Own Script 108 names actors 1..4 Zak/Annie/Melissa/Leslie at +00DE/+00E6/+00F0/+00FC and sets player range at +0182. Room 1 object 15 verb 14 executes `pickupObject var[0x009]` at +0025. No ScummVM executable or original-game runtime comparison was used.

## Differences and uncertainties kept

Final-guard integration failure retained: the initial three-test pass preceded shared `2d74a55`. Parent's integrated run then failed the two editable-state tests in each Zak family. A bounded 3000-frame wait still refused editing because the guard treated every resident object script as a player action. Enhanced diagnostics showed no running input script 4 or sentence script 2, but persistent Room 1 background verbs 0xFD for objects 101, 95 and 107. Own object 107 +0054/+0057/+0058 random/break/jump loop and object 101 +0047 delay120/+004B backward jump prove waiting alone cannot finish them. No Core guard was bypassed and no game state was assigned; the shared owner was notified. Tests and harness now explicitly wait for `CaptureInventoryStatus().CanEdit` (3000-frame cap) before editing.

Native switchability has additional Script 4 guards; the recipient list proves player membership, not current switchability. Raw item name includes trailing `@@@@`; presentation must remove native padding. NPC-held inventory remains observable internally but is not eligible for editing. S5 visual comparison/colour limits, S7 capped previews and S9 absent alternate editions remain unchanged. Harness emitted NuGet vulnerability-feed warnings because the network was unavailable; execution succeeded. Parent owns actual-window Apply/Cancel/Undo checks.

## User checks (exact steps and where; none if none)

Reproduction in Play at native 320×200 coordinates: wait 150 frames; key W, click (50,81), sentence (10,145), wait 180; key D, click (55,78), sentence (10,145), wait 180 (advance two frames after each key/click). Open Game Status, assign phone bill from Zak to Annie, Apply; inspect both inventories; Undo and inspect restored bill. Cancel a pending reassignment to confirm no write. After closing, key X then O then sentence (10,145) reads the restored bill. Automated Core checks completed; parent performs WPF checks.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Script map](../../docs/zak/ZAK_SCRIPT_MAP.md), [tracker](../../docs/zak/SUPPORT_STATUS.md), `ScummZakEnhancedStateTests.cs` and the adjacent reproducible StateCheck harness.
