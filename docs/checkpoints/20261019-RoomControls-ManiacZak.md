# Room controls and audio lifecycle, Maniac/Zak — 19 October 2026

## Plan

User requests LOOM-equivalent room cutscene/Interact controls in Normal and Advanced, readable names, native room-card following, effective hotspots/depth/walkboxes/doorways, Advanced room scripts/interactions, one-click Game Status Apply and close, and room Sounds across supported games with no audio leaking across folder changes. These are explicit requirements, recorded before implementation as R-6.17–20, R-8.2a, R-12.14 and R-13.11–12.

Baseline clean master `b1f231c5` includes the other orchestrator's executor allocation and scene-scan improvements. Integration reuses `.worktrees/maniaczak-status-polish` on `codex/room-controls-maniac-zak`. Three isolated worktree agents own room actions/source panels, scene overlays, and audio/status respectively. Parent exclusively owns shared MainWindow XAML/code-behind, Play integration, requirements and final documentation. New additive partials keep ownership disjoint. The overlay worker exclusively owns extracting the existing generic doorway algorithm into ScummDoorwayFinder, with the LOOM API retained as a wrapper; no overlapping engine writer. No other-family support promotions, state forcing, ScummVM execution, user profile/game-data mutation, junction creation or worktree removal.

Read-only diagnosis first; meaningful source tests and serial private-profile WPF checks follow. Only one WPF verification lease at a time. Integrate each completed item, preserve concurrent master, regenerate support matrices, build and run the required full regression with sound/Indy3 EGA/MT-32 roots. Fast-forward master only, refresh normal executable and stop clean. At most two distinct approaches per blocker; retain failures and disclose unavailable cases.

## Done

Requirements and ownership plan recorded. Investigation found the generic native screen is separate from LOOM's room surface, so old overlays never cover live Play. Native room following updates the tree/card but does not reveal an offscreen selector card or refresh room evidence. Sound collection/refresh and Interact/source-panel paths still rely on LOOM state.

## Not done and why

Implementation and validation in progress. No support cell changes yet.

## Decisions made

Use native data/session snapshots and existing explicit action paths. Direct Interact is an exploration action, not sentence gameplay. Normal content stays readable; technical provenance stays Advanced. Source relationships remain distinct from executed routes. The existing bounded manual-entry fallback remains intact.

## Proposed requirements and user checks

No approval gate: the listed rules are explicitly requested. Optional listening/window checks will be recorded with exact steps after verification.

## Cost and next run

Three isolated agents plus parent integration; stop at this checkpoint with no automatic next feature.
