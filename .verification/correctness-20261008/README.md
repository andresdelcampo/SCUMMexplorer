# EGA / VGA correctness audit — 8 October 2026

**Historical full-audit baseline:** subsequent focused work corrected Room 70 operands/resource boundaries, VGA v4 walking, closed portal traversal and the fresh English Rusty catalog return. The standalone fortress reveal is parked; remaining original-game comparisons are listed in [CURRENT_WORK](../../docs/CURRENT_WORK.md). Counts and raw callback classifications below belong to this audit's checkpoint.

The audit found and fixed three product issues. The checked replay catalogs are usable, but **the project cannot yet be described as fully final or proved faithful to the original game**. Room 34 reachability, incomplete EGA resources, and original-game comparison remain open.

## Fixes

1. **Native responses override static navigation possibilities, in both editions.** Double-clicking the grave in Room 04 executes the game's look sentence. Without Reading, it stays in Room 04. Previously the explorer then ran the look directly and opened its possible destination, Room 64, overriding the branch the game had taken. Playback now counts actual look/use sentences and leaves their answers intact. Doorway fallback also uses the executor's own object hit selection, so another object overlapping a doorway cannot become automatic navigation. Untouchable doorways remain labelled exploration fallbacks when no native object is selected. Native Room 03 → 04 clicks still work in both editions.
2. **VGA generic reactions no longer leak into the cutscene picker.** Splitting draft verbs before counting helper references let the same widely shared response appear as several small groups. Helpers are now counted across draft verbs; genuine distinct spell scenes remain separate.
3. **A VGA start must finish with balanced scopes before it is accepted as a completing start.** A terminated script could previously appear complete because `InCutscene` became false. Trials now also require matching end boundaries and 30 settled frames. Room 20 local 206 reads Bobbin's coordinates at +002D; the catalog has no established arrival there. The triggering object's own walk point (153,102 for object 315) provides a generic, explicitly labelled placement estimate before the direct verb. It is accepted only after a balanced completion trial. No story flag or cutscene ending is forced.

## Verified coverage

Reference data: `Games/loom ega mt32 en` and `Games/loom vga en`. Spanish comparison: `Games/ALTERNATIVE LOOMS/loom ega mt32 es`. The English EGA catalog's source-file hashes match the current game files. The Spanish files do not match that catalog, so its English recipes were not applied to Spanish data.

| Check | Result |
| --- | --- |
| Full Core suite after fixes | **833 passed, 5 skipped, 0 failed** (838 total) |
| Solution build | **0 warnings, 0 errors** |
| EGA English scripts: globals, entry, exit, local, object verbs/fallbacks | **1,427 decoded/formatted; 1,424 complete control-flow decodes** |
| VGA scripts: all the same script kinds | **1,470 decoded/formatted; all 1,470 complete** |
| EGA Spanish script comparison | **1,419 decoded/formatted; 1,417 complete; 4 invalid global resources reported** |
| English EGA catalog replay | **328/328 complete normally; 328/328 complete with Escape** |
| VGA derived replay groups after fixes | **222/223 complete normally; 222/223 complete with Escape** |
| Isolated direct object-verb sweep | **658 EGA + 662 VGA actions**; anomalies rerun to 6,000 frames |
| Real WPF Advanced Scripts | **126 assertions passed**, English EGA / Spanish EGA / VGA |
| Real WPF shared presentation/status/sound rows | **23 assertions passed**, EGA / VGA |
| EGA real-clock playback, pause, step, actors/text, room changes, Escape | **15 assertions passed** |
| New real WPF native click and playback guards | **30 assertions passed**, EGA / VGA |
| EGA Room 02 native path changes and final exit | **7 assertions passed**; three clicks follow the game's two walk-box scenes before Room 03 |

The five skipped tests are explicit diagnostic/dump tests (`LoomObjectLookAuditTests` and `LoomSceneSetupPlannerTests`). Separate audit harnesses exercised the relevant game data. The initial suite had one failure in generic-helper grouping; it is fixed. All harness reports were inspected for actual `FAIL` assertions, rather than trusting process exit codes. `Failures: 0` is a summary, not a failed assertion.

Completion here means a bounded preview reaches matching cutscene starts/ends, has no open live cutscene, and settles dialogue, movement and camera. EGA recipes also preserve their recorded room membership. This is stronger than observing that a scene began; it still carries the session's recorded estimates and unapplied presentation operations.

## Remaining gaps and limits

- **Room 34:** the VGA entry group still cannot be started by its derived crossing; the required walk boxes are not neighbours. The earlier EGA local 200 +0035 reachability question also remains open. Passing the 328 existing EGA recipes does not establish an additional unreachable source site.
- **EGA resource decoding:** English global 0 has out-of-resource control-flow targets; Room 70 locals 203/204 have truncated final instructions. Spanish global 0/51/55/116 have invalid resource lengths, and its Room 70 locals 203/204 remain incomplete. These entries are retained as unresolved source evidence rather than assigned invented meanings.
- **Direct verbs do not reconstruct prerequisites.** At 1,200 frames, EGA had 383 completed scopes, 266 settled non-scene responses, seven timeouts and two unbalanced scopes; VGA had 387 completed scopes, 261 non-scene responses, six timeouts, six unbalanced scopes and two entry-only outcomes. Longer probes resolved six EGA and two VGA timeouts. Raw EGA Room 20 object 390/391 and VGA 315/316 callbacks lack Bobbin's coordinates. EGA Room 38 object 611's look stalls from this isolated state. VGA sword responses in Room 41 need the preceding foreman scene, already supplied by the picker setup. VGA Room 64 object 757 and Room 93 object 899 direct callbacks overlap their open entry scenes and do not establish balanced independent starts; Room 93 object 900's two entry-only verbs add no target cutscene. These diagnostic callbacks are not advertised as successful native gameplay.
- **No original-game fidelity claim:** catalog arrivals, direct verbs, setup placements, seed-0 random choices and CD music-timer estimates remain labelled. Routed synthetic WPF events and headless execution do not prove physical input, independent listening, or frame-for-frame agreement with ScummVM. The audit inspected an EGA trial capture for visible actors and game-font dialogue, and verified decoded rendering/state behavior through the suite and UI checks.

## Reproduction and evidence

Run from `D:\Documents\Personal\Code Projects\SCUMMexplorer`:

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy powershell -NoProfile -File .verification/correctness-20261008/run.ps1
```

The EGA sweep takes roughly 20 minutes on this machine because each action starts an independent post-boot session. The driver fails on command failures and WPF assertions; audit classifications remain in JSON for review. It requires the local game folders and never downloads or commits game data.

Local, Git-ignored evidence:

- `results/correctness-fixed.trx`: final suite.
- `out/ega/{scripts,scenes,interactions,catalog}.json`: full English EGA run, including the matched catalog fingerprint.
- `out/vga-final/{scripts,interactions}.json`: corrected scope accounting and all direct VGA actions.
- `out/vga-fixed/{scenes,setups}.json`: completing-start validation after the Room 20 fix.
- `out/ega-es/scripts.json`: Spanish decoded blocks and invalid resources.
- `out/{ega,vga}-anomalies/anomalies.json`: longer isolated callback probes.
- `out/scripts-ui/report.txt`, `out/parity-ui/report.txt`, `out/playback-ui/cutscene-playback-check.txt`, `out/interaction-ui06/report.txt`: passing WPF reports and captures.
- `out/room02-ui/room02-exit-click-check.txt`: the native three-click path still works after the fallback correction.

Application build: `rtk proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet`, from the repository directory above. Artifact: `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`.

The changes are committed locally; no push or new chat was requested.
