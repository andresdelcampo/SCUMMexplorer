# Maniac/Zak room sound menus and one-click status

Implementation/first verification: 19 October 2026. Initial evidence finalized on 22 October after interruption; final integration regression completed on 23 October. The original packet records implementation through `cd42af7e`; the final accepted run below verifies the assembled production source `bde49369`. Source commits: `8c5b0d47` (menus, lifecycle and status), `303a267f` (independent background readers), `e2634e1a` (native CD track entries). No registry level changes: menu availability and lifecycle checks are not listening acceptance.

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

## Final integration regression — 23 October 2026

Worktree `b0ce49cd` contains the final harness and has **no production-source diff against `bde49369`**. Build: zero warnings/errors, 9.81 seconds. New checks additionally cover the parent CD-selection hook and render-source lifetime fixes (`fbc7ff3`, `7035931`, integrated by the parent with captured callback sources). The rendering worker captures its game/delegate/ROMs before scheduling, uses a meaningful cancellation token, and retires old cached WAVs after completion. No Core changes were made for these final corrections.

- **PASS, 32.82 seconds, exit 0:** all four native-status/Normal-Sounds routes above rerun against final source. Actual CD combo selection changes to enabled then muted preserve each generic room's sound choices and visible Sounds button. All original atomic Apply/close/Undo/stale-rejection assertions pass again.
- **PASS within that batch:** deliberately gate the old renderer, change folders through the actual handler, attach a native session from the replacement game, then release the old renderer while it deliberately ignores cancellation. Its token is canceled; no replacement-renderer call, `Starting` event, voice or output is produced. This is a deterministic lifecycle probe, distinct from native playback.
- **PASS, 3.13 seconds, exit 0:** actual native LOOM Sound 13 rendered with AdLib opens with `MediaPlayer.HasAudio`; actual Maniac-folder open closes the player, retires the WAV, detaches the old source and clears its menu. No listening or audiovisual timing acceptance claimed.
- **All 14 final captures inspected.** Four representative captures are retained beside the exact final transcripts in `records/`; the remainder stay in ignored local output. Both WPF runs close their windows and exit 0. Afterward an owned-process inventory finds no `AudioStatusCheck.dll` process; the serial WPF lease is released, with no further windows planned.

Final commands are the reproduction commands above with `output/final-window-20261023` and `output/final-native-audio-20261023` respectively. The bounded data-source census was already accepted and was not repeated. The ordinary sandbox denied the read-only CIM inventory; the same narrow owned-process query succeeded with the authorized escalation. No product/check failure occurred.

## Remaining acceptance

The parent integration owns requirements, final suite and support matrices. The old CD-device selection handler's generic-visibility overwrite and the reviewed render-source lifetime race are corrected and covered by the final run above. User listening remains needed for audible continuity, device fidelity and synchronization; these automated checks do not replace it. A bounded listening check is to audition LOOM Sound 13 using AdLib, open a Maniac folder while it is playing, and confirm the old music ends; this does not gate the completed implementation checkpoint.
