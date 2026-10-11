# Play Cutscene in Room 01 during the first-launch scan (11 October 2026)

User report (loom ega en): switching Room 02 -> 01 mid-scan, Play Cutscene did nothing, then the button vanished.
Cause: a scanned EGA edition lists only scanned triggers; Room 01 has none, so the button showed disabled ("looking for scenes") and
disappeared once the lists finished. Fix: the opening (script 044, which plays in Rooms 01, 02, 53) is listed in those rooms.
`MidscanClick <game folder>`: Room 01 mid-scan now enabled; the click starts playback (button disabled while it plays).
