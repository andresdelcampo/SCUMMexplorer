# Room 09 speakers and a short pitch-gain check, 9 October 2026

The user reports that physical keyboard and mouse casting already works in EGA and VGA. They separately report that selecting VGA Room 09 spell responses leaves Hetchel and the Dead One absent. CD picture/voice/music matching is assigned to their full manual run and removed from active work.

## Reproduction and correction

`out-before/` reproduces missing target actors in all five VGA picker responses: Hetchel object 198 verbs 4D/4F/51/56 and Dead One object 200 verb 56. Ten presence/picture assertions fail while session retention and scene settlement pass. The earlier actor matrix covered staff, Rusty and close-ups, not this case.

EGA's direct starts already derived actor placement and dressing from the edition's own scripts. VGA's scene planner checked only whether a cutscene began, which can happen with its actors absent. `LoomSceneSetupPlanner.DeriveActors` now exposes the existing actor-only preparation through `IScummDataSource`, and CD picker/Interact starts use it. No staff/story preparation or bit writes are added by this path. Actors already in the room keep their existing look and position.

Source anchors are edition-specific:

- VGA Dead One: Room 09 local 217 +0052, costume 82 hex, feet (68,100), animation 6. English EGA uses its own +004A placement at (68,130).
- VGA Hetchel: Room 09 local 218 +0044, costume 34 hex, feet (230,132), animation 4. Alternative source placements remain listed in the disclosed estimates.
- Hetchel's 4F response can place/change her later through its called scripts. A later placement no longer excludes the named target from initial preparation. This also covers the fresh-state generic reaction branch.

These are the existing labelled source-derived placement estimates, not a reconstruction of every prior story scene or native actor bounds. Preparation is shared code with no Room 09 actor list, coordinates, costumes or story flags hardcoded in production.

## Qualified results

`out-qualified/` passes **55 WPF assertions**: real Room 09 selection, both targets present in the picker, ten independent picker/Interact responses with five checks each, and three pickup/pitch-award checks. Each response keeps session identity and chosen note/draft state, shows its target actor with nonzero costume pixels overlapping the real WPF actor bitmap, and ends outside a cutscene. Captures were visually inspected for Hetchel and the Dead One.

The focused Core suite passes **24 tests / 3 existing diagnostic skips**, including seven new EGA/VGA source-placement cases. It checks own-source costume/placement, visible pixels, unchanged variables/staff/note state, and retention of an already present actor. Release solution build succeeds with zero warnings/errors. All 339 relative links across the five changed/new guides resolve; whitespace check passes.

The EGA WPF comparison in `out-ega/` passes **four assertions**, including both Interact Use and the exact picker route from three to five pitches. Game Status is not edited during either pitch-gain route; source staff pickup is executed first.

## Short manual recipe

With the staff held and fewer than five pitches, select Room 13, then Play Cutscene:

- English EGA: **Room 13 - Reached Room 15** (the Use response, object 323 / verb 38 hex).
- VGA: **Room 13 - Use object 248**.

Let the scene finish, then open Game Status. Available pitches should be **five**. Both exact picker choices were checked in the real MainWindow from a fresh three-pitch session with source-picked-up staff; Script 54 reaches Script 29(5). The short recipe uses catalog arrivals and a direct ordinary Use response to test the source award and UI update, not a continuous original-game route. A single click on the Room 13 edge can run the distinct Walk To / Script 53 scene, so it is not the recipe above. EGA's `Use response` wording in the raw replay JSON is simplified to `Reached Room 15` in the normal UI.

Physical casting already has the user's EGA/VGA keyboard/mouse confirmation. This optional check concerns acquiring more available pitches, not repeating casting delivery checks.

## Preserved failed attempts

- Initial private-home restore had no cached packages and failed to resolve NuGet; the driver now uses the existing user cache with failed-source tolerance and audit disabled. WPF SDK access and testhost communication required normal sandbox escalation. No packages were added to the project.
- A nullable-boolean compile error in the new harness was corrected before reproduction.
- `out-after/` retains the missing 4F target caused by excluding later placements; `out-after-targets/` retains the failed ordinary-click candidate.
- `out-final/` retains the incomplete Interact pitch check: the deterministic actor harness had stopped the idle clock, so Script 54 had loaded Room 15 but had not finished. `out-final-clock/` restores the normal idle clock and completes it. This was a harness limit, not a pitch-award defect.
- `out-tests/` retains the sandbox testhost timeout. `out-tests-final/` retains a harness assertion comparing snapshots containing different array references. The corrected test compares retained fields and palette contents; `out-tests-passed/` is the passing result.

Full source copies, text, traces, private profiles, bitmaps and build outputs remain ignored under `out*/`. The original actor inventory is untouched and uncommitted. No native UI, running application, save, ROM, game data or normal user profile was changed; nothing was pushed. Existing CD cue/timer checks can detect stalls and source transitions, but cannot independently prove perceived synchronization. No new audio automation is requested.

## Reproduction

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`. All shell/child commands use RTK. The muted nonactivating WPF window uses private profile/cache/output directories; no OS input or native game is used.

```powershell
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/room09-speakers-20261009/run.ps1 -OutputName out-reproduction
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/room09-speakers-20261009/run.ps1 -GameFolder 'Games/loom ega mt32 en' -OutputName out-ega-reproduction
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter 'FullyQualifiedName~LoomDirectSpeakerTests|FullyQualifiedName~LoomSceneSetupPlannerTests|FullyQualifiedName~LoomSceneSetupSceneTests|FullyQualifiedName~LoomCdRunningSetupTests'
rtk proxy dotnet build SCUMMexplorer.slnx -c Release --no-restore
```

Harness artifact: `.verification/room09-speakers-20261009/out-build/bin/debug/LoomRoom09SpeakerCheck.exe`. Updated app artifact: `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`. Completed actor routes need no routine rerun without a new mismatch.
