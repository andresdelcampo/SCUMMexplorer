# Manual browsing and explicit game intro window proof — 19 October 2026

Owned packet: source/native-input investigation and isolated actual-window harness only. Parent owns product implementation and integration. Baseline latest master `fb3da87b` was merged into this existing verification worktree. No MainWindow is launched during preparation; final windows require the parent's combined source freeze and exclusive WPF lease.

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

Preparation builds pass with zero warnings/errors; latest harness build1.58s. Source census: ten native indexed boot sources pass; no native execution or UI claim is made by that census. Actual-window results will be appended after the frozen implementation and exclusive lease are supplied.
