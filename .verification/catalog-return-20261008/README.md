# Fresh English EGA catalog return: Rusty's source prerequisites

8 October 2026. The user rebuilt/reopened the app on master, selected Room 46, returned to 34 and saw sleeping Rusty move during the ghost conversation. Closed-portal fix `ebdf3b2` covered a different entry path. This fresh catalog case was reproduced and corrected after the user explicitly approved running real source prerequisites for direct late-room selection.

## Original reproduction

The isolated raw probe creates the post-boot Core session, selects Room 46, then uses actual double-click dispatch on its hole and Room 34's portal. Nine diagnostics reproduce the missing history. They are defect-reproduction assertions, not a passing fix. The raw probe intentionally omits the new App catalog recipe to preserve that evidence.

| Capture | Frame | Room | Late D7A | Ghost D9C | Actor 3 costume | Visible actor pixels |
| --- | --- | --- | --- | --- | --- | --- |
| Catalog selection | 40 | 46 | 1 | 0 | 122 | 400 |
| Return through Script 97 | 248 | 34 | 1 | 0 | 112 | 325 |
| Conversation movement | 898 | 34 | 1 | 0 | 112 | 325 |

Source/rendered output was inspected. Room 46 entry `+0000` sets D7A; direct catalog selection skips the earlier dream route. Room 34 entry tests D9C at `+01A4`, assigns ghost costume 120 at `+01CA`, or sleeping-boy costume 112 at `+0289`. The return reaches Script 97 `endCutscene +0042`. Object 560's verb `37` can start Script 98 while DC6 is clear. Script 98 moves actor 3 without first supplying its ghost costume or D9C. Script 87 establishes D9C at `+022B`/`+0916` during the earlier dream route. No independent renderer defect was demonstrated.

## Correction

- Reviewed prerequisite operands and exact scene boundaries live in `loom-replays.json` as a catalog-start recipe, behind its existing English source-file fingerprints. The generic Core preparer knows no room/actor/flag numbers. A different edition's catalog has no such recipe; Spanish exclusion was checked in WPF.
- On a direct English Room 46 selection, the running session executes missing Awakening, Reflection, Firewood and dream stages, then visits the chosen room. These are actual source scripts. Already established ghost D9C or restored DC7 suppresses the prelude; completed Awakening/Reflection/Firewood stages are omitted by their source result variables.
- A native/game-driven arrival or an already entered room runs no prelude. Single cutscene and stated-sequence policy is preserved.
- Failure stops later source stages and cancels the target room visit; the App displays a preparation-stopped message. No ghost flag is manufactured on failure.
- The preparation note is retained in session assumptions. Direct verbs, catalog hops and the supported staff-held edit remain explicit preview assumptions; this is not a claimed original-game route. No story-bit, actor-position, walk-box or costume override is used.

The approved exception is direct late-room catalog preparation. This implementation supplies the reviewed English EGA Room 46 recipe; it does not automatically reconstruct every room or guess Spanish/VGA operands. The standalone Room 34 local-200 fortress reveal stays parked and separate from Script 98/local 206.

## Final validation

- **32 focused Core checks pass**, zero skips: six new catalog-start cases plus existing replay-sequence/first-visit checks. They cover source-established ghost pixels during actual visible conversation frames, ghost/restored preservation, existing Awakening, already-entered room and failed-stage cancellation.
- **18 real WPF assertions pass** in `out-ui-complete/report.txt`: fresh room-card selection runs the dream; native hole/portal input returns/completes the ghost scene; costume 120 and real actor overlay retained; early Room 34 remains sleeping/closed; fingerprint exclusion and App failure cancellation. The failure case changes harness recipe metadata only.
- Real WPF captures `01-return-ghost.png`, `02-conversation-ghost.png` and `03-early-closed-portal.png` were inspected. The conversation shows ghost Rusty, not the sleeping boy.
- Solution build: **zero warnings/errors**. No wholesale replay audit or full Core suite rerun.

All outputs/profile/cache files are ignored. `SCUMM_EXPLORER_PROFILE_ROOT` points this WPF process into its output folder, so it does not read/write the user's saved Explorer profile. The raw probe instantiates no App. No native ScummVM interaction, save/ROM/game-file mutation, push or replay-coverage promotion.

An initial Core picture assertion ran before the source brought actor 3 into the room and correctly found no drawn actor; the test now checks the actual visible Script 98 frames. This was a test-timing correction, not a renderer change.

## Reproduce

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/catalog-return-20261008/Probe/Probe.csproj -- 'Games/loom ega mt32 en' '.verification/catalog-return-20261008/out'
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter 'FullyQualifiedName~LoomCatalogStartTests|FullyQualifiedName~LoomReplaySequenceTests|FullyQualifiedName~LoomFirstVisitRenderingTests' --verbosity minimal
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/catalog-return-20261008/Ui/Ui.csproj -- 'Games/loom ega mt32 en' '.verification/catalog-return-20261008/out-ui-complete'
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
```

The raw output is `checks.txt`, `states.json`, `trace.txt`, `source.txt` and three Core-rendered PNGs. Separate bit defaults are reported using the executor's storage model rather than treating the public nullable story lookup as an explicit assignment. The WPF output retains actual window PNGs/report and its private profile/cache. Built app: `D:\Documents\Personal\Code Projects\SCUMMexplorer\Exe\SCUMMexplorer.App\Debug\net10.0-windows\SCUMMexplorer.App.exe`.
