# Stable selectors and deliberate cutscenes, all games — 19 October 2026

## Plan

User request: outside LOOM EGA, keep plain backgrounds in the room selector after loading; still generate and cache native first-visit scenes upfront. Manual room selection cancels the departing cutscene and must not autoplay an entry scene. Native doorway travel retains its entry scenes. Opening a game must not autoplay its intro; a source-backed intro missing from Room 01 needs a Normal-mode header action, Play Game Intro.

Baseline master `fb3da87b` includes concurrent Monkey CD edition work. Reuse the clean integration checkout on `codex/manual-room-cutscenes`. Parent owns shared App navigation, intro UI and requirements. Three isolated workers own respectively RoomCards-only selector changes, Core navigation/intro APIs plus tests, and a private-profile window harness. No overlapping writers. Read-only/data-derived investigation precedes policy changes; no opcode skipping, invented flags, state-forcing or per-room exceptions. All commands through RTK; no ScummVM, game data/profile changes, application interference, junction creation or worktree removal.

Integrate worker commits, run focused policy/cache/window checks with exclusive serial WPF ownership, regenerate support matrices, merge master in integration, build and run the required full suite with sound/Indy3 EGA/MT-32 roots. Preserve both sides of concurrent work, fast-forward master only, refresh the normal executable and stop clean.

## Done

Investigation: generic StartPlayAsync boots then VisitRoom and starts its timer; manual catalog entry therefore plays native entry scenes. LOOM separately starts Catalog arrival frames after VisitRoom. Stopping the App clock alone does not retire suspended global cutscene owners. Existing explicit launch paths and native doorway paths must stay distinct from the new catalog policy.

## Not done and why

Implementation and acceptance in progress. No support level is raised by this shared presentation/lifecycle work.

## Decisions made

Keep native first-visit initialization and complete caching even for static selectors. LOOM EGA remains the explicit user-requested artwork exception. Requirements for manual navigation and explicit intros are user-confirmed rules, not inferred support promotions. Intro availability must come from a supported native boot/established intro action, never a guessed room or script id.

## Proposed requirements and offers

None awaiting user confirmation; record any additional inference before claiming it implemented.

## User checks

Recorded after private-profile window acceptance; no mid-run approval gate.

## Cost and next run

Three independent workers plus parent integration. Stop at this checkpoint; do not begin another feature automatically.
