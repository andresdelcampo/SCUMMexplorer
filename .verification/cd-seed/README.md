# CD scene-list seed (10 October 2026)

`dotnet run --project .verification/cd-seed -- "Games/loom vga en" src/SCUMMexplorer.App/loom-cd-scenes.json` makes the shipped seed: for each of the 223 scanned groups the rooms its headless run entered (from the planner's derived start setup), and the intro's rooms. 49 s. It is tied to `ScummV4Archive.Fingerprint` (SHA-256 of the decoded DISK01.LEC and 000.LFL), so the Kixx release, which has the same data, matches too; any other data is scanned live as before. Regenerate after any change to the scan, the planner or the headless run.

Categories are not in the seed. The run says 19 of the static rule's 98 scenes stay in their room (Room 34's sleep reactions, Room 28's dragon, Room 43 Opening at cage, ...), and several of those leave the room in the validated EGA game, so the CD keeps the static rule until the run is checked against the real game (ScummVM). The 19 are printed by the generator. Room 34's entry finds no start.

Check: `.verification/scanned-ega-lists` with the CD folder and `1 cd` (room lists known at once, The Intro first in Room 01, Room 04 lists 7 entries): PASS.

## Why the run may be unreliable for the 19 (10 October, Room 34 "Sleep at boy")
`cutscene-scan-ega setup 34 487 67` (CD): the planner needs no setup (a fresh start plays it) and the headless run stays in Room 34. The verb's code branches on `var[0x102]`: 0 starts global script 85 (Rusty's long, `var[0x0E]`-timed wake-up conversation); non-zero prints "He's already fast asleep." The run ends at the first moment no cutscene is open and no line is shown, which can fall between the conversation and what follows it. The same reaction leaves for Room 52 in the English EGA catalog. So the CD run is not trusted for categories until it is compared with the real game (ScummVM 2.9 is back in `scummvm/` but has no LOOM entry and cannot read the .cue/.bin; the disc's files would have to be extracted first).
