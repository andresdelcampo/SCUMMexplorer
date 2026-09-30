# Cutscene controls, 30 September 2026

The user's revised H: a normal Play a cutscene button when one scene applies, a dropdown with simple titles when several apply, and no control when none applies. J: a Skip line button sharing the '.' path.

The control now chooses directly from the room's existing decoded scene membership. Single-scene clicks start playback immediately. Multiple-scene menus use plain titles; source and script details stay in tooltips. No-match controls are collapsed. Skip line calls SkipTalk, sets the talk delay to zero and leaves the whole scene running; it is enabled only for nonempty talk.

ControlsCheck in this folder drives the real WPF window with catalog selections and synthetic button events. Final report: [run-02/controls-check.txt](run-02/controls-check.txt), 10 PASS, zero failures. Covers no scenes in Rooms 20/41, two scenes in Room 11, the direct trial button in Room 08, a nonempty trial line, zero-delay skip and the next frame without ending the scene, and inspection-mode visibility. No physical input proof. Initial run-01 failed because the harness waited for a room before selecting one; setup corrected before the final run.

Harness build: zero warnings/errors. The committed test suite passed 244/244; the unrelated untracked LoomObjectLookAuditTests was explicitly excluded. No Core/session/click/load behavior change, so RoomAudit, Blockers, DoorwayClickCheck and unrelated playback checks were skipped. Source cwd: D:\Documents\Personal\Code Projects\SCUMMexplorer. Commands used the absolute RTK executable with explicit cwd and approved escalation.
