# Script executor allocations and the cutscene derivation, 19 October 2026

Follows the cutscene snapshot record (`../cutscene-snapshot-20261018/README.md`), which left the per-script result copies of `ScummBoundedScriptExecutor` as the remaining cost. All changes keep behaviour identical: same lists, same runs, same traces.

## What changed

Commit `644d55fc` (executor and session):

1. **No copies nobody reads.** `Execute(..., snapshotState)`: a run with a runtime copied the object states and positions into its result every time; now only when the caller keeps the result (`LastEntryRun`, `LastAction`: `RunScript(..., keepState: true)`). The variables were copied too, although the dictionary is the run's own; the result now holds it.
2. **A pool of variable dictionaries.** Every run starts with a copy of every global (812 in Indy 4, 1424 in Zak). A run whose result is not kept borrows a dictionary from a per-thread stack (runs nest) and fills it in the runtime's order, so its enumeration order is that of a new copy; it goes back at the end, and the result holds only the locals of a paused run (what the session reads to resume it). Observed runs never borrow.
3. **Prepared instructions once per disassembly** (`ConditionalWeakTable`): the matrix-op normalisation, the v1/v2 override expansion and the offset index were rebuilt on every run. The 26 local keys are shared strings.
4. **`ScummGameSession.StepFrame`**: the frame of `AdvanceFrame` without building the session snapshot (states, positions, owners, variables, classes, lists: 130 KB in Indy 4) that the cutscene runs, the planner's verb search and `Settle` never read. `ScummSession.AdvanceOneFrame` is now `StepOneFrame` + `Snapshot`; LOOM's `AdvanceFrame` is unchanged.
5. **getActorFromPos reuses its drawing**: `ScummActorRenderer.Render` takes optional buffers; the hit test lends two per-thread room-sized arrays, cleared as new ones would be.

Commit `de7e9f80` (executor):

6. **The v1/v2 preparation records its writes.** Before each v1/v2 instruction the executor copied every variable to find what `PrepareV2Instruction` changed (1424 entries per instruction in Zak). The preparation now sees a recording view (`WriteRecordingVariables`: key, whether it existed, value before the first write); the changed keys are found in the dictionary's own order, as the comparison found them, and only when something was written.
7. **One instance of each operand pattern per thread** (no observed effect on scaling; kept because a shared `Regex` hands its cached matcher to one caller at a time).

Commit `dfe38e1c` (App):

8. **At most four workers for a disk or archive game** (`MainWindow.PlayCutscenes`, was eight). Measured below: on this 16-core machine the runs got slower per worker as workers were added, and eight were slower than two or four. File-per-room games keep half the cores.

## Identical results (acceptance 1)

"Before" is the harness built on master `fb3da87b` (the tip at the end, after the S9 MonkeyCd merge); "after" is the final branch code (merged). `CutsceneList` `loom` for LOOM EGA/CD, `scan run` for the ten generic families; timings removed before hashing (`compare.ps1` of the snapshot record). Fixtures under `D:\Juegos\Lucas` as in the snapshot record.

| List | Lines | SHA-256 before = after | Same |
|---|---|---|---|
| indy3-ega | 156 | 084f3a28290648246b901ac4fcc510444acdddbb7445d3710a0e9962133df9d5 | yes |
| indy3-vga | 156 | 72c74abb8a8fafb6c58c927fb28cb426928fe7f376428148f9480041ade8beb3 | yes |
| indy4 | 430 | 882f873926202eb3868190371d82c39f940b271c7407da9b937949522f8da9fe | yes |
| loom-cd | 224 | e131389f80d7e8995909dda268997b339b1346e2942370cf41ce4369b995800d | yes |
| loom-ega | 219 | 79236d5a8204e1f0ef6f53002c44ea20c02bb9d3cc7a49ef09de75ceda426341 | yes |
| maniac | 30 | fcd55cc97e2a86dcc02396d0d68baad55500805e0a2f4c544522c3011c5884c1 | yes |
| maniacE | 30 | 10d7cdaa6fea93589d96c0425a964216813aaed8d8b1123fa8ef97bf55ad1ee2 | yes |
| monkey-cd | 163 | 6b4b5cea33ebe6d658863688c30e8d439def1af7085f25e746ed4bde0d5aceaf | yes |
| monkey-floppy | 167 | 63da19ac3c4c21614f371d18da8243205b7e3d80452afa19d1ed384394707eed | yes |
| monkey2 | 263 | e68d13a528de8cdeeaaca132cf02e1d514b9e7540528ecb21dbcb24c7216b596 | yes |
| zak | 57 | 12bdf13c30b2a36edb93569e9aa86e0c24389bdf3de6820fecb72fb618a82a49 | yes |
| zakE | 57 | 8b5a7c87c2cdf2799a2a62c8c0f5a928d338884d8f8bd134857d03bc98037a48 | yes |

(Indy3 and Zak hashes differ from the snapshot record's because master changed those lists in between; both sides here are on the same base.) The intermediate steps were hashed too: step 1 (`644d55fc` content) and step 1 + pooled v2 copy, against the start-of-day master `ea89e1ef`: all twelve the same. The eight-worker per-run lines (`prun 8`, header removed) are the same before and after for all ten generic families.

**After the second master merge** (`42be6241`, manual rooms; `Settle` moved to `ScummSessionSettle` and keeps master's `AdvanceFrame`): the twelve lists rebuilt on the merged tree hash the same as above; `Cutscene|Snapshot|MonkeyCd|ExecutorAllocation|ManualRoom|Settle|Architecture` tests 233 passed, 0 failed.

## Tests (acceptance 2)

`tests/SCUMMexplorer.Tests/ScummExecutorAllocationTests.cs` (8 cases): Indy 4, Monkey Island 2, Zak and Indy3 EGA, three entries each from one snapshot, 300 frames with `AdvanceFrame` on one copy and `StepFrame` on another: same room, states, positions, variables (in order), trace, uncertainties and pending scripts; and a kept run's (`LastAction.Run`) states and positions do not change over 200 later frames. Full suite on the merged tree (S9 MonkeyCd FR/DE/IT included): **1680 passed, 0 failed, 5 skipped** in 7 m 39 s (`SCUMM_SOUND_TEST_ROOT` = `D:\Juegos\Lucas\loom ega mt32 en`, `SCUMM_INDY3_EGA_TEST_ROOT` = `indy3 ega en`, `SCUMM_MT32_ROM_ROOT` = `mt32`; a first run with the sound root set to `D:\Juegos\Lucas` itself failed five LOOM audio tests that read a LOOM folder there, as expected). Solution build: zero warnings, zero errors.

## Allocations per frame (acceptance 3)

`CutsceneList <folder> alloc <room> <object> <verb> 300`: bytes one frame allocates on the run's path, before = `AdvanceFrame` (with its snapshot, as the runs did), after = `StepFrame`.

| Game, trigger | Before | After |
|---|---|---|
| Indy 4, Room 85 object 1123 verb 0x0A | 980,662 B | 193,843 B |
| Monkey Island 2, Room 5 entry | 351,083 B | 29,851 B |
| Monkey Island CD, Room 33 entry | 673,542 B | 61,059 B |
| Zak McKracken, Room 1 object 15 verb 0x0C | 3,372,403 B | 159,150 B |
| Maniac Mansion, Room 1 entry | 235,560 B | 12,384 B |

Single thread, 1000 frames after the first 300: Indy 4 Room 85 about 0.55 ms a frame, Monkey Island 2 Room 5 about 0.2 ms; Zak Room 1 300 frames 1415 → 338 ms. The pool is faster than a fresh copy too (same binary, pool off by an experiment switch: Indy 4 552/537 → 588/611 ms, Monkey Island 2 218/177 → 378/414 ms for 1000 frames), so allocation was not the only gain. An instrumented build (not committed) put most of the remaining executor time in the per-run variable copy and sync (start and end of each run, every event), which is O(number of globals) and would need change tracking of the runtime's variables to remove; not done (decision 2).

## Timings (acceptance 3)

**Harness, eight workers** (`prun 8`, one game at a time, Release; machine shared with other agents' test runs, load sampled 6–25 %):

| Game | Before (`fb3da87b`) | After | CPU s before → after |
|---|---|---|---|
| Monkey Island 2 | 67.5 s | 75.3 s | 162 → 160 |
| Indy 4 | 87.3 s | 74.5 s | 270 → 232 |
| Monkey Island CD | 14.3 s | 9.9 s | 55 → 50 |
| Monkey Island floppy | 14.4 s | 9.1 s | 59 → 43 |
| Indy3 EGA | 13.6 s | 9.7 s | 53 → 37 |
| Indy3 256 colours | 11.3 s | 7.3 s | 48 → 33 |
| Maniac Mansion | 8.3 s | 8.7 s | 22 → 19 |
| Maniac Mansion enhanced | 44.8 s | 18.2 s | 58 → 30 |
| Zak McKracken | 54.6 s | 22.7 s | 240 → 92 |
| Zak McKracken enhanced | 53.4 s | 16.0 s | 238 → 81 |

All twelve lists in parallel (`scan run`/`loom`, twelve processes): 192.6 → 112.1 s.

**Workers** (after, `prun N`): Monkey Island 2 1/2/4/8 workers 29.9/20.3/26.1/55.6 s (CPU 32/38/72/145 s); Indy 4 2/4/8 workers 50.6/42.5/74.5 s (CPU 97/132/232 s). The CPU for the same runs grows about fourfold from one to eight workers; GC is not the cause (the harness's `rooms 8 85 86 87 88` on Indy 4: GC pauses 0.9 s of 48 s; `DOTNET_GCgen0size=0x10000000` or `DOTNET_gcServer=1` in the harness only: 59 s and 53 s, no better), nor the shared regexes (step 7). Each pooled worker warms its own copy of the game's caches, and the runs' working sets compete for the caches; the cause was not isolated further. Hence decision 1.

**Real window, first launch** (`Indy4CutsceneWindowCheck`, `Monkey2CutsceneWindowCheck`; before built from `fb3da87b`, after from this branch; private `SCUMM_EXPLORER_PROFILE_ROOT` and `SCUMM_ROOM_CACHE_DIR` in each output folder; background runs timed from play start, as in the snapshot record):

| Game | Before | After | Same list |
|---|---|---|---|
| Monkey Island 2 | 55 s (69 s from opening) | **29 s** (41 s from opening) | 536 triggers, 260 groups, 76 scenes, 165 begun |
| Indy 4 | 109 s (118 s from opening) | **68 s** (77 s from opening) | 666 / 426 / 182 / 210, verb names from Room 10 after 2 skips and 110 frames |

Target "under a minute": met for Monkey Island 2, **not for Indy 4** (68 s). What remains for Indy 4 is fourteen runs that play the full 6000 frames (Rooms 21, 29, 85–88; about 3.3 s each alone) and the per-run variable copy and sync.

## Decisions (§4.5)

1. **Four workers for disk and archive games** in the App instead of eight: measured faster for both target games, results independent of the count. Undo: the `Math.Min(4, …)` in `MainWindow.PlayCutscenes`.
2. **No change tracking of the runtime's global variables.** The executor syncs its copy with the runtime at each event and at the end, and code outside the executor writes the runtime's dictionary directly mid-run; making the sync proportional to the changes means wrapping `ScummScriptRuntimeState.GlobalVariables` (a public `Dictionary` used across Core, App and tests) or moving to indexed variables. Larger than this step and shared with the Maniac/Zak orchestrator; logged as the next lever.
3. **Results of unkept runs hold the live states and positions and only the locals** (`snapshotState: false`), as the nested-limit result already held live ones; the default stays `true` for every other caller (LOOM inspection and tests).
4. **No GC setting changed** (server GC stays the user's call); the experiments above set it only in the harness process.
5. **No engine note in `docs/engine/`**: no new SCUMM fact.

## Commands

```
rtk proxy dotnet build .verification/indy3-cutscenes-20261017/CutsceneList/CutsceneList.csproj -c Release -o <bin>
CutsceneList "<folder>" loom | scan run | prun <workers> | rooms <workers> <room>... | alloc <room> <object> <verb> <frames> [advance]
Indy4CutsceneWindowCheck "D:\Juegos\Lucas\indy4 vga en" <out>      Monkey2CutsceneWindowCheck "D:\Juegos\Lucas\mi2 talkie en" <out>
```

`alloc` now plays `StepFrame` unless `advance` is given, and times the same number of frames again. Lists, run lines and window reports stayed in the session scratchpad (their hashes and figures are above).

## User checks

1. With an empty room cache, open `D:\Juegos\Lucas\mi2 talkie en`, select Room 4 and time the gold progress line until it disappears (here 29 s from play start).
2. The same for `D:\Juegos\Lucas\indy4 vga en` (here 68 s).
3. Open a few scenes in both games and in Zak McKracken: the lists and scenes should be as before.
