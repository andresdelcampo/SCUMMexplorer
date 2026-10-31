# Next session

Read `docs/ROADMAP.md` (the seven gates per game and the steps left), `docs/USER_CHECKS.md` (what only the user can check or decide) and `docs/CURRENT_WORK.md`. Maniac Mansion and Zak McKracken stay with their own orchestrator (`NEXT_SESSION_PROMPT_MANIAC_ZAK.md`).

## Done on 30 October 2026 (all on master, not pushed)

- Roadmap and per-game user checks; crash log in the shipped app (`%LOCALAPPDATA%\SCUMMexplorer\crash-logs`); Sound ◐ for the ten non-LOOM rows until the user's listening check.
- MI2 floppy copy protection answerable in Play Game Intro: strings inside strings resolve; talk lines wrap from version 4 on (ScummVM `displayDialog`/`addLinebreaks`).
- MI1 CD Rooms 88 (empty entry block), 89, 99 (icon-holder room the game never enters), lookout frame: closed.
- Indy 4 Rooms 76, 77, 87: an at-once walk with no box route ends where it started (ScummVM `walkActor`); first-state images and Room 4's magenta line no longer seen.
- Indy 3: castle Rooms 20, 28, 32 explained (cut-away plan, guards that can start a fight; decision 7 in USER_CHECKS); `getActorWidth`; `actorFollowCamera` into another room loads it in version 3 too.

- 31 October: a held object's Look at works in another room (Indy 3 Script 2 `getDist`, ScummVM `getObjectOrActorXY`).
- 30–31 October, later: indexed script arguments (all 88 Indy 3 rooms clean), first-launch freeze fixed (one inspection copy, room reads on workers, reopen after first render).

## How to work

The user said "keep making fixes, no need to ask": take the next roadmap item, fix, check through the real window where it shows, run the relevant tests (full suite before committing engine changes, about 8 minutes, `SCUMM_SOUND_TEST_ROOT` must be the LOOM folder `D:\Juegos\Lucas\loom ega mt32 en`), commit each item separately, update the roadmap, CURRENT_WORK, CHANGELOG and the game's tracker. Stop only for product decisions (gate G). Read ScummVM's source on GitHub (raw.githubusercontent.com) before porting engine behaviour. Edit files with the Edit tool or UTF-8 Python, never PowerShell `Get-Content`/`WriteAllText` (it damaged "§" once).

Harnesses added today: `.verification/mi2-intro-protection-20261030/IntroWindowCheck` (real window, Play Game Intro, keys), `IntroProbe`; `.verification/monkeycd-rooms-20261030/StartFrames` (real window, pick a room, rooms per frame, picture), `BlockDump`; `.verification/indy3-castle-20261030/SourceDump` (any game: `g:`, `e:`, `l:`, `v:` object verbs, `o:`, `find:`, `box:`, `visit:`, `prep:`).

## Scope question (31 October, waiting for the user)

The user asked whether the scope (a fully playable game per title) can be lowered toward the original idea: a static explorer that replays interactions and cutscenes. Proposed: keep the script engine (scenes are scripts, so it is needed anyway); replace the generic start planner E4 with session snapshots (save any moment of your own play or a catalog pick, start scenes and interactions from it); make Play a means, not a gate, for the non-LOOM games (drop E5 and the R-7.x sign-off, lighter C/F passes); gate B as spot checks. Do not start E4 or E5 before the user answers; rewrite ROADMAP's gates if they agree.

## Next, in order

1. ~~Gate A leftovers~~ done 31 October: held objects' verbs work in another room (`getDist` owner position in every version); Rooms 1 and 15 never rest (crowds; not a bug).
2. The user's crash and "thin black rooms" reports once they name the game and room (the crash log now records them).
3. First launch is fixed (Indy 4 play at 4.5 s). `dotnet-trace` is installed (global tool): `--profile dotnet-sampled-thread-time --format Speedscope` with `LaunchStalls` finds UI stalls; remember App.xaml's StartupUri opens a second window under `app.Run()`.
4. Shared engine work E3 (a working ScummVM capture for picture comparisons), E4 (generic cutscene start planner), then E5 (Status beyond v1/v2) if the user wants it.
5. Release readiness for LOOM (packaging, version, clean-profile first run).

Ask the user, once, for the answers in USER_CHECKS (seven decisions, the retests) if they have not given them.
