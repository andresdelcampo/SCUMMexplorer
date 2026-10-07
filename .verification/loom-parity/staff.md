# Staff state parity, 7 October 2026

The shared LOOM session reconciles the distaff after catalog entry has settled, with 32 **editable** frames rather than 32 elapsed frames. It rebuilds printed note letters even if the image verb slots are already on. Gameplay input ends this catalog adjustment, so scripts retain control during interaction and cutscenes.

Game Status refreshes both the appearance and note controls through the game's scripts 017–019. The CD costume default for the large look changes even while Bobbin currently wears a smaller look: otherwise Room 05's look-0 entry restores the old without-staff costume. The two CD costumes remain derived from the script assignments (172 with, 171 without); EGA retains its animation-based appearance.

Room 69 is the user-authorized menu exception in the LOOM session. The composed strip filters distaff verbs; on entry the old verb-screen letters are erased before the menu writes its own text. Possession is preserved for the next room.

## Targeted validation

- `LoomStaffParityTests.CatalogVisitsKeepStaffAppearanceNotesAndMenuSuppressionTogether`, both reference EGA and VGA: passed. Room 03 → give staff → Room 05; remove → Room 03; give → Room 69 → Room 03. Checks the staff costume/animation, drawn note verbs and printed note letters, plus menu suppression and retained possession.
- Existing `StaffEditsPreserveOtherBitsAndRefreshTheSourceNoteControls`, `BrowsingRaisesHeldStaffToInitialLevelAndKeepsHigherExplicitChoices`, and `TheCdStatusSchemaIsReadFromTheScriptsAndItsEditsShowTheDraftVerbs`: passed in the focused six-test run (the new note-text assertion was subsequently repaired to match the source labels' instruction offsets).
- One bounded `ParityCheck` UI harness, with the supplied EGA and VGA data: staff checks passed in `ui-02/report.txt`, with `ega-staff05.png`, `vga-staff05.png`, `ega-menu69.png`, `vga-menu69.png`. Captures are synthetic WPF renders; the user still checks physical input and appearance.

No full suite, replay catalog, long scene playback or audio rendering run.
