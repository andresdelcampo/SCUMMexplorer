# Globals change tracking and the cutscene derivation, 19–20 October 2026

Follows the executor allocation record (`../executor-alloc-20261019/README.md`), whose decision 2 left the per-run copy and sync of the runtime's globals (800 to 1400 entries) as the main remaining executor cost. Behaviour is unchanged: same lists, same states, same variable order.

## Design

**The store.** `ScummScriptRuntimeState.GlobalVariables` is now a `ScummGlobalVariables` (`Core/Scumm/Scripts/ScummGlobalVariables.cs`) instead of a `Dictionary<string, ushort>`. It implements `IDictionary` and `IReadOnlyDictionary` (indexer, `TryGetValue`, `ContainsKey`, `Add`, `TryAdd`, `Remove`, `Remove(key, out value)`, `Clear`, `Keys`, `Values`, `Count`, enumeration), so every caller (LOOM's session, the generic session, the Maniac/Zak v1/v2 partials, the App, tests, harnesses that build) compiles unchanged. It keeps its own slots with the same enumeration order as `Dictionary`: insertion order, a removed entry's slot reused by the next addition, last freed first (the .NET free list). An addition during enumeration throws, as `Dictionary`'s does; value changes and removals do not. While any script run is active it records every write and removal (key, whether it existed, the value before), and the slot and addition number of each key; the record is cleared when the outermost run ends.

**The run's variables.** `ScummRunVariables` (`Core/Scumm/Scripts/ScummRunVariables.cs`) replaces the run's `Dictionary`. It has two modes:

- **Through the store** (a run whose result does not keep its variables: `snapshotState: false`, no observer, and no global named like one of the run's own keys). The run reads a global as it was at its last copy point: the store's current value, or the value before the first recorded change since that point. Its own writes and its 26 locals (and the v1/v2 indirect-read marker) are kept apart. At the old copy points it reproduces the old full copies:
  - before an event and at the end (old: every `var[`/`bit[` key of the run's copy into the runtime): writes back the run's changed globals, creates the run's new keys in the order the run created them, and puts back the value of any global the runtime changed outside an event since the last copy point (the old copy overwrote those);
  - after an event (old: every runtime global into the run's copy): new runtime keys join the run's order in the runtime's slot order; the run's own non-global keys shadowed by the runtime fall through to it.
- **Its own dictionary** (every other run, and a run through the store from the moment the order of its old copy could no longer be followed: a removal from the store or from the run, a global the runtime added outside an event, a non-global the runtime changed outside an event). Materialising builds the dictionary in exactly the old copy's order (globals the run started with in the store's slot order, then the run's first keys, then later keys in arrival order) and the run continues with the old code.

Enumerations that the run itself makes follow the old order: the v1/v2 preparation's changed keys (`InOrder`: slot, first keys, arrivals), the locals kept by a paused run (`CopyLocalsTo`), `Count`/`Keys`/enumeration (a built copy). The objects are reused per thread.

**Prints.** `print`/`printEgo` copied all the run's variables into the request (`ScummPrintRequest.Variables`) for the text conversion; the conversion happens inside `ScummMessageTable.Print`, before the run goes on. The request now carries a `PrintView` of the run's variables, detached after the print (a later read throws instead of reading another run's values; nothing reads it later). In Indy 4's Room 85 that was 2000 copies of 811 entries in 1300 frames.

## Identical results

Harness `CutsceneList` (`.verification/indy3-cutscenes-20261017/CutsceneList`), `loom` for LOOM EGA/CD, `scan run` for the ten generic families, all twelve as parallel processes, timings (` N ms`) and planner lines removed before hashing. "Master" is the harness built from an archive of master `a6c0082c` (sound polish, MI2 talkie speech and the Indy 4 credits fix merged); "branch" is this branch with that master merged (`git merge master`).

| List | Lines | SHA-256 master = branch | Same |
|---|---|---|---|
| loom-ega | 219 | 3fa9d49c4b4afeaf434a8b6c8f2bbb76488e998b29fe46bfa4d3eb034d009d08 | yes |
| loom-cd | 224 | fe21e785a275cc245ff6947bb22331ef22b23ec199bd9f9fafac4e6a2cf92974 | yes |
| indy3-ega | 156 | 5ae32d2e9e475c130ca7b862be21c01aa12fcbb153c1273c7411d1a8f3cc8bc5 | yes |
| indy3-vga | 156 | abaef470a8cce1a446279b79da595dd4bf50e20efc7f6ca6fef940b0c0e16278 | yes |
| monkey-floppy | 167 | 5bd3ac36e563a0cf787c1833cf7c5a33f3b6ceb979ab0e8e5e0699a678ffe1dd | yes |
| monkey-cd | 163 | 8a7a25562f24c967891dbb705b9dffd1643eb03663f4e3b30f4cc1b378ca78a9 | yes |
| monkey2 | 263 | f22ff558583b11cf15c0d2c39300c1b3ab639b25c6667b82b61144c4fd55b257 | yes |
| indy4 | 430 | ca7cb9d761911bf33da41c1347c7a6bccc9e0e1d69944fed462aa632d2ef8e5f | yes |
| maniac | 30 | 6ca4563399df00f874f9e9046c87a1b4701863d7bc6f2cd72a33bebf16a21824 | yes |
| maniacE | 30 | d073e2a4f16ce5f258983c8eadbdcd871e08e41b4da12be30c5b1b439c298f4e | yes |
| zak | 57 | 2c500c7d682e1e902f73d486b822341b1de13cffdc725c40b7acba2a644b0588 | yes |
| zakE | 57 | 02af26d0ea9a5e4c7ee5814c0a185d745f24f6c3b4450fefb063b33acfddf275 | yes |

These hashes differ from the executor-alloc record's because master changed every list in between (the sound clock and the merged features); both sides here are the same master. Before the merges, the first step (the store and tracked runs, without the print view and the reuse of run objects) was also hashed on the earlier master (`b1f231c5`): all twelve equal to the executor-alloc record's hashes.

## Tests

`tests/SCUMMexplorer.Tests/ScummGlobalsTrackingTests.cs`:

- the store against a `Dictionary` under 20,000 random sets, removals and clears (same pairs in the same order, `Keys`, copies);
- an addition while enumerating throws, a value change does not;
- six games (Indy 4, MI2 talkie, MI1 CD, Zak, Maniac enhanced, Indy3 EGA): the boot and four cutscene triggers played 400 frames with full copies (`ScummRunVariables.FullCopiesOnThisThread`, a test switch) and through the store: same room, states, positions, variables in order, estimated states, picks, screen messages, trace, uncertainties and pending scripts.

Full suite on the merged tree (master `a6c0082c` merged): **1724 passed, 0 failed, 5 skipped** in 7 m 43 s (`SCUMM_SOUND_TEST_ROOT` = `D:\Juegos\Lucas\loom ega mt32 en`, `SCUMM_INDY3_EGA_TEST_ROOT` = `indy3 ega en`, `SCUMM_MT32_ROM_ROOT` = `mt32`). Solution build: zero warnings, zero errors.

## Timings

Release, this machine (16 cores) shared with other agents; load sampled 3–31 %.

**Harness, four workers** (`prun 4`, the App's count for disk and archive games):

| Game | Master | Branch | CPU s master → branch |
|---|---|---|---|
| Indy 4 | 51.9 s | 26.2 s | 143 → 76 |
| Monkey Island 2 | 25.3 s | 20.6 s | 69 → 53 |
| Zak McKracken | 20.9 s | 7.4 s | 72 → 22 |

All twelve lists in parallel: 122.7 → 70.0 s.

**Single frames** (`alloc`, one thread): Indy 4 Room 85 object 1123 verb 0x0A, 2000 frames after 300: 1468 → 444 ms; Monkey Island 2 Room 5 entry, 3000 frames: 511–626 → 256–291 ms; Zak Room 1 object 15 verb 0x0C, 300 frames: 328 → 177 ms.

**Indy 4's long runs** (`rooms 4 21 29 85 86 87 88`): the 6000-frame runs went from 2.1–4.8 s each to 0.35–1.24 s (Room 85 object 1123: 3555 → 1048 ms; Room 29 object 308: 2068 → 353 ms).

**Real window, first launch** (`Indy4CutsceneWindowCheck`, `Monkey2CutsceneWindowCheck`, both built from master and from the branch; each sets a private `SCUMM_EXPLORER_PROFILE_ROOT` and `SCUMM_ROOM_CACHE_DIR` in its output folder):

| Game | Master | Branch | Same list |
|---|---|---|---|
| Indy 4 | 45 s from play start (54 s from opening) | **34 s** (41 s from opening) | 666 triggers, 426 groups, 182 scenes, 210 begun |
| Monkey Island 2 | not measurable: play started 104 s after opening, runs already done | the same | 536 / 260 / 76 / 165 |

Target "Indy 4 under a minute": met (34 s from play start, 41 s from opening; 68 s in the executor-alloc record).

Monkey Island 2's window check on the merged master starts play 104.5 s after opening on both sides, after the background runs have finished, so it no longer measures the runs (the executor-alloc record had play at about 12 s). Both sides behave the same, so the cause is in master, not here; it is listed for the coordinator (blocked item 1).

## What would shorten the long runs further

The fourteen 6000-frame Indy 4 runs (Rooms 21, 29, 85–88) now take about 1 s each and no longer dominate (Indy 4's 426 runs take 26 s on four workers). They run to the frame limit because the planner sees no end. Without changing results: (a) stop a run when two successive frames leave the session's whole state (rooms, states, positions, variables, pending scripts with their offsets and delays, messages) unchanged and no timer-driven script is waiting — a fixed point, after which the remaining frames cannot change the result; whether the reported frame count must stay 6000 decides if the list text changes; (b) the remaining per-frame cost is operand parsing (`Regex` groups are now the largest allocation) and the session's per-frame work, not the variables.

## Decisions (§4.5)

1. **The store emulates `Dictionary`'s order itself** rather than wrapping one, so the run can rank keys by slot without enumerating. The random-operations test pins the equivalence.
2. **Tracking only for runs whose result is not kept**; observed runs and kept results keep a full copy (their variables are the result).
3. **Fallback to the old copy on rare cases** (removals, runtime-added globals between events, runtime-changed non-globals between events) instead of following them through the store: exact by construction, rare in the twelve derivations.
4. **The print reads the run's variables through a detached view** instead of a copy; nothing reads the request's variables after the print. `ScummMessages.cs` (another agent's file) is not changed.
5. **A thread-static test switch** (`FullCopiesOnThisThread`) keeps the old path testable against the new one.
6. **No registry, App or GC setting changed.**

## Commands

```
rtk proxy dotnet build .verification/indy3-cutscenes-20261017/CutsceneList/CutsceneList.csproj -c Release -o <bin>
CutsceneList "<folder>" loom | scan run | prun <workers> | rooms <workers> <room>... | alloc <room> <object> <verb> <frames>
Indy4CutsceneWindowCheck "D:\Juegos\Lucas\indy4 vga en" <out>      Monkey2CutsceneWindowCheck "D:\Juegos\Lucas\mi2 talkie en" <out>
```

Lists, run lines and window reports stayed in the session scratchpad (hashes and figures above).

## User checks

1. With an empty room cache, open `D:\Juegos\Lucas\indy4 vga en`, select Room 4 and time the gold progress line until it disappears (here 34 s from play start).
2. Open a few scenes in Indy 4, Monkey Island 2 and Zak McKracken: the lists and scenes should be as before.
