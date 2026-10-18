# Inventory catalog completion — Maniac/Zak, 18 October 2026

## Plan

Explicit request: resolve remaining inventory catalog gaps for ManiacClassic, ManiacEnhanced, ZakClassic and ZakEnhanced, especially Zak letter139; parallel agents, existing coordination rules, honest support matrix, clean checkpoint.

Baseline master `170842d9`. Current catalogs:49 named Maniac items and82 Zak items per edition; Status Partial because indirect pickup-target coverage remains unproved. Inspect every unresolved pickup/Pickupable writer and special-handler context against native version semantics, classify letter139 and the prior excluded headers, and verify safe grants/retained code/Undo. No item-ID allowlist or room-specific engine exception.

Three independent workers: ZakClassic (exclusive shared Core catalog writer), ZakEnhanced (own-data audit/tests), ManiacClassic (own-data audit/tests). Parent audits ManiacEnhanced, owns shared docs/registry/matrices/UI integration and serializes source changes. Each uses its own worktree and evidence folder. Other-family feature work, ScummVM execution, game/profile/ROM/save mutation, junctions and worktree removal are excluded.

After review: merge current master into integration worktree, preserve both orchestrators, regenerate README/REQUIREMENTS matrices with `SCUMM_UPDATE_SUPPORT_MATRIX=1`, build and run full suite with sound/Indy3/MT-32 roots and zero failures, then fast-forward master. If master moves, repeat. Stop at this checkpoint.

## Done

Pending.

## Not done and why

Pending source audits.

## Decisions made

Admin edits do not require earned pickups or story flags. Inventory capacity, valid retained item code, unique identity, atomic validation and exact Undo remain required. A matrix level reflects demonstrated catalog coverage, not the number of items added.

## Proposed requirements to confirm or reject

None planned; existing admin requirements govern this pass.

## User checks

To be recorded after automated/source and real-window checks.

## Offers

No automatic follow-on work.

## Cost

Three parallel workers plus parent audit/integration; no explicit user budget. One shared Core writer. Stop a blocker after two distinct failed approaches and preserve evidence.

## Next run

Stop at a clean checkpoint after completed changes are integrated and verified.
