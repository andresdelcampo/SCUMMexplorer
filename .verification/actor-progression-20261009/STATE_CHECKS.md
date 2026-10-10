# Actor state checks, 9 October 2026

The early source-inventory `Probe/` (217/219/216 ego sites and ten level sites per edition, reviewed as an inventory, never correctness assertions) was deleted on 10 October once the work below superseded it. `Review/` adds own-edition entry scripts, relevant close-up scripts and lesson call sites in separate ignored output.

## Edition story controls

VGA Game Status could not change its own ghost bit: the story range reader called the EGA-only schema overload and fell back to D72, while the scanner also had a literal D72 threshold. The actual CD learned flag is bit 009; the corresponding story range begins at 012. English EGA uses learned D68 / story D71; Spanish uses learned D69 / story D72. The status schema now carries its own measured shift, and both the event scanner and edit boundary use it. No story bit or costume is automatically assigned.

Three edition regressions check that the story selectors are listed/editable, that an explicit ghost choice selects the actual source costume, and that restoration removes Rusty. Own Room 34 entry selectors are D9C/DC7 (English), D9D/DC8 (Spanish), and 03D/068 (CD). These are prepared exploration choices, not earned history.

`Check/` reports **168/168 assertions** in `out-actors-final/report.txt`:

- Staff held/absent in Rooms 05, 17 and 37, three editions. Actual Script 17 frame branches and stored CD costumes are checked; Bobbin remains visible after the refresh.
- Sleeping, ghost and restored Rusty from each edition's own Room 34 selectors. A ghost costume alone does not make actor 3 present: the source conversation visibility gate remains in effect. The established catalog-return route already checks the visible ghost during actual conversation and was not repeated.
- Twelve source sphere/scythe close-ups across the three editions and both staff choices. The actual close-up room is reached, scopes finish balanced, and Bobbin is present and visible afterwards with possession retained.

The failed `out-actors/` and `out-actors-after/` packets are retained. The first found VGA's rejected story edit; the second had harness expectations reversed for EGA staff frame numbering and expected the ghost to be visible before its source conversation gate. Those expectations were corrected from own source; no renderer patch was justified.

These checks qualify bounded source state and exploration starts. They do not measure native opaque bounds, physical input, inherited original-game history, or audio. Room 43 stays accepted; Room 09/17 native size comparisons remain in the final-gap reference.

## CD single-scene preservation

`PlayCdCutscene` replaced the session whenever a derived setup was needed. The own-source Room 41 sword/Blunting trigger demonstrates this: `out-ui-before/report.txt` fails session identity and retention of eight chosen notes. The correction keeps the running session and carries the exact variable/value assigned by each own-source prelude. An already established result omits that prelude, including nested setups. Repeated stages are filtered against the state before the set begins so a deliberate second source execution is retained when initially needed. Unknown results and placement/box estimates are preserved.

The real nonactivating, muted WPF check passes **6/6 assertions** in `out-ui-final/report.txt`: settled preparation, actual derived sword setup, retained session, eight notes, explicitly unlearned Transcend and chosen ghost state. These are prepared choices for the actor/state test; they earn no progression credit. `out-ui-after/` preserves the first run that hit the separately corrected VGA story-edit rejection.

Two Core regressions verify that the actual foreman conversation establishes its own prelude result, that already earned preparation is not repeated, and that later notes/draft choices survive. Missing repeated stages and unknown results remain. The combined focused suite passes **64 tests / 3 pre-existing diagnostic skips**, with no full replay audit rerun.

The UI harness uses only its private `profile/` and `cache/`, opens an offscreen window without activation, and disables sound. It does not send OS input or operate ScummVM. The user's running applications and profile remain untouched.

## Reproduction

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/actor-progression-20261009/run-check.ps1 -OutputName out-reproduction
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/actor-progression-20261009/run-ui.ps1 -OutputName out-ui-reproduction
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/actor-progression-20261009/run-regressions.ps1
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/actor-progression-20261009/run-build.ps1
```

The executable is `.verification/actor-progression-20261009/out-check-build/bin/debug/LoomActorStateCheck.exe`. Game data is read only. No App/user profile or native game is opened by this Core harness; build and trace outputs are private.

The isolated Release solution build also succeeds with zero warnings/errors. `run-build.ps1` supplies private `BaseOutputPath` and `ArtifactsPath`; the resulting App is `out-solution-build/bin/release/SCUMMexplorer.App.exe`.
