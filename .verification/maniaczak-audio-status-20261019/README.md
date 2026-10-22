# Maniac/Zak room sound menus and one-click status

Implementation/first verification: 19 October 2026. Evidence finalized on 22 October after interruption. This packet records the accepted implementation through `cd42af7e`; later integration requires its own targeted check. Source commits: `8c5b0d47` (menus, lifecycle and status), `303a267f` (independent background readers), `e2634e1a` (native CD track entries). No registry level changes: menu availability and lifecycle checks are not listening acceptance.

## Changes and limits

Non-LOOM room Sounds use `IScummDataSource`: entry/exit/local/object scripts, literal child calls to depth four, and sound events actually reached by the live room. Static candidates are possible starts, not a claim that every branch was played. Variable targets/deeper calls remain unresolved. Independent archive readers prevent the background scan from sharing live seekable streams. CD-only native track ids retain their sound entries.

Opening another folder retires the old audition, native sound event sources, CD/speech sources, pending renders and room sound choices before loading the next game. A delayed old render cannot restart output. Inventory **Apply and close** applies one valid batch and closes immediately; unchanged Apply closes too. Refused/stale edits remain open, and same-session reopen retains Undo.

## Verification

Fixtures are plain folders under `D:\Juegos\Lucas`. No game files, saves, normal profile, original-game runtime or ScummVM were changed. All WPF checks held the serial window lease, used a private profile, and closed their windows before release.

| Check | Result |
|---|---|
| Four real `MainWindow` routes: Maniac Classic English, Maniac Enhanced Spanish, Zak Classic/Enhanced English | PASS, 49.87 seconds for the batch. Native intros and actual party clicks establish control. Each actual Game Status modal stages a grant, applies/closes in one click, reopens/undoes, closes unchanged, and rejects a deliberately stale batch after one native frame without closing. All four Normal sound popups open with source-free labels/tooltips; candidate counts 1/1/22/22. |
| Actual native LOOM English MT-32-edition Sound 13 rendered using AdLib, then real folder change to Maniac Enhanced | PASS, 5.10 seconds. `MediaPlayer.HasAudio` proves the native rendition opened; folder change closes the audition, removes its temporary WAV, detaches its event source and clears room choices. No listening or synchronization claim. |
| Synthetic delayed-render cancellation through the real LOOM-to-Maniac folder-open path | PASS within the four-family batch. Late completion creates no voice/output; old CD source is null. This is a cancellation probe, not a native audio playback claim. |
| `ScummInventoryAdminTests` | 18 passed, 0 failed, 0 skipped; existing meaningful Core coverage. The new closing behavior is checked through real modal buttons above. |
| Original source census before independent archive reopening | All 10 data-source families, 24.18 seconds, 866 rooms, 2,712 room/sound links. Historical baseline only. |
| Corrected independent-reader source census | First eight families completed with identical counts. The full v5 sweep was stopped because reopening an archive for every room was unnecessarily expensive. Retained partial output; no completed ten-family corrected sweep claimed. |
| Bounded corrected v5 source check | PASS, 53.35 seconds: MI2 talkie Room 7 has 4 linked sounds; Indy4 Room 4 has 5, including its independent direct entry/exit start. No session or scripts executed. |

All 14 captures from the two accepted window batches were visually inspected when finalizing this packet: four Normal windows, four pending status dialogs, four actual sound popups and two native-audio lifecycle windows. They confirm the visible controls and readable labels; assertions establish state/lifecycle. Captures remain in ignored local `output/` to avoid storing redundant images.

## Reproduction

From this worktree, prefix every command with RTK:

```powershell
rtk proxy dotnet build .verification/maniaczak-audio-status-20261019/WindowCheck/AudioStatusCheck.csproj -p:NuGetAudit=false --verbosity minimal
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter FullyQualifiedName~ScummInventoryAdminTests --logger "trx;LogFileName=inventory-admin.trx" --results-directory .verification/maniaczak-audio-status-20261019/output/inventory-tests
rtk proxy dotnet Exe/AudioStatusWindowCheck/Debug/net10.0-windows/AudioStatusCheck.dll window D:\Juegos\Lucas .verification/maniaczak-audio-status-20261019/output/window-attempt1
rtk proxy dotnet Exe/AudioStatusWindowCheck/Debug/net10.0-windows/AudioStatusCheck.dll window D:\Juegos\Lucas .verification/maniaczak-audio-status-20261019/output/actual-audio actual-audio-only
rtk proxy dotnet Exe/AudioStatusWindowCheck/Debug/net10.0-windows/AudioStatusCheck.dll source D:\Juegos\Lucas .verification/maniaczak-audio-status-20261019/output/bounded-v5 bounded
```

The harness cancels background room-card generation after actual folder open to keep this check focused. It uses real native intro inputs and actual modal/header/popup button events. The deliberate stale transaction advances one native frame; no variables, actors, flags, rooms or puzzle prerequisites are forced.

## Remaining acceptance

The parent integration owns requirements, final shared-window hooks, final suite and support matrices. In particular, the old CD-device selection handler separately overwrote generic Sounds visibility; that hook was reported to the parent on 22 October for a combined-path correction and targeted rerun. User listening remains needed for audible continuity, device fidelity and synchronization; these automated checks do not replace it.
