# ZakClassic fidelity: bounded classification of five previously capped starts

| | |
|---|---|
| Family / stage | ZakClassic / requested engine-fidelity pass, capped scenes |
| Fixture and edition | `Games/zak ega en`, supplied English v1; `TestGames.Root` = `D:/Juegos/Lucas` |
| Date and author | 2026-10-18, Codex capped-scene worker |
| Commits | Integration base `cff4fde`, containing the native actor-distance fix; family test/evidence commit follows |
| Registry change | Unchanged: Cutscenes stays Partial. Two completed previews and three classified bounds do not supply a hand-validated complete catalog. The palette item is owned separately. |

## What was done

Independently checked Classic's own Script 104 and 78 bytes, then added five Classic-only tests. Room 2 entry and Room 14 object 55 verb 11 use the production planner and complete after the shared native distance conversion. Room 11 entry and object 249 verb 1 advance only 1500 frames to verify the unchanged native ticket-owner guard; Room 23 object 410 verb 253 advances only 1500 frames to reach both terminal prompts without an automatic restart. Source observers copy selected executed instructions/variables and never supply state.

No Core/App edit, game-state shortcut, new full census or duplicate harness. Parent owns the common capped-scene diagnostic, Zak map/tracker, matrices and final integration. The existing Classic palette checkout remains the other worker's property; this item uses its own `zak-classic-capped-fidelity` worktree.

## Commands run (through RTK)

```powershell
rtk git worktree add .worktrees/zak-classic-capped-fidelity -b codex/zak-classic-capped-fidelity codex/maniaczak-fidelity
rtk proxy dotnet run --project .verification/zakclassic-cutscenes-20261018/CutsceneCheck -p:NuGetAudit=false -- "D:\Juegos\Lucas\zak ega en" global 104 78
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~ScummZakClassicFidelityTests|FullyQualifiedName~ScummZakClassicCutsceneTests|(FullyQualifiedName~SupportConformanceTests&DisplayName~ZakClassic)" --logger "console;verbosity=normal"
```

The creation command ran at the repository root. Source-only dump ran in the already existing `.worktrees/zak-classic-fidelity` checkout before the naming collision was noticed; no tracked file was changed there. Tests run at `D:/Documents/Personal/Code Projects/SCUMMexplorer/.worktrees/zak-classic-capped-fidelity`.

Parent's retained baseline/post-fix diagnostics are `.verification/zak-fidelity-20261018/classic-before.txt` / `classic-results.txt` in the integration worktree (generated text ignored). Their common record supplies the harness command; no 6000-frame census was repeated by this family item.

## Results (numbers)

- Five new bounded cases, four retained Classic cutscene cases and one family conformance row: **10 passed, zero failed/skipped**, 36 seconds. Restore/build completed without warnings/errors.
- **2/0/0 completes in 646 frames; 14/55/11 in 907** in this family's independent production-planner tests, matching the parent's pass: attributable beginnings, Room 16 reached, null failure.
- The two Room 11 cases retain owner 51 = **15** (room owner), with object 51 absent from held inventory. At 1500 frames they are still in Script 104's `+01C5/+01C9` loop. The loop waits **while** the object remains room-owned; the next instruction is reached only when owner differs from 15. The test does not acquire the ticket or invent flight/story context.
- Room 23 reaches `THE GAME IS OVER` / `Press ESCAPE to start again.` within 1500 frames, stays in Room 23/in cutscene, loops Script 78 `+0211 ->01D5`, and never executes `+0217 restart` without player input.
- Shared-code/LOOM regression results belong to the shared opcode record and parent integration; this item only adds tests/docs.

## Comparison with ScummVM (source files and date; runtime comparison if any)

The shared engine rule comes from official ScummVM `script_v5.cpp::walkActorToActor` / `script_v2.cpp`, read 18 October 2026: native v1/v2 approach distance converts to eight-pixel units. [Shared record](../maniacclassic-fidelity-20261018/README.md). This item's source conditions, offsets and messages were read afresh from the Classic fixture's own production V1 data source. The relevant offsets independently match Enhanced; no language/offset branch was added to engine code. No ScummVM execution or original-game timing/visual acceptance occurred.

## Differences and uncertainties kept

The first worktree creation requested the palette worker's already existing branch name and Git safely refused it. This item created a distinct worktree/branch, without moving/resetting/removing the existing checkout. Enhanced's earlier two failing guard assertions established that owner15 is the repeated wait, not the desired fallthrough; Classic starts with that corrected, source-verified condition.

Direct room starts are state previews, direct verb invocation is an exploration action, and completion is not an earned story route. The two Room 11 starts still lack genuine flight/story preparation; the terminal scene intentionally retains the player's restart decision. No owner/flag writes, catalog prerequisites, automatic Escape, teleport or room-specific patch were used. Existing palette/first-visit image, original timing/listening, walking metric and unavailable-edition limits remain. Game data, profile, ROM and saves were untouched; no junction or worktree cleanup, no full suite.

## User checks (exact steps and where; none if none)

Optional after integration: open the Classic fixture and let derived Room 2 Entry / Room 14 object 55 verb 11 previews finish. A fresh Room 11 preview remains context-dependent; Room 23 retains the terminal prompt for the player's decision. Original-game comparison remains prohibited/unperformed.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent owns [Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md) and [support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries). Suggested Classic rows (decoded + play/Explorer preview, own fixture, 2026-10-18): Script104 `+01C5 getObjectOwner var1,0033`, `+01C9 isNotEqual var1,000F ->01C4` waits while room-owner15; Script78 `+01D5/+01EF` terminal messages, `+0211 ->01D5` loop, `+0217` restart behind override. No proposed requirement is needed.
