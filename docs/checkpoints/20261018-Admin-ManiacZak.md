# Admin Game Status and playback controls — 18 October 2026

## Plan

Explicit user clarification: Game Status is an admin editor, including grants from the game's item catalog with native capacity limits, pictured recipients in Maniac Mansion, and native item pictures where the game has graphical inventory. Review all four Maniac/Zak families; restore Normal sound selection for playable games with sound/cutscenes; review playback controls against LOOM.

Integration branch `codex/admin-status-controls`. Separate item worktrees: one exclusive Core inventory writer; one inventory-window writer; one playback/audio UI writer. Parent owns requirements, matrices, final evidence, checkpoint and handoff. No other-family feature work; shared UI regression checks include LOOM. No ScummVM execution, game data/profile changes, junctions or worktree removal.

## Done

Pending.

## Not done and why

Pending review and implementation.

## Decisions made

The user's explicit admin intent supersedes the old held-items-only restriction. Grants remain explicit user choices, separate from earned pickups and story flags; native capacity and valid retained object resources still apply.

## Proposed requirements to confirm or reject

User-requested clarifications are confirmed rules; implementation-derived proposals will be listed here if needed.

## User checks

Pending.

## Offers

No automatic follow-on work.

## Cost

Pending.

## Next run

Stop at a clean checkpoint after integration and required checks.
