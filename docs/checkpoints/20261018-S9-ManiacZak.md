# Maniac/Zak S9 checkpoint — 18 October 2026

## Plan

Scope: S9 edition checks for ManiacClassic, ManiacEnhanced, ZakClassic and ZakEnhanced, widening the completed S4b–S7 evidence only where local copies exist. Inventory the available data before claiming alternate-release coverage. The current root has one folder per family: classic English Maniac, enhanced Spanish Maniac, classic English Zak and enhanced English Zak. Separate families do not count as alternate editions of one family. Missing copies are recorded as blocked checks, never inferred successes.

Three workers run concurrently in separate worktrees, one family per item. ManiacClassic and ManiacEnhanced own separate tests/evidence; Classic coordinates Maniac tracker changes. ZakEnhanced completes its item before taking ZakClassic in a separate worktree, coordinating Zak tracker changes. Parent owns shared engine/App files, registry/matrices, checkpoint, CURRENT_WORK, CHANGELOG and dedicated handoff. Workers report a demonstrated engine issue before editing shared files. No other orchestrator families, ScummVM execution, game-data mutation, Games junctions or worktree removal.

Use the relevant edition checks from `.verification/editions-20261005`: content identification, resource/script/text/font differences, own-data boot/play/scene assumptions and fail-closed preview behavior. Reuse S7 evidence rather than repeat broad audits without a plausible difference. One bounded family harness and meaningful regression per new compatibility claim; log two failed approaches and move on. No user budget was supplied; stop at one complete bounded pass per available family and record missing copies honestly.

Integration protocol: merge master in the integration worktree, preserve both orchestrators' changes, regenerate README/REQUIREMENTS using SCUMM_UPDATE_SUPPORT_MATRIX=1, build and run the full suite with the three external fixture variables, require zero failures, then fast-forward master. Repeat if master moves. S8 and seeds remain opt-in.

## Done

All four available-copy items have bounded checks and evidence; additional-release checks remain blocked by missing fixtures. Each family keeps its current registry levels. README/REQUIREMENTS now state the exact checked editions and do not imply language/revision coverage from the matrix family names.

| Family | Available-copy results | Additional editions |
|---|---|---|
| ManiacClassic | 52 rooms, 23 costumes, 173 scripts, 61 sounds; own V1 dialect, native party startup and source scan; unreviewed menu rejected before inputs/state change | Blocked: no additional classic copy |
| ManiacEnhanced | Supplied Spanish V2 detected; exact executable-font decoder, label/fallback boundaries and rendered-glyph checks; real-window accented TV line passes | Blocked: no additional enhanced copy |
| ZakClassic | 58 room structures, 35 costumes, 151 scripts, 110 sounds; own V1 text and profile; two focused tests pass | Blocked: no additional classic copy |
| ZakEnhanced | 58 room structures, 39 costumes, 148 scripts, 112 sounds; own V2 text and profile; two focused tests pass | Blocked: no additional enhanced copy |

Shared corrections are small and additive: two obsolete v1/v2 scanner notes now describe resource directories actually read; an optional native-font descriptor decodes only the fingerprinted Spanish executable, validates the decoded font and supplies it to v1/v2 drawing. Other or missing executables retain the previous fallback. No game font/executable is committed. Independent read-only review reports no actionable issue. The Maniac README's stale classic dialect was corrected to V1.

The targeted window harness reuses the real S7 room/picker callbacks with a private profile. Enhanced TV Room 17 reaches Script 112 in Room 40 and displays `músicos y diseñadores` correctly; capture inspected, unchanged 7-begun census. This is Explorer integration evidence, not original-game observation.

Integrated family commits: ManiacClassic `c77f792`; ManiacEnhanced decoder/test `ab99c97`, final glyph evidence `59b0c52`; ZakEnhanced `e22106f` and record correction `c8ff89e`; ZakClassic `265c507`. Shared rendering hookup `80587b6`, scanner text `274a098`, coverage notes `bfbf142`, coordinated Maniac tracker `79a9774`; integration evidence `965be68`.

Final validation against master `f6d8f0b`: `git merge master` already up to date; solution build **zero warnings/errors**; generated matrix **three passed**; full suite **1227 passed, zero failed, five skipped** (1232 total, 8m24s). The required external sound, Indy3 EGA and MT-32 settings are recorded in the [integration evidence](../../.verification/maniaczak-editions-20261018/README.md). TRX is retained locally at `.worktrees/maniaczak-s9/.verification/maniaczak-editions-20261018/test-results/maniaczak-s9-full.trx`. Final integration is fast-forward only; worktrees are retained and no game/profile data was removed.

## Not done and why

No alternate language, revision or demo within any family exists in the supplied data root. Classic/enhanced are separate families and cannot satisfy each other's missing comparison. This S9 run closes the available-copy pass, not the requested wider compatibility coverage. ScummVM execution remains prohibited. Existing S5 original-picture, darkness/flashlight, walking, perceived timing, Classic Maniac Script 174, Zak's 11 estimated costume palettes and five capped cutscene starts remain open. S8 and seeds were not requested.

## Decisions made

Do not download or fabricate missing editions; record blocked comparisons and reuse prior unchanged-copy S4b–S7 results. Synthetic negative checks prove fingerprint boundaries only, not another language. Do not raise a feature cell from such checks.

A raw 1024-byte executable slice was initially tried (`a8542c4`) and reverted (`1ead78e`) when the all-glyph check found compressed low glyphs. Existing tests had passed but did not establish correctness. The source-backed extraction loop and complete decoded-font fingerprint replace that approach; the failed attempt remains recorded. Native bytes are optional data and bypass fallback language substitutions. No source-specific state, room or opcode workaround was introduced.

## Proposed requirements to confirm or reject

No new requirement was proposed. Existing S6/S7 proposals remain in force.

## User checks

Optional corrected-font look: open the supplied Spanish enhanced Maniac copy with its reviewed executable present, choose Room 17, Play Cutscene → `Encender TV`; the first TV message should read `músicos y diseñadores`. The app is built in `.worktrees/maniaczak-s9/Exe/Debug/SCUMMexplorer.exe`. Previous S6/S7 listening, timing and original-game comparison checks remain in those records and are not requested again. More edition comparisons require actual additional copies.

## Offers

S8 state design and shipped seeds only on explicit request.

## Cost

Three concurrent family workers plus parent; ZakClassic follows ZakEnhanced in its own worktree. One bounded inventory/compatibility harness per available copy; S4b–S7 broad audits reused where no new data warrants a repeat. Enhanced adds one targeted real-window font check. No user budget supplied.

## Next run

When additional copies are available: "Resume the blocked S9 edition comparisons for ManiacClassic, ManiacEnhanced, ZakClassic and ZakEnhanced using the new local copies. Keep the coordination rules and stop at a clean checkpoint." Otherwise the next work is an explicit choice among retained visual/engine limits or optional S8; no automatic next stage is started.
