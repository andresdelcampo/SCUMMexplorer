# Game Status pictures, party and item choices — Maniac/Zak, 18 October 2026

## Plan

User parks the broader fidelity backlog and requests five focused corrections: all Zak recipient pictures, classify repeated Zak item names, Maniac selected-party availability after character selection, consistent full-body Maniac pictures, and compact bottom-aligned item/recipient/Add controls.

Baseline master `defbf54e`, clean. One Core/character-art worker owns recipient eligibility, source appearance derivation and dependent tests; a separate Zak identity auditor owns unique evidence/tests; parent owns InventoryStatusWindow layout/labels/rendering, requirements/docs, actual-window checks and integration. Each works in an isolated worktree. No other-family feature work, ScummVM execution, game/profile/ROM/save mutation, junctions or worktree removal. Keep broad engine/travel/palette/catalog work parked.

Merge current master into integration, retain both orchestrators, regenerate both matrices if warranted, build, required full suite with sound/Indy3 EGA/MT-32 roots and zero failures, then fast-forward master; repeat if master moves. Stop at a clean checkpoint.

## Done

Pending implementation and source audit.

## Not done and why

Broader support backlog explicitly deferred. No original-game runtime or full-playthrough claim.

## Decisions made

The user's selected-party preference supersedes the preceding seven-recipient Maniac UI contract. Unearned item grants remain admin operations; party selection does not authorize actor switches, teleports or story changes. Pictures are native full-body display copies; absent live costumes require source-backed appearances, not invented per-character tables. Duplicate names alone do not establish duplicate logical items.

## Proposed requirements to confirm or reject

Record the user's current party/picture/layout clarification in §12; no approval gate inside this authorized run.

## User checks

To be recorded after sequential private-profile actual-window checks.

## Offers

No automatic follow-on work.

## Cost

Two parallel workers plus parent integration; one shared Core writer, one source identity auditor. No explicit budget. Preserve failed checks; cap repeated blocker attempts and report evidence honestly.

## Next run

Stop here after clean verified integration. The broader fidelity backlog stays deferred.
