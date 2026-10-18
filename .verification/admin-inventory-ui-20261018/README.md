# Maniac/Zak admin inventory window

| | |
|---|---|
| Family / stage | ManiacClassic, ManiacEnhanced, ZakClassic, ZakEnhanced / explicit admin-status clarification |
| Fixtures | `mmansion ega en`, `mmansionE ega es`, `zak ega en`, `zakE ega en` under `D:/Juegos/Lucas` |
| Date and author | 18 October 2026, admin inventory UI worker |
| Registry change | Parent records the final support decision; this change does not enable another family |

## What was done

The real Game Status window now offers the source-derived item catalog with a name search, recipient choice, pending additions/transfers, native inventory capacity, Apply Changes, Undo last change and Close. Already-held items remain in the catalog and the held-item list; moving one consumes no new inventory slot. Closing discards pending choices only. Applied edits are disclosed admin choices, never earned pickups, played scenes or automatic puzzle completion.

Recipient images use native source artwork: the selection objects where Core establishes their actor binding, otherwise the current native actor costume/animation/palette. The display uses cropped immutable actor copies, never mutates or places a live actor. The selected recipient and dropdown use the same image template and nearest-neighbor scaling. A character without resolved source artwork retains its own source name, with no fabricated image. Raw identifiers, portrait provenance and existing palette estimates stay in Advanced. Parent window resources are merged so local image templates and the existing dark controls both remain available.

## Commands run (through RTK)

Workdir `.worktrees/admin-inventory-ui`:

```powershell
rtk proxy dotnet build .verification/admin-inventory-ui-20261018/WindowCheck/InventoryStatusWindowCheck.csproj --no-restore -p:NuGetAudit=false --verbosity quiet
rtk proxy Exe/InventoryStatusWindowCheck/Debug/net10.0-windows/InventoryStatusWindowCheck.exe "D:\Juegos\Lucas\mmansion ega en" ".verification/admin-inventory-ui-20261018/window-maniac-classic-final" 44 ".verification/admin-inventory-ui-20261018/WindowCheck/maniac-classic.json"
```

Repeat the real-window command for the other three fixtures/configs, room 44 for Maniac and room 1 for Zak. Every harness creates its own private profile/cache and preserves the user's profile. The native key/phone-bill acquisition routes are copied from the accepted S8 harness; no current game save or fixture is modified.

## Numbers and result

Final build against Core follow-up `9889edd`: zero warnings/errors. All four final native-route windows pass with the final source-derived catalogs and character metadata:

| Family | Catalog / recipients | Real-window result |
|---|---|---|
| ManiacClassic | 49 named items; all 7 playable children, 7/7 pictures | PASS; final `window-maniac-classic-admin`, frame 3310; source party 3/1/5, grant and transfer to unselected Razor 2, Undo exact; 48 pending additions of 79 free slots exhaust the remaining catalog |
| ManiacEnhanced | 49 named items; all 7 playable children, 7/7 pictures | PASS; final `window-maniac-enhanced-admin`, frame 1555; same independently read party/recipient contract; own Spanish item names; 48/79 pending additions |
| ZakClassic | 82 named items; 4 playable characters, current Zak picture | PASS; final `window-zak-classic-final`, frame 520; grant to Annie 2; 77/77 free slots queued, Add disabled at native 80 |
| ZakEnhanced | 82 named items; 4 playable characters, current Zak picture | PASS; final `window-zak-enhanced-final`, frame 522; independently checked grant to Annie 2; same 77/77 capacity limit |

All four checks pass held-item transfer Apply/Undo, catalog grant Apply/Undo, complete catalog visibility, empty-search refusal/reset, pending capacity limits, Close discard, unchanged unrelated state, the real header/modal route and restoration of both prior paused/running playback modes. Neither opening nor closing advances a game frame. Seven-character Maniac dropdowns are rendered and saved independently (`status-recipients-dropdown.png`), and both selected/dropped-down picture bindings are asserted. Native Razor grants prove that admin editing is not confined to the currently selected party.

Final captures inspected: Maniac Classic selected/native dropdown and applied Razor grant; Maniac Enhanced applied Razor grant and seven-character dropdown; Zak Classic full pending inventory and Zak Enhanced catalog/grant. Earlier milestone captures and the rejected Zak picture assertion are retained locally. Parent performs solution/full-suite/matrix integration and records Status Partial under the wider all-items requirement because indirect source-target completeness remains bounded.

## Comparison with ScummVM (source files and date; runtime comparison if any)

This is an explicit admin UI over the Core edit contract, not a simulated pickup or native Give action. Native inventory capacity, retained source code/header/state and eligibility are checked in the Core evidence. Character pictures reuse existing native actor/object decoders and renderers. No ScummVM executable was launched.

## Differences and uncertainties kept

The first Zak harness incorrectly required pictures for every character even though this request requires Maniac pictures and three Zak actor costumes are not initialized in the reached state. That assertion failed for both Zak copies and is retained locally. The corrected harness requires every Maniac recipient picture; Zak requires the current source picture and validates other dropdown pictures only where artwork is resolved. No portraits are invented for uninitialized Zak actors. Audio/timing and original-game visual comparison are not claimed by these deterministic real-window checks.

## User checks (exact steps and where; none if none)

Open any supplied Maniac or Zak copy, finish startup choices, then open Game Status. Find an item, choose a character, Add item and Apply Changes; Undo restores the prior inventory. Choose more items and Close without applying to discard them. Maniac recipient names and pictures should appear together in the selected choice and dropdown. A full inventory stops further additions while allowing transfers.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent checkpoint and REQUIREMENTS §12 record the clarified admin contract. Core evidence records source candidate/character derivation, native capacity and edit lifetime. This record and its reproducible WindowCheck cover only the App presentation and actual-window behavior.
