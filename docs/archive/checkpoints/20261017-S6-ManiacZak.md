# Maniac/Zak S6 checkpoint — 17 October 2026

## Plan

The user explicitly requests S6 for all four families, retaining the S5 visual uncertainties rather than treating those as permission to invent state. Three workers run concurrently in separate family worktrees; the fourth family uses the first released slot. ManiacEnhanced owns shared engine input, sentence, actor and opcode prerequisites. ManiacClassic owns its tests/evidence and shared Maniac documentation. ZakEnhanced owns its tests/evidence and Zak documentation, then hands off for ZakClassic. The parent owns App play integration, requirements proposals, registry/matrix generation and final integration. No simultaneous edits of shared engine files; needs go to their owner.

Read REQUIREMENTS section 7.1 and compare the Indy3/MonkeyFloppy S6 records. Each family needs a bounded route driven by genuine clicks/keys, its script/instruction evidence, regression tests and a real-window check. Review old-engine inventory/sentence presentation rather than assuming later SCUMM semantics. Native ScummVM execution remains prohibited; comparison limitations must stay explicit. No S7/S9 expansion in this run. No requested token/time budget; stop each blocked item after two distinct failed approaches and record it at a green commit.

Keep v1/v2 opcodes in their own executor partial; shared profile, variables, session and UI edits small and additive. Do not touch other families or NEXT_SESSION_PROMPT.md. Never create Games junctions, remove worktrees, force, stash or change user profiles/game data. Merge current master in the integration worktree, preserve both orchestrators' work, regenerate README/REQUIREMENTS matrices, build, run the full suite with required external fixture variables (zero failed), then fast-forward master. Repeat validation if master moves.

## Done

Native v1/v2 Play is implemented for all four families: script 4 input globals, the game's verbs and sentence, native inventory and scrolling, built-in DOS fonts, cursor, walk-box routing, old actor directions/placement, held-object code and positions, and function-key translation. Reviewed resource hashes select the four fixture editions and the Spanish font. First Play retains Maniac's actual party menu; browsing assumptions do not answer it.

Both Maniac editions complete one continuous route: choose Syd/Bernard, start, skip the Room 44 override, walk to Room 1, reveal and pick up the key, see it in inventory and select it. Headless routes take 1993 frames/15 inputs plus Escape (classic) and 1554/13 plus Escape (enhanced). Both also pass in the actual WPF window, with Normal-mode technical text absent; captures were inspected.

Both Zak editions compose a sentence, open the drawer, pick up the phone bill, display/select it, walk out through native exits, skip the triggered alien scene and choose Buy for the shop's nose glasses. The native CashCards-only response finishes and returns control in Room 7. Both actual-window routes pass at frame 2911 with 32 inputs plus Escape; captures were inspected. The initial shop cleanup blocker was the v3 player-class rule applied to v1/v2; the source-defined ego/range predicate fixed it without modifying the room matrix.

Objects and Play are now Full for all four families, reflecting native first-visit state and the bounded click routes. README and REQUIREMENTS matrices were regenerated with `SCUMM_UPDATE_SUPPORT_MATRIX=1` (3 tests passed). Costumes, Sound, Cutscenes and Status levels remain unchanged. In particular ZakClassic costume estimates remain Partial, and S7/S8 are not claimed.

The initial walking port regressed both Maniac Room 2 settling tests. The source direction tables corrected the turn loop; assertions were retained. Independent review found stale inventory scrolling after changing actors/ownership; the fix has a focused regression. Shared changes stay version-gated, with v1/v2 opcodes in their own partial.

Master was merged in the integration worktree, preserving concurrent Monkey CD Play, Monkey2 and Indy4 work, including checkpoint `b320884`. Conflicts in `ScummProfile`, charset loading and documentation retain both sides. The final merged solution builds with zero warnings and errors; matrix regeneration passes three tests. The full suite passes **1197 tests, zero failed, five skipped** (1202 total, 6m58s), with `SCUMM_SOUND_TEST_ROOT=D:/Juegos/Lucas/loom ega mt32 en`, `SCUMM_INDY3_EGA_TEST_ROOT=D:/Juegos/Lucas/indy3 ega en`, and `SCUMM_MT32_ROM_ROOT=D:/Juegos/Lucas/mt32`. The generated report is `.verification/maniaczak-play-20261017/test-results/maniaczak-s6-final-merged.trx`; commands and build paths are in the integration record. An earlier pre-Monkey2 full run passed 1187 tests, zero failed, five skipped; it does not replace this final merged result.

## Not done and why

S5 original-game visual comparisons remain open: ScummVM execution was prohibited. Source evidence and app captures are separate. Existing black-region and camera findings remain recorded; native movement offsets are now source-backed, but that does not close original-picture acceptance. Darkness/flashlight rendering, additional puzzles, other parties and unreviewed editions remain unverified. Old walking still uses a generic closest-polygon-point metric in native units rather than the full v2 search metric. S7, S8 and S9 are outside this run. No worktree, game data or user profile was removed.

## Decisions made

One shared-engine owner; independent family route discovery in parallel. Native party selection and shop transaction choices are used where these early games have no later Talk/dialogue menu. Routes use clicks/keys, never story-state assignments. Registry levels reflect the bounded supported behavior and retain explicit visual limitations.

## Proposed requirements to confirm or reject

R-7.19 (native sentence/inventory and old input contract), R-7.20 (startup choices remain the player's), and R-7.21 (native choices in early games) remain Proposed. They were committed separately (`f0c34f0`, `a00790f`) as required; implementation proceeded without approval gates.

## User checks

Open the checked fixture and select a room. In Maniac, choose Syd and Bernard and Start, use Escape during the opening, then follow the key route in the family record. In Zak, follow the drawer/phone-bill and shop route in its record. Check physical mouse feel, perceived timing, spoken-text pacing, and PC-speaker/PCjr sound synchronization. The automated window checks exercise the real handlers/rendering but do not establish those subjective results.

## Cost

Three concurrent workers and parent, four family worktrees. No user budget supplied.

## Next run

S7 derived cutscenes and S9 edition checks are the next roadmap stages; S8 remains opt-in. Retain unresolved S5 visual findings and the bounded-play distinction. Read [the dedicated handoff](../../../NEXT_SESSION_PROMPT_MANIAC_ZAK.md) and [integration evidence](../../../.verification/maniaczak-play-20261017/README.md).
