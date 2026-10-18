# ZakEnhanced fidelity: distinguish fixed walks, missing flight context and terminal prompt

| | |
|---|---|
| Family / stage | ZakEnhanced / requested engine-fidelity pass |
| Fixture and edition | `Games/zakE ega en`, supplied English v2; `TestGames.Root` = `D:/Juegos/Lucas` |
| Date and author | 2026-10-18, Codex family worker |
| Commits | Based on integration `f900d81`, containing shared native-distance fix; family test/evidence commit follows |
| Registry change | Unchanged: Cutscenes remains Partial. Two corrected starts and a bounded classification do not establish an authored, hand-validated catalog or a full playthrough. |

## What was done

Added five edition-owned regressions covering the five previously capped S7 starts, without another full census. Production planner runs Room 2 entry and Room 14 object 55 verb 11; both now finish after the shared source-backed eightfold native actor-distance conversion. No Core/App change is made by this item.

Two direct previews exercise Room 11 entry and Room 11 object 249 verb 1 for 1500 frames each. A non-mutating observer checks the reached Script 104 ticket-owner loop. A final preview invokes Room 23 object 410 verb 253 for 1500 frames, verifying both terminal prompts and the native loop without Escape or restart. These are disclosed source previews/direct exploration actions, not earned flight/story routes.

Parent owns the common bounded diagnostic harness, Zak tracker/script map, matrix generation and final integration. No duplicate family harness or 6000-frame census was introduced.

## Commands run (through RTK)

Worktree: `D:/Documents/Personal/Code Projects/SCUMMexplorer/.worktrees/zak-enhanced-fidelity`.

```powershell
rtk git worktree add .worktrees/zak-enhanced-fidelity -b codex/zak-enhanced-fidelity codex/maniaczak-fidelity
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~ScummZakEnhancedFidelityTests|FullyQualifiedName~ScummZakEnhancedCutsceneTests|(FullyQualifiedName~SupportConformanceTests&DisplayName~ZakEnhanced)" --logger "console;verbosity=normal"
```

The worktree command ran at the repository root. Parent's prior one-pass diagnostics are retained under `.verification/zak-fidelity-20261018/` in the integration worktree (`enhanced-before.txt`, `enhanced-after.txt`, `enhanced-results.txt`, `source-enhanced.txt`; generated text is ignored). Parent's common record describes those commands and results. This item reuses their baseline rather than repeating the census.

## Results (numbers)

- Five new bounded cases, four retained Enhanced cutscene cases and one family conformance row: **10 passed, zero failed/skipped**, 43 seconds. Restore/build completed without warnings/errors.
- Corrected starts: **2/0/0 finishes in 646 frames; 14/55/11 in 907**, both with an attributable beginning, Room 16 reached and no planner failure. The earlier native-distance error caused actor overshoot and retained waiting at Script 126/96; parent owns that engine diagnosis.
- The two Room 11 previews remain in a cutscene at the native Script 104 `+01C5` owner loop after 1500 frames; repeated `+01C9` reads see owner 51 = **15**, unchanged from boot, and object 51 remains absent from native held inventory. No object is supplied or moved to release this condition.
- Room 23 reaches `THE GAME IS OVER` and `Press ESCAPE to start again.` within 1500 frames, remains in Room 23/in cutscene, and executes Script 78 `+0211` back to `+01D5`; it does not execute the restart instruction `+0217`.
- No shared source was changed by this family item, so shared LOOM regressions are covered by the parent/shared-opcode validation rather than repeated here.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Official `engines/scumm/script_v5.cpp::walkActorToActor` and `script_v2.cpp` were read by the shared opcode owner on 18 October 2026; v1/v2 distance operands use eight-pixel native units. Engine facts and regression details are in [the shared record](../maniacclassic-fidelity-20261018/README.md). This item's conditions, messages and offsets are decoded from the Enhanced fixture's own scripts, then observed in the Explorer session. No ScummVM executable was run; no original-game timing or visual comparison is implied.

## Differences and uncertainties kept

The initial test source assumed the inventory snapshot contained item records; it actually contains ids, corrected before test execution. In the first five-case run, the two guard cases incorrectly expected owner 51 to differ from 15; **three passed, two failed**. Actual copied query values establish owner 15 before/after. The corrected reading is essential: decoded `isNotEqual` names the fallthrough condition, and its jump repeats **while owner equals room-owner 15**, until the object is actually held/non-room-owned. The Explorer does not acquire it as a shortcut. Both failures are retained here; this corrected classification supersedes the parent's initial tentative wording.

Two completed scenes do not make every cutscene playable. Room 11 needs genuine flight/story context with the required object acquired; terminal Room 23 deliberately waits for player restart input. No invented prerequisites, automatic Escape, owner/flag write, teleport, stored save or family/room exception was introduced. Earlier original-picture, timing/listening, first-visit image, palette and unavailable-edition limits remain. Game/profile/ROM/save data were untouched; no junction/worktree cleanup and no broad full suite was run by this worker.

## User checks (exact steps and where; none if none)

Optional Explorer inspection after parent integration: open the supplied Enhanced copy; use the derived Room 2 Entry or Room 14 object 55 verb 11 preview and let it finish. Room 11 remains context-dependent from a fresh preview. Room 23's terminal prompt remains for the player's decision. Original-game comparisons remain unperformed under the standing prohibition.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent integrates these edition-owned rows in [Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md) and [tracker](../../docs/zak/SUPPORT_STATUS.md): Script 104 `+01C5 getObjectOwner var1,0033` / `+01C9 isNotEqual var1,000F ->01C4` repeats while owner 15; Script 78 `+01D5/+01EF` prints terminal prompts, `+0211 ->01D5` loops, `+0217` is restart after override. Evidence: decoded + play (Explorer preview); own Enhanced fixture, 18 October 2026. No new requirement is proposed.
