# ZakClassic palette choices: actual-window check

19 October 2026. ZakClassic S2 palette-context UI acceptance, with Classic Maniac and ZakEnhanced regressions. Fixtures are `Games/zak ega en`, `Games/mmansion ega en` and `Games/zakE ega en`, rooted at `D:/Juegos/Lucas`. Support cells are unchanged; these are conditional source/opening/room-preview colours, not original-game visual acceptance. Canonical facts belong in the parent's [Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md).

Private profiles/cache and serial WPF processes only, under the parent orchestrator's explicit exclusive WPF lease. The harness observes the actual MainWindow; no native game state is assigned. It preserves the stopped native Play snapshot, actors, messages, inputs and frame while inspecting source-derived costume colours.

The Zak mode checks asynchronous cold loading, cancellation of an obsolete selection, native image pixels and default restoration for costume 32 (estimate 7 / reached opening 0) and costume 13 (estimate 11 / room-preview 0). It captures all rendered pose galleries, selected/open plain labels, Normal/Advanced evidence visibility and compact 900×640 layouts. Every gallery's pose count, individual image dimensions and expanded pixels are compared with the direct Core decoder using the selected source colour. A visible difference is required when that native artwork uses the changed channel. The `other` mode checks that Classic Maniac and enhanced v2 keep the native gallery without Zak's selector; Classic Maniac specifically verifies Dave's costume 3 with the original suit colour 6, guarding against an accidental fallback to an unspecified colour.

## Commands

Run from the agent worktree; all shell commands use RTK. The parent first integrated provider `97bd82ae` and App `166f6b2`; the final windows include the reviewed App label correction `96dfb06` and the parent's subsequent master merge.

```powershell
rtk proxy dotnet build .verification/zakclassic-palette-ui-20261019/WindowCheck/ZakPaletteWindowCheck.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet Exe/ZakPaletteWindowCheck/Debug/net10.0-windows/ZakPaletteWindowCheck.dll "D:\Juegos\Lucas\zak ega en" .verification/zakclassic-palette-ui-20261019/window-zak-classic-final zak 1
rtk proxy dotnet Exe/ZakPaletteWindowCheck/Debug/net10.0-windows/ZakPaletteWindowCheck.dll "D:\Juegos\Lucas\mmansion ega en" .verification/zakclassic-palette-ui-20261019/window-maniac-classic other 44
rtk proxy dotnet Exe/ZakPaletteWindowCheck/Debug/net10.0-windows/ZakPaletteWindowCheck.dll "D:\Juegos\Lucas\zakE ega en" .verification/zakclassic-palette-ui-20261019/window-zak-enhanced other 1
rtk proxy dotnet run --project .verification/zakclassic-palette-ui-20261019/CoreCheck/ZakPalettePixelsProbe.csproj -p:NuGetAudit=false --verbosity quiet
```

Run these sequentially, never together. Normal user profiles are untouched. Generated reports/captures stay local and ignored. Original-interpreter comparisons and full Mars play remain unproved; the combined checkpoint and canonical source facts belong to the parent records.

## Results

The final harness build has **zero warnings/errors**, 11.95 seconds. **Three serial actual-window processes exited zero** and end in `PASS`:

| Fixture | Actual window acceptance | Native clock/state | Captures |
|---|---|---|---:|
| ZakClassic | Costume 32: all 28 poses, both source colours, direct Core pixel equality, default restoration; costume 13: all eight poses, real changed pixels, direct Core equality, restoration; plain selected/open Normal labels, Advanced provenance, cancellation/stale-result guard and both compact layouts | Frame 2; exact snapshot/actors/messages/input preservation | 15 |
| ManiacClassic | Dave costume 3: all 21 native poses/pixels match original suit 6; selector hidden in Normal and Advanced | Frame 3; exact state preserved | 1 |
| ZakEnhanced | Costume 13: all eight native poses/pixels match unchanged Core colours; selector hidden in Normal and Advanced | Frame 2; exact state preserved | 1 |

Accepted total: **17 captures**. Eight were visually inspected by the worker: ZakClassic costume 32 compact Normal, dropdown and Advanced; costume 13 default Normal, compact alternate and Advanced; and both other-family native-palette captures. Every decoded image/count also has the automated individual-pixel/dimension comparison. The normal dropdowns contain plain labels, with source and estimate details restricted to Advanced. All native state/clock assertions pass.

The final cold/cancelled load took **127.57 seconds**, with **1,958 dispatcher heartbeat ticks** and a largest observed gap of **390 ms**. The first cancelled costume cannot replace the latest costume's choices. This is an observed responsive asynchronous run on this machine, not a performance guarantee. No script/frame cap was increased and no palette or story state was assigned.

The WPF lease is released after the third process; no harness window remains active. The parent owns the required whole-solution build/full suite and clean master promotion.

## Failed evidence retained and diagnosed

The initial ZakClassic process exited 1; its four captures and report remain in separate `window-zak-classic`. Its cold/cancel load completed in 130.18 seconds, with 2,006 heartbeat ticks and a largest gap of 983 ms. The failed image showed a real product defect: a private record's `ToString` appeared in Normal instead of its plain label. Parent fixed presentation in `96dfb06`; final selected/open text checks and inspected captures pass without changing state guards.

The initial harness also demanded that changing costume 32's suit must change its pixels. A separate direct-Core diagnosis shows that assumption was false for the supplied artwork: costume 32 has **28 poses / 7,968 stored pixels and zero changed expanded pixels** for suit 7→0. Costume 13 has **eight poses / 1,536 stored pixels and 548 changed expanded pixels** for suit 11→0. The corrected acceptance compares every image with the direct native decoder for both selections, preserves genuine unused-channel equality, and still requires the real visible difference for costume 13. No image was recoloured by hand and no actual pixel/state/provenance guard was weakened to accept a discrepancy.

## Comparison and user checks

This checks the actual Explorer window against its independent source-state context and direct Core decoding. The engine-source comparison is in the [core context record](../zakclassic-palette-core-20261019/README.md) and [Mars source record](../zakclassic-mars-palettes-20261019/README.md). ScummVM was not executed. No original-interpreter appearance, full Mars route, timing or listening acceptance is claimed. No user action is required to finish these bounded window checks; remaining original-runtime acceptance stays in the parent's checkpoint.
