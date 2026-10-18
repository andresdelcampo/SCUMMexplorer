# Maniac/Zak admin Game Status and shared playback review

| | |
|---|---|
| Families | ManiacClassic, ManiacEnhanced, ZakClassic, ZakEnhanced; LOOM EGA/CD shared UI regressions |
| Date | 18 October 2026 |
| Fixtures | `mmansion ega en`, `mmansionE ega es`, `zak ega en`, `zakE ega en`, `loom ega mt32 en`, `loom vga en` under `D:/Juegos/Lucas` |
| Request | Explicit admin inventory grants, native character pictures, Normal sound selector and playback-control review |
| Registry | Status Full → Partial in all four families under the clarified all-items contract; all other levels unchanged |

## What was done

Requirements R-1.7–R-1.9, §12, R-7.3, R-8.2 and R-13.10 clarify the user's request. The former restriction to already-held items is superseded. Native capacity, resource validity and atomic edits are engine invariants, not earned-story prerequisites. Graphical inventory artwork (including v5) is required for future inventory editors; this run does not enable new other-family Status windows.

One Core writer, one inventory-window writer and one sound/playback writer worked in separate worktrees; the latter also reviewed inventory transactions and native artwork independently. The parent owns requirements, matrices, checkpoint and final integration. Core/API commits `1f8d4b4`, `fe03d0c`, `9889edd`; playback `8526453`; inventory UI `ea3e54c` integrated as `c6fe209d`.

Catalogs expose 49 named items per Maniac edition and 82 per Zak edition, including held items. Admin grants/owner transfers use native slots, retained room/code/preposition and pickup-state bits, preserving unrelated state. Native capacity is 80 global slots. All seven Maniac recipients are established by party-selection assignments, with six selection-object images and Dave's immutable native actor-renderer snapshot. No active hero, party or room is changed by choosing a recipient. Pending choices, Apply Changes, full Undo and Close are distinct; the modal pauses/restores the earlier clock state. Catalog provenance and unresolved targets are Advanced only.

## Commands run

All commands through RTK. Final integration merges current master into `codex/admin-status-controls`, regenerates the support matrices, builds and runs the full suite with the three required external fixture settings before fast-forward promotion. Exact result paths and counts will be recorded below.

## Results

- Core focused: **50 passed**, zero failed/skipped (18 new admin cases, twelve legacy family state cases, twenty LOOM status cases). Grants to unselected Maniac kid, native full-capacity mixed-batch refusal, held transfers while full, stale/invalid targets, exact Undo including absent keys, and retained Zak Read code away from its source room are checked. [Core record](../maniaczak-admin-inventory-20261018/README.md).
- Playback focused: **44 passed**, zero failed/skipped. Seven real-window runs pass: four Maniac/Zak, LOOM EGA/CD and one v3 sound-variable regression. V1/v2 zero-frame sound switches preserve all known globals. [Playback record](../admin-playback-20261018/README.md).
- Inventory UI: four final native-route window checks plus two explicit unselected-kid checks pass, using source-derived party bindings. Seven of seven native Maniac images; grants and transfers to Razor while she is outside the selected party; Zak queues exactly 77 free slots before Add disables at 80. All retain unrelated state and restore previous paused/running modes. [Window record](../admin-inventory-ui-20261018/README.md). Parent inspected the seven-kid Classic dropdown and Enhanced applied grant as well as earlier inventory/playback captures.
- Parent pre-integration catalog conformance + LOOM status: **35 passed**, overlapping the above filters. Integrated solution build: **zero warnings/errors**, 7.82s. Generated matrices: **three passed**. Final full-suite result follows after master integration; focused filters are not additive.

## Comparison with ScummVM

Source-derived version rules only; no ScummVM executable is run. Real-window checks drive the WPF application with private profiles. Neither kind of check is original-game observation or a timing/listening comparison.

## Differences and uncertainties kept

Pause controls the live clock, including ordinary play. Skip requires the currently active native override. A room's Play Cutscene list contains verified launch entries and is independent of the current native cutscene; entry/global/interaction scenes can therefore expose Skip without a same-room launch button. Real stale caption/transport and generic audio-lock defects were fixed. The absent-launch-list harness case deliberately clears the planner while retaining a real native scene; it checks independence rather than discovering a missing scene.

Exhaustive indirect pickup-target coverage remains open. The candidate method covers source flags/writers and normal object/helper contexts, not every arbitrary runtime target. Nine initial Zak headers inferred as their own targets from resident special handlers were removed; letter 139 merits further classification. Index actor/metadata entries without object code are excluded. No candidate is excluded for source-room ambiguity in the checked copies. Status remains Partial, and known uncertainties/failed assertions are retained in the worker evidence. No uninitialized Zak portrait is invented. V5 artwork is a confirmed requirement for future editors; no other-family Status feature is enabled.

## User checks

Open Game Status after startup, search for an item, choose a pictured Maniac recipient or source-named Zak character, Add item / Apply Changes, then Undo. Close without applying discards pending choices. Normal sound is in the header; Space updates Pause/Play immediately, and a paused skippable scene resumes from its native override on Skip. Worker records give exact reproducible private-profile commands. No approval gate or repeated listening/timing request.

## Where the facts went

[Requirements](../../docs/REQUIREMENTS.md), [checkpoint](../../docs/checkpoints/20261018-Admin-ManiacZak.md), [Maniac tracker](../../docs/maniac/SUPPORT_STATUS.md), [Zak tracker](../../docs/zak/SUPPORT_STATUS.md), [dedicated handoff](../../NEXT_SESSION_PROMPT_MANIAC_ZAK.md).
