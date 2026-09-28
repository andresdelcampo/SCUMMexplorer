# Changelog

Finished work, oldest first. Each entry links to the validation record that holds the details, the commands and the counts. Reverse-engineering findings live in the [script map](LOOM_SCRIPT_MAP.md); priorities live in the [plan](LOOM_EGA_PLAN.md). This file replaces `WIP_2026-09-27.md` and the history table that was in the plan (28 September 2026).

Unless noted, "no physical input" applies: the WPF harnesses drive the real window's handlers with synthetic events.

## 23–26 September: groundwork

| Change | Result | Record |
| --- | --- | --- |
| Research and format readers | LFL index and room readers, EGA room decoder, costume poses, PC speaker and AdLib audio | [research](RESEARCH.md) |
| Audit of exploration | Room state and object clicks were unresolved; direct verb calls labelled as exploration actions | [exploration audit](EXPLORATION_AUDIT.md) |
| Parent-state correction | Code-only parents keep their states for image selection | [object states](SCUMM_OBJECT_STATES.md) |
| Room 03 local scripts | Scripts 200–202 decode from Room 03's directory; object hotspots are focusable | [room03-local-action](../.verification/room03-local-action/validation.md) |
| Game Texts browser | Searchable lines, object names and stored strings with room context, then object context and conversation groups (719 entries at the time) | [game-texts](../.verification/game-texts/validation.md), [refinement](../.verification/game-text-refinement/validation.md) |
| Room viewport | 320 source px wide by default, scaled up, pan controls, costume pose gallery | [room-viewport](../.verification/room-viewport/validation.md) |

## 27 September: rendering, navigation and the boot

| Change | Result | Record |
| --- | --- | --- |
| Indy3 EGA opcode dialect and dialogue choices | `ScummScriptDialects.For(game.Id)`; choice sets grouped with their replies | [audit](AUDIT_2026-09-27.md) |
| Call-order renderer, draw batches, gap filling, doorways through started scripts | SCUMM paint order (reverse directory, then the `drawObject` queue); 0 gaps in 76 rooms | [room-audit](../.verification/room-audit/validation.md) |
| Script-backed fallback for rooms with nothing visible | Script draw batches first, then a labelled first stored image | [fallback-batches](../.verification/fallback-batches/validation.md) |
| Global scripts 0, 51, 55, 116 | Leftover index entries pointing inside other resources; listed as such in Game Texts | [global-script-index](../.verification/global-script-index/validation.md), [leftovers](../.verification/game-texts-leftovers/validation.md) |
| One doorway button per target room | | [doorway-buttons](../.verification/doorway-buttons/validation.md) |
| Verb-area images kept off the room; reached and unavoidable entry states | First-stored-image rooms 24 → 4 | [fallback-evidence](../.verification/fallback-evidence/validation.md) |
| Room 03 exit | Object 162 → Room 04 reached; `stopScript` modelled; the `0xD7A` wait was read inverted before | [room03-exit](../.verification/room03-exit/validation.md) |
| `getRandomNr` | Labelled seeded pick; later steps are estimates | [random-picks](../.verification/random-picks/validation.md) |
| Doorway clicks and object classes | `findObject` order, class 24 untouchable; 75/75 doorway boxes navigate | [doorway-clicks](../.verification/doorway-clicks/validation.md) |
| Compact room panel | | [room-panel](../.verification/room-panel/validation.md) |
| Local-array draws, `setVarRange` | Room 04 rest frame | [local-array-draws](../.verification/local-array-draws/validation.md) |
| Cutscene, freeze, waits, `isSoundRunning` | Hooks `var[0x023]`/`var[0x024]`; waits continue next frame (timing labelled) | [opcodes](../.verification/opcodes/validation.md) |
| Global scripts across room loads, `beginOverride`, `isScriptRunning` | Cutscene bodies decoded (spoken lines 580 → 1086) | [cutscene-continuation](../.verification/cutscene-continuation/validation.md) |
| Boot past the intro | Room 69 answer assumed; opening cutscene 044; session starts in Room 02 with the real hooks | [boot](../.verification/boot/validation.md) |
| Walk boxes | Old-v3 BOXD reader and ScummVM box geometry; overlay checked by eye | [walk-boxes](../.verification/walk-boxes/validation.md) |
| Actor positions and queries | `LoomActorTable`; walks end at once (estimate) | [actor-positions](../.verification/actor-positions/validation.md), [analysis](../.verification/actor-queries/analysis.md) |
| Hotspot highlight and reverse hover | | [hotspot-highlight](../.verification/hotspot-highlight/validation.md) |
| Room music row | Reached cues only since entry, or the room's own and not stopped | [room-music](../.verification/room-music/validation.md) |
| Camera after `loadRoomWithEgo`, `actorFollowCamera`; optional entry pan | | [camera-ego](../.verification/camera-ego/validation.md) |
| Camera limits (`roomScroll`) | Room 04 opens on the half its entry picks | [camera-limits](../.verification/camera-limits/validation.md) |
| `findObject` and the input loop | Script 006 idles with an assumed pointer below the room | [input-loop](../.verification/input-loop/validation.md) |

## 28 September: input and arguments

| Change | Result | Record |
| --- | --- | --- |
| Catalog arrivals | A catalog visit takes a decoded `loadRoomWithEgo` as an assumed arrival | [catalog-arrivals](../.verification/catalog-arrivals/validation.md) |
| `loadRoom` arrivals | Rooms 12, 28, 31, 32, 43, 49, 65, 66 | [loadroom-arrivals](../.verification/loadroom-arrivals/validation.md) |
| Pointer as game input, `verbOps`, `startObject` | Optional hover through script 006; close-up from script 008 | [pointer-input](../.verification/pointer-input/validation.md) |
| A click as game input | `Click` through scripts 005–007 (optional in the app) | [click-input](../.verification/click-input/validation.md) |
| Verb area (presentation) and verb-area clicks | `LoomVerbArea`, `findVerbAtPos`; notes reach script 010 | [verb-area](../.verification/verb-area/validation.md), [verb-click](../.verification/verb-click/validation.md) |
| Keys as game input | Script 005 area 4 decoded; Escape's draft cancel corrected | [key-input](../.verification/key-input/validation.md) |
| `startScript` with a local target | Boot runs Room 01 locals 200–208 | [dynamic-scripts](../.verification/dynamic-scripts/validation.md) |
| VisitProbe input steps | `v:`, `k:`, `s:` | [probe-input](../.verification/probe-input/validation.md) |
| Escape's `abortCutscene` | Script 052 skips from Room 11 to Room 13 | [abort-cutscene](../.verification/abort-cutscene/validation.md) |
| Estimated script arguments | Draft notes passed as labelled estimates (commit 06fc19a) | [estimated-arguments](../.verification/estimated-arguments/validation.md) |
| Room loads by room-owned scripts end the script | ScummVM `startScene`; Blockers 5905 → 5839 (commit 3b15611) | [room-owned-loads](../.verification/room-owned-loads/validation.md) |
| Visual-first UI refresh | Room-art picker, larger scene, advanced details collapsed (commit 92c2e3c). CameraPanCheck 2 FAIL and VerbAreaCheck 1 FAIL since then | [ui-refresh-complete](../.verification/ui-refresh-complete/validation.md) |
| Replan | Priorities moved to visible behaviour: interactions, on-screen text, actors, playback. Plan and changelog cleaned up; no code changed | [plan](LOOM_EGA_PLAN.md) |
| M1e: the game's click by default | Explore-mode clicks go through scripts 005–007; double click adds verb 0x36; doorways fall back to their verb, labelled (75/75: 29 by the game's click); inspection mode cycles artwork | [game-click](../.verification/game-click/validation.md) |

## State at the end of 28 September

- `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 198/198 with `SCUMM_SOUND_TEST_ROOT=Games\loom` and `SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA`.
- RoomAudit: 0 gaps in all 76 rooms.
- WPF checks: all pass except CameraPanCheck (2) and VerbAreaCheck (1), see above.
- Blockers baseline: `.verification/room-owned-loads/blockers-after.txt` (5839 over 134 reasons). The real stops left are `expression` (3), `getActorCostume` (1) and `getActorElevation` (1). The rest are labels.
