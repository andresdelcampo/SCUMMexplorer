# Maniac/Zak S8 checkpoint — 18 October 2026

## Plan

Scope: explicitly requested S8 for ManiacClassic, ManiacEnhanced, ZakClassic and ZakEnhanced, each with completed S6 and an available fixture. Add a useful Game Status inventory view across characters and guarded explicit reassignment of items already held, with Apply/Cancel/Undo. Names and eligible characters must come from the edition's own data. No arbitrary variable/flag editor, new item creation, actor placement/costume edits, hidden prerequisites or claimed played scenes. New requirements are Proposed in a separate commit before implementation.

Three family workers in separate worktrees: ManiacEnhanced owns the new shared Core state API, ManiacClassic owns its tests/source facts and coordinated Maniac docs, ZakEnhanced owns its family tests/docs then ZakClassic as a separate item/worktree. Parent owns new App dialog/header integration, requirements, registry/matrices and final integration. Shared engine edits are limited to a new state partial/types plus small additive hooks; existing v1/v2 opcode partial remains separately owned if needed. Parent announces shared ownership in CURRENT_WORK before code changes.

Bounded pass per family: derive edition-specific actor/item facts, edit/undo a genuinely acquired item, verify untouched state and blocked/stale-edit guards, then one real-window Apply/Cancel/Undo check. No user budget supplied. Stop any blocker after two distinct failed approaches and retain failed evidence. Do not execute ScummVM or touch other orchestrator families, Games/profile data, junctions or worktree removal. Reuse earlier Play/edition evidence where relevant.

Integrate master in the worktree, preserve concurrent work, regenerate README/REQUIREMENTS with SCUMM_UPDATE_SUPPORT_MATRIX=1, build and full suite with the three external fixture settings; require zero failures and fast-forward master. If master moves, repeat. Finish clean with own handoff only.

## Done

All four families now have Game Status with source-established character inventories, explicit owner reassignment, Apply/Cancel and guarded Undo. Status None → Full for this bounded inventory editor; README/REQUIREMENTS matrices regenerated, three matrix tests passed. Other cells are unchanged, including ManiacClassic's accepted costume status and ZakClassic's 24 derived/11 estimated costumes.

| Family | Headless acceptance | Final actual-window acceptance |
|---|---|---|
| ManiacClassic | Three tests; genuine key 55, Dave → Syd → Undo; native slot 0 / retained Room 1 | PASS, frame 3310; source names, native strip refresh, Cancel and prior running/paused states |
| ManiacEnhanced | Three tests; genuine `llave` 55, same owner round trip, own source/menu route | PASS, frame 1556; corrected name rendering and native strip refresh |
| ZakClassic | Three tests; phone bill 15, Zak → Annie → Undo, native slot 2 / retained Room 1; original Read still responds | PASS, frame 520; Apply/Undo/Cancel and both clock states |
| ZakEnhanced | Three tests; same independently checked native acquisition and edit/undo contract | PASS, frame 521; source names, NPC exclusion and both clock states |

All four captures were inspected. Normal mode shows source item/character names and a choice label; raw identifiers, evidence and refusals are Advanced. No item creation, arbitrary flag editor, actor placement/costume or party changes. Inventory slot/code/state/classes and unrelated variables/actors remain unchanged. Apply/Undo do not advance scripts or count played scenes. The window stops the clock and sounds, restoring prior pause/timer state on close.

Integrated focused verification: **39 passed, zero failed/skipped**, including twelve family state cases, native object-slot lifecycle, four family conformance rows and LOOM Game Status regressions. Six LOOM EGA/CD boot regressions also pass. Independent read-only Core/App review found no actionable issue; later integration testing identified and corrected the special-slot false refusal below. Final solution build: zero warnings/errors. Full suite: **1240 passed, zero failed, five skipped** (1245 total), 9 minutes 16 seconds, with all three external fixture settings. Validated against master `cb17c44`; the retained local report is `.verification/maniaczak-state-20261018/test-results/maniaczak-s8-full.trx`.

Key commits: shared API `d8fb705`, provenance `ebc9e38`, initial busy guard `d2d3263`, native slot classification `7d563bb`; Classic Maniac `4093d15`, Enhanced Maniac `18be391`, ZakEnhanced `cd9eb68` / final regression `b1d9ed7`, ZakClassic `3df8ab5` / final regression `e9e88f0`; App/window harness `ea14390`; registry/conformance/matrices `d3a0ed6`; requirement proposal `d3103ed`. [Integration evidence](../../.verification/maniaczak-state-20261018/README.md) contains commands, failed evidence and links to all four family records.

## Not done and why

No complete-game, native Give/pickup simulation or original-game visual/timing/audio acceptance is claimed. Existing S5 images/darkness, walking approximation, Classic Maniac Script 174, Zak estimates/capped scenes and S9 missing alternate copies remain open. No ScummVM executable was run, and no game files, ROMs, saves or user profiles were modified. Worktrees are retained; none was removed. No catalog or seed work.

## Decisions made

Show inventories across source-established playable characters, without arbitrary story flags or new items. Retain reached actor names; derive selected-party values from native ego-switch bindings, or the source-written native player range. Names alone do not make an NPC or unselected child eligible.

Use owner-table-only writes with opaque session/frame/signature/revision tokens. Undo is intentionally conservative and confined to the paused modal: any playback frame invalidates it. Cancel discards pending choices; an applied choice is reversed explicitly with Undo.

The initial guard treated every pending object slot as a player action and blocked Zak permanently. Waiting 3000 real frames failed. Native `0xFD`/`0xFA` special/background lifecycle flags distinguish resident handlers from ordinary foreground work; the corrected guard retains all active input/sentence/executing/foreground protections. A regression pins special+foreground coexistence, stop and execution behavior. No room exception or forced state was added. Failed tests/waits remain recorded. The captured ComboBox record-string display and enhanced harness owner/JSON config errors were also corrected, with failure evidence retained.

## Proposed requirements to confirm or reject

**R-12.7–R-12.10 remain Proposed**: cross-character inventory view, explicit held-item reassignment, atomic Apply/Cancel/Undo and modal clock behavior. They were committed separately before implementation and do not authorize puzzle flags, new items or actor/party changes.

## User checks

Optional short UI check: open a checked Maniac copy, choose the party, lift the mat and take the key. Open Game Status, choose Syd, Apply, then Undo before closing; choose a different pending recipient and Cancel. For either Zak copy, open the dresser drawer and take the phone bill, then repeat with Annie. The native inventory strip should follow the explicit choice and Undo. The app is `.worktrees/maniaczak-s8/Exe/Debug/SCUMMexplorer.exe`. Previous perceived timing/listening and original-game comparison checks remain separate and are not requested again.

## Offers

No seed or authored catalog work is included.

## Cost

Three concurrent workers plus parent; four separate family worktrees, ZakClassic after ZakEnhanced. One bounded family harness each, four final WPF routes and a targeted slot regression. New guards justified focused reruns; failed checks were retained. No user budget supplied. The mandatory full suite is run once after final code integration; repeat only if master moves or a real failure requires a correction.

## Next run

No automatic next stage. With more same-family copies: "Resume the blocked Maniac/Zak S9 edition comparisons using the new local copies, retain the coordination rules and stop at a clean checkpoint." Otherwise choose a specific retained engine/visual limit for a new bounded run. Authored catalogs/seeds remain opt-in.
