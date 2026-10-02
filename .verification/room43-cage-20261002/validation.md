# Room43 cage appearance — 2 October 2026

Continues `627b682`. **No native route improved.** A source-backed rendering defect is corrected: both existing sword capture previews now retain the closed cage background. The unchanged raw4 route remains the dated Room41,15/14,7659-frame failure. Nothing in this change relaxes its conditional Room42 refusal.

## Exact entries and the latest user clarification

The user first described Play Cutscene, then clarified that the latest attempt used the spell, and finally stated: **Play Cutscene works (18 and 9); manually applying18 does not reach Room42.** Preserve those as user reports, not fresh WPF verification. Response18 is `Room 41 object 633 Verb 0x4B`, previously titled `Draft response 18 at espada`, now `Sharpening (reverse) at espada`. Response9 is `0x42`, now `Twisting (reverse) at espada`; its existing catalog record is a short Room41 reaction, not the capture. Do not silently identify9 with8: the other catalog capture is response8/`0x41`, now `Twisting at espada`.

The bounded comparison uses the exact two capture recipes from the existing catalog: fresh post-boot seed0 session, catalog Room41 visit, direct633 response41 or4B, then the ordinary frame clock, bounded to3000 frames and32 settled frames. These recipes establish exploration previews, not native casting prerequisites. They are distinct from raw4 (slot4/41 plus actual204 Escape), from the historical longer direct control, and from the user's manual18 attempt. Exact native18 starting state and inputs remain uncaptured.

## One diagnostic cycle and implementation decision

**Hypothesis:** the open cage is caused either by the replay's source state or by the artwork fallback overriding that state. Existing completion reports lack per-frame object selection, so `CageProbe` adds only the necessary read-only snapshots and background/object compositions. It references Core only; no App process, UI, actor rendering, input injection, source-state mutation, or observer framework change.

**Finding:** both captures reproduce the defect. At first Room43 arrival (frame584 for41,578 for4B), reached Room43 entry `+0098` sets object645 (`0x0285`) to0. Entry `+00A8` also sets644 to0. These source-written states remain unestimated. The raw composition has no active object overlays and displays the closed cage already in the background. `ShowUnresolvedArtwork` instead guesses645 from **unexecuted script091 `+009C`**, painting the open door. The same state and error occur after returning from Room60 and through the final conversation. All42 recorded Room43 state/message changes per recipe have the same image selection defect.

**Correction:** the generic fallback rejects later-reaction/stored-image guesses that overlap an intact background region whose hidden state was selected by reached source. It checks both batch selection and secondary batch steps, including larger overlapping images. It retains the existing entry/opening-priority estimates, unconditional entry-reset fallback policy, gap repair, interface exclusions, parent checks and actual later `DrawObject` behavior. This is a limited artwork-estimate correction, not a claim that every first-visit estimate is execution-proven. No object/room IDs or closed image are hardcoded in production.

The initial narrower correction prevented645 but selected larger646 from script096 `+0002`, still showing the door open. `capture-fixed` preserves that unsuccessful intermediate result. The final correction covers overlap. Broad tests also exposed existing Room02 opening and Room86 unconditional-reset artwork policies; those remain intact. Failed development logs are retained, not replaced or presented as passes. The old Room43 test explicitly expected the wrong later091 overlay; it is replaced with a source-closed-background assertion. An early aggregate attempt failed because the still-running Blockers scan had not written its report; the completed scan and subsequent successful aggregation supply the final results.

## Before and after

| Exact capture recipe | Frames | Final room / scopes | Source state645 | Before painting | Final painting |
| --- | --- | --- | --- | --- | --- |
|633/41, response8|1729|43 /3 begun,3 ended|0, source-written|645, guessed from091|background, closed cage|
|633/4B, response18|1723|43 /3 begun,3 ended|0, source-written|645, guessed from091|background, closed cage|

Before: `Exe/Room43CageSession-20261002/capture-1/`. Final: `capture-final/`. Exact comparisons cover recipe, room changes, frames, boundaries, filtered source events, final full snapshot, and all42 per-recipe recorded states/estimates/source-written sets/messages/raw paint orders. Only display selection/its artwork estimates change. Final shown BMPs equal the raw source composition byte-for-byte. No native completion or literal-site promotion follows; coverage remains168/235=71.49%,67 unvalidated sites,334 starts,50 outstanding owners,123 unresolved edges.

## Verification and limits

* Final Core657 passes (654 prior plus three synthetic cases; the Room43 regression replaces one old case). Focused rendering48 passes. The unrelated protected `LoomObjectLookAuditTests` is excluded as before.
* Fresh363 literal-root results/fingerprints, four established memberships and both sequences equal the phase-payload baseline as whole JSON values; Blockers report is byte-exact,0 blocked sites.
* All76 room renders have zero gaps. Only08 and43 differ; other74 BMPs are byte-exact to `Exe/RuntimePhasePayloadFinalSession2-20261002/room-audit-2/`. Room08 is the same defect: reached entry `+0073` hides259, while the old fallback paints it from later script046 `+0DD0`. The new render retains the intact stained-glass background. This source-backed consequence is recorded separately, not disguised as an unchanged baseline.
* Normal main `dotnet build SCUMMexplorer.slnx -c Release` succeeds with zero warnings/errors. An earlier isolated App Release also passed at `Exe/Room43CageSession-20261002/app-release/`. The user App PID34056 was initially present and was never stopped, launched or interacted with by the agent. It disappeared independently before the normal build; a read-only no-App guard then allowed that build. A new user App PID64792 was present at final verification and is left untouched. No new WPF, physical-input, listening or visible-app claim; no push.
* All57 protected files remain hash-exact/unstaged; raw4 recipe and the prior route-summary's historical evidence files retain their hashes. Game files, generated catalog, earlier verification packets and executor/observer code are unchanged. Full prior numeric/native/camera/13-packet chronology suites were not rerun: they remain dated evidence. Fresh capture comparisons and root regressions do not replace those records.
* All initialization/continuity, phase-payload, sequence-gap, origin, writer/alias/external/concurrent/cancellation/replacement/scheduler/callee bounds and barriers remain. No estimate clearing, forced state, input prerequisite, stop/wait bypass, argument-equality proof, or room exception.

## Reproduction

From `D:\Documents\Personal\Code Projects\SCUMMexplorer`, use a fresh output directory:

```powershell
rtk proxy dotnet run --project .verification/room43-cage-20261002/CageProbe/CageProbe.csproj -c Release -- Games/loom Exe/<fresh-cage-output>
```

The probe never needs to close or launch the App. This session's final read-only comparisons and protection checks:

```powershell
rtk proxy C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 .verification/room43-cage-20261002/analyze.py
```

See [summary.json](summary.json) and [spell naming validation](names-validation.md). Next bounded native experiment: establish the user's exact manual Sharpening-reverse/slot9 setup in a **separate** recipe before comparing its response dispatch and090 continuation. The user emphasizes that the correct conversation already triggers: this is evidence for correct response dispatch, not evidence of a gameplay rule preventing departure. In traced raw4, the game script actually requests42 and the explorer's uncertainty guard refuses it. The missing fact is why the valid-looking manual path retains uncertainty; manual18's exact cause is not yet captured. Play Cutscene resets to a fresh session and invokes the same response code directly; it does not prove success with arbitrary current game states. Keep raw4 unchanged and do not re-run its already bounded Room42-guard investigation or expand general observers. A visible cage check remains pending user authorization. Session token usage is unavailable; account limits were not substituted.
