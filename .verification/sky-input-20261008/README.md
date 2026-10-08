# VGA sky input from the source opening — 8 October 2026

The [earlier catalog-prepared attempts](../draft-input-20261008/README.md) lost their aim and reached Script 13's aim-first response. This checkpoint tests a different, source-supported arrival: play the supplied CD opening, then aim at the sky without a catalog visit. **Both forward and reverse casts pass, 24/24 focused input assertions.** No App/Core behavior changes or native control.

## Exact prepared route

Each cast has a separate detached Core session. `playCdIntro: true, completeBoot: false` plays the source boot/opening until settled. The existing preview assumes the skill answer and omits the draft challenge; seeded choices remain estimates. It is not a native player-completed introduction.

The opening reaches Room 02 with Bobbin at **(828,61), camera 800**. The public Game Status staff/eight-note controls are explicit preparation; no draft, story bit, costume, actor position, box or camera is edited. The own-edition Opening draft is already marked learned by that source execution; its note pattern retains the seeded-source origin.

One ordinary room click at the sky's rectangle center **(668,28)** runs Scripts 5/6/7. Bobbin reaches the object's own **(808,59)** walk point. Named and aimed targets remain **81**; observed Script 7 +0007 sees distance zero. Four ordinary note-letter keys then use the game's input/casting scripts and dispatch the expected response through Script 7 +002E.

| Independent fresh state | Note IDs / keys in this seeded run | Target response | Observed response | Draft-playing frames | Scene boundaries |
| --- | --- | --- | --- | --- | --- |
| Forward Opening | 3,1,3,2 / e,q,e,w | 81 / 3A → Script 49 | “Is it over?” | 76 | Script 49 +0041 / +02B5, one balanced scope |
| Reverse Closing | 2,3,1,3 / w,e,q,e | 81 / 3B | “I'd have to open it first.” | 105 | Zero target scenes: an ordinary refusal |

The reverse starts from its **own unopened state**, not after the forward cast. Both settle in Room 02 without an open scene, draft or movement. Frame totals including boot, waits and final quiet periods are 1,665 / 1,250; they are deterministic Core chronology, not measured original-game or WPF timing.

## Closure boundary

This qualifies **prepared Explorer sky input after the source opening**. It does not establish earned staff/note learning, physical WPF keyboard/mouse behavior, native pictures, sound, or the failed catalog-arrival path. Original failed packets remain intact and are not reclassified as successful casts. The new prefix establishes no reason to patch a source aim guard or the renderer.

The probe also reads the CD edition's own callback identities for the remaining comparison sheet. This is static source reading only: Room 08 object 180 / 38 → Script 46; Room 24 sheep 359 / 3E → Script 70; Room 41 conversation 557/558 / 36 → local 210; Room 43 cage 569 / 3A → Script 91 and orb 576 / 38 → Script 93; Room 09 feather 199 / 36,38 → Script 105. Conditions and story prerequisites still govern those callbacks; the numeric list is not a set of native input shortcuts.

## Verification and reproduction

From `D:\Documents\Personal\Code Projects\SCUMMexplorer`:

```powershell
rtk proxy dotnet run --project .verification/sky-input-20261008/Probe/Probe.csproj -- 'Games/loom vga en' .verification/sky-input-20261008/out-opening
```

The harness rebuilds with the repository failure policy. Its executable is `.verification/sky-input-20261008/out-build/Debug/net10.0/LoomSkyInputProbe.exe`; the source-gap probe now likewise has its own ignored output directory and assembly name. No shared historical `Exe/Probe` binary is needed for reproduction. Reports, guard observations, callback identities and traces remain ignored under `out-opening/`. No game/save/ROM/profile writes or interaction with running applications.

Solution verification from the same directory succeeds with **zero warnings and errors**:

```powershell
rtk proxy dotnet build SCUMMexplorer.slnx --verbosity quiet '-p:BaseOutputPath=D:\Documents\Personal\Code Projects\SCUMMexplorer\.verification\sky-input-20261008\out-solution\bin\' -p:UseArtifactsOutput=true '-p:ArtifactsPath=D:\Documents\Personal\Code Projects\SCUMMexplorer\.verification\sky-input-20261008\out-solution'
```

The verified executable is `D:\Documents\Personal\Code Projects\SCUMMexplorer\.verification\sky-input-20261008\out-solution\bin\debug\SCUMMexplorer.App.exe`. `BaseOutputPath` must be explicit because the repository's `Directory.Build.props` overrides the artifacts default. An initial artifacts-only build used `Exe`; the corrected build above verifies the isolated output. No application was launched or restarted. The source probe passes 33/33 assertions after its output change, the sky probe passes 24/24, and all 377 relative links in the nine changed Markdown files resolve; whitespace checks pass.

The original-game actor/staff, physical-input and listening tasks are now specified by edition, source phase, trigger and measurement in [the live dossier](../../docs/loom/FINAL_GAPS_HANDOFF.md). Room 34's standalone fortress reveal and Native Computer Use remain parked.
