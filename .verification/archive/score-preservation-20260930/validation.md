# Persistent score during note playback

30 September 2026. New folder checked with `git ls-files` before writing.

**Visible result:** score/staff glyphs remain after repeated notes. The distaff glow still animates and ends on the plain note. No note-row geometry or input path changed.

**Reproduction:** real MainWindow, script 018 [2] enables the distaff through the test seam; scripts 027, 031 and 028 mark/play/reset successive notes. These are assumed setup actions, not an independently played game route. `before-fix-6/score-check.txt` loses 137 staff pixels after repeated drafts; `after-fix-1/score-check.txt` loses zero. Captures show before, intermediate animation frames, after one note and after repeated notes. Initial exploratory runs 1–3 used incomplete synthetic bytecode and are not conclusive; run 4 corrected literal arguments; run 5 hit script 027's four-note limit and did not exercise repeated drafts. Run 6 includes the decoded reset.

**Cause:** `LoomMessageTable` kept only 32 drawn strings. Script 018 already prints 24 note/staff glyphs; subsequent note marks and names evicted unrelated persistent glyphs without any game erase. Script 031's 24-row images at y144 end at y167 and do not overlap the standard score at y169. The fix removes arbitrary FIFO eviction and coalesces identical glyph geometry repainted at the same position. Explicit drawBox/verb erases and room redraw still clear text; the message log keeps every print.

**Evidence boundaries:** “score” here means the standard-difficulty staff below the distaff. Practice-mode draft marks at y188 may legitimately clear through script 028/verb 0x35; do not make those permanent. No physical input or ScummVM runtime comparison. Engine reference: [ScummVM drawVerbBitmap](https://github.com/scummvm/scummvm/blob/master/engines/scumm/verbs.cpp), [EGA strips](https://github.com/scummvm/scummvm/blob/master/engines/scumm/gfx.cpp); decoded scripts in `fallback-evidence/code.tsv`.

**Checks:** 247 committed tests pass (unrelated untracked audit excluded); main Release solution zero warnings/errors; ScoreCheck 5 PASS/0 failures; existing VerbAreaCheck zero failures. RoomAudit: 76 rooms, zero remaining gaps; all 76 pixels hashes match saved room PNGs (`png-comparison.txt`), so no PNG refresh. Blockers skipped: executor/opcodes and route dispatch unchanged. DoorwayClickCheck and other WPF harnesses skipped: no click, room loading, actor or cutscene-clock changes.
