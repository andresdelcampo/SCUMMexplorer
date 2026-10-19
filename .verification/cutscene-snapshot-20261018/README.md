# Cutscene derivation from one boot (cutscene snapshot), 18–19 October 2026

**What changed.** The script-derived cutscene list of every generic-session family (Indy3 EGA/VGA, Monkey Island floppy and CD, Monkey Island 2, Indy 4, Maniac Mansion and Zak McKracken, both editions each) ran each entry from a fresh boot (`ScummGameSession.BootForPlay`), one boot per entry. Now:

1. **`ScummBootSnapshot`** (`src/SCUMMexplorer.Core/Scumm/Session/ScummBootSnapshot.cs`): `Of(session)` keeps a deep copy of a booted session; `Start()` gives a new, independent session in that state. The copy is generic (fields by reflection, identity and cycles kept), so it covers everything the session owns without a per-game or per-field list: variables and bits, script slots with instruction pointers and locals, cutscene and override stacks, actors, objects and states, loaded room, camera, verbs, messages, sound clock and iMUSE/CD timelines, the seeded `Random` and its picks, pending input, trace, uncertainties and every per-session cache (the data source's own caches included). **Shared, not copied:** the `GameLibrary` and everything reachable from it (archives, disks, disc image, speech bank: the readers), strings, reflection objects and field-less objects (comparers, lambda caches). Delegates are rebound to the copied targets (`ActorFromPosition`, `PlayVerbName`, the speech-length closure). Dictionaries and sets whose keys hash by identity are rebuilt with the copied keys; all others keep their copied buckets, so iteration order is unchanged. Operating-system and threading objects (streams, handles, tasks, `ConcurrentDictionary`) outside the game are refused with an exception, never copied silently.
2. **`ScummBootSnapshotPool`** (same file): for a disk or archive game, whose workers must not share a reader. A run takes a free snapshot or boots a new one over its own copy of the game, and returns it when done: at most one boot per run going on at once (8), whatever threads the runs land on.
3. **`ScummCutscenePlanner`** takes `ScummCutsceneStart`: `SharedSnapshot` (default: one boot, copies on every thread; file-per-room games and the Maniac families through `ManiacCutsceneSetup.Planner`), `SnapshotPerWorker` (the pool; the App for `V4Archive`/`V4Disks` games), `BootPerRun` (as before; kept for comparisons). The verb names still come from a copy of the boot (`UntilAVerbIsOn` runs on that copy, never on the snapshot).
4. **App** (`MainWindow.PlayCutscenes.cs`): disk/archive games use the pool; the planner's own copy of the game serves the first pooled boot (one scan of the game fewer); entries are handed to the workers one at a time (`Partitioner.Create(..., NoBuffering)`), because the default split gave each worker a fixed range and the worker that drew the long scenes held the others back.
5. **`getActorFromPos` keeps its costumes** (`ScummGameSession.ReadCostumeOnce`): the session re-read and re-parsed every drawn actor's costume on each call; Indy 4's interface script asks it every frame. That was 3.2 of the 4.2 MB a frame allocated in an Indy 4 run (Room 85 object 1123 Talk to: 4,182,629 → 957,718 bytes a frame), and with it eight long runs together were no faster than one after another (Rooms 85–88, 16 runs: 64 s on one worker, 68 s on eight, 264 GB allocated). Costumes are read-only data (the animation state is the actor's); a read that fails still throws and is not kept.

## Identical results (acceptance 1)

`CutsceneList` (`.verification/indy3-cutscenes-20261017/CutsceneList`): `loom` mode for LOOM EGA/CD, `scan run` for every generic family (the Maniac families now through `ManiacCutsceneSetup.Planner`, as the App). "Before" is the harness built on master `80ae6fbf` (+ merge `defbf54e`) with the old planner; "after" is the final code (snapshot, pool, one-at-a-time, costume cache). Timings (` N ms` and the planner line) removed before hashing. Fixtures under `D:\Juegos\Lucas`: `loom ega mt32 en`, `loom vga en`, `indy3 ega en`, `indy3 vga en`, `monkey1 ega en`, `monkey1 vga en`, `mi2 talkie en`, `indy4 vga en`, `mmansion ega en`, `mmansionE ega es`, `zak ega en`, `zakE ega en`.

| List | Lines | SHA-256 before | SHA-256 after | Same |
|---|---|---|---|---|
| indy3-ega | 156 | 08516a67cfb2a93d81217ab4f5cd8f2489ebe4279f368550d2e828edb236c767 | 08516a67cfb2a93d81217ab4f5cd8f2489ebe4279f368550d2e828edb236c767 | yes |
| indy3-vga | 156 | 4396c8c794bbf30d9a9af98377db55c6729cae84b565a62492d21f4b23fe4f34 | 4396c8c794bbf30d9a9af98377db55c6729cae84b565a62492d21f4b23fe4f34 | yes |
| indy4 | 430 | 882f873926202eb3868190371d82c39f940b271c7407da9b937949522f8da9fe | 882f873926202eb3868190371d82c39f940b271c7407da9b937949522f8da9fe | yes |
| loom-cd | 224 | e131389f80d7e8995909dda268997b339b1346e2942370cf41ce4369b995800d | e131389f80d7e8995909dda268997b339b1346e2942370cf41ce4369b995800d | yes |
| loom-ega | 219 | 79236d5a8204e1f0ef6f53002c44ea20c02bb9d3cc7a49ef09de75ceda426341 | 79236d5a8204e1f0ef6f53002c44ea20c02bb9d3cc7a49ef09de75ceda426341 | yes |
| maniac | 30 | fcd55cc97e2a86dcc02396d0d68baad55500805e0a2f4c544522c3011c5884c1 | fcd55cc97e2a86dcc02396d0d68baad55500805e0a2f4c544522c3011c5884c1 | yes |
| maniacE | 30 | 10d7cdaa6fea93589d96c0425a964216813aaed8d8b1123fa8ef97bf55ad1ee2 | 10d7cdaa6fea93589d96c0425a964216813aaed8d8b1123fa8ef97bf55ad1ee2 | yes |
| monkey-cd | 163 | 6b4b5cea33ebe6d658863688c30e8d439def1af7085f25e746ed4bde0d5aceaf | 6b4b5cea33ebe6d658863688c30e8d439def1af7085f25e746ed4bde0d5aceaf | yes |
| monkey-floppy | 167 | 63da19ac3c4c21614f371d18da8243205b7e3d80452afa19d1ed384394707eed | 63da19ac3c4c21614f371d18da8243205b7e3d80452afa19d1ed384394707eed | yes |
| monkey2 | 263 | e68d13a528de8cdeeaaca132cf02e1d514b9e7540528ecb21dbcb24c7216b596 | e68d13a528de8cdeeaaca132cf02e1d514b9e7540528ecb21dbcb24c7216b596 | yes |
| zak | 57 | bfd5397b72941e8c048a4df21c7dc7972b41dd5c5aa5bfdce386d569eab285cd | bfd5397b72941e8c048a4df21c7dc7972b41dd5c5aa5bfdce386d569eab285cd | yes |
| zakE | 57 | bf4f2524dcdb9aa6b41de974ec7c58997c669d8ae459a73391d265d8b9aa3d8c | bf4f2524dcdb9aa6b41de974ec7c58997c669d8ae459a73391d265d8b9aa3d8c | yes |

The same table matched after the first commit (snapshot only) too. The eight-worker paths (`prun 8`: the pool, as the App) give the same per-run lines as the old code's eight-worker runs and as a boot per run (header line removed):

| prun | SHA-256 (old code = pool = boot per run) |
|---|---|
| monkey2 | 3b732adb19a0bc11e8515b14e555cd1f55ab52d9698c40055d8a6313b25878b0 |
| indy4 | 8af2cea97008c6d7429ad2b737077be68ebf6f2850b99f78ea97d84c8eed740e |
| monkey-cd | 8630833137a12a666e6003a685f9943c5df98fde65097f7014ed55a5fde2b64a |
| monkey-floppy | 04c20e7750a6c6a479da066d706c08ff790f1e0f0caa0a3f1f3aa7f6e5a59f68 |
| indy3-ega | 128eb66af6b52a52a0dec4d4577895b8e0f2d906ac5f0e799d2e9e647867f6fa |
| indy3-vga | 33a3acc694a8eb3f72069ead0449867df35f9afbc3d2b469aa3380b45b3daa18 |
| maniac | e85a199460642b4436ae1fd9b9c1970d01904efd5452e1ff98b8ae1fd3b6db5e |
| maniacE | f28f22eb9a56df6fbd78b28dd04f4eefa86f3983a1d6f15cc7fdb5349bb5f1cc |
| zak | 4f24eaf66f1c32b7b5d7794f5082ec45d9e91fb4fcd5ae881f50aae67bd50254 |
| zakE | 2c79d8f772e25d1b1c8810107099e5f178a41ece89bb2adf658069b2de915f96 |

(This table is from the pool before the costume cache; the final `prun monkey1 ega en` was compared again after the last App change: same lines.)

## Unit tests (acceptance 2 and 4)

`tests/SCUMMexplorer.Tests/ScummBootSnapshotTests.cs`: for Monkey Island CD, Monkey Island 2, Indy 4 and the Monkey floppy disk set, six entries spread over each list run once after their own boot and once from a copy of one boot: same `Begun`, `Frames`, `RoomsEntered`, `Failure`, final room and the **whole session trace, line for line**; four copies of one snapshot, 200 frames each on several threads, equal the booted session after the same 200 frames (the snapshot is not changed by its copies nor by the original going on); the pool (4 workers, 24 entries, Monkey floppy) gives the same results as a boot per run with at most 4 boots.

`rtk proxy dotnet test ... --filter "Cutscene|Play|Session|Boot|Support|Loom|Snapshot|Actor"` (with `SCUMM_INDY3_EGA_TEST_ROOT`, `SCUMM_SOUND_TEST_ROOT`, `SCUMM_MT32_ROM_ROOT`): **1108 passed, 0 failed, 5 skipped** (final code before the master merge); after merging master, `Snapshot|CutsceneTests|Support`: 151 passed, 0 failed. Build: zero warnings, zero errors.

## Timings (acceptance 3)

The machine was shared with the two S9 agents' test runs during every measurement (CPU load sampled at 30–80 %; one sample run was cut by a ten-hour suspend and is not used), so wall times vary by up to 2×; CPU seconds are steadier. All `prun 8` (eight workers, as the App), Release.

| Game | Entries | Old code, wall (two runs) | Final, boot per run (same binary) | **Final, pool** | CPU s boot → pool |
|---|---|---|---|---|---|
| Monkey Island 2 | 260 | 100.8 s / 190.6 s | 87.3 s | **55.5 s** (48.2 s before the costume cache) | 289 → 134 |
| Indy 4 | 426 | 126.5 s / 282.2 s | 104.9 s | **69.9 s** | 310 → 220 |
| Monkey Island CD | 160 | 32.0 s / 37.5 s | 18.9 s | **11.5 s** | 88 → 52 |
| Monkey Island floppy | 164 | 33.0 s / 35.4 s | 30.2 s | **10.9 s** | 140 → 44 |
| Indy3 EGA | 153 | 40.8 s / 44.4 s | 34.7 s | **10.3 s** | 148 → 38 |
| Indy3 256 colours | 153 | 23.8 s / 25.1 s | 24.5 s | **8.6 s** | 103 → 37 |
| Maniac Mansion | 27 | 9.8 s / 10.0 s | 13.2 s | **7.3 s** | 37 → 18 |
| Maniac Mansion enhanced | 27 | 33.4 s / 39.2 s | 42.7 s | **31.7 s** | 65 → 42 |
| Zak McKracken | 54 | 79.0 s / 86.0 s | 88.8 s | **56.7 s** | 330 → 253 |
| Zak McKracken enhanced | 54 | 105.8 s | 100.8 s | **55.1 s** | 380 → 249 |

(The old-code `prun` of Maniac/Zak used the plain planner; the final one too, so the comparison holds.) Single thread, side by side (`bench`): Monkey Island 2, 32 entries: 23.0 s with a boot each, 5.7 s from copies (a boot 0.55 s, a copy 5.6 ms); Indy 4, 42 entries: 20.6 s → 13.6 s (a boot 0.17 s, a copy 1.7 ms). `snap`: boot / keep the snapshot / one copy: Monkey Island 2 2148 / 34 / 10.2 ms, Indy 4 885 / 29 / 5.0 ms, Monkey CD 1262 / 16 / 3.9 ms, Indy3 EGA 2033 / 15 / 3.7 ms, Maniac 1223 / 11 / 1.7 ms. A server GC (`DOTNET_gcServer=1`) would take Indy 4's pool from 100 s to 68 s on that loaded machine; not enabled (decision 4).

**Real window, first launch** (`Monkey2CutsceneWindowCheck`, `Indy4CutsceneWindowCheck` from their S7 records; private `SCUMM_EXPLORER_PROFILE_ROOT` and `SCUMM_ROOM_CACHE_DIR` inside each output folder; play running in Room 4; background runs from play start):

| Game | Before (S7 records) | Snapshot + one at a time | + costume cache | **Final** |
|---|---|---|---|---|
| Monkey Island 2 | 140 s, 174 s | 95 s | 72 s | **70 s** (86 s from opening) |
| Indy 4 | 185 s | 201 s (stalled 70 s on Rooms 85–88) | 140 s (planner start 30–40 s under 50–60 % load) | **129 s** (143 s from opening) |

The window lists were the same (MI2: 536 triggers, 260 groups, 76 scenes, 165 begun; Indy 4: 666 / 426 / 182 / 210, verb names from Room 10 after 2 skips and 110 frames).

**Target not reached for the window:** "well under a minute" for MI2 and Indy 4 is not met on this shared machine (70 s and 129 s). The boots are no longer the cost: what remains is the runs themselves, mostly allocation. An Indy 4 frame still allocates about 0.96 MB, chiefly the script executor's result for each script run (`ScummBoundedScriptExecutor` line ~1530 copies the object states, positions and known variables into a new `ScummBoundedScriptResult` every time) and the per-frame `Snapshot` record `AdvanceFrame` returns (143 KB, unused by the cutscene run). Indy 4's tail is ten runs in Rooms 85–88 that each play the full 6000 frames (about 10 s alone).

## Decisions (§4.5)

1. **A generic reflection copy, not a hand-written `Clone` per class.** Options: per-type copy constructors across the session, runtime, actors, verbs, messages and audio state (dozens of classes, easy to miss a field as features land) or one field-by-field graph copy with an explicit shared set. Chosen: the graph copy (always complete; refuses OS/threading objects loudly). Undo: `ScummCutsceneStart.BootPerRun`.
2. **Per-worker boots for disk/archive games** (the pool) rather than one boot whose copies are re-pointed at each worker's reader: re-pointing needs a structural map between two scans of the game; eight boots cost about one boot of wall time in parallel. Undo: as above.
3. **Play start does not reuse the snapshot.** The play boot (`MainWindow.Play`) starts at the same moment as the derivation, needs the indexed object snapshot and, for disk/archive games, must not share a reader with the workers; its one boot is not the bottleneck. Logged, not changed.
4. **No server GC in the App.** It helped the harness (Indy 4 pool 100 s → 68 s) but changes the whole WPF app's memory behaviour (a heap per core on a 32-thread machine); proposed for the user instead.
5. **No change to the executor's per-script result copies** in this stage: shared engine code that the parallel S9 agents' games depend on; written down as the next step for the remaining time.
6. **No engine note in `docs/engine/`:** no new SCUMM fact; the snapshot and the costume cache are implementation facts recorded here and in the code.

## Commands

```
rtk proxy dotnet build .verification/indy3-cutscenes-20261017/CutsceneList/CutsceneList.csproj -c Release
CutsceneList "<folder>" loom | scan run | prun 8 [boot] | bench <n> | snap | rooms <workers> <room>... | alloc <room> <object> <verb> <frames>
Monkey2CutsceneWindowCheck "D:\Juegos\Lucas\mi2 talkie en" <scratch>\win4\monkey2
Indy4CutsceneWindowCheck "D:\Juegos\Lucas\indy4 vga en" <scratch>\win4\indy4
```

Outputs were kept in the session scratchpad (not committed: lists of 30–430 lines per game; their hashes are above).

## Uncertainties kept

- Timings on a shared machine; the user's own first-launch time should be checked (below).
- The master merge (`6359b881`) brought S9 and Maniac/Zak Core changes after the hash comparison; the snapshot, cutscene and support tests pass on the merged code, but the twelve lists were not re-hashed on it (no old-code baseline exists for the merged base).

## User checks

1. With an empty room cache, open `D:\Juegos\Lucas\mi2 talkie en`, select Room 4 and time the gold progress line under the game until it disappears (here 70 s; before about 2.5 minutes).
2. The same for `D:\Juegos\Lucas\indy4 vga en` (here 129 s; before about 3 minutes).
3. Open a few scenes from the picker in both games and in Monkey Island CD: the list and the scenes should be as before.
