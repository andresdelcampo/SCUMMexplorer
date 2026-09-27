# Validation: getRandomNr as a labelled seeded pick (27 September 2026)

## Change

- `LoomSeededRandom` (Core) stands in for the engine's random source. ScummVM `o5_getRandomNr` stores `getRandomNumber(max)`, a value from 0 to max inclusive. Each value is computed from the seed, the script, the instruction offset and how often that instruction has drawn, so a pick does not shift when unrelated scripts run in between.
- The executor models `getRandomNr` only when a session runtime supplies that source. Each pick is a `LoomRandomPick`: source, offset, range, value, seed and draw number. Without a session it stops as before ("needs a session's seeded random source").
- **Estimates are tracked, never reached.** The picked variable is marked as an estimate, and so is anything computed from it (`move`, arithmetic, indexed reads through it). Once a branch reads an estimate, the rest of that run is on an estimated path, and pending scripts keep that mark across frames. On an estimated path:
  - `setState` is applied to the preview, but the object goes into `EstimatedStates`, not `StatesSetInRoom`. The nothing-visible fallback therefore never treats it as a state set by reached code.
  - `drawObject` is applied and its object is also listed in `EstimatedStates`.
  - Events carry the `Conditional` (possible) certainty. A room load there is not entered; the session reports "possible Room NN load follows a seeded pick".
  - Children started there, or started with a picked global as an argument, run on an estimated path too.
- The session takes a `randomSeed` (default 0). For every pick it adds an uncertainty: "getRandomNr at +0x… picked V of 0..MAX (seeded pick, seed S, draw N); what follows from it is an estimate, not a reached game state". So a snapshot with a pick is never faithful. `Snapshot.RandomPicks` and `Snapshot.EstimatedStates` expose the picks and the estimated objects.
- **Indexed operands** (ScummVM `readVar`/`writeVar` with 0x2000): operands such as `var[0x0B3+local[0x002]]` and `local[0x004+0x001]` now read and write the variable they address. Script 42 needs this right after its pick (+0x0037/+0x003E), and so do the local-array draws in task 3. An index that comes from a pick makes the value an estimate.

## Before and after

The [Blockers harness](Blockers/Program.cs) runs, for every room, a catalog visit plus up to 8 frames, and then each object verb activated directly in a fresh session, again plus up to 8 frames. It lists every `getRandomNr` the session stopped at or picked through, and a histogram of uncertainty shapes. The before run is the same harness built against HEAD (`4ad1d3a`).

| | [Before](blockers-before.txt) | [After](blockers-after.txt) |
| --- | --- | --- |
| `getRandomNr` stops | 2 sites in 2 scripts: Room 04 local 203 +0x0000 and Room 43 local 205 +0x0030 | 0 |
| Rooms where a visit or action hit one | 03 (object 162 → Room 04), 04 (visit), 43 (visit), 64 (object 825 verb 0x36 → Room 04) | same rooms, now picking through |
| Where those scripts go next | — | Room 04 local 203: its countdown (4 to 36 frames) stays pending and loops frame by frame. Room 43 local 205: stops at `delayVariable` (+0x0039), which is not modelled. |
| Visits and actions stopped by an indexed read `var[N+local[..]]` | 13 | 0 (no new stop reason appeared in their place) |

The decoded code has 64 `getRandomNr` instructions. Only the 2 above are reached from boot-state visits and direct verb calls. The others, including script 42 +0x0026, sit behind conditions or started scripts that these routes do not reach. For example, the Room 03 entry starts script 42 only when bits 0xD62 and 0xD6E are both 0, and script 001 sets 0xD62 at boot.

## Room audit and Room 03 probe

- RoomAudit: **0 gaps in all 76 rooms**, unchanged. The report differs from before in one line: Room 04 now has 1 pending script (local 203's countdown) instead of 0. Paint order, draw queues, estimates and the fallback are identical in every room, so no committed room PNG needed a refresh.
- Room 03 probe ([before](probe-before.txt), [after](probe-after.txt)): the Room 03 → 04 → 03 route is unchanged. In Room 04, "operation 'getRandomNr' is not modeled" is replaced by the labelled pick (value 6 of 0..32, seed 0, draw 0), and one more script is pending because local 203 now runs its countdown.

## Tests

`LoomRandomPickTests`, 6 tests (124/124 in total, 0 warnings):

- `SeededPickStaysInRangeAndRepeatsForTheSameSeed`: the range is inclusive, the sequence repeats for the same seed, and a pick does not depend on draws at other instructions.
- `PickNeedsASessionSeedAndIsRecordedWithItsRange`
- `StatesAndLoadsBehindABranchOnAPickAreEstimatesNotReached`: a `setState` before the pick counts as reached, and the one after a branch on it counts as an estimate. A global copied from the pick is an estimate, and the `loadRoom` event is `Conditional`.
- `IndexedOperandsReadAndWriteTheVariableTheyAddress`
- Real data: `Room04VisitPicksThroughLocalScript203AndStaysAStatePreview` (with seed 4) and `Script42PicksActor2PositionAndItsTimeOnScreen`. The second decodes script 42, starts it with Room 03's argument 5 and resumes it. It picks at +0x0026 and +0x004F, pauses at +0x0085 and sets bit 0xD7A. Because the bit follows a loop that branches on the pick, it is marked as an estimate.

## Limits

- The mark is conservative. Script 42 sets bit 0xD7A "whatever the random value", but it does so after the +0x002B loop, which branches on the pick. The bit is therefore labelled as an estimate even though every path sets it.
- A picked value is one possible value, not the game's. Different seeds show different, equally possible previews; the app uses seed 0.
- `delayVariable` (Room 43 local 205), actor placement and timing are still not modelled.
