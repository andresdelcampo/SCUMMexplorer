# Scythe background preservation

User reported the missing scythe in the glassworkers' polishing scene. The bundled Spanish EGA index identifies that scene as **Room20**; it contains no Room21 resource. Source objects390/391 are the scythe interaction targets. No game data changed.

The decoded background already contains the scythe. The empty-object fallback guessed script099's later +0x02BB draw batch392..396, which repairs the wall/background over that artwork. Each overlay uses a proper subset of the background rectangle's palette colours; none adds a new colour. All five have index state0. The room entry supplies neither a positive entry state nor an entry/opening draw for them.

Generic `RoomObjectSelection` fallback now retains an intact, index-hidden background when an overlay only removes background colours and has no stronger entry/opening evidence. This is a conservative **artwork-estimation policy**, not an inferred source transition. Actual reached `DrawObject`, positive entry states, opening batches and gap filling keep their existing precedence. No room number or object ID is embedded in the production rule.

Two synthetic regression tests cover retention versus actual reached erasure, and an entry batch explicitly overriding the same erasing overlay. The real-data first-visit test now distinguishes the still-decoded later batch from the actual background to retain. Existing owl, blacksmith and close-up hood regressions pass.

Fresh full Core: **277/277**, both absolute data roots, unrelated untracked LoomObjectLookAuditTests excluded. Fresh RoomAudit: **76 rooms**, gaps3 before filling/0 after. Byte comparisons against the pre-change BMPs show **only Room20 changed**;75 renders match exactly. [Hashes](room-comparison.json). Only historical `.verification/room-audit/room-20.png` refreshed. Native actor rendering beside the retained scythe was inspected at frames0/1/10/50/200 and in the actual WPF window.

`ScytheProbe` reproduces native background, overlays and actor compositions. Full images/source traces remain ignored under `Exe/StatusRefinementsSession-20261001/scythe-before-2/` and `scythe-after-1/`; room packets under `room-before/` and `room-after-1/`. Early overly broad background retention produced three unrelated first-visit failures plus the obsolete scythe expectation. Narrowing the rule to colour-removal images restored those scenes; final277 pass. An initial Get-FileHash command was unavailable; the retained Python comparison performs actual76-file comparisons and asserts the single expected change.

Fresh source audit results are recorded in [the combined validation](validation.md). Replay executor, prerequisites, catalog metadata and source scripts are unchanged.

Reproduce from `D:\Documents\Personal\Code Projects\SCUMMexplorer` with required RTK:

```powershell
rtk proxy dotnet run --project .verification/RoomAudit/RoomAudit.csproj -c Release -- Games/loom Exe/StatusRefinementsSession-20261001/room-fresh 20
rtk proxy dotnet run --project .verification/archive/status-refinements-20261001/ScytheProbe/ScytheProbe.csproj -c Release -- Games/loom Exe/StatusRefinementsSession-20261001/scythe-fresh
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/status-refinements-20261001/run-source-audits.ps1 -Output Exe/StatusRefinementsSession-20261001/audits-fresh
```
