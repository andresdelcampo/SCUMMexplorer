# Final-gap investigation — 8 October 2026

This is a focused follow-up to the [correctness audit](../correctness-20261008/README.md). Its baseline remains historical evidence, not a rerun of every replay. All commands run from `D:\Documents\Personal\Code Projects\SCUMMexplorer` through the required RTK wrapper. Game bytes, user saves and ROMs are unchanged.

## Room 70: corrected operand grammar

The reported truncations in EGA Room 70 locals 203/204 were a decoder defect. Opcode `A7` was consuming only the save/load selector. It then interpreted the nested filename and string-range commands as bytecode. The final string range is `02 09 14 FF`: strings 9–20, followed by the close-file command. `14` is an operand here, not a print opcode. The next `A0` is the real script stop.

The matching bundled executable is ScummVM 2.2.0 (14 September 2020). Its [v3 opcode setup](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/script_v3.cpp#L31) inherits the [v4 save/load-variable parser](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/script_v4.cpp#L143). The reader now consumes that nested grammar, including variable/string ranges, filename, and its three termination forms. Readable output retains those operands. This is decoding/presentation support; it does not implement save-file I/O in the bounded executor.

Both locals are byte-identical in the five checked local EGA folders: reference English Roland, plain English, Spanish Roland, plain Spanish, and Spanish v1.2. These are local comparison copies, with no claim of independent provenance. English starts are `0x101A` / `0x102F`; Spanish starts are `0x1146` / `0x115B`. Lengths are 21 / 72 bytes. Their code SHA-256 values are respectively `C555C72A383F71C49EAD34EB0386312F49DB84FB72395CB1C401F0652EF79E8E` and `0FC9BE95522ADEB6DD231EB87A7053657A492F9C3BB173E6525DA278D1EFCE03`.

Validation: 67 focused Core checks passed, including real English/Spanish Room 70 locals and malformed-operand regressions. All-script formatting checks now report English EGA **1,427 / 1,426 complete**, Spanish EGA **1,419 / 1,419 complete with four read errors**, and VGA **1,470 / 1,470 complete**. English global 0 remains an incomplete interpretation of an interior index entry. These counts include index entries and do not establish that every decoded resource is a valid native script.

## Resource-boundary evidence

The existing `LflResourceChain` classifies these entries by walking the containing LFL resources. This is stronger evidence than their apparent length or accidental opcode decode.

| Reference edition | Entry | Interior owner / displacement |
| --- | --- | --- |
| English Roland | 0 | Costume 1 at `01.LFL:0x70D7`, +11,568 bytes |
| English Roland | 116 | Script 114 at `02.LFL:0x6F37`, +48 bytes |
| Spanish Roland | 0 | Costume 1 at `01.LFL:0x718C`, +11,387 bytes |
| Spanish Roland | 51 | Script 49 at `11.LFL:0x449D`, +728 bytes |
| Spanish Roland | 55 | Room 18 at `18.LFL:0`, +16,891 bytes |
| Spanish Roland | 116 | Script 114 at `02.LFL:0x6F4D`, +26 bytes |

All four Spanish entries are also interior entries in the plain and v1.2 folders. The English plain copy has interior entries 0/55; its 116 instead starts a resource. ScummVM's [runScript](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/script.cpp#L39) returns immediately for script 0. Its [old-bundle resource loader](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/resource.cpp) uses the indexed offset/length; it does not validate these entries as standalone resource starts. No literal start/chain call to 0/51/55/116 was found in the checked reference EGA blocks. Dynamic targets are still a limit of that caller check.

The evidence supports stale/interior index entries, not a finding of damaged game copies. English Roland 116 additionally needs annotation protection: it happens to decode completely, although its index points into Script 114. A fingerprint of that accidental decode alone is insufficient evidence for a reviewed title.

## EGA straw look: prerequisite established

The raw Room 38 object 611 look starts Script 87 and stalls in Room 37. At 1,000, 3,000 and 6,000 frames its continuation is `+0x0466`, reading actor 5's walk box; `+0x046A` waits until it is 2. Actor 5 is settled at `(243,101)` in box 5.

Room 37 entry checks `bit[0xD99]` at `+0x0067`. With the bit clear it hides boxes 2/3/4/6 and starts the first-visit workers/monitor. Those boxes are still hidden in the raw trace. The straw callback skips the preceding Firewood sequence. Running the app's existing disclosed `FirewoodSteps` instead completes Script 86 and sets D99 through the game code. The following straw look completes Script 87 with **1 target start / 1 target end**, ending at `+0x0984` in Room 38 with no open cutscene. No story bit or actor destination was forced. This establishes a prepared bounded route; it does not independently verify original-game timing.

## Room 34 and native comparison

English EGA, Spanish EGA and VGA each independently retain the same monitor offsets: current-box test `+0x0020`, previous-box test `+0x0027`, camera test `+0x002E`, cutscene `+0x0035`. Their written story bits differ: English `DCF`, Spanish `DD0`, VGA `070`. The EGA historical offset is now checked against both actual language resources.

The matching ScummVM [actor factory](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/scumm.cpp) selects `Actor_v3` only for version 3, and `Actor` for ordinary version 4. Their [walking/path/step algorithms](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/actor.cpp) differ. The explorer currently uses the old-v3 walker for both editions. This is a concrete comparison gap relevant to the VGA crossing; it does not establish that changing the walker will trigger the branch. Do not classify the source branch as unused before comparing native v4 behavior.

The packet includes entry/local/exit/object code, object names, room geometry and literal/dynamic caller operands. VGA object 610 in Room 46 is named `hole`; its look starts global 97, which returns to Room 34 at `(246,64)`. Room 34's named objects include Rusty, boy and grave. No LOOM saves were found in repository game/bundled folders or the usual local ScummVM save locations. The bundled ini and roaming configuration have old paths; they were not modified. The user was asked only whether an existing save is available near this return route, not to replay the game.

## Local evidence and reproduction

Generated game-derived JSON/dumps and downloaded ScummVM source remain ignored. `out-boundaries/` is the latest packet; `rusty-prepared/` captures the successful prelude/retry; `entries/` compares VGA callbacks before/after room-entry settlement; `coverage-ega-en/`, `coverage-ega-es/`, `coverage-vga/` hold post-fix script reports.

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/final-gaps-20261008/Probe/Probe.csproj -- .verification/final-gaps-20261008/out-boundaries
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/final-gaps-20261008/Probe/Probe.csproj -- .verification/final-gaps-20261008/rusty-prepared rusty-prepared
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/final-gaps-20261008/Probe/Probe.csproj -- .verification/final-gaps-20261008/entries entry-callbacks
```
