# Manual room preparation and explicit native opening, 19 October 2026

Core-only delegated item, based on master `fb3da87b`. No App, requirement, game data, profile or save writes. ScummVM was not run.

## Contract

`ScummSession.PrepareManualRoom(room, frameLimit: 2400, quietFrames: 90, cancellationToken: token)` prepares an independent copy of the actual concrete session. `ScummManualRoomResult.Session` is non-null only on success (`Succeeded`); `Reason`, `Frames`, last `State`, and `Cancelled` describe unavailable results. The caller adopts a successful copy atomically. Failure or cancellation never publishes the partially prepared copy and never modifies the original.

The departed route's active scene owners, live ancestors and child slots are retired without playing their remaining story or taking an override jump. Old-room slots and a continuing boot route are retired too. Native cutscene UI cleanup is reused; no story prerequisites are assigned. The selected room runs its existing native entry and initialization offscreen. Manual same-room selection reruns entry instead of returning with an old scene alive. LOOM retains its concrete type and existing catalog staff policy. Native `VisitRoom`, doorway dispatch and explicit cutscene paths retain their previous contracts.

Entry roots are captured only during the initial room visit, not when unrelated ambient/input slots later resume. Slot ancestry then follows real script starts. A pending direct entry, or its descendant whose remaining decoded control flow can start a scene, keeps preparation busy. Literal called helpers are inspected; unknown/dynamic/cyclic scene routes conservatively remain unresolved. This is source analysis for readiness, not a second script interpreter. Cancellation is checked before and after copying, after entry, during scope cleanup and each settling frame.

`ScummGameSession.StartIntro(game, out indexedObjects)` constructs the ordinary session without the preplayed boot loop and runs the existing boot script to its first pause. Subsequent real input/frames drive the opening. It supplies no assumed answer, key, party or story flag. `BootForPlay` retains its previous behavior for planners and previews.

## Focused results

The final compatibility selection passed **30 tests, 0 failed, 0 skipped**, 29 seconds, with zero build warnings/errors. Selection: the new policy tests, existing four independent boot-snapshot copy cases, all `LoomBootTests`, the native Spanish enhanced Maniac play route, and all `ScummMonkeyFloppyCutsceneTests`. A final rerun after removing unused assertion branches covers the 12 new cases alone.

- Synthetic unskippable departed scene: pending child and old-room continuation cannot load another room or run the old story tail after adoption; an unrelated ambient loop still advances. Original session stays in its old scene.
- Synthetic delayed new-entry child: its scene begins only after 300 frames, finishes offscreen, and does not reappear during 400 further frames after publication.
- Frame exhaustion, pre-cancellation and entry changing to a different room return no live session.
- Actual enhanced Spanish Maniac party is selected through the existing fingerprint-reviewed native click helper. An explicitly activated doorbell object verb starts a scene; manual Room 2 preparation cancels it, preserves all chosen actors and remains in Room 2 without another scene through 600 further frames. The object-verb activation is a disclosed test trigger, not a claimed player route.
- Uncommitted native Maniac party is refused without inventing a selection.
- Actual Maniac enhanced, Zak enhanced and Monkey floppy explicit opening factories start with `BootRunning`, zero preplayed boot frames and no delivered/assumed input.
- Actual Monkey CD and floppy Room 51 preparations are **unavailable at 2400 frames**, not successful live rooms. Their persistent entry-created conditional watchers retain possible future scene routes. CD diagnostic slots included locals 201/205/207. Actual LOOM EGA Room 2 likewise remains unavailable (globals 45/42); the original LOOM session is unchanged. The separate bounded Room 4 attempt also did not settle and was not pursued. App integration must retain a static preview without a live timer on these failures. There is no claim that every manually selected room remains playable.

Initial expected-success assertions exposed those real limitations and were retained as bounded-unavailable regressions after diagnosis. A first overly broad root-capture implementation also adopted resumed ambient slots; it was corrected before freeze. Neither failure justified changing story flags, deleting conditional watchers, raising bounds or claiming scene-free native behavior.

Command from the isolated worktree (all shell commands use RTK):

```powershell
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter "FullyQualifiedName~ScummManualRoomPolicyTests|FullyQualifiedName~CopiesOfOneSnapshotStayIndependent|FullyQualifiedName~LoomBootTests|FullyQualifiedName~ScummManiacEnhancedPlayTests|FullyQualifiedName~ScummMonkeyFloppyCutsceneTests" --logger "trx;LogFileName=manual-final.trx" --results-directory .verification/manual-room-policy-20261019/test-results --verbosity minimal
```

Local TRX files are ignored. Parent owns combined App/window verification, full suite and master integration. This record does not establish original-runtime acceptance or promote any family support stage.
