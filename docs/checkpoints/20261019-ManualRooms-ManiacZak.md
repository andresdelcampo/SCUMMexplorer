# Stable selectors and deliberate cutscenes, all games — 19 October 2026

## Plan

User request: outside LOOM EGA, keep plain backgrounds in the room selector after loading; still generate and cache native first-visit scenes upfront. Manual room selection cancels the departing cutscene and must not autoplay an entry scene. Native doorway travel retains its entry scenes. Opening a game must not autoplay its intro; a source-backed intro missing from Room 01 needs a Normal-mode header action, Play Game Intro.

Baseline master `fb3da87b` includes concurrent Monkey CD edition work. Reuse the clean integration checkout on `codex/manual-room-cutscenes`. Parent owns shared App navigation, intro UI and requirements. Three isolated workers own respectively RoomCards-only selector changes, Core navigation/intro APIs plus tests, and a private-profile window harness. No overlapping writers. Read-only/data-derived investigation precedes policy changes; no opcode skipping, invented flags, state-forcing or per-room exceptions. All commands through RTK; no ScummVM, game data/profile changes, application interference, junction creation or worktree removal.

Integrate worker commits, run focused policy/cache/window checks with exclusive serial WPF ownership, regenerate support matrices, merge master in integration, build and run the required full suite with sound/Indy3 EGA/MT-32 roots. Preserve both sides of concurrent work, fast-forward master only, refresh the normal executable and stop clean.

## Done

Investigation: generic StartPlayAsync boots then VisitRoom and starts its timer; manual catalog entry therefore plays native entry scenes. LOOM separately starts Catalog arrival frames after VisitRoom. Stopping the App clock alone does not retire suspended global cutscene owners. Existing explicit launch paths and native doorway paths must stay distinct from the new catalog policy.

Requirements and plan `9861d09` precede implementation. Selector `ae8007ca` keeps plain cards outside LOOM EGA while retaining native preparation/complete cache; four cold/warm windows pass 3,748 observations, 72 headless combinations pass. Core `b6231d7e` adds independent quiet preparation with route/child retirement and native intro startup without assumed input; 30 focused compatibility tests and all 12 new cases pass. App `273cc39` separates catalog/native/explicit scene intents, guards async publication, aborts old clocks/audio/queued clicks and adds the header intro action. Ten generic families have actual indexed native boot sources. Final solution build zero warnings/errors (8.02 seconds), three regenerated-matrix checks pass; no cell is raised. [Combined evidence](../../.verification/manual-rooms-final-20261019/README.md).

Final Maniac Enhanced combined-window route passes real party input, manual abort of outdoor intro, explicit restart/native Skip, quiet manual Room 01/28 and native no-override telescope cancellation with 180 subsequent frames. Monkey CD and LOOM first attempts found actual App failures, retained: roomless native startup discarded before a clock could start, and a display-title suffix defeating intro deduplication. App-only `ac074cb` starts native roomless frames without guessing a room and uses source membership for existing intro detection. It also follows native loads without catalog re-entry. Independent final review finds no actionable issue. Final LOOM rerun passes gap filling and no duplicate intro button. Monkey CD's explicit native intro reaches actual player control without supplied answers; its selected doorway route remains unproved. Final App `98abc9b` also keeps the established short-demo preview explicitly launchable from a static room: the real header starts its native preview, and manually selecting Room 02 cancels it without autoplay. All private windows are closed and the lease released.

Final assembled solution build: zero warnings/errors, 8.15 seconds. Regenerated README/REQUIREMENTS matrices: **three passed, zero failed/skipped**, 23 ms; no support level changes. Required full regression with all three external roots: **1,684 passed, zero failed, five expected skips**, 17m30s. All 12 new manual-policy/intro cases are confirmed Passed in the TRX. Core/tests remained identical to `273cc39` throughout that regression; final App-only corrections were built without rebuilding those running binaries. A final solution build followed regression completion. Exact commands and evidence are in the combined record.

## Not done and why

No claim of universal live manual starts or full cutscene support. The selected Monkey CD Room 52/object 621 doorway remains unproved after two distinct preparations: raw startup had not reached native control, and the later actual-control source could not produce a safely quiet manual start. The second case did not attempt the verb. No extra attempt, forced state, frame-cap increase or weakened assertion was used. The zero-input-script fallback is an additional App guard; the Monkey window source had input script 4, so that guard is not claimed as window-covered.

## Decisions made

Keep native first-visit initialization and complete caching even for static selectors. LOOM EGA remains the explicit user-requested artwork exception. Requirements for manual navigation and explicit intros are user-confirmed rules, not inferred support promotions. Intro availability must come from a supported native boot/established intro action, never a guessed room or script id.

Quiet availability is deliberately bounded: LOOM reference Rooms 02/04 and Monkey floppy/CD Room 51 have persistent entry-created conditional scene watchers and remain static manual previews. No watchers or flags are deleted to make them appear ready. Ordinary native input/doorway paths and explicit openings remain available through their existing sessions; this run does not claim all manual starts stay live. Existing user-approved R-8.20 late-room story prerequisites retain their prior source-session semantics; the new transactional entry guarantee does not retroactively make that exception rollback-safe. Independent review found and fixed pending-room/intro replacement and stale error-publication races before source freeze.

## Proposed requirements and offers

None awaiting user confirmation; record any additional inference before claiming it implemented.

## User checks

Optional review in the refreshed normal executable: open a game and confirm it remains quiet; use Play Game Intro (or the established Room 01 action), then select another room during playback. Plain selector cards should stay unchanged outside LOOM EGA; LOOM's gap-filling thumbnails should complete as before. A static manual fallback is intentional when native entry cannot settle safely. No mid-run approval gate.

## Cost and next run

Three independent workers plus parent integration, with exclusive serial WPF ownership. Retained first-attempt failures led to the two App corrections above; later manual-start bounds remain disclosed rather than relaxed. Stop at this checkpoint; do not begin another feature automatically.

## Clean promotion

Master stayed clean at `fb3da87b` throughout validation; the final integration merge reported already up to date. Fast-forwarded master to `143196d7` without rewriting history, preserving the concurrent Monkey CD edition work. Rebuilt the normal `Exe/Debug/SCUMMexplorer.exe` from the root checkout: zero warnings/errors, 7.42 seconds. No user application was stopped. Final product source remains `98abc9b`, with Core/tests unchanged from `273cc39`; only this promotion record follows. Parent verifies matching clean master/integration heads after its final fast-forward. No automatic next item.
