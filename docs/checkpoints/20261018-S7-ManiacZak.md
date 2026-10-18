# Maniac/Zak S7 checkpoint — 18 October 2026

## Plan

Scope is S7 only for ManiacClassic, ManiacEnhanced, ZakClassic and ZakEnhanced. S6 is complete on all four checked fixtures. Use the shared script-derived scan/planner and verify at least three source-established scene triggers per family from fresh starts, then check the picker and room playback in the actual window. The user has not named particular scenes; choose examples from each scan and disclose that choice. No handwritten catalog or shipped seed. Source comparison remains distinct from original-game observation; ScummVM execution remains prohibited.

Three family workers run concurrently in separate worktrees. ManiacEnhanced owns shared Core cutscene prerequisites; ManiacClassic owns its family tests/evidence and Maniac documentation, coordinating enhanced facts. ZakEnhanced owns its family evidence and Zak documentation, then handles ZakClassic in a separate worktree when its first item is committed. Parent owns App integration, requirements proposals, registry/matrices, final verification and handoff. Shared edits are small and additive, with v1/v2 opcode changes confined to their partial. Engine work is announced in CURRENT_WORK before implementation.

Per item: one complete scan/run census, three representative fresh-start proofs and one window check; stop a blocker after two distinct failed approaches. No user time/token budget was supplied. Keep unresolved S5/S6 visual, walking and timing limitations. Do not extend S8/S9 or touch other orchestrator families. No Games junctions or worktree removal.

Integrate current master inside this worktree, preserving both orchestrators; regenerate README/REQUIREMENTS with SCUMM_UPDATE_SUPPORT_MATRIX=1; build and run the full suite with the three external fixture environment variables, expecting zero failures; fast-forward master only. Repeat if master moves.

## Done

All four family items now have derived lists, complete run censuses, three representative source-attributed scene proofs, and passing actual-window room/picker checks. Cutscenes is Partial for all four; README and REQUIREMENTS are regenerated (three matrix tests pass). Other columns remain unchanged.

| Family | Triggers / groups / static scenes | Fresh starts begun / not begun | Frame bounds | Representative completed proofs |
|---|---|---|---|---|
| ManiacClassic | 59 / 27 / 25 | 6 / 21 | 0 | Doorbell 187 frames; circuit breaker 905; TV 366 |
| ManiacEnhanced | 59 / 27 / 25 | 7 / 20 | 0 | Doorbell 187; circuit breaker 888; TV 390 |
| ZakClassic | 130 / 54 / 37 | 13 / 41 | 5 | Room 8 entry 911; doorbell 142; tool-kit Buy 256 |
| ZakEnhanced | 130 / 54 / 37 | 13 / 41 | 5 | Same independently checked proofs and frame counts |

All four window censuses agree with headless results. Maniac's room-list doorbell and global-picker TV display their own source messages and artwork; Zak's room-list secret-room intertitle and global-picker baker response display the expected lines. Captures were inspected. Background derivation in paired window runs: 12.7s/17.5s for Maniac classic/enhanced, 132.9s/135.9s for Zak. These are integration timings, not original-game timing comparisons.

Shared corrections accept old script-call/text syntax, gate old printEgo counting, attribute native beginnings by source, and bind direct actions' engine arguments. Native startup preparation uses reviewed inputs only and preserves queued, partial and completed player choices. Spanish readable labels use a verified executable/font fingerprint; rendered text is unchanged. Independent review found no additional actionable issues. Family commits integrated: 7052898 (classic Maniac), 893fe90 (enhanced Maniac record), 3f9449a (enhanced Zak), 1248448 (classic Zak); shared prerequisites 87363d2, cf75039, 112d92e. App binding d88bf6b. The solution builds with zero warnings/errors; final merged full-suite validation follows below.

## Not done and why

S8 is opt-in; S9 is outside this run. Existing visual/original-game comparison, darkness/flashlight and walking limits remain open. Classic Maniac's post-doorbell Script 174 getClosestObjActor remains unsupported and explicitly pinned. State-gated triggers receive no invented prerequisites; five Zak starts reach the frame bound (2/0/0, 11/0/0, 11/249/1, 14/55/11, 23/410/253). A counted start does not prove scene completion, full-story prerequisites or original-game fidelity. The Spanish rendered-font discrepancy is retained, despite readable labels being derived from verified glyphs. No ScummVM executable was run. Game data and the user's profile were preserved; no junction was created and no worktree was removed.

## Decisions made

One shared Core owner, independent family scans/tests in parallel. The user named no particular scenes; the three examples per family were selected transparently from the scan and tied to source instructions. Cutscenes becomes Partial with a derived playable list and regression evidence; Full remains reserved for a hand-validated catalog. Initial zero/seven-trigger scans, stale-object wrong-branch results and intermediate counts remain in the evidence. Direct actions are disclosed exploration actions, and room visits are state previews.

## Proposed requirements to confirm or reject

R-8.34 (commits e51cca8, 75ef5b7): a disclosed native-input startup preview for scene derivation/launch, preserving queued/partial/completed player choices and ordinary Play. Existing R-8.32/R-8.33 apply. The proposal remains Proposed until the user reviews it; no approval gate was added.

## User checks

Exact steps are in the [integration record](../../.verification/maniaczak-cutscenes-20261018/README.md): open either Maniac fixture, Room 1's doorbell scene, then Advanced global picker Room 17 TV or Room 8 circuit breaker; open either Zak fixture, Room 8 Entry then Room 3 Push doorbell in the picker. Check Pause, available Skip, text pacing, sound synchronization and appearance. On Maniac's unanswered menu a launched preview uses the disclosed party; a selection already begun must be finished by the player. Automated checks do not establish physical mouse feel, perceived timing or original-interpreter fidelity.

## Offers

S8 state UI and shipped seeds are not included.

## Cost

Three concurrent workers plus parent, four family items/worktrees. Zak Classic preparation proceeded while the shared fix was pending, with family commits kept separate. Family censuses took about 40s for Maniac and 147–159s for Zak with the measured worker settings; paired window timings are above. No user budget supplied.

## Next run

Suggested request: "Go on with S9 for all four Maniac/Zak families. Parallelize agents as much as possible, keep the same coordination rules, update support notes as warranted, and stop at a clean checkpoint." Keep the remaining S7 bounds and source/observed distinction explicit. S8 remains opt-in; no seed or hand catalog is authorized by this run.
