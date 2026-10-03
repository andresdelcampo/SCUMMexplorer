# Room 41 guard: pick-derived vs unknown uncertainty (3 October)

Investigation only: no production, catalog or evidence-policy change.

## Finding 1: the guard the handoff describes is already gone

The parked item ("the conditional Room42 load stop is an overzealous guard", `HandleEvent` about line 2000, executor about line 357) was fixed on 2 October in `0318792` ("Continue executed room loads while preserving estimated state"), which came after the `55cb5ec` priority note that CLAUDE.md and the older handoff text still quote. The common predicate is `IsExecutedRoomLoad` (`LoomExplorationSession.cs`): `RoomLoaded`, `Executed`, and certainty `Reached` or `Conditional`.

Re-run today of the unchanged recipe (`ProgressionProbe` on `Exe/Room41RouteSession-20261002/raw4-only.json`, fresh output `Exe/Room41GuardCheck-20261003`, ignored by git, result SHA-256 `4a988637d0bf569b49001de7ea1cfca3fa3d0af4fc81e83bb17a702a2c439b92`):

`room41-raw4-click: room 43, 15/15 scopes`. Room changes ... 13 (3749) -> 41 (3989) -> 42 (4660, "Room 42 entered by estimated execution at Script 90 +0x0005; execution continues without promoting the transition's certainty") -> 43; boundaries include `cutscene/endCutscene at Script 90 +0x0000/+0x0730`. This matches the 2 October record (Room43, 15/15). The 096 note "still stops at the conditional Room42 load" in the handoff is stale.

## Finding 2: the two kinds of uncertainty are already told apart

- **Estimated** (a seeded pick, a catalog arrival, a labelled status edit, or any value computed from them): the value is known to the executor and marked in `estimated`/`EstimatedVariables`; branches that read it set `onEstimatedPath`, and events behind them are `Conditional` with `Executed = true`. Executed conditional loads are followed and labelled "estimated execution"; certainty is never promoted.
- **Unknown/unmodelled** (no value, unreadable operand, unmodelled opcode, unresolved branch): the executor cannot evaluate, sets `unresolved` and stops that script (`LoomBoundedScriptExecutor` condition/operand paths); no event is produced, so no load follows.

Limit: `Conditional` does not say which estimate source it came from (seeded pick versus assumed arrival versus status edit); `LoomScriptEventCertainty` has no reason field, and `_picks` records the seeded picks separately. Nothing in the raw4 route needs that finer split, so no field was added. If an audit ever needs "pick-only" provenance it would be a new reason set on `Estimated*` entries, not a policy change.

## Consequence

The user's decision about a scoped exception is moot: the exception already exists in its general form (executed estimated loads proceed, unknown stops). The Room 41 native090/raw4 route is no longer a blocker. CLAUDE.md "Current work" and the handoff's first-task wording are out of date; this validation and the handoff update replace them. Audited 234/235 and fresh 169/235 unchanged.
