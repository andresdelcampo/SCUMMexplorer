# Global cutscene picker (1 October)

Task 1: moved Play Cutscene into the top bar, replacing normal Game Texts' position. Its replay list is global: the opening, trial, transition and firewood can be started from Room 16 or any other room. The catalog expansion is a separate task; this change does not claim full replay coverage yet.

`powershell -NoProfile -ExecutionPolicy Bypass -File .verification/global-cutscene-picker-20261001/run.ps1`: PickerCheck **6/6**. Real visible WPF window/layout, routed menu click, fresh sessions, pause and room departure. Screenshot inspected at `Exe/VerificationSession-20261001/picker-task1/normal.png`. Synthetic input; no physical input or independent listening. Main Release build: zero warnings/errors. No Core execution/rendering changes; rendering audit skipped. Navigation unchanged; focused departure check used.

Task 2 validation will use the same harness with `-Advanced`, a fresh output folder and three additional visibility/round-trip assertions.
