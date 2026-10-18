# Maniac/Zak engine fidelity checkpoint — 18 October 2026

## Plan

Explicit user scope: Script 174, darkness/flashlight rendering, ZakClassic palette estimates and the five capped Zak cutscene starts in each edition. All four supplied family fixtures are available. Retain existing support levels until evidence justifies a change; Full Cutscenes still requires a hand-validated catalog. No catalog, shipped seed, story-state workaround or wider edition claim.

One isolated worktree per item. ManiacClassic worker owns Script 174 and all v1/v2 opcode changes in ScummBoundedScriptExecutor.V2.cs. ManiacEnhanced worker owns shared lighting/rendering implementation, checking each family against its own data; requests opcode/runtime hooks from the opcode owner. ZakClassic worker owns ScummV1ActorColors and palette provenance only. Parent owns capped Zak scene diagnosis in the integration worktree, requests shared fixes from their owner, and owns App integration, generated matrices, checkpoint and dedicated handoff. Family documentation ownership is coordinated before editing shared trackers/maps.

Bound each item to reproducing the recorded failure, source-grounded implementation or classification, meaningful regression and one targeted harness. Investigate all five capped starts per Zak edition, without increasing a cap to manufacture success or assigning prerequisites. Palette analysis must preserve context-dependent alternatives and label unresolved values. Lighting must follow engine state and native source behavior, without room exceptions or pixel fills. Two distinct failed approaches to a blocker are recorded and the item stops. No overall user budget was supplied.

Read the complete game-support instructions, CLAUDE, current work and latest checkpoints. All shell calls use RTK. Data stays under D:/Juegos/Lucas; never execute ScummVM, mutate game/profile/ROM/save data, create Games junctions, stash, force or remove worktrees. Only the four Maniac/Zak families are in scope. Do not edit NEXT_SESSION_PROMPT.md.

Merge master in the integration worktree, keep both orchestrators' changes, regenerate README/REQUIREMENTS with SCUMM_UPDATE_SUPPORT_MATRIX=1, build, full suite with external sound/Indy3/MT-32 settings (zero failed), then fast-forward master. Repeat if master moves. Stop at a clean checkpoint.

## Done

Pending implementation and bounded validation.

## Not done and why

Original-game runtime/picture/timing comparison remains outside the authorized run. Additional same-family releases are absent. Full-game completion and hand-validated catalogs are not claimed.

## Decisions made

Parallel source investigation and family verification; exclusive ownership of each shared engine file. Source-derived fixes and retained uncertainties take precedence over detector counts or scene completion counts.

## Proposed requirements to confirm or reject

None yet; existing proposals remain binding.

## User checks

To be recorded with final routes and artifact paths; do not repeat prior subjective checks without a new result to inspect.

## Offers

No authored catalog or seed work.

## Cost

Three workers plus parent; one bounded item per worktree, no user-specified budget.

## Next run

Decide from the retained evidence at this clean checkpoint; no automatic continuation.
