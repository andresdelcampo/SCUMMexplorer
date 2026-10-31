# Next session

Read `docs/ROADMAP.md`: scope, readiness, the ten features and the board, what is open, the work in order, the user's checks and the rules. Maniac Mansion and Zak McKracken stay with their own orchestrator (`NEXT_SESSION_PROMPT_MANIAC_ZAK.md`). What was done lately is in `docs/CHANGELOG.md`, not here.

## How to work

The user: "you choose what to do next ... don't wait for me". Take the next item of the roadmap's "The work, in order", easiest first when they are alike; fix it, check it in the real window where it shows, run the relevant tests (the full suite before committing engine changes, about 6 minutes, with `SCUMM_SOUND_TEST_ROOT` = `D:\Juegos\Lucas\loom ega mt32 en` and `SCUMM_INDY3_EGA_TEST_ROOT` = `D:\Juegos\Lucas\indy3 ega en`), commit each item separately. Mark a board cell ✅ only when it works in the app (no progress on paper). Stop only for product decisions. Read ScummVM's source on GitHub (raw.githubusercontent.com) before porting engine behaviour. Edit files with the Edit tool or UTF-8 Python, never PowerShell `Get-Content`/`WriteAllText`.

Keep the documents tidy (user, 31 October; rule in ROADMAP and CLAUDE.md): when an item is done, its result goes to the CHANGELOG and its record, and its lines leave `docs/ROADMAP.md`, this file and the trackers in the same commit; nothing "done" stays in current-state documents. The user works from `docs/ROADMAP.md` directly (no artifact).

## Harnesses

- `.verification/scene-setup-20261031/Measure` (headless: every scene group of a game, fresh run, setup search, at-cutscene start; `MEASURE_TRIALS=1`, `MEASURE_DIRECT=1`, `MEASURE_VERBS=1`).
- `.verification/scene-setup-20261031/WindowCheck` (real `MainWindow`: `room,object,verb` plays a listed scene; `r:room` opens a room; `o` lists Interact; `c:group,choice` runs one; `i:room,object,verb` an action; `a` LOOM objects with verbs not present).
- `.verification/mi2-intro-protection-20261030/IntroWindowCheck` (`hands-off`), `IntroProbe`; `.verification/indy3-castle-20261030/SourceDump` (`g:`, `e:`, `l:`, `v:`, `o:`, `ofind:`, `find:`, `box:`, `visit:`, `prep:`); `.verification/monkeycd-rooms-20261030/StartFrames`.

## Next

1. Roadmap work item 1: scenes started at their cutscene that show nothing (Indy 3 10, MI1 10, MI2 24, Indy 4 35; e.g. Indy 3 Room 13's entry jumps to Room 73).
2. Then the roadmap's order (derivation time, readable names, the user's reports, release).

Open questions for the user (ask once, in passing): a Cutscenes node in the Advanced tree; the decisions 2, 4, 5, 7 in ROADMAP's "Your checks" and the retests.
