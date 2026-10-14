# S4a: the generic session (14 October 2026)

Stage S4a of [GAME_SUPPORT_INSTRUCTIONS](../../docs/GAME_SUPPORT_INSTRUCTIONS.md): split the game-independent part of `LoomExplorationSession` from LOOM's part. Behaviour of LOOM must not change. Plan file: [docs/checkpoints/20261014-S4a.md](../../docs/checkpoints/20261014-S4a.md).

## Plan (written before the first move)

C# `partial` classes cannot span namespaces and `Scumm` cannot see `Loom`, so the split is an inheritance split:

| Target | Folder | Holds |
|---|---|---|
| `ScummSessionSnapshot`, `ScummObjectActionResult`, `ScummSessionSoundEvent` (renamed from `Loom*`) | `Core/Scumm/Session/ScummSessionTypes.cs` | the result records every session returns |
| `ScummSession` (abstract) | `Core/Scumm/Session/ScummSession*.cs` (partial files: core, rooms, input, frames) | the script runner with child-script tracking, cutscene/override/freeze stack, pending scripts, variables, actors, camera, verbs, messages, object states, room entry, click/key delivery to the game's input script, frame loop |
| `LoomExplorationSession : ScummSession` | `Core/Loom/Session/` (unchanged public API) | the LOOM boot (script 001, skill screen, CD intro), catalog visits and arrivals, ego size estimates, staff sync, drafts, story bits, Game Status, catalog recipes |

Game differences reach the base through constructor settings (initial variables, separate bit storage, sound lifetime reader, engine variable numbers), protected virtual hooks and `ScummProfile` properties, never through `GameId`. Each move is its own commit; the LOOM tests (EGA reference and CD) are re-run after each.

## Baseline (before the first move)

Commands (via RTK, repository root of the worktree; `Games` is a directory junction to the main checkout's data):

```powershell
dotnet build SCUMMexplorer.slnx
dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~Loom" --logger trx
dotnet RoomAudit.dll "Games\loom ega mt32 en" <out>      # about 1 minute
```

- Unit tests (all `Loom*` classes: boot, first-visit rendering, clicks, keys, pointer, actors, scenes, cutscene and replay catalog, catalog starts, CD tests `LoomCd*`/`LoomVga*`, Game Status, sound): **837 total, 831 passed, 1 failed, 5 skipped**. Per-test outcomes: `outcomes-baseline.txt`; summary: `summary-baseline.txt`.
- The one failure exists before any change: `LoomStoryEditionTests.StoryControlsUseTheirOwnEditionAndChangeRustysSourceBranch` for the edition `ALTERNATIVE LOOMS/loom ega mt32 es` (the English EGA and `loom vga en` cases pass). It is not part of the comparison.
- Room audit, English Roland EGA: 76 rooms, gaps before filling 3, after 0, rooms with remaining gaps 0 (`room-audit-baseline.tsv`).
- `WindowCheck` was not run: it opens the WPF `MainWindow`, is not cheap, and the unit tests above drive the same session through the same public API. The room audit stands for the picture check.

## Moves (each its own commit; the baseline tests were re-run after each and gave the same outcomes)

| # | Commit | What moved | Left in LOOM / hook |
|---|---|---|---|
| 1 | `59782d2` | `LoomSessionSnapshot`, `LoomObjectActionResult`, `LoomSessionSoundEvent` became `ScummSessionSnapshot`, `ScummObjectActionResult`, `ScummSessionSoundEvent` in `Scumm/Session/ScummSessionTypes.cs` (renamed in the App, tests and one harness; archived records untouched) | nothing |
| 2 | `77336bf` | `ScummSession` (abstract) in `ScummSession.cs`: fields, core constructor, script runner (`RunScript`, `RunStartedScript`, `RunIndexedScript`, `HandleEvent`, chain/stop/freeze, cutscene begin/end/abort, overrides, pending scripts, `ScriptFrame`), route records, `Snapshot` | sound lifetime reader passes as a constructor delegate; `IsObjectSentence(verbId)` virtual (LOOM: verbs 0x36, 0x38) |
| 3 | `c7ea29e` | `ScummSessionRooms.cs`: `EnterRoom`, the `loadRoomWithEgo` placement, camera set/forget/follow, virtual mouse and pointer, actors, costume source, verb area and verb images | arrival placement after `loadRoom` is a delegate argument (LOOM: `PlaceEgoAfterLoadRoom`); `ErasesVerbScreenOnEntry`, `HidesVerb`, `AlignVerbRows` virtual (LOOM: staff menu, CD layout) |
| 4 | `87b31aa` | `ScummSessionFrames.cs`: timers and script delays, `AdvanceOneFrame`, `StepWalksAndCamera`, `ContinueUntilRoomChanges`, `DispatchRequestedLoads`, `EnterReachedLoad`, `AdvanceFrame` (virtual) | `VisitReachedRoom` abstract (LOOM: catalog `VisitRoom`); `UpdateMusicTimer` virtual (LOOM: CD disc-cue timer); `AdvanceFrame` override adds staff sync and the played boot |
| 5 | `64d464a` | `ScummSessionInput.cs`: `Click`, `ClickVerbArea`, `PressKey`, `FinishInput`, scene click, `ActivateObjectVerb`, `RunLocalScriptAt` (delivery to `VAR_VERB_SCRIPT`) | `BeforeInput` virtual (LOOM: ends staff browsing) |
| 6 | `fc6eae1` | engine variable numbers into `ScummVars` (room, override, timers, input, cutscene, keys, the four entry/exit hooks) | nothing |
| 7 | `67724b1` | `ScummSessionBoot.cs`: `BootScript`, `RunBootScript`, `BootGoesOn`, `FinishBoot`, `BootFrames`, `FindAwaitedInput`, `AssumeBootAnswer`, `SoundCardVariable`, `SoundCard`, `ShadowPalette`; the constructor's engine set-up (costume source, verb image size, talk animation, timer zeroing) | LOOM: `CompleteBoot`, the CD intro and skill screen, machine-speed/CD variables |
| 8 | `c3f7b7b` | architecture tests (`ScummSessionArchitectureTests`) | |

## Final structure

- `Core/Scumm/Session/` (namespace `SCUMMexplorer.Core.Scumm`): `ScummSession` split in five `partial` files (`ScummSession.cs` core, `...Rooms`, `...Frames`, `...Input`, `...Boot`) and `ScummSessionTypes.cs`. About 115 KB. It contains no `GameId`, no LOOM script or room number in code (some comments still cite LOOM as the example), and takes everything game-specific through the constructor (`IScummDataSource`, observer, sound lifetime delegate) and the virtual hooks above.
- `Core/Loom/Session/LoomExplorationSession.cs` (56 KB, was 164 KB) `: ScummSession`, plus `LoomGameStatus.cs` (unchanged, still a partial of it): the LOOM boot (script 001 intro answer, opening cutscene 044, CD intro and skill screen), catalog visits (`VisitRoom`, arrivals, ego size estimate, arrival classes), staff sync and floor browsing, setup edits (`PlaceEgo`, `WalkEgoTo`, `PlaceActorInRoom`), story/draft/status. Its public API is unchanged except the three renamed record types.

## Baseline against final

| Check | Baseline | Final |
|---|---|---|
| `Loom*` unit tests (EGA reference and CD) | 837 total, 831 passed, 1 failed (Spanish-edition case, before and after), 5 skipped | 840 total (the 3 new architecture tests), 834 passed, the same 1 failed, same 5 skipped; **per-test outcomes identical** for all 837 (`outcomes-baseline.txt` against `outcomes-final.txt`, only the 3 new rows differ) |
| Room audit, English Roland EGA | 76 rooms, gaps 3 before filling, 0 after | `room-audit.tsv` **byte-identical** to the baseline |
| `ScummSessionArchitectureTests` | | 3 pass |
| Whole solution (`SCUMMexplorer.slnx`, App included) | builds | builds, 0 warnings |

`ScummSessionArchitectureTests.TheGenericSessionBootsALoomEgaGameWithNoLoomCode` derives a session in the test project from `ScummSession` alone, runs `RunBootScript`, answers the intro screen and plays frames: it reaches Room 02 with the same trace line (`Script 01 chains to script 6`) as `LoomBootTests`. That is the S4b entry point for a second game.

## Decisions (S4a decision log)

| Open question | Options | Choice and why | How to undo |
|---|---|---|---|
| How to split a `partial` class across namespaces | cannot (partial classes must share a namespace and `Scumm` cannot see `Loom`); aliasing; composition; inheritance | an abstract base class `ScummSession` in `Scumm`, LOOM's session derives from it: keeps the public API, lets the moved code reach shared state as `protected` members, smallest edits | revert commits 2 to 7 |
| Rename of the three result records | keep `Loom*` names (impossible, `Scumm` would need `Loom`), type aliases | renamed to `Scumm*` in all current code (Core, App, tests and the `ReverseHoverCheck` harness); archived 3 October harness sources under `.verification/archive/` were left as they were | rename back |
| Where LOOM's behaviour enters | `GameId` tests, profile properties, data | constructor delegate (sound lifetimes), `abstract`/`virtual` hooks in LOOM's derived class; `ScummProfile` already carries version facts (walking) and needed nothing new | |
| Engine variable numbers | per-version table in `ScummProfile`, constants | constants in `ScummVars` (they were v3/v4 numbers, the table's own comment says a later numbering gets its own table); a v5 table is S4b work | |
| Input constants (`SingleClickGap` 60, `DoubleClickGap` 10, `InputMode` 0) | leave, make virtual | left unchanged as `public const` in the base (the App uses them); they assume LOOM script 006's 40-jiffy double-click window and v3's `runInputScript` mode argument. S4b must make them per game | |
| `LoomCutsceneIndex`, `LoomScriptPresenter`, `LoomTextExtractor`, `LoomSpeakerNames` | move now, later | **not moved**: not part of the session, they carry LOOM verb roles (0x36 to 0x38, draft verbs 0x39+) and names; moving them is not cheap or safe without the verb-role data a second game supplies (K-06 says move at S7) | |
| `WindowCheck` | run | not run (WPF window, not cheap); the unit tests drive the same session API, the room audit covers the picture | |

## What remains for S4b and later (the next moves)

1. **`IScummDataSource` for v1, v2 and v5.** `ScummDataSources.For` returns `ScummV4DataSource` for every `UsesV4Archive` game and `ScummV3DataSource` otherwise; no source exists for the v1/v2 classic and enhanced Maniac Mansion and Zak McKracken, nor for Monkey Island 2 and Indy 4 (v5). `BareSession` takes any `IScummDataSource`.
2. **Boot variables per version.** `ScummVars` holds v3/v4 numbers; v5 (and v2) number the engine variables differently. A table chosen by `ScummProfile` replaces the constants in the base (`VerbScriptVariable`, timers, hooks, `CameraVariable` and so on are `protected const` aliases of `ScummVars`, one place to change). `BootScript = 1` is the boot script for these versions; the boot answer heuristic (`FindAwaitedInput`) assumes script 1 waits on a bit.
3. **LOOM assumptions still in the base**: `SingleClickGap`, `DoubleClickGap`, `InputMode` (above), `KeepVerbScreenOnRoomEntry` (set by LOOM's constructor), `SeparateBitStorage` (set by LOOM CD's constructor through the runtime), camera 160/320 geometry (v3 screen), `MaxNestedScripts = 8`.
4. **Still in `LoomExplorationSession` but generic in kind**: the CD intro/`ContinueCdBootAfterIntro` family (LOOM CD specific), `CompleteBoot` (uses opening script 044, LOOM), `VisitRoom` catalog (uses `LoomEgoArrivalIndex`). A second game needs its own `VisitRoom` (or the base's `EnterRoom` directly) and its own boot completion.
5. **Cutscene index, presenter, text extractor, speaker names** stay LOOM until S7 / the game's glossary exists.
