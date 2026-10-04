# Returning from Advanced restores interactive clicks

30 September 2026. New evidence folder checked before writing.

**Visible result:** after inspecting artwork in Advanced, turning Advanced off restores room/object and visible distaff-note clicks immediately. The same session and room state are retained; no catalog revisit is needed.

**Reproduction:** the Interactive checkbox could be unchecked in Advanced and remained unchecked after Advanced was closed, where that control is hidden. `before-fix-1/mode-check.txt` shows game input and both click types disabled in this case. Merely opening/closing Advanced with Interactive left enabled already sent clicks correctly. Its first hit-test assertion used the center of the full wide room image outside the scrolled viewport; corrected to the visible viewport center in the final check.

**Fix:** leaving Advanced restores Interactive. A scoped flag makes the synchronous mode-change handler rebuild the view from the existing session snapshot instead of invoking `VisitRoom`. Direct inspection toggles in Advanced keep their existing behaviour.

**Verification:** final ModeCheck 9 PASS/0 failures: both round-trip variants, live room hit testing, room/object input, visible note input, same session and unchanged trace on returning from inspection. Synthetic WPF events/test seams; no physical input. Required DoorwayClickCheck Rooms 02,03,04,08,10,37: 16 PASS/0 failures (`doorways-final.txt`). Main Release solution zero warnings/errors; committed Core tests 247/247. Core state/rendering unchanged by this App-only fix; RoomAudit, Blockers, PNG refresh and other WPF checks skipped.
