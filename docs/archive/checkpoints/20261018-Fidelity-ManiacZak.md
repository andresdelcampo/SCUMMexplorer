# Maniac/Zak engine fidelity checkpoint — 18 October 2026

## Plan

Explicit user scope: Script 174, darkness/flashlight rendering, ZakClassic palette estimates and the five capped Zak cutscene starts in each edition. All four supplied family fixtures are available. Retain existing support levels until evidence justifies a change; Full Cutscenes still requires a hand-validated catalog. No catalog, shipped seed, story-state workaround or wider edition claim.

One isolated worktree per item. ManiacClassic worker owns Script 174 and all v1/v2 opcode changes in ScummBoundedScriptExecutor.V2.cs. ManiacEnhanced worker owns shared lighting/rendering implementation, checking each family against its own data; requests opcode/runtime hooks from the opcode owner. ZakClassic worker owns ScummV1ActorColors and palette provenance only. Parent owns capped Zak scene diagnosis in the integration worktree, requests shared fixes from their owner, and owns App integration, generated matrices, checkpoint and dedicated handoff. Family documentation ownership is coordinated before editing shared trackers/maps.

Bound each item to reproducing the recorded failure, source-grounded implementation or classification, meaningful regression and one targeted harness. Investigate all five capped starts per Zak edition, without increasing a cap to manufacture success or assigning prerequisites. Palette analysis must preserve context-dependent alternatives and label unresolved values. Lighting must follow engine state and native source behavior, without room exceptions or pixel fills. Two distinct failed approaches to a blocker are recorded and the item stops. No overall user budget was supplied.

Read the complete game-support instructions, CLAUDE, current work and latest checkpoints. All shell calls use RTK. Data stays under D:/Juegos/Lucas; never execute ScummVM, mutate game/profile/ROM/save data, create Games junctions, stash, force or remove worktrees. Only the four Maniac/Zak families are in scope. Do not edit NEXT_SESSION_PROMPT.md.

Merge master in the integration worktree, keep both orchestrators' changes, regenerate README/REQUIREMENTS with SCUMM_UPDATE_SUPPORT_MATRIX=1, build, full suite with external sound/Indy3/MT-32 settings (zero failed), then fast-forward master. Repeat if master moves. Stop at a clean checkpoint.

## Done

| Item | Result | Support level |
|---|---|---|
| ManiacClassic Script 174 | Native closest query and edge/tie/unknown handling. Genuine boot/party/key/Push-doorbell route continues 3000 frames through 484 queries, Ed's outdoor response and return; Dave retains key/control. | Unchanged, Cutscenes Partial |
| Native v1/v2 darkness/flashlight | Room light and actor-colour modes, default 6x4 strips, native pointer/camera/edge/corner geometry. Preview, thumbnail and Play share mapping order. Four real-window exact bitmap checks and both Zak moving beams pass; eight captures inspected. | Existing bounded Objects/Play Full retained |
| ZakClassic palettes | Corrected v1 costume changes resetting dynamic actor palettes. Native contexts and variable-writer/alternate-actor provenance are verified; early boss palette 0 precedes later 7. | Costumes remains Partial, 24 derived / 11 estimates |
| Capped Zak starts, independently per edition | Room 2 entry and Room 14 response now finish in 646/907 frames. Room 11 object-action actor approach fixed; both flight previews still wait while ticket object 51 is room-owned. Room 23 deliberately loops at the Escape/restart prompt. | Cutscenes remains Partial |

Shared native-distance correction removes unaligned actor destinations: a four-unit v1/v2 approach had been interpreted as four pixels, so eight-pixel steps never arrived. All values, including FF, now use the native conversion; later-engine width behavior stays separate. This is a version rule, not a room patch. Native v1 palette persistence similarly changes only that version; enhanced/generic reset behavior is pinned.

Source fixes: `6194eea` opcode/query/distance, `66e530a` v1 palette retention, `6024094` lighting, `6b2b1d7` App previews. Family evidence: `f900d81` Classic Maniac, `5e0ae9d` / `72f87e4` Classic Zak palettes, `cff4fde` Enhanced Zak scenes, `3592307` Classic Zak scenes, `9150bab` four-family window checks; production diagnostic `2130bc5`.

Validation against master `9430cb5`: merge already up to date, solution build **zero warnings/errors**, generated support matrices **three passed**. Independent read-only review of native units, palettes, lighting/order and App mapping found no actionable issue. Integrated focused run: **65 passed, zero failed/skipped**, including all four native Play routes and LOOM EGA/CD safeguards. Final full suite: **1286 passed, zero failed, five skipped** (1291 total), **7m37s**, with the required external sound, Indy3 EGA and MT-32 settings. Local TRX: `.worktrees/maniaczak-fidelity/.verification/zak-fidelity-20261018/test-results/maniaczak-fidelity-full.trx` (18 October 15:14:14–15:21:52 CEST). No feature level is raised without its missing evidence: matrices retain the four Partial Cutscenes cells and ZakClassic Partial Costumes. Integration is fast-forward only; worktrees are retained.

Records: [Classic Maniac](../../../.verification/maniacclassic-fidelity-20261018/README.md), [lighting and actual windows](../../../.verification/maniacenhanced-fidelity-20261018/README.md), [Classic Zak palettes](../../../.verification/zakclassic-fidelity-20261018/README.md), [capped starts before/after](../../../.verification/zak-fidelity-20261018/README.md), [Classic Zak scenes](../../../.verification/zakclassic-fidelity-scenes-20261018/README.md), [Enhanced Zak scenes](../../../.verification/zakenhanced-fidelity-20261018/README.md).

## Not done and why

Original-game runtime/picture/timing comparison remains outside the authorized run. Additional same-family releases are absent. Full-game completion and hand-validated catalogs are not claimed. The native walking search metric, prior S5 image/camera findings and sound listening/synchronization remain open. Zak flight preview preparation is absent; a terminal ending is not falsely declared complete.

Maniac battery insertion remains unresolved after two distinct attempts. The source-correct radio pickup/Open acquires the batteries; genuine Use → batteries → flashlight → sentence inputs are accepted but insertion does not complete in the bounded enhanced run, which later reaches the native capture continuation. Whether input handling or reached story/action context blocks composition is not established. Both editions' acquisition/use source was read; no successful battery-powered beam is claimed for either Maniac copy. The failed diagnostic `c726c26` is retained separately from passing darkness/geometry/Zak-beam acceptance. No battery/flag is fabricated. No authored catalog, seed, game/profile/ROM/save mutation, junction or worktree removal.

## Decisions made

Parallel source investigation and family verification; exclusive ownership of each shared engine file. Source-derived fixes and retained uncertainties take precedence over detector counts or scene completion counts. Parent adopted the palette worker's demonstrated v1 reset defect and corrected it in its announced actor-runtime ownership. Actual contexts do not silently replace global card estimates. Three remaining Zak caps are source waits; neither ticket ownership nor restart input is supplied as a shortcut. Rendering never changes object/actor/story state.

Failed evidence is retained in the records: decimal-vs-hex/query setup mistakes; native Push key assumption; pending-script observation seam; real palette mismatch plus initial snapshot-index test mistake; pointer input changes its native variables and cannot be included in rendering-only equality; file-lock build and interrupted stale diagnostics; reversed initial ticket-branch interpretation, corrected against observed owner 15 and source fallthrough semantics. No failed result is used as acceptance.

## Proposed requirements to confirm or reject

None new; existing S6/S7/S8 proposals remain binding and retain their Proposed status.

## User checks

Optional new visual check: open either Maniac copy and Room 5; native darkness replaces the old lit preview, with gray actors where the source places them. In Classic Maniac, choose Syd/Bernard/Start, take the front-door key, select Push and ring the doorbell; let Ed finish outside and return. In Zak, let the derived Room 2 entry or Room 14 object 55 response finish without Escape. Flight previews retain their source-state requirement and Room 23 retains its terminal restart decision. The built app is `.worktrees/maniaczak-fidelity/Exe/Debug/SCUMMexplorer.exe`. These optional checks do not repeat original-game timing/listening requests or establish a complete playthrough.

## Offers

No authored catalog or seed work.

## Cost

Three workers plus parent, isolated item worktrees. Independent source and family proofs ran in parallel; shared files had one owner each. The opcode worker then handled Enhanced and Classic capped-scene regressions sequentially in separate worktrees, while lighting completed actual-window checks and palettes supplied independent read-only review. No user-specified budget. One targeted ten-start diagnostic and compact production rerun; no full audit driver. Full-suite integration is run after final code, repeated only for a real correction or moved master.

## Next run

Suggested bounded request: "Investigate native inventory Use-with composition for ManiacEnhanced, reproduce the battery/flashlight failure from the fidelity record, distinguish input handling from reached action/story context, verify the corresponding Classic route where applicable, retain the coordination rules and stop at a clean checkpoint." Alternatively choose genuine Zak flight preparation or the native walking metric as a separate bounded pass. No automatic continuation or catalog/seed work.
