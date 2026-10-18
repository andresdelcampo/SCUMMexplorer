# Maniac/Zak Game Status: selected party and native body pictures

| | |
|---|---|
| Family / stage | ManiacClassic, ManiacEnhanced, ZakClassic, ZakEnhanced / Game Status correction |
| Fixture and edition | Under `D:/Juegos/Lucas`: `mmansion ega en`, `mmansionE ega es`, `zak ega en`, `zakE ega en`; no Games junction |
| Date and author | 2026-10-18, character-art agent |
| Commits | This change; parent records the final integration checkpoint |
| Registry change | Unchanged: recipient/picture correction within existing Status contract |

## What was done

Maniac's recipients now come only from current party bindings identified in its native input script. Filling menu slots alone is insufficient: native startup after Start must have placed every selected actor in a nonzero room. That commitment is remembered so a later hidden/removed party member does not erase all recipients. NPCs and unselected children are not targets. Unearned grants and exact Undo remain supported for the chosen three.

`ReadInventoryCharacterAppearance` returns a detached standing actor snapshot. It starts the native v2 showActor sequence (stand, init, talk-stop) in private limb state: off-room Annie therefore has a picture even though her live limbs have never been decoded. When an eligible actor's costume is zero, a fallback requires a unique nonzero literal actor/costume assignment in the edition's complete global/room/local/object script census. Ambiguity returns no invented choice. Palette/animation are copied; no source code runs and no live actor or story state changes.

Both Zak copies boot Melissa/Leslie with costume zero in Room26. Their unique nonzero literal assignments are Script81 +0x005A/+0x005E: Classic7/7, Enhanced7/36. These are native small Mars body sprites. Cropped dimensions are Classic18x62/18x62/10x27/10x27 and Enhanced19x64/21x64/9x28/9x28 for Zak/Annie/Melissa/Leslie. Core substitutes no selection portrait and manufactures no larger art.

## Commands run (through RTK)

From `.worktrees/status-character-art`: `rtk proxy dotnet build src/SCUMMexplorer.Core/SCUMMexplorer.Core.csproj --verbosity quiet`; `rtk proxy dotnet run --project .verification/status-character-art-20261018/Probe/Probe.csproj -- "D:\Juegos\Lucas\zakE ega en" none 81` and the Classic equivalent. The focused test filter covers `ScummInventoryCharacterArtTests`, `ScummInventoryAdminTests`, both Maniac CatalogTests and both Maniac StateTests. TRXs are retained locally under `test-results/`.

## Results (numbers)

Core build: zero warnings/errors. Six new cases cover all four pictured rosters and native Start/selected-party gating in both Maniac editions. Existing admin/catalog/state expectations change from seven Maniac recipients to the selected three; complete48-item Maniac catalogs, retained verbs and Undo remain checked. Final focused result: **44 passed, zero failed/skipped**, 1m19s, `test-results/character-art-final.trx`. Shared LOOM status regression: **20 passed, zero failed/skipped**,26s, `test-results/loom-status.trx`. Parent owns integrated full-suite/window acceptance; this agent launched no WPF window.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Uses the existing `Actor_v2::showActor` / animation port in `ScummActors.cs` and `ScummActors.V2.cs`, checked 18October2026. Own-script assignments/startup placements are read by the probe. No ScummVM execution, original-game visual comparison or earned visit to Mars is claimed.

## Differences and uncertainties kept

Prototype tests assumed native bodies were at least30pixels tall; native Mars27/28pixel bodies disproved that assumption. Tests now respect native dimensions without production changes. A prototype grant ran before native startup dialogue/action guards allowed safe edits; final tests wait for CanEdit. The first44-case TRX preserved five failures from these harness issues and historical seven-recipient/target assumptions. No failure counts as acceptance. A premature concurrent rebuild failed because the prior testhost locked its DLL; subsequent checks are serialized. The probe initially had doubled here-string quotes and failed compilation before correction.

The unique literal fallback is display-only, not live appearance/story progress. Existing classic palette uncertainties elsewhere remain. No game data, ROM, profile, save, junction or other family's feature changed.

## User checks (exact steps and where; none if none)

Parent owns the final actual-window route. Optional: finish Maniac's selection and Start, then Status offers only that party with full-body pictures. Zak offers four pictured playable actors; selecting a recipient changes no live actor.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent owns requirements/maps/checkpoint/handoff. Existing [Maniac](../../docs/maniac/MANIAC_SCRIPT_MAP.md) and [Zak](../../docs/zak/ZAK_SCRIPT_MAP.md) maps retain source facts.
