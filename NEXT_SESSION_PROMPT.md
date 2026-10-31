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

## Scope (decided 31 October)

The user narrowed the product (REQUIREMENTS R-1.14 and §8.6, rewritten [ROADMAP](docs/ROADMAP.md)): pick any room and trigger its interactions and cutscenes at any time, without playing to them; teleports and magic costume changes are fine; no save/load; Play stays as it is and comes last. Build incrementally.

## Next, in order

1. **Scene setup for every game (§8.6, roadmap item 1), Indy 3 first.** Read `Loom/Scenes/LoomCutsceneReadiness.cs` (the LOOM search: fresh trials, raise one tested story item at a time, keep it when the scene begins or reaches further) and `Scumm/Session/ScummCutscenePlanner.cs`. Move the search to `Scumm/` and widen it to object states and owners (items given), actors placed and dressed. Measure on Indy 3's 58 groups that begin nothing ([record](.verification/indy3-cutscenes-20261017/README.md)), then Monkey Island 1 (Script 117's SCUMM Bar exit: open door, `bit[0x1BE]`), Monkey Island 2, Indy 4. Keep LOOM's behaviour unchanged (before/after on its tests).
2. Interactions in a picked room (R-8.38): compare what a room offers with its objects' verbs that have code.
3. Derivation time once the setup adds trials (MI2 140–174 s).
4. The user's crash and "thin black rooms" reports when they name the game and room. `dotnet-trace` is installed for UI stalls (`--profile dotnet-sampled-thread-time --format Speedscope`, with `LaunchStalls`).
5. Release readiness for LOOM.

Decisions 1, 3 and 6 in USER_CHECKS were answered by the scope decision; 2, 4, 5 and 7 and the retests are still open: ask once if the user has not given them.
