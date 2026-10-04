# Remove obsolete Advanced controls, 30 September 2026

The user requested removal of "Pan to the camera on entry" and its test, and removal or repair of "Show verb area", which appeared ineffective.

Removed the entry-pan checkbox, its presentation timer and all callers. Room entry now applies the session camera at once, preserving script-driven camera motion, manual pan limits and framing. Removed CameraPanCheck's project/source and its entry in wpf-checks.ps1. Historical evidence is preserved.

Removed the redundant verb-area toggle: interactive LOOM already always drew the verb screen, regardless of its value. The visible interactive strip now also receives note clicks without that hidden toggle being checked. Inspection mode still sends no game input. Updated VerbAreaCheck for this behavior.

Verification of this task: [run-01/verb-area-check.txt](run-01/verb-area-check.txt), zero failures; absence of both controls, slot images, plain note after its glow animation, close-up, note input before/after aiming and layout. [doorways-01.txt](doorways-01.txt): the required Rooms 02, 03, 04, 08, 10 and 37 all passed. These are synthetic WPF handlers, not physical mouse proof. A later combined run-02 with the pending audio clock fix failed one old assertion: the fully completed draft correctly cleared var[0x062], whereas the old harness expected it to remain 1 after instant playback. That assertion is corrected and rerun in the audio task; the failed report is preserved. Harness build was zero warnings/errors after removing an unused helper; final solution build was zero warnings/errors. The committed suite passed, with unrelated untracked audit tests excluded. The final combined session suite has 246 tests (including two separate audio metadata regressions).

No room artwork, masks, or session state transitions were modified by this task. RoomAudit/Blockers and broad unrelated playback tests were skipped. Each run used a fresh output folder. Commands used absolute RTK, explicit cwd D:\Documents\Personal\Code Projects\SCUMMexplorer and approved escalation. Only this task's changes were staged; audio changes were left for their separate commit.
