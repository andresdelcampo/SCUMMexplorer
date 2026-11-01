# Next session

Read `docs/ROADMAP.md`: scope, readiness, the ten features and the board, what is open, the work in order, the user's checks and the rules. Maniac Mansion and Zak McKracken stay with their own orchestrator (`NEXT_SESSION_PROMPT_MANIAC_ZAK.md`). What was done lately is in `docs/archive/CHANGELOG.md`, not here.

## How to work

The user: "you choose what to do next ... don't wait for me". Take the next item of the roadmap's "The work, in order", easiest first when they are alike; fix it, check it in the real window where it shows, run the relevant tests (the full suite before committing engine changes, about 6 minutes, with `SCUMM_SOUND_TEST_ROOT` = `D:\Juegos\Lucas\loom ega mt32 en` and `SCUMM_INDY3_EGA_TEST_ROOT` = `D:\Juegos\Lucas\indy3 ega en`), commit each item separately. Mark a board cell ✅ only when it works in the app (no progress on paper). Stop only for product decisions. Read ScummVM's source on GitHub (raw.githubusercontent.com) before porting engine behaviour. Edit files with the Edit tool or UTF-8 Python, never PowerShell `Get-Content`/`WriteAllText`.

Keep the documents tidy (user, 31 October; rule in ROADMAP and CLAUDE.md): when an item is done, its result goes to the CHANGELOG and its record, and its lines leave `docs/ROADMAP.md` and this file in the same commit; nothing "done" stays in current-state documents. The user works from `docs/ROADMAP.md` directly (no artifact).

## Harnesses

- `.verification/scene-setup-20261031/Measure` (headless: every scene group of a game, fresh run, setup search, at-cutscene start; `MEASURE_TRIALS=1`, `MEASURE_DIRECT=1`, `MEASURE_VERBS=1`).
- `.verification/loom-editions-20261031/Editions` (headless: LOOM's scanned scenes on LOOM's own path and on the shared engine; `EDITIONS_LOOM_SESSION=1`, `EDITIONS_TRACE=room,object,verb`); `.verification/loom-answer-key-20261031/Compare` (the English catalog as the answer key; `COMPARE_LOOM_SESSION=1`).
- `.verification/scene-setup-20261031/WindowCheck` (real `MainWindow`: `room,object,verb` plays a listed scene, LOOM's scanned lists included; `r:room` opens a room; `w:seconds` waits; `s:id` opens a global script's page and logs its readable view; `o` lists Interact; `c:group,choice` runs one; `i:room,object,verb` an action; `a` LOOM objects with verbs not present).
- `.verification/mi2-intro-protection-20261030/IntroWindowCheck` (`hands-off`), `IntroProbe`; `.verification/indy3-castle-20261030/SourceDump` (`g:`, `e:`, `l:`, `v:`, `o:`, `ofind:`, `find:`, `box:`, `visit:`, `prep:`); `.verification/monkeycd-rooms-20261030/StartFrames`.

## Next

1. Roadmap work item 1: scenes started at their cutscene (10.8% of the five non-LOOM copies: Indy 3 11.1%, Monkey Island 1 12.8%/11.9%, Monkey Island 2 11.1%, Indy 4 9.4%; 26 show nothing). What is left is one-off tests: arrivals from pseudo rooms (Indy 4 `var[0x0E0]` = 149, 154, 163), Indy 4's ladder (Sophia in `var[0x0B6]`), walk-offs that wait on the hero's box and x position (Indy 4 Room 44's local 203), a scene that only plays when VAR_ROOM is another room (Monkey Island 2's dog, Room 53), music-synchronised waits (Monkey Island 2's Wally, Room 79, local 207 reads `var[0x038]`), "this object" passed to a local script through a variable (Monkey Island 2's coffin, Room 18). Measure with `.verification/scene-setup-20261031/Measure` (all games in parallel; `MEASURE_SCRIPTS=1`, `MEASURE_TRIALS=1`) and `python summarise.py <run> <previous run>` in that folder (latest: `*-run23.txt`).
2. LOOM: Room 47's swan prints nothing after the window's room visit (open in ROADMAP); the remaining length differences before shortening the English catalog.
3. Then the roadmap's order (variables and bits for readable names, the user's reports, release).

How to work (user, 31 October): batch small fixes; focused tests while fixing, then one all-games measurement, one full suite and one commit per batch. Keep the README's readiness table (percentages) in step when a measured number changes.

Open questions for the user (ask once, in passing): the decisions 2, 4, 5, 7 in ROADMAP's "Your checks" and the retests.
