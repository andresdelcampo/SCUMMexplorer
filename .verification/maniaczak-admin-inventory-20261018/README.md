# Maniac/Zak admin inventory Core contract

| Field | Result |
|---|---|
| Scope | ManiacClassic, ManiacEnhanced, ZakClassic, ZakEnhanced; explicit admin inventory clarification |
| Fixtures | `mmansion ega en`, `mmansionE ega es`, `zak ega en`, `zakE ega en` under `D:/Juegos/Lucas` |
| Date | 18 October 2026 |
| Core milestones | `1f8d4b4`, `fe03d0c`; final safety followup reported by orchestrator |
| Support | Parent owns registry/matrices. Enumeration is source-derived and bounded; exhaustive dynamic item coverage is not claimed. |

## What was done

`CaptureInventoryStatus` exposes a source inventory catalog and the native 80-slot global capacity. `ApplyInventoryEdits` validates a whole grant/transfer batch before writing. Grants allocate a native slot, preserve the exact source room and preposition header, set native intrinsic/untouchable flags while preserving other object-state bits, and change ownership. No pickup, callback, puzzle, conversation or story script runs. Native Give/pickup rules do not gate an explicit admin choice.

Undo restores original slots, membership, owners, states, retained room and header, including absent dictionary keys. Frame, revision, cross-session and exact relevant-state guards reject stale changes. The older held-only transfer method remains available for compatibility; the admin UI uses the new method.

All seven Maniac recipients are derived from native selection code. Script 4 assigns ego from party variables 0x2F/0x30/0x31; helper 114 assigns its argument 0x6D to 0x30/0x31 at +0x33/+0x3C. Room 45 selection objects 260–265 assign actors 1,2,4,5,6,7 to that argument before starting 114; boot assigns actor 3 to 0x2F. This admits unselected children and excludes NPCs without a hand-written actor list. Portrait metadata points to those native selection objects; Dave retains the reached actor costume fallback. Zak uses its native player range and source names.

## Commands run

Cwd `.worktrees/admin-inventory-core`; every command uses RTK:

```powershell
rtk proxy dotnet build src/SCUMMexplorer.Core/SCUMMexplorer.Core.csproj --no-restore -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter "FullyQualifiedName~ScummInventoryAdminTests|FullyQualifiedName~ScummManiacClassicStateTests|FullyQualifiedName~ScummManiacEnhancedStateTests|FullyQualifiedName~ScummZakClassicStateTests|FullyQualifiedName~ScummZakEnhancedStateTests|FullyQualifiedName~LoomGameStatusTests" --verbosity quiet
rtk proxy dotnet run --project .verification/maniaczak-admin-inventory-20261018/Probe/Probe.csproj -p:NuGetAudit=false --verbosity quiet
```

## Results

The integrated focused selection passes **46 tests, zero failed/skipped**: 14 new admin cases, twelve prior four-family status cases and twenty LOOM status cases. Four additional tests verify absent owner/state keys restore exactly. New tests cover unearned grants, the unselected Maniac recipient, exact native-state undo, 80-slot overflow atomicity with a mixed held/unheld batch, transfers while full, invalid/NPC/duplicate targets, stale unheld state and frame rejection, and both Zak editions running the granted phone bill's retained Read code in a different room. No source flags are asserted as earned gameplay. Build has zero warnings/errors.

| Fixture | Indexed source candidates | Returned catalog | Code-bearing object IDs | Excluded candidates |
|---|---:|---:|---:|---|
| ManiacClassic | 57 | 49, all named | 450 | 1,3,4,5,22,23,24,27: no object-code record |
| ManiacEnhanced | 50 | 49, all named | 460 | 3: no object-code record |
| ZakClassic | 82 | 82, all named | 756 | None |
| ZakEnhanced | 82 | 82, all named | 761 | None |

Candidates combine the index's native Pickupable bit, source Pickupable writes, literal pickup operands, normal object invocation's ActiveObject1 and variable-copy assignments, and literal start/chain helper calls. Repeated local records use native last-record lookup; source room must resolve uniquely. No final candidate is omitted for multiple source rooms in these copies. Actor/index entries without code cannot supply a retained inventory object and are excluded.

## Source comparison

The existing executor's ScummVM `script_v2.cpp` pickup semantics and `object.cpp` inventory storage are the engine reference (reviewed 18 October 2026). Native `o2_doSentence` FA/FD/FE special handlers do not establish their handler object as ActiveObject1. The catalog respects this distinction. No ScummVM executable was run; this is source execution evidence, not original-game visual/timing acceptance.

## Differences and uncertainties retained

The first source probe failed to compile because of an incorrect project-relative path and guessed disassembly output field; corrected before acceptance. An initial 91-entry Zak catalog over-inferred nine special-handler objects as their own pickup targets: 117 (infrared sensor), 139 (letter), 274/276/298/307/358/607 (unnamed), 632 (platform). The native special-handler correction yields 82. The intermediate 85-count assertion failed twice and was corrected to the measured 82; no failed result is presented as acceptance. Raw local logs remain ignored beside this record.

Generic indirect targets remain explicitly reported by `InventoryCatalogNotes`: Maniac Script 116; Zak scripts 119/24/55 and FD handlers in rooms 2/13/17/41. Ordinary object context and indexed Pickupable metadata supply the static roster, but arbitrary dynamic target completeness is not proven. In particular, the named FD-only letter should not be asserted to be either a safe grantable item or permanently non-inventory without further source tracing. Parent should keep Status Partial under the clarified exhaustive-all-items acceptance bar. The implementation has no known omitted *statically resolved* named inventory candidate. v5 item art is outside this Core scope.

## User checks

The parent/UI agent owns the four real-window checks and image inspection. Core requests no separate listening/timing check.

## Documentation

Parent owns REQUIREMENTS, support notes, CURRENT_WORK, checkpoint, handoff and generated matrices; this record supplies source and automated evidence for that integration.
