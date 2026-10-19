# ZakClassic display-only costume colour contexts

| | |
|---|---|
| Family / stage | ZakClassic / S2 context follow-up |
| Fixture | `Games/zak ega en`, DOS v1 English, external root `D:/Juegos/Lucas` |
| Date / baseline | 19 October 2026 / `5634143c` |
| Registry | Unchanged: 35 costumes, 24 derived and 11 labelled global estimates. No original-runtime comparison. |

## What was done

`ScummV1ActorColors.ContextsOf` adds display-only colour alternatives with source provenance and separate `SourcePath`, `Opening` and `RoomPreview` bases. It never changes `ColorOf`, an actor, the running Play session or game state. Classic Maniac's accepted fixed table path returns no new contexts and runs no independent session.

The source reader accepts a local pair only for consecutive identical actor operands inside one decoded basic block. Calls, yields, numeric writes, different or potentially aliasing actor selectors, unresolved values, branch targets and missing code break that proof. A conditional source pair is a possible path, never execution evidence.

Native contexts observe the existing generic boot and independent room previews. They reconstruct only copied costume/palette instructions, including the native v1 indirect operand, and validate the final reconstruction against the native actors. They do not interpret script control flow, assume a child completed, alter a timer, issue player input, activate object verbs or patch prerequisites. Candidate rooms come from each copy's own costume-setting instructions; variable costume operands retain all candidate room entries and use the existing native resolver. No room/actor/costume/source-offset production lookup table is introduced. Visibility and original-game appearance remain unproved even for a native opening operation. Room preview contexts always remain estimates with an explicit unearned-route description.

The source filter checks cancellation before every executed instruction without capturing every instruction's numeric state. Cancellation also checks before boot, after boot, between rooms and after settling. Native boot and existing 2,400-frame/90-quiet-frame first-visit bounds remain unchanged. Completed boot/room results are cached, and canceled work is never published. The App must call this asynchronously, reject stale selections and retain the existing default preview.

## Commands and numbers

All commands run through RTK in `.worktrees/zakclassic-palette-core-20261019`:

```powershell
rtk proxy dotnet build src/SCUMMexplorer.Core/SCUMMexplorer.Core.csproj -p:NuGetAudit=false --verbosity minimal
rtk proxy dotnet run --project .verification/zakclassic-palette-core-20261019/ZakPaletteContextsProbe.csproj -p:NuGetAudit=false -- "D:\Juegos\Lucas\zak ega en"
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~ScummV1PaletteContextTests" --logger "trx;LogFileName=palette-context-final.trx" --results-directory .verification/zakclassic-palette-core-20261019/test-results --verbosity minimal
```

Core build: zero warnings/errors. Cold source reading 176 ms; first request spent 34.3 seconds deriving the opening and shared variable-costume room previews. Additional cold room groups cost 5.6 seconds, 4.3 seconds and 30.7 seconds; all warm requests cost 9–13 ms. These are local observed costs under parallel work, not performance promises. Asynchronous loading and cancellation are essential.

The eleven requested estimate cards expose 11 context records covering seven distinct costume/colour pairs on seven cards. Four cards remain without a new proved context. Duplicate records preserve independent source provenance rather than claiming extra colour choices. The App can coalesce equal skin/suit pairs while retaining their evidence. Global counts remain 24 derived / 11 estimates.

Validation packets overlap and must not be added: **29 passed, zero failed/skipped**, 1m13s, covers the original 18 context cases (including the 58-second own-copy transformation census), seven legacy colour cases, three palette-persistence cases and one generic LOOM EGA boot safeguard (`palette-context-final.trx`). Independent review then added 15 native indirect-operand cases, for 33 context cases in total. The reviewed subset plus required LOOM EGA/CD regression and ZakClassic conformance passes **49 tests, zero failed/skipped**, 1m23s (`palette-context-reviewed.trx`): 32 fast context cases, 15 LOOM CD cases, one generic LOOM EGA case and one ZakClassic conformance case. After the final capability property, the frozen 32 fast context cases pass again, zero failed/skipped, 880ms (`palette-context-frozen-fast.trx`). Parent runs the full suite over the final integrated source, including the slow own-copy case. No meaningful runtime behavior changed between the own-copy proof and the final freeze.

Facts and exact transformation/Mars source mappings belong in the parent-maintained [Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md), not a second decoded reference table here.

## Source comparison and limits

Official ScummVM master sources read 19 October 2026, never executed: [actor.cpp `initActor` / `setActorCostume`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/actor.cpp), [script_v2.cpp `o2_actorOps`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp), [costume.cpp `byleRLEDecode_C64`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/costume.cpp). The engine starts the palette at zero, a v1 costume change retains it, the palette operation selects the actor's slot, and DOS Zak uses skin colour 8. This helper derives metadata from that existing behavior and source observations; it does not port a new interpreter.

The first no-restore build failed because the new worktree had no assets file; an ordinary restored build succeeded. The initial focused test expected both 0 and 7 for opening costume 32, but the independent census disproved 7: actor 10 already wears costume 31 by the later palette write. The test is corrected to reject that invented context, retaining the failed 17/18 result in `palette-context-initial.trx`. Review found that the observation helper initially dereferenced only slot 14; this was corrected before commit to all native slots 14–16, with overwrite, actor-selection, missing selected-cell and independent estimate tests. Final-state equality is an additional consistency check, never a generic proof of every transient effect. A temporary PowerShell command for summarizing the TRX had escaping errors and was corrected; test results were unaffected. No global estimate is promoted or silently replaced by a sampled alternative. Native first-visit previews for late rooms do not establish the game's prerequisites; a naturally reached script can still execute under an estimated preview path. Mars/prior-selector blockers and explicit object-verb evidence from the parallel trace agents remain separate from this production provider.

No ScummVM execution, game/ROM/save/profile change, Games junction, worktree removal, original-runtime picture comparison or subjective listening/timing acceptance occurred. Parent owns the actual-window acceptance, shared notes, matrix regeneration, full suite and promotion.
