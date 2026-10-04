# Test and harness review, 4 October 2026

Full run of the unit tests and of every verification executable in `.verification/` (129 projects) at the end of the LOOM EGA work, followed by the clean-up decisions the user delegated. Raw results: `.verification/harness-review-20261004/results.tsv` (results of the first run, before the clean-up) and the driver `run-all.ps1` next to it.

## Result of the run

- **Build:** 0 errors; 2 nullable warnings (`MainWindow.xaml.cs:3691` CS8604 and `:3692` CS8602, the `SceneSoundPlayer.Attach` call).
- **Unit tests:** 703 passed, 0 failed, 5 skipped (diagnostic dumps).
- **Harnesses:** 76 ran clean, 53 did not; none of the 53 showed a game-behaviour regression. Blockers: 0 blocked random sites. DoorwayClickCheck 16/16 (rooms 2, 3, 4, 8, 10, 37; over all rooms it takes more than 15 minutes). RoomAudit: 0 rooms with gaps.
- Room17Check ended in an interrupted walk: the first approach to the tomb triggers a cutscene (user, 4 October). Expected, no defect.

## Decisions taken (criteria: effort to repair now, chance of regression, effort to rebuild later, is the code still being touched, does it pass now, does it help other versions or games)

**Kept, passing (about 76).** Cheap to keep, they pass now, and the generic ones are the tools for auditing other LOOM editions and other games: `RoomAudit` (room rendering gaps), `UiShots`, `SceneShots`, `StatusShots`, `WindowCheck`, `TextGalleryCheck`, `DoorwayClickCheck`, `Blockers`, `VisitProbe`, `CodeDump`, `ScriptRefs`, `DurationAudit`, the audio, input, key, click, score, walk-box, opening, cutscene-playback, sequence, mode, scene-leave and music checks.

**Repaired (3).** `StatusShots` (the picker method was renamed), `TextGalleryCheck` (conversations are now drawn as lines in their own panel; the check reads that panel), `SpellAudioCheck` (the arrival to Room 13 now plays through Room 10; the check visits Room 13 before the look). All three pass; they cover the picker/Game Status screenshots, the Game Texts gallery (generic, reusable for Indy3 and other games) and spell audio.

**Removed (35 projects).** Source remains in git history (`git log --diff-filter=D -- .verification/<topic>/<name>`).
- *Stale UI checks (20).* They reflect on private members of the old window (toggles and methods that no longer exist) or assert layouts and counts that changed on purpose, so each would need a rewrite against the current UI, and a rewrite later is as cheap as a repair now: `BusyCheck`, `ControlsCheck`, `CutsceneMenuCheck`, `PickerCheck`, `ReplayCheck`, `RoomReplayCheck`, `NamesCheck`, `StaffCheck`, `RefinementCheck`, `StatusRefinementCheck`, `StatusUiCheck`, `PointerInputCheck`, `ObjectInteractionCheck`, `VerbAreaCheck`, `PresentationCheck`, `SoundLifetimeCheck`, `RouteUiCheck`, `ScopeUiCheck`, `NativeReplayCheck`, `Room17Check`. Three of them (BusyCheck, ControlsCheck, ReplayCheck) hung for ever on the modal Play Cutscene picker.
- *Probes that need an old saved input (15).* They read a recipes JSON in a shape that no longer exists, and their findings are in the `validation.md` of each topic folder: `HandshakeAudit`, `CliffCamera`, `coverage-routes/RouteProbe`, `ReplayAudit`, `GapProbe`, `MembershipAudit`, `SequenceAudit`, `SharpeningProbe`, `staff-correction/RouteProbe`, four `ProgressionProbe`, `DraftProbe`, `CertaintyAudit`.
- **Coverage gap this leaves:** nothing automatic now exercises the Play Cutscene button guards (no start during playback), the Game Status window layout, or the pointer/hover handling at window level. Unit tests cover the session logic; UiShots/StatusShots give screenshots. Rebuild these as small checks against the new UI if a change touches them.

**Kept on purpose although they are not regression tests.**
- The ~36 route-evidence probes of 2–3 October (Room 02 … Room 62 inputs, `GlobalInputProbe`, `Room34*`, `Room69*`): the long bespoke input recipes behind the "234/235 audited sites" figure. 26 pass when given a fresh output folder; 10 fail exactly as `PLAN_HISTORY_2026-10.md` records (Room 34 chain/watcher/local200, Room 69, Room 09), and they are the starting point if the parked Room 34 work resumes. Rebuilding them would take hours; keeping them costs nothing.
- Utilities that print data and need arguments: `MessageRender`, `CostumeAnimRender`, `CacheCheck` (needs `SCUMM_ROOM_CACHE_DIR`).
- `ConsoleFailureProbe`, `FailureProbe`: crash on purpose to prove the harness failure policy (exit 1 is their pass).
- The 5 skipped unit tests (`LoomObjectLookAuditTests` ×2, `LoomSceneSetupPlannerTests.Dump*` ×3): `Skip`-marked diagnostics that cost nothing to carry and are the quickest audit of costumes, speakers and catalog setups when another game or edition is opened.

## The `Exe` folder (27.6 GB → about 1.4 GB)

`Exe/` is git-ignored build output plus evidence packets. The 27.2 GB were 75 evidence folders from the 2–3 October observer and route sessions (mostly `.jsonl` captures, referenced by the `validation.md` files). Each was packed unchanged into `Exe/_evidence-archive/<name>.tar.gz` (1.1 GB in total) and removed only after the archive's file list matched. To read one: `tar -xzf Exe/_evidence-archive/<name>.tar.gz -C Exe`. Build outputs of the removed harnesses were deleted; the rest rebuilds with `dotnet build`. The app (`Exe/SCUMMexplorer.App`) was not touched.

## Repository housekeeping still open

- `.claude/worktrees/` holds 17 historical git worktrees (clean ancestors of `master` per `CLEANUP_REVIEW.md`); they slow file searches.
- `docs/CLEANUP_REVIEW.md` proposals (source backup, old patch, `ui-refresh-*` captures) are still open.
- The 2 nullable warnings above.

## How the run was done

Each harness project was built and run as `<exe> <Games\loom> <fresh output folder>` with a 200 s limit and a watcher that stops a process whose extra window stays open 30 s. Probes that need other arguments got `src/SCUMMexplorer.App/loom-replays.json` and a fresh output file. `SequenceUiCheck` takes 8 minutes, `Blockers` 9 minutes (its second argument is a file).

## Correction (4 October, later): exit code 0 is not a pass

WPF harnesses catch assertion failures inside their window handler and still exit 0, so the "76 ran clean" figure above counted some that print FAIL lines. Reading the reports instead: ClickInputCheck (expects one click to reach Room 03; the game now interrupts the walk by the box-4 scene), SceneSoundCheck (Script 046: 3 sounds expected, 0 played; worth a look: the trial's music may not start from the picker), FirewoodPlaybackCheck and GameTextsCutsceneCheck (status text now starts with "Room NN ·"), KeyInputCheck (note key 'e', Escape during a draft and '.' in the trial: worth a look), MessageAppCheck (text row offset), MusicPanelCheck (the music panel became the Sounds menu). UiShots had also failed silently at its Game Texts step; it is repaired (it picks the first room that has a cutscene group) and now runs through. Of the harnesses I kept as passing, the ones that really report `Failures: 0` are ActorDrawCheck, ArrivalCheck, AudioCheck, InputCheck, CutsceneLayoutCheck, CutscenePlaybackCheck, HighlightCheck, ReverseHoverCheck, ModeCheck, OpeningPlaybackCheck, Room02ExitClickCheck, MusicRowCheck, SceneLeaveCheck, RoomControlsCheck, ScoreCheck, TextLineCheck, WalkBoxCheck and the repaired UiShots, StatusShots, TextGalleryCheck and SpellAudioCheck. The seven failing ones above were not removed; they need a repair-or-remove decision with the same criteria.
