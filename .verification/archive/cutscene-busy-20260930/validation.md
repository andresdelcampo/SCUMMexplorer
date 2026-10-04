# Cutscene start controls while playback is active

Before: single button, dropdown items, opening and known-scene entry points could replace the current session and restart during playback. Recommendation implemented: disable new scene starts during playback, including pause, with guards at actual entry points. A stale open menu cannot bypass the guard. A cutscene still active after the clock stops remains guarded. The player can pause, skip a line, or skip the whole scene at its source-established override.

The user sees a dimmed Play a cutscene control during playback. Completion/skip, browsing a different room and returning to interactive mode restore it when the state is idle.

BusyCheck final/busy-check.txt: 11 PASS, zero failures. Real MainWindow, synthetic button/menu calls, accelerated source frames. Single control, stale dropdown, opening call, paused state, menu disabled items, source skip availability, natural script 052 completion, trial skip completion, multiple control, different catalog room and mode return covered. No physical input or executor state fabricated.

run1 had three harness assumptions wrong: it checked skip before script 046 reached +0x0020 beginOverride, and selected the already selected Room 08 (not a catalog transition) before checking recovery. Final waits for the real skip point and selects a different room. Earlier report retained.

Release build zero warnings/errors. Existing Core suite 249 passes; this task changes only App control enablement/guards, with no further Core changes. Room click/doorway dispatch, loading, renderer and executor unchanged, so doorway, RoomAudit, Blockers and PNG refresh skipped.
