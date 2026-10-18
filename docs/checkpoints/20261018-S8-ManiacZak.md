# Maniac/Zak S8 checkpoint — 18 October 2026

## Plan

Scope: explicitly requested S8 for ManiacClassic, ManiacEnhanced, ZakClassic and ZakEnhanced, each with completed S6 and an available fixture. Add a useful Game Status inventory view across characters and guarded explicit reassignment of items already held, with Apply/Cancel/Undo. Names and eligible characters must come from the edition's own data. No arbitrary variable/flag editor, new item creation, actor placement/costume edits, hidden prerequisites or claimed played scenes. New requirements are Proposed in a separate commit before implementation.

Three family workers in separate worktrees: ManiacEnhanced owns the new shared Core state API, ManiacClassic owns its tests/source facts and coordinated Maniac docs, ZakEnhanced owns its family tests/docs then ZakClassic as a separate item/worktree. Parent owns new App dialog/header integration, requirements, registry/matrices and final integration. Shared engine edits are limited to a new state partial/types plus small additive hooks; existing v1/v2 opcode partial remains separately owned if needed. Parent announces shared ownership in CURRENT_WORK before code changes.

Bounded pass per family: derive edition-specific actor/item facts, edit/undo a genuinely acquired item, verify untouched state and blocked/stale-edit guards, then one real-window Apply/Cancel/Undo check. No user budget supplied. Stop any blocker after two distinct failed approaches and retain failed evidence. Do not execute ScummVM or touch other orchestrator families, Games/profile data, junctions or worktree removal. Reuse earlier Play/edition evidence where relevant.

Integrate master in the worktree, preserve concurrent work, regenerate README/REQUIREMENTS with SCUMM_UPDATE_SUPPORT_MATRIX=1, build and full suite with the three external fixture settings; require zero failures and fast-forward master. If master moves, repeat. Finish clean with own handoff only.

## Done

Pending.

## Not done and why

Pending.

## Decisions made

Pending.

## Proposed requirements to confirm or reject

Pending proposal IDs.

## User checks

Pending exact steps.

## Offers

No seed or authored catalog work is included.

## Cost

Pending.

## Next run

Pending results; no automatic continuation.
