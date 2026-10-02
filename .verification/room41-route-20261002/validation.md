# Room41 raw4 route investigation — 2 October 2026

Continues `55cb5ec`. **No route improved.** The unchanged `room41-raw4-click` recipe still ends in Room41 with 15 scene starts and 14 ends. This is a failure of this particular native-input recipe, not evidence that the capture scene cannot play in the app. The user clarifies that **Play Cutscene reaches the cage scene, but the cage appears open instead of the expected closed state**. That is a separate, user-reported visual/state issue; the exact picker entry and cause remain unverified. No production, catalog, observer or recipe change was made.

## Expected scene and the important route distinction

The user describes the sword spell provoking the blacksmith's complaint, a conversation, two gray figures surrounding Bobbin, and capture followed by the cage conversation in Room43, possibly via Room42. This is user-supplied game-behavior evidence, not a new WPF check. The loaded source supports the capture continuation:

* Room41 object633 response `0x41` starts local204 with argument1; response `0x4B` starts the same local204 with argument0.
* Local204 contains the complaint and conversation, and actor6 approaches Bobbin at `+02DF`. `+031E` ends its scope and `+031F` chains script090. This source reading does not independently identify or visually count the two gray figures.
* Script090 opens a scope at `+0000`, loads Room42 at `+0005`, then Room43 at `+00CF`. It subsequently uses Room60 at `+01F5` and returns to43 at `+0379`; `+0702` is a conditional final Room43 load. Its source end is `+0730`.

**The fixed raw4 recipe uses slot4 / Twisting / response0x41, not the slot9 Sharpening pair0x4A/0x4B.** These display associations are in `LoomStatusPresentation.cs`; the separate response arguments and shared continuation are in the loaded object/local source. Do not silently replace the recipe with a Sharpening route. Also, raw4 waits for local204's actual override and sends Escape: it does not play the full blacksmith conversation. The user's full-scene description and this recipe are distinct evidence.

The previously captured direct633/0x41 plus actual204 Escape control completes in Room43, 15/15, including script090 `+0730`. It is a direct exploration action and was **not rerun** here. The user used Play Cutscene, not native note input; the precise picker entry is not recorded. Neither scope balance nor reaching Room43 proves that the cage is visually correct. Do not equate the user's selected replay with this historical control without identifying its entry/recipe.

## Before and after

| Measurement | Before: latest phase-payload baseline | Fresh unchanged replay |
| --- | --- | --- |
| Final room | 41 | 41 |
| Scene scopes begun / ended | 15 / 14 | 15 / 14 |
| Total recipe frames | 7659 | 7659 |
| Final source090 settling budget | 3000 | 3000 |
| Script090 events | conditional cutscene0000, conditional loadRoom0005 | identical |
| Room42 entered / script090 end reached | no / no | no / no |
| Entire JSON packet | historical, immutable | byte-for-byte identical |

Fresh evidence: `Exe/Room41RouteSession-20261002/raw4-1/room41-raw4-click.json`. Historical comparison: `Exe/RuntimePhasePayloadFinalSession2-20261002/final-evidence/room41-1/room41-raw4-click.json`. [Checked values, selected events and SHA-256 hashes](route-summary.json). Equality covers every snapshot, event, estimate, action, trace, uncertainty, frame and room record in the packet; there are no new instruction/call/native streams or changed chronology IDs.

## Two bounded diagnostic cycles

### 1. The correct response is dispatched, but the room-load guard stops continuation

**Hypothesis:** the raw4 recipe reaches the intended sword response and script090; the failure is the conditional room-load guard rather than a missing target or an unfinished local204 wait.

**Focused check:** rebuild and run the existing Core-only `ProgressionProbe` with only the unchanged selected raw4 recipe. Compare its whole output against the saved baseline. Read the exact loaded source and the session/executor guard.

**Result:** confirmed. The final events are script090's conditional cutscene `+0000` and literal Room42 load `+0005`. `LoomExplorationSession.HandleEvent` rejects non-Reached room loads (the branch near line2000). `LoomBoundedScriptExecutor` treats that nonempty explanation as unresolved and exits, without assigning a resume offset (near line357). The final pending list has **no script090 continuation**, although frozen callers remain. Waiting longer cannot resume090 from this packet. The final `InCutscene=false` flag is not evidence of completion: the scope counts remain15/14.

This localizes an intentional evidence-policy stop. Simply accepting conditional loads, clearing inherited uncertainty, forcing an end, or inventing a resume is not an authorized execution correction.

### 2. The uncertainty predates the capture handoff

**Hypothesis:** a propagation defect at local204's chain into090 introduces the uncertainty, allowing a small handoff correction without changing earlier estimates.

**Focused check:** inspect the fresh packet's final-cast snapshots and executed event path, plus the existing same-setup direct control. No new instrumentation or second runtime sweep.

**Result:** rejected. Before casting, D6C/D67/D70, mode064, target067=633, count062=0, raw07A=4 and packed draft03A are already estimated. Response word03B=0x4241 is independently unestimated; that does not make its selected index, argument or path known. Script022 `+00C3`,026 `+007F`, object633/0x41 `+0000`, local204 `+031E/+031F`, and090 `+0000/+0005` all retain Conditional. The old direct control has Reached local204/090 events and completes. That contrast localizes the earlier input/draft path; it is not return/completion proof from equal arguments or structural records.

Earlier provenance evidence remains relevant: Room06 practice makes D6C0 estimated, native031 reads it, and020 reads the estimated D67 completion. The correct writer/wait pairing is frame3036, child412/parent408; the superseded previous-frame pairing is not revived. Final input, indexed matching, raw4 and movement dependencies remain independent. The preserved D671 early-stop counterexample and D6C terminal-projection limits prohibit a blanket reset.

**Decision:** no supported generic execution correction established. Stop the investigation at the two-cycle bound. The missing proof is earlier native-input/draft path validity under its retained numeric, movement and effect dependencies; another observer-expansion session is not the next task.

## Next bounded experiment

The user has identified **Play Cutscene**, so do not ask again whether the successful transition used native notes. Identify the exact selected entry/recipe and the point at which the cage appears open. In one bounded Core/source comparison, check the Room43 cage object's source-written state and chosen image at that point, distinguishing a wrong replay prerequisite/state from object selection/painting or a later scene state. First establish the source-defined closed appearance; do not guess a bit or force an image. A user-authorized visual check would be required to verify the visible fix. Keep that reported cage issue separate from the raw4 refusal; do not claim a shared cause yet.

For native raw4, the immediate failure is already reproduced exactly. Do not spend another cycle rediscovering the Room42 guard or expanding observers. A future correction must address an identified earlier input/draft dependency and demonstrate the same unchanged recipe before/after. Keep all historical records intact. No additional route experiment was run in this session after the diagnostic bound.

The game-behavior account establishes what to expect, but does not supply values for missing prerequisites or authorize clearing estimates. Any later implementation needs a specific generic execution defect or a supported proof for the relevant branch/effects, with all other barriers retained.

## Verification and retained limits

* Fresh Core harness rebuild and exactly one raw4 replay; whole packet byte equality and loaded-code SHA verified. No new working route or literal site. Coverage remains168/235=71.49%, 67 unvalidated sites,334 starts,50 outstanding owners,123 unresolved edges. The broader plan has no fixed denominator.
* All57 protected files retain their original hashes and remain unstaged. Tracked production/tests and prior verification packets remain unchanged. No game/scummvm writes, worktree operation, catalog promotion or push.
* Main Release, Core654/focused328,363 roots/fingerprints/memberships/sequences,Blockers0,76 unchanged zero-gap renders,13 primary packets and native/numeric regressions remain the **dated** phase-payload baseline. They were not rerun for this diagnostic/documentation task. Building the console probe is not a new final main-App build or WPF check.
* Every incoming-dispatch initialization/continuity, call phase-payload, native sequence-gap, origin, writer/alias/external/concurrent/cancellation/replacement/scheduler/callee barrier remains. No numeric/effect transport, estimate clearing or new original writer resumed-terminal chain. Primary366/55/0 and76 writer terminals, separate objects16/14/9/3/195/rejection1/fallback0,15 unknown message fields and separate camera55/220 remain historical evidence with all independent limits.
* Process handling: an existing App PID61360 was stopped under the handoff's stray-instance authorization. Another App instance then appeared. The user explicitly requested **leave it open**, superseding the no-running restriction for their instance. The agent did not launch or interact with the App. The first analysis check correctly failed on the newly running instance; the final check records the user's exception instead of asserting App0. No physical-input, visual or listening validation is claimed.
* An exploratory command looked for an instruction stream in the separate object capture; that capture contains calls/selections/correspondence, not that file. The failed read changed nothing and supplied no evidence. Existing packet events and source were sufficient; no new recorder was added.

## Reproduction

From `D:\Documents\Personal\Code Projects\SCUMMexplorer`:

```powershell
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/room41-route-20261002/run-route.ps1 -Output Exe/<fresh-folder>
```

The runner requires the App closed, a fresh folder, and the original recipe. It rebuilds the existing console harness, captures only raw4 and checks the packet/protection results. Do not close the user's current App without permission. For read-only reanalysis of this completed run while their explicitly authorized instance remains open:

```powershell
rtk proxy C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 .verification/room41-route-20261002/analyze.py Exe/Room41RouteSession-20261002 --allow-user-app
```

Session token-usage tooling is unavailable; account limits were not substituted.
