# Manual browsing and explicit game intro window proof — 19 October 2026

Owned packet: source/native-input investigation and isolated actual-window harness only. Parent owns product implementation and integration. Baseline latest master `fb3da87b` was merged into this existing verification worktree. Preparation created no MainWindow; the bounded real-window runs below used only parent-frozen source and exclusive WPF leases.

## Read-only source preparation

`Program.cs source` reads only supplied native scripts, room-entry scripts and clickable geometry. It creates no App/MainWindow or session and runs no game code. `census` checks the native index actually contains nonempty global Script1, the scanned library has an actual top-level Script1 resource, and the existing profile plays through native verbs. Both the initial index census and the final resource-gate census pass all ten rows. An indexed boot script proves availability of a native start, not completion or correctness of every intro branch.

All ten supplied generic families pass this census:

| Family | Supplied folder | Script1 decoded instructions |
|---|---|---:|
| Indy3Ega | indy3 ega en | 201 |
| Indy3Vga | indy3 vga en | 159 |
| MonkeyFloppy | monkey1 ega en | 672 |
| MonkeyCd | monkey1 vga en | 1,031 |
| Monkey2 | mi2 talkie en | 1,284 |
| Indy4 | indy4 vga en | 2,041 |
| ManiacEnhanced | mmansionE ega es | 221 |
| ManiacClassic | mmansion ega en | 174 |
| ZakEnhanced | zakE ega en | 386 |
| ZakClassic | zak ega en | 384 |

Source candidates, distinguished from reached play:

- ManiacEnhanced's genuine startup uses Room45's party menu. Native clicks on objects260/263/403 select Syd/Bernard/Start; Script131 runs outdoors in Room44. Script1 loads45 at+025E and starts131 at+04B4. Existing native-party tests pin the live choice variables; the harness will deliver those real inputs, not assign them.
- Enhanced Script23 opens a cutscene at+0001, yields delay60 at+0002 and ends+0081 without a decoded beginOverride. Native Room28 telescope413 Use11 is an existing successful source scene and a sustained no-override candidate. Its clickable box is120,24/104×56; walk point96,112. This is a directly selected exploration start, not an earned route to the telescope.
- An initial doorbell candidate was rejected after decoding: Script87 DOES set beginOverride at+0025, following cutscene+001F and loadRoom26+0020. No no-override claim is made for it.
- MonkeyCd Room51 entry opens a no-override cutscene at+0039, yields at+003E and ends at+004A. The real WalkTo doorway from Room52 object621 loads51 at+0005; clickable box48,24/120×64, walk point82,80. This is a candidate for comparing actual native doorway entry with manual selection of the same room, without assigning previous-room variables.
- MonkeyCd Room35 entry has conditional previous-room33/34 scenes at+0107/+00E3; incoming WalkTo427 from33 is far right atX984, and433 from34 atX752. These are decoded alternatives, not an executed route.
- LOOM EGA's existing intro is source Script44, started by Script1+021A; its explicit Room01 intro entry remains the duplicate-header guard. Rooms56/58 remain the gap-filling selector samples.

All source outputs remain ignored under `source-*`. No game files, saves, profiles, ROMs or running apps are changed. No ScummVM is run.

## Prepared actual-window scope

`WindowProof.cs` calls real OpenGameFolder with an isolated profile/cache, immediately cancels and awaits its automatic full card batch, then retains original card controls for bounded native batches without removing GameLibrary resources. It will assert no automatic intro clock on game load, plain non-LOOM selector pixels after native cache work/cold/warm reopening, and LOOM's existing gap-filled cards. Header playback uses the actual `GameIntroButton` handler. Maniac's party selections use native clicks; manual browsing must abort its outdoor intro and preserve those selections. Telescope input and Monkey52/621→51 are actual native clicks/frames, without forced scripts, actor placement or state.

The harness observes the parent's `_catalogPreviewOnly` fallback contract. A failed bounded preparation must show the static browsed scene, hide PlayScreenImage and stop the old play clock. Such a result is recorded as a valid quiet fallback; the native-input route remains unproved and is not silently replaced by assigned prerequisites. Source and state previews are kept distinct from earned play.

## Commands and preparation validation

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer\.worktrees\zakclassic-mars-palettes-20261019`.

```powershell
rtk git merge fb3da87b
rtk proxy dotnet build .verification/manual-browse-intro-window-20261019/WindowCheck/ManualBrowseIntroWindowCheck.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy .\Exe\ManualBrowseIntroWindowCheck\Debug\net10.0-windows\ManualBrowseIntroWindowCheck.exe census 'D:\Juegos\Lucas' '.verification\manual-browse-intro-window-20261019\source-intro-all-generic'
rtk proxy .\Exe\ManualBrowseIntroWindowCheck\Debug\net10.0-windows\ManualBrowseIntroWindowCheck.exe census 'D:\Juegos\Lucas' '.verification\manual-browse-intro-window-20261019\source-intro-all-generic-resource-gate'
rtk proxy .\Exe\ManualBrowseIntroWindowCheck\Debug\net10.0-windows\ManualBrowseIntroWindowCheck.exe source 'D:\Juegos\Lucas\mmansionE ega es' '.verification\manual-browse-intro-window-20261019\source-maniac-enhanced' 1 87 131
rtk proxy .\Exe\ManualBrowseIntroWindowCheck\Debug\net10.0-windows\ManualBrowseIntroWindowCheck.exe source 'D:\Juegos\Lucas\mmansionE ega es' '.verification\manual-browse-intro-window-20261019\source-maniac-enhanced-telescope' 23
rtk proxy .\Exe\ManualBrowseIntroWindowCheck\Debug\net10.0-windows\ManualBrowseIntroWindowCheck.exe source 'D:\Juegos\Lucas\monkey1 vga en' '.verification\manual-browse-intro-window-20261019\source-monkey-cd-doorways'
rtk proxy .\Exe\ManualBrowseIntroWindowCheck\Debug\net10.0-windows\ManualBrowseIntroWindowCheck.exe source 'D:\Juegos\Lucas\loom ega mt32 en' '.verification\manual-browse-intro-window-20261019\source-loom-ega' 1 44
```

Preparation builds pass with zero warnings/errors; preparation harness build1.58s. Source census: ten native indexed boot sources pass; no native execution or UI claim is made by that census.

## Actual-window results

Eight serial real-window processes cover ManiacEnhanced, MonkeyCd, reference LOOM EGA and the short LOOM demo. Each uses its own ignored output/profile/cache. Real OpenGameFolder is used, its full automatic card load is canceled/awaited, and the full GameLibrary remains intact for the bounded card batches. Actual native clicks and clock frames drive the scripted cases. No game data, saved state, actor placement, story flags, native input handlers or previous-room variables are assigned; no ScummVM is run.

| Packet | Product source | Result / observed seconds |
|---|---|---|
| window-maniac-enhanced-attempt1 | 273cc39 | PASS20.65; party/active-intro abort, explicit restart/real native Skip, no-override telescope abort |
| window-monkey-cd-attempt1 | 273cc39 | Retained FAIL96.51; roomless native startup was discarded before its clock started |
| window-loom-ega-attempt1 | 273cc39 | Retained FAIL14.81; duplicate Intro header caused by comparing a suffixed presentation title |
| window-monkey-cd-attempt2 | ac074cb | Retained FAIL22.79; roomless Intro fixed, but a native doorway click from unfinished startup did not enter51 |
| window-loom-ega-attempt2 | ac074cb | PASS14.75; gap-filled56/58 cold/warm and no duplicate Intro |
| window-maniac-enhanced-final | ac074cb | PASS21.05; same full native party/intro/telescope assertions, with exact Script23 trace check |
| window-monkey-cd-native-ready | d2bc66b | PASS bounded UI24.10; explicit roomless Intro reaches real Room38 control without inputs;51/52 stay static; doorway route UNPROVED |
| window-loom-demo-short | 98abc9b | PASS7.37; explicit native demo director, then manual cancellation/no autoplay |

Final product source is parent App commit `98abc9b`, merged here as `b275307943acdc87e17c4df62894859abe2436fd`; the preceding guards are included. Core/test source is identical throughout the App-only corrections after `273cc39`. The parent explicitly limited reruns to affected cases: MMe and full-LOOM final windows use `ac074cb`; the additional `d2bc66b` input-handler guard applies to the Monkey case, and `98abc9b` only changes the demo path. No blanket all-family native acceptance is claimed.

Key actual assertions:

- All real loads in the table show **no intro autoplay**. Non-LOOM selectors remain exact plain background pixels after completed native initialization and cold/warm cache loads: MMe1, Monkey33/37. Their saved native bitmaps differ from the backgrounds, proving the selector policy is independent of retained native computation. Full LOOM56/58 still show their exact gap-filled native cache images.
- MMe's explicit top **Play Game Intro** starts the real45 party menu. Actual Syd/Bernard/Start inputs select native variables30/31 as1/5. Manual1 during active outdoor Script131 chooses a valid static fallback, stops the clock, keeps the source session unchanged during300ms dispatcher wait, and leaves explicit Intro restart available. The chosen party is retained.
- After an explicit fresh Intro and the same real menu inputs, the actual native Skip button at131's override returns player control. Manual1 and28 then adopt quiet native sessions. Native Use11 on telescope413 produces the exact `cutscene at Script23` trace, with no override and Skip disabled. Manual1 cancels it;180 further native frames stay in1 without an active scene/old continuation. This proves manual cancellation does not depend on an available native skip point.
- Corrected Monkey Intro starts a clock even before an indexed room exists. On the final approach, a fresh explicit native Intro runs to **first** genuine player control in38: native input script4, ego1 at256,166, visible verbs populated. Delivered inputs remain0 and no `Player input assumed` trace exists. This proves a native start and first control, not completion of every opening branch.
- Monkey manual51 conservatively remains static because entry-created watchers may begin another scene later; after real startup,52 also returns static. Both keep the old source unchanged, stop Play and leave explicit restart available. The actual native52/621→51 entry-cutscene route remains **UNPROVED**; it was not bypassed or asserted successful.
- The final raw first Monkey pause already reports inputScript4 but ego0/no actor/no visible verbs. Therefore the harness's conditional raw-input0→manual52 subcase did **not** execute; no raw52 window coverage is claimed. Its read-only state is retained. The completed-startup static52 case did execute under `d2bc66b`.
- Short demo load/manual01 does not autoplay. Static01 offers **Play Demo Preview**; its actual header action starts `_playback.Preview=True`, fresh native `_demoPreview=True` and running source director45. Manual02 cancels the preview, stops its clock, keeps the old director unchanged for300ms and stays static without autoplay. The full game's PlayOpening is never used for this demo.

Exact window invocations (same executable as above):

```powershell
# Initial273cc39 and correctedac074cb use the same assertions and per-attempt output names shown in the table.
rtk proxy .\Exe\ManualBrowseIntroWindowCheck\Debug\net10.0-windows\ManualBrowseIntroWindowCheck.exe window 'D:\Juegos\Lucas\mmansionE ega es' '.verification\manual-browse-intro-window-20261019\window-maniac-enhanced-final' '1'
rtk proxy .\Exe\ManualBrowseIntroWindowCheck\Debug\net10.0-windows\ManualBrowseIntroWindowCheck.exe window 'D:\Juegos\Lucas\loom ega mt32 en' '.verification\manual-browse-intro-window-20261019\window-loom-ega-attempt2' '56,58'
rtk proxy .\Exe\ManualBrowseIntroWindowCheck\Debug\net10.0-windows\ManualBrowseIntroWindowCheck.exe window 'D:\Juegos\Lucas\monkey1 vga en' '.verification\manual-browse-intro-window-20261019\window-monkey-cd-native-ready' '33,37' complete-intro
rtk proxy .\Exe\ManualBrowseIntroWindowCheck\Debug\net10.0-windows\ManualBrowseIntroWindowCheck.exe window 'D:\Juegos\Lucas\loom demo short ega en' '.verification\manual-browse-intro-window-20261019\window-loom-demo-short' '1'
```

Builds against `273cc39`, `ac074cb` and `d2bc66b`: zero warnings/errors,10.46/10.69/10.80s. The final demo harness rebuild found a protected `IsScriptRunning` method; its read-only query was changed to reflection, then build passed with zero warnings/errors,2.13s. This was a harness compile correction, not a product fix or a relaxed assertion.

## Retained failures, costs and limits

The first Monkey failure exposed a real App bug: GameIntro_Click discarded a legitimate native first pause without an indexed room. `ac074cb` starts the existing roomless renderer/clock and follows later native room metadata without a catalog re-entry. The first full-LOOM failure exposed a second App bug: membership `opening` starts69/includes1, so its displayed title contains `(played from Room69)` and failed an exact title comparison. `ac074cb` uses the existing opening identity/membership. Both original failed packets/captures remain intact.

The next Monkey failure is preserved separately. It tried doorway621 from a quiet52 selected before native startup had produced an ego/interface;2,500 actual frames did not enter51. No frames were delivered to the abandoned static51 source: `Frames(30)` already ran only inside the successful-live branch, and static state was checked unchanged by a dispatcher wait. The parent then added `d2bc66b` to reject prepared generic sessions without a native input handler. One different, authorized approach restarted the native Intro, ran source frames to first actual control, then selected51/52; conservative static52 prevented native input. That route stops **UNPROVED** under the two-approach limit. No third native-doorway attempt or forced recovery is scheduled.

There are **41 ignored captures**, including12 captures in the three failed packets;14 were inspected. They include the native MMe party scene, no-override telescope and static restart, corrected Monkey roomless clock/first real control and static52, corrected full-LOOM header, and short-demo native logo/director/cancellation. Process inventories before leased launches and at final completion are empty of other verification windows; no user app was stopped. **Exclusive WPF lease released.**

This packet proves the listed real UI paths and conservative fallback behavior. It does not prove actual native doorway entry playback for the blocked Monkey route, every manual room, every intro branch/edition, original-interpreter visual/timing parity, or audio listening. The parent owns the required full suite, shared records, matrix and master promotion; this worker changes only the owned harness/evidence packet.

The final harness summary now repeats an explicit UNPROVED label whenever a route was blocked; this is a logging-only clarification after the recorded runs, without changing assertions or rerunning a route. Parent separately reports the required full regression **1,684 passed,0 failed,5 skipped**,17m30s, with all12 new policy cases passed; final `98abc9b` solution build0 warnings/errors8.15s and three matrix checks passed23ms. This worker did not rerun that suite. No further routes or attempts remain authorized in this packet.
