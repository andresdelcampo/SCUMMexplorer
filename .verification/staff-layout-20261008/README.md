# Staff coordinates and Room 17 actor source check — 8 October 2026

The existing VGA staff alignment agrees with the **complete ScummVM 2.2.0 coordinate path** for the eight note images in the supplied English CD resources. No production correction is retained. An experimental change based on the drawing routine alone was withdrawn after the user pointed out that this already worked and the preceding opcode's coordinate adjustments were checked. The original working application was rebuilt.

## Complete native note path

[ScummVM's `o5_verbOps`, lines 2343–2369](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/script_v5.cpp#L2343-L2369) explicitly adjusts LOOM v4 note verbs 90–97 before drawing. The raw Script 18 coordinates alone are therefore not the native final coordinates.

| Verb | Raw Script 18 y | Native upward adjustment | Final native y |
| --- | --- | --- | --- |
| 5A / 90 | 175 | 7 | 168 |
| 5B / 91 | 175 | 7 | 168 |
| 5C / 92 | 174 | 6 | 168 |
| 5D / 93 | 172 | 4 | 168 |
| 5E / 94 | 171 | 3 | 168 |
| 5F / 95 | 169 | 1 | 168 |
| 60 / 96 | 168 | 0 | 168 |
| 61 / 97 | 173 | 5 | 168 |

[`drawVerbBitmap`, lines 1131–1208](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/verbs.cpp#L1131-L1208) uses x/8 and y minus the virtual-screen top. [`Gdi::drawBitmap`, lines 1785–1858](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/gfx.cpp#L1785-L1858) then starts its destination at that adjusted y. The apparent unrounded drawing coordinate does not undo the opcode's earlier adjustment. The current Explorer row alignment gives the same final coordinates for these eight supplied notes, including their click rectangles and Game Status artwork. This is a bounded comparison; it does not establish a generic native rule that all image verbs round y to a multiple of eight.

The diagnostic [Check](Check/Program.cs) constructs a reference from the actual session's verb state, applying the native opcode's eight explicit adjustments before the unchanged default compositor. It compares the live strip, all eight click-rectangle origins and the Game Status colored sprite pixels. The reference shares Explorer's resource/pixel decoder; it is a source-coordinate comparison, not an independent C++ renderer or a full ScummVM screenshot comparison. Game Status deliberately treats palette zero as transparent, so its sprite assertions compare visible nonzero colors only.

Final `out-native-path/report.txt`: **18 assertions passed, zero failed**. Detached EGA/VGA sessions and rendered Game Status windows were used; no native game input or saved Explorer profile editing is part of this harness. The rebuilt solution has zero warnings/errors. No full scene/Core audit was rerun.

The following original hashes are restored exactly:

| Image buffer | SHA-256 |
| --- | --- |
| EGA live score, indexed crop | `0300C4CEA84FA819EB9C563AA30AE3FC779368FE3941B45E98EF3E5F648E9B0B` |
| EGA Game Status score, BGRA | `085F33C04A5167365E3BBA909FB9DEA0FCBAF0AD28EA0ED8CAC53B1A97A6BA97` |
| VGA live score, indexed crop | `ECD544CC3B5D2D1AC529CDE83D1261D0E9BB4A8C3430CD7851CF5B8154151C6B` |
| VGA Game Status score, BGRA | `49EFA38B5641371F7334402043E8AA7F69717C957B8FD3B0B1AB8AE303E7E007` |

The earlier local `out-before/` and `out-after/` used an incomplete reference which omitted `o5_verbOps`. Their apparent failures/successes and uneven staff lines are **withdrawn diagnostic results**, not evidence of a production defect or an authentic source glitch. No runtime edits or incorrect regression from that experiment remain in the final patch.

## Room 17 EGA actor sizing contract

The second exact source check records English Roland EGA Room 17 entry, local 201 and global 17 in `out-actor/room17-source.txt`.

- Entry `+002B` calls global 17 with the current look variable `var[07C]`. It does not establish a universal new ego size for this room. Global 17 selects the costume through `var[077/078/079]` for look 0/1/2, sets elevation zero at `+009B`, and uses separate held/unheld animation frames based on D61. An inherited look must not be replaced by an invented room default.
- Entry `+0083` starts local 201. Its `+0010` assigns actor 2 costume `3F` and elevation zero; class assignments at `+0003` give classes 20/22. It chooses among five source positions, shows animation 6 and removes the actor again after its short frame loop. This is an intermittent actor, so a native comparison must match its visible phase, not an arbitrary settled screenshot. No global story identity is inferred from actor slot 2.
- [`Actor_v3::setupActorScale`, lines 385–401](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/actor.cpp#L385-L401) sets both scales to 255 for LOOM EGA. Its separate Indy3 workaround does not apply. Explorer's EGA look defaults to scale 255 and the old-v3 boxes carry no scale word. This check supplies no reason to change EGA scaling.

Native visual agreement of Room 17 actors/costume poses remains open. This check does not measure their native displayed pixel bounds or settle the other actor/staff/audio items in the dossier. The running Rusty session was preserved instead of switching it to another room for this comparison.

## Native session limit

The user's supplied readings establish the early Room 34 state, as recorded in [NATIVE_SESSION](../room34-state-20261008/NATIVE_SESSION.md). The other chat completed that record. This chat inspected the now-submitted console and tried **one Escape key** through documented computer use to close it before an ordinary walk. Escape was ignored; the console remained open. No walk, room change, saved-slot reload, new debugger query or state assignment was performed here. Automated letter/Enter limitations remain; do not repeat failed inputs or the fulfilled save/console-reading requests.

## Reproduction and source identity

Run from `D:\Documents\Personal\Code Projects\SCUMMexplorer`, with every shell/child command through RTK:

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/staff-layout-20261008/Check/Check.csproj -- Games .verification/staff-layout-20261008/out-native-path
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/staff-layout-20261008/Check/Check.csproj -- Games .verification/staff-layout-20261008/out-actor room17
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
```

Matching cached native source: `script_v5.cpp` SHA-256 `C572940A96AADAA6D54C9417233C1FF5322D5D435A64E428807B9F2F6CD05C79`; `actor.cpp` `766C8BEAE69FCF431BD8086DC08F7A0E861EEFF5763F66CA31616737D4B131EE`; `verbs.cpp` `4BC7938C42A63A208129E867CAF76B71D8348BE54317F12458EBB3B92CDA42C5`; `gfx.cpp` `9BD6B8F386A0F139BCDFB364798EFD318B3DAC18BBD5668E2B8F1FFF3954F439`. The first two remain in the ignored final-gap source cache; the latter two are in `../room34-state-20261008/out-comparisons/`. Source downloads, generated output, game data and saves stay out of Git.

Application: `D:\Documents\Personal\Code Projects\SCUMMexplorer\Exe\SCUMMexplorer.App\Debug\net10.0-windows\SCUMMexplorer.App.exe`.
