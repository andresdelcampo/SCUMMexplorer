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

## Moves

(filled in as the commits land)
