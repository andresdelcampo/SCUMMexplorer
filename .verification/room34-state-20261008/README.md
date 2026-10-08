# Room 34 state, Rusty and camera — 8 October 2026

The source explains a late-state camera prerequisite consistent with the user's [provisional EGA observation](../final-gaps-20261008/USER_OBSERVATIONS.md). It does **not** establish which state that debugger run had, a normal route to the crossing, or dead code. No production correction is justified by this investigation. The unresolved picker note stays in place.

The [native VGA walking correction](../native-walk-20261008/README.md) remains valid. This investigation checks the next question: whether source-established transitions pair an enabled box 6 with an eligible Room 34 local 200 monitor. The checked transitions do not demonstrate that pairing.

## Edition-specific entry guards

The static exporter reads each edition independently. These are numeric roles established by these particular instructions, not interchangeable numeric IDs across releases.

| Role / instruction | English Roland EGA | Spanish Roland EGA | English VGA |
| --- | --- | --- | --- |
| Initial entry branch gate at `+000D` | `D71` | `D72` | `012` |
| Late-state entry flag, also written by Room 46 entry `+0000` | `D7A` | `D7B` | `01B` |
| Crossing-complete flag | `DCF` | `DD0` | `070` |
| Input/cutscene flag tested before starting the monitor | `D6D` | `D6E` | `00E` |
| Ghost conversation flag, Script 98 `+0000` | `DC6` | `DC7` | `067` |
| Rusty restoration flag, local 209 | `DC7` at `+0373` | `DC8` at `+038C` | `068` at `+03FC` |
| Monitor entry tests: late / complete / input | `+034B/+0350/+0355` | `+035E/+0363/+0368` | `+0375/+037A/+037F` |
| Start local 200 | `+035A` | `+036D` | `+0384` |
| Enable box 6 in the late entry branch | `+00CB` | `+00CF` | `+00D4` |
| Hide box 6 in the early entry branch | `+014A` | `+0153` | `+015E` |

Once entry passes its initial branch gate, local 200 starts only when **late = 0, complete = 0, input = 0**. That same early entry hides box 6 and enables boxes 7/8/9. The late branch enables box 6, hides boxes 7/8/9 and skips the monitor. The camera lock applies in the late branch while the ghost conversation flag is clear: EGA `+006B` requests 152..160, clamped to 160..160; VGA `+006C` requests 216..216. Both hide boxes 10/11 there.

The formatted branch target is taken when the named condition fails. Matching ScummVM 2.2.0 `jumpRelative` confirms this; `o5_isGreater` tests its second operand against the first. Consequently local 200 `+002E` falls through only when camera x is **below 464**. All three editions retain `+0020` current box 8, `+0027` previous box 6 and `+0035` cutscene. The previous box is the monitor's preceding *observed box change*, stored in local 2; it is not an injected actor box or an independently sampled native-engine field.

## Source writers and scope

Script 85 begins the wake/conversation path by stopping local 200 at `+000A` and setting the edition's crossing-complete flag at `+000C`. This is before its first `breakHere`. Boy object 562/561/487 draft verb `44` starts Room 34 local 201; local 201 can chain to Script 85. The scripted wake therefore suppresses the crossing even if its monitor was previously running. Ordinary boy look `36` starts Script 112 instead; “talked to Rusty” does not by itself identify which interaction or story phase occurred.

Script 98's ghost conversation sets its own flag at `+0000`, visits the close-up room and returns to Room 34. That reload can enable box 6 through the late entry branch. The camera range is opened by local 206 `+0023` during the conversation and by Script 98's cleanup (`+05B2` English EGA, `+05C5` Spanish EGA, `+061B` VGA). Its cleanup enables boxes 10/11 at `+05D7/+05DA`, `+05EA/+05ED` and `+0640/+0644`, respectively. It does not restart local 200 or clear its completion flag.

The checked literal Room 34/global box-6 writers are entry, local 207 `+0000` (hide), local 209 `+0399/+03B2/+0422` (enable) and global 97 `+002D` (hide). Local 209 is the restoration callback reached by bones object 587/586/512 verb `4D`; it does not start local 200 or clear the crossing-complete flag. The legitimate prepared restoration trace reaches its write after the wake and ghost conversation, with the monitor absent.

English/Spanish literal complete-flag writes occur in Script 85 and local 200; the checked VGA quick-start Script 120 also sets `070` at `+0523/+05D2`. No literal clear of those flags appears in the decoded search. English/Spanish late-flag writes in that search set the flag. VGA Script 99 temporarily clears `01B` at `+0462` for a Room 20 close-up, then restores it at `+0476`; it does not demonstrate an eligible Room 34 entry. These are **literal-reference findings**, not an exhaustive proof over dynamic targets, word aliases, save loading or undecoded bytes. Calls to local IDs in other rooms retain their own room namespace.

## Bounded observations

`Probe` records copied pre-instruction operands, source access, sequence/slot/parent/invocation IDs, cancellation events, frame state, camera range, input, box flags, and pending Room 34 monitor state. Bit snapshots resolve EGA backing words and VGA's initialized separate storage; missing values stay null. The operand filter also includes backing words, so selected guard bindings are not silently filtered. No private state writes, manual actor placement, box assignment, forced bits or cutscene boundaries are applied.

The fresh room and existing Firewood/ghost preparation remain **Explorer previews**. Catalog visits have inherited arrival/position estimates, staff setup is an explicit exploration step, and direct verbs bypass native walking/casting. The English continuation uses the already established wake/swap/Firewood/dream route, then a disclosed catalog visit to Room 46 before its actual object-685 look callback runs global 97. It is not a complete original-game route.

| Trace | Checked outcome |
| --- | --- |
| Fresh Room 34, all three editions | Three monitor guards read 0; local 200 runs; box 6 is `80` |
| Wake, English EGA / Spanish EGA / VGA | Settles in 1,004 / 1,028 / 553 frames with 2/2 boundaries; Script 85 reaches stop `+000A`, then complete-flag write `+000C`; monitor absent afterward |
| English EGA source Room 46 return after preparation | 26 frames, 1/1 boundaries; late=1, complete=1; global 97 hides box 6 and replaces Bobbin at `(246,82)`/box 5; camera range 160..160, input=1; monitor absent |
| English EGA ghost conversation | 1,125 frames, 1/1 boundaries; camera range opens to 160..464; boxes 6/10/11 are enabled after the scripted reload/cleanup; monitor absent |
| Public eastward walk after conversation | 51 frames to settled `(378,88)`/box 11, camera 320; no crossing boundaries; monitor absent |
| English EGA restoration | 584 frames, 1/1 boundaries; local 209 reaches `+0399`; box 6 remains enabled; monitor absent |

All **24 focused source/state assertions pass**: 14 English EGA and five each Spanish EGA/VGA. Spanish/VGA here verify fresh entry and wake only; their late camera behavior is source-compared, not dynamically rerun. The existing full EGA/Core/WPF audits were not rerun because this task changes only diagnostics and records. A clean solution build passes with zero warnings/errors.

This provides a candidate explanation for the user's earlier camera observation **if their debugger visit selected the late ghost state**. That earlier visit's state and precise callback remain unknown, and its observation file is preserved unchanged. The user subsequently supplied an existing save and a running English Roland EGA ScummVM 2.2.0 session; [native session identity, actions and supplied console readings](NATIVE_SESSION.md) are recorded separately. Its Room 34 hash matches this source packet. The user's screenshots establish active local 200, box 6 hidden, camera x=160, actor 1 settled in box 2, and all three monitor guards clear. This native sample is the early state, so the late-ghost camera branch does not explain this particular sample.

## Evidence limits and next checks

The source-supported transitions checked here enable box 6 after the early monitor has been stopped or skipped. No reachable eligible 6-to-8 sample was demonstrated. The earlier corrected native geometry still observes box 7 between 6 and 8. These facts narrow the question but do not prove unused code, every native placement/input path or full original-game reachability. Keep the unresolved note. The existing-save request is fulfilled; use the supplied slot and running game, without requesting a whole-game replay.

There is no new demonstrated product picture/audio/input discrepancy. The user fulfilled the console assist; the next native check is ordinary movement with box/monitor sampling or an existing-slot reload, preserving the save. Camera range and previous observed box remain unread. Automated console submission remains unavailable. Other perceptual checks should select at most one or two exact comparisons from the remaining source-backed actor/staff/input cases in [the dossier](../../docs/loom/FINAL_GAPS_HANDOFF.md).

## Identity and reproduction

The exporter records resource identities without modifying game files. Room 34 encoded SHA-256: English EGA `726AB2C92507FF64CA653394469BA73303EC9C435FDC2453105BB5FA33472AC6`; Spanish EGA `083A1AE30C73B49FAD9D3FDA2344ADCBFBBFC5DB20318E9332036DE85AAA7B89`. Decoded VGA `DISK01.LEC`, read from the local disc, SHA-256 `8A23DBBAFC432CE3F428DF15AFA47B6ED4F38117BF9490ED7DDB0F6F23E6318B`. Packet files also include EGA index/Room 46 hashes and source offsets/lengths/issues.

Matching cached [ScummVM 2.2.0 opcode source](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/script_v5.cpp) establishes conditional and camera-scroll semantics (`jumpRelative` line 398, comparisons 1237–1284, room scroll 1650 onward). [Script scheduling](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/script.cpp) line 888 visits running slots in order, once per loop; [the main loop](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/scumm.cpp) runs scripts before walking, then camera movement (2311, 2335–2336). These source facts do not prove the Explorer's entire slot scheduling frame-for-frame.

Cached source SHA-256: `script_v5.cpp` `C572940A96AADAA6D54C9417233C1FF5322D5D435A64E428807B9F2F6CD05C79`; `script.cpp` `3C37C192B112BD0D110E4A12B4D78875ED916A78829B2C5C2152615B0D60B52C`; `scumm.cpp` `3A88FA7708C78B7F091B63F2A46EDF1992710D2D2C454E6E0BB70A6BE82EDFD2`.

Run from `D:\Documents\Personal\Code Projects\SCUMMexplorer`, with every shell/child command wrapped by RTK:

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/room34-state-20261008/Probe/Probe.csproj -- .verification/room34-state-20261008/out
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/room34-state-20261008/Probe/Probe.csproj -- .verification/room34-state-20261008/out-transitions-checked transitions
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
```

`out*/` is ignored. Static packets cover 1,427 English EGA blocks, 1,419 Spanish EGA blocks (four known interior-entry read errors retained) and 1,470 VGA blocks. Generated disassemblies/traces, source downloads and game/ROM/save data are not committed. The app remains `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`.
