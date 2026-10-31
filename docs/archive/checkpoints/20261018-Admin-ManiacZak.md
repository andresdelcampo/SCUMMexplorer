# Admin Game Status and playback controls — 18 October 2026

## Plan

Explicit user clarification: Game Status is an admin editor, including grants from the game's item catalog with native capacity limits, pictured recipients in Maniac Mansion, and native item pictures where the game has graphical inventory. Review all four Maniac/Zak families; restore Normal sound selection for playable games with sound/cutscenes; review playback controls against LOOM.

Integration branch `codex/admin-status-controls`. Separate item worktrees: one exclusive Core inventory writer; one inventory-window writer; one playback/audio UI writer. Parent owns requirements, matrices, final evidence, checkpoint and handoff. No other-family feature work; shared UI regression checks include LOOM. No ScummVM execution, game data/profile changes, junctions or worktree removal.

## Done

- User-confirmed requirements now define Game Status as an admin editor, including unearned item grants, native capacity, pictured Maniac recipients and native item icons for graphical inventory games. R-1.7–R-1.9, R-7.3, R-8.2 and §12/§13 agree; the held-only restriction is superseded. Requirement commits `15fd25f`, `65d14ad`, conformance clarification `a0bd55d`.
- All four Maniac/Zak families offer searchable catalogs and atomic explicit grants/transfers/Undo: **49 named items per Maniac edition, 82 per Zak edition**, native **80 global slots**. All seven Maniac kids are derived from actual native party-selection assignments, with six native selection pictures and Dave's native costume snapshot. The UI offers held entries too, so full inventories can still transfer items. Closing discards only pending edits; applied choices remain. No script/puzzle progress is claimed.
- Core commits `1f8d4b4`, `fe03d0c`, `9889edd`; [Core record](../../../.verification/maniaczak-admin-inventory-20261018/README.md). **18 new admin cases**, twelve prior family state cases and twenty LOOM status cases pass (50 scoped checks). Parent's earlier conformance + LOOM status check: 35 passed, overlapping these tests. Retained item code is executed after a grant outside its source room in both Zak copies.
- Shared UI `8526453` restores the existing sound selector in Normal for registry-supported live cutscenes/audio, fixes generic scene device locks, Space/Pause captions, immediate native transport updates and Skip resuming a paused scene. V1/v2 device changes preserve every known game word; v3 uses native device values. **44 focused tests and seven real-window checks** pass, including LOOM EGA/CD and a v3 device regression. [Playback record](../../../.verification/admin-playback-20261018/README.md).
- All four Status cells are **Full → Partial** under the clarified all-items bar; no other support level changes. This rebaselines the earlier bounded held-item claim rather than presenting unresolved indirect coverage as complete admin support. README/REQUIREMENTS matrices are regenerated from the registry.
- Inventory UI `ea3e54c` (integrated as `c6fe209d`) passes four final native-route windows plus two explicit unselected-kid checks, with seven of seven native Maniac pictures and final 49/82 catalogs. Parent inspected the Classic seven-character dropdown and Enhanced applied Razor grant. [Window record](../../../.verification/admin-inventory-ui-20261018/README.md).

## Not done and why

Indirect pickup targets remain unresolved in Maniac Script 116 and Zak scripts 119/24/55 plus special handlers. Static source metadata/normal object/helper contexts establish the offered catalogs. No resolved named candidate is dropped for source-room ambiguity in the supplied copies, but arbitrary dynamic target completeness is not established. Zak's FD-only letter header (139) needs further tracing before it can be classified or granted safely. The initial nine false-positive Zak special-handler headers and failed assertions are preserved in evidence.

V5 inventory pictures are now a confirmed requirement for future Status implementations; no v5 Status window is enabled by this run. The other orchestrator's families, earlier full-story/original-game visual/timing/listening limits and absent alternate Maniac/Zak releases are unchanged. Uninitialized Zak actors keep source names without invented pictures; the user's picture requirement for Maniac is checked separately.

## Decisions made

The user's explicit admin intent supersedes the old held-items-only restriction. Grants remain explicit user choices, separate from earned pickups and story flags; native capacity and valid retained object resources still apply.

Pause controls the ordinary live clock as well as cutscenes. Skip requires a native active override; Skip line requires a message. Play Cutscene is a room launch list, not a current-scene indicator. Thus Pause without Skip, or Skip without a room launch entry, can be legitimate. Fix stale state, not these independent meanings; no launch entry or skip point is invented. The absent-launch-list harness case deliberately clears the planner while a real native scene is playing; it is a controlled independence test, not a newly discovered catalog omission.

One shared Core writer, isolated UI owners and an independent read-only review of transactions, source ambiguity, recipients and artwork found no remaining actionable issue. Native pictures render from immutable copies or decoded object art. Changes are reversible by reverting the bounded implementation/requirements and associated registry/test notes together; preserve historical records and other-family work.

## Proposed requirements to confirm or reject

None new. The admin/picture/audio clarifications are explicit user decisions, not proposals. The independent transport meanings document existing native semantics and LOOM's reference behavior.

## User checks

Optional UI look: open any supplied Maniac/Zak copy, finish its startup choices and open Game Status. Search for an item, select a recipient, Add item / Apply Changes, then Undo. For Maniac the dropdown contains all seven native names with pictures. Close with unapplied choices discards them. The sound selector is present in Normal; during a native scene it is locked. Space changes the Pause/Play caption immediately; Skip resumes from an established native override. No repeated listening/timing acceptance is requested.

## Offers

No automatic follow-on work.

## Cost

Three independent implementation worktrees plus parent integration; the playback worker also performed read-only inventory review. No fixed user token budget. Catalog source analysis corrected a 91-entry Zak over-inference to 82, and actual-window checks corrected assumptions about uninitialized Zak artwork. No game/profile/ROM/save changes, ScummVM execution, Games junction, worktree removal, force or stash.

## Next run

Stop here after final validation and fast-forward promotion. A further request can review the remaining indirect inventory roster, especially Zak's letter 139 and special-handler pickup targets, against the supplied copies. Keep Status Partial until exhaustive catalog coverage is supported. Wider v5 Status work belongs to a separate explicit request coordinated with its orchestrator. Never edit `NEXT_SESSION_PROMPT.md`.

## Final validation and integration

Final integrated solution build: **zero warnings/errors**, 7.82s. README/REQUIREMENTS regenerated with `SCUMM_UPDATE_SUPPORT_MATRIX=1`: **three passed**, including both generated tables and registry shape. Production source was frozen before merging the latest master and running the required full suite with sound/Indy3/MT-32 fixture settings.

Post-merge solution build: **zero warnings/errors**, 7.74s. Master merge was already up to date at `41e530ce`; no other-family source change was needed. Combined final App checks pass for ZakClassic, ManiacEnhanced (isolated and overlapping-window diagnostic runs) and ManiacClassic. The first concurrent enhanced check failed a state-preservation assertion without advancing a frame; exact before/after diagnostics were added and the same source then passed three further checks. No differing JSON was emitted and no production change or weaker assertion was made. The [integration record](../../../.verification/maniaczak-admin-20261018/README.md) preserves the unproved pointer-event hypothesis and failure path.

Required full suite: **1357 passed, zero failed, five skipped; 1362 total**, exit 0, **10m 49s**. All 18 `ScummInventoryAdminTests` cases executed and passed in the TRX. Run 19:22:10–19:33:00 Copenhagen time, 18 October 2026; local ignored report `.worktrees/admin-status-controls/.verification/maniaczak-admin-20261018/test-results/admin-status-final.trx`. Settings: `SCUMM_SOUND_TEST_ROOT=D:/Juegos/Lucas/loom ega mt32 en`, `SCUMM_INDY3_EGA_TEST_ROOT=D:/Juegos/Lucas/indy3 ega en`, `SCUMM_MT32_ROM_ROOT=D:/Juegos/Lucas/mt32`. Command: `rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore --verbosity quiet --logger "trx;LogFileName=admin-status-final.trx" --results-directory .verification/maniaczak-admin-20261018/test-results`.

Master remained `41e530ce919fb24a1d9702bf48d08edf33bf99fa` throughout validation. Only final records and harness failure diagnostics follow tested production commit `761b98a`; no production code or support level changed afterward. Promotion is `git merge --ff-only codex/admin-status-controls` in the clean master checkout, with matching clean heads verified afterward; repeat the protocol if master moves. Worktrees are retained. The runnable App is `Exe/Debug/SCUMMexplorer.exe` (actual project output), refreshed in the main checkout after promotion.
