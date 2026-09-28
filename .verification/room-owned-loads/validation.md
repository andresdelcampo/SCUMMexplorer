# Room loads by room-owned scripts end the script (28 September 2026)

## The label

After task 1, the most frequent label that read as a stop was "script requests Room #; room transition awaits bounded session dispatch" (66 in Blockers). `Blockers.dll <game> <out> "awaits bounded session dispatch"` lists every site: all 66 are object verbs that load a room (for example Room 03 object 160 verb 0x38 → Room 02, Room 05 object 202 → Room 10), or Room 03 locals 200–202, which a verb starts and which load Rooms 04, 05 and 10 one frame later. In each case the session already entered the destination: `ActivateObjectVerb`, the input methods (`FinishInput`) and `AdvanceFrame` take the first reached room load and visit it, and Blockers counts those actions as "loadRoomWithEgo at … → Room #". The label was left over from the script's own run: the executor stopped at the load as unresolved.

## ScummVM behaviour (`room.cpp`, `startScene`)

When `loadRoom` or `loadRoomWithEgo` reaches `startScene`, a current script that is a room, object or local script (`WIO_ROOM`, `WIO_FLOBJECT`, `WIO_LOCAL`) is ended at once (`nukeArrays`, `_currentScript = 0xFF`), so nothing after the load runs; then `killScriptsAndResources` ends the other room-owned slots. Global scripts survive (the session already pauses them after the load and resumes them in the new room).

## Model

- In the scopes that enter a reached load (an action, a room click, a verb-area click, a key, a frame advance), a reached `loadRoom`/`loadRoomWithEgo` into an indexed room by a room-owned script now ends that script (`StopAfterEvent`, a normal stop) and adds the trace line "`loadRoomWithEgo` at … ends this room-owned script (startScene); the session enters Room NN". The session enters the room as before.
- Unchanged: a possible load (behind a seeded pick) is still not entered; a global script's load still pauses and resumes; a load outside these scopes (the boot's room-owned scripts), or into a room that is not indexed, keeps the old label.
- Not modelled (unchanged): `killScriptsAndResources` ending the other room-owned scripts in the same step. For example, a verb that starts a local which loads at once goes on with the verb's remaining instructions in the session.

## Evidence

Test: `LoomObjectVerbTests.ReachedObjectVerbRoomLoadChangesSessionRoom` now checks that the verb's run ends normally at the load (no unresolved reason, no "awaits" label) and that the trace names the room entered.

## Verification

- Tests: 198/198.
- Blockers (`blockers-after.txt` against `../estimated-arguments/blockers-after.txt`): 5905 → 5839. "script requests Room #; room transition awaits bounded session dispatch" 66 → 0; no other count changed, so every destination is entered as before.
- Room audit: 0 gaps in 76 rooms; `compare-png.ps1`: 0 changed renders.
- WPF checks (`wpf/`, `wpf-checks.log`): same as after task 1. Every check passes except CameraPanCheck (2 FAIL) and VerbAreaCheck (1 FAIL), which fail the same way with the Core before this session (see `../estimated-arguments/validation.md`); they come from the uncommitted UI refresh.
