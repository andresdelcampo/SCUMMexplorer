# EGA/VGA parity fixes, 7 October 2026

User feedback after the 7 October first pass. Staff-state changes and their evidence are in [staff.md](staff.md), commit `745d419`.

## Presentation changes

- EGA Advanced Sounds shows the sound resource, kind, decoded source path and duration when measured. Multiple source paths are summarized on the row and retained in the tooltip. The shared popup wraps long labels within 640 pixels. VGA retains disc position and duration. No audio rendering or listening check was needed for these label changes.
- EGA room thumbnails and first-visit gallery pictures use the session's shadow palette, as the room view already did. CD first-visit room cards carry their entry palette too. The cache stamp includes the App build as well as Core, so an App-only rendering fix invalidates old thumbnails. Room 68's slots 9/10/13/15 map to 11, producing the same robes as the room view.
- EGA doorway replay titles drop the redundant Use/Look/Walk response before an established Reached Room(s) label. VGA uses the same `LoomDoorwayFinder` as the room hotspots, including destinations in called local scripts. These remain script-derived possible destinations; the picker note continues to state the evidence limits.
- Both picker paths append `(verb 0xNN)` only in Advanced. VGA's intro is **The Opening**, without a room prefix: it starts in the skill screen and crosses several rooms. EGA's existing Room 01 opening title is unchanged.
- VGA Room entry now has script resource links. Global and local scripts are catalogued separately by `GameResource.ScriptRoom`; local 200 in Room 02 remains distinct from local 200 in Room 03. Links open through the CD data source and v4 dialect. CD script labels use their own IDs rather than EGA glossary names; raw headers identify DISK01.LEC and the actual code offset (6-byte SC/EN/EX headers, 7-byte LS header). Generic disassembly metadata keeps EGA's default format unchanged.

## Validation and commands

### Follow-up clarification pass

- Advanced rows now use the same formatter: resource / kind / initiating source / minutes:seconds duration. EGA's full call paths and alternate references stay in the tooltip; CD's exact disc time and spoken text stay in its tooltip. Inspected the actual popup captures `ui-followup-02/ega-sounds.png` and `ui-followup-vga/vga-sounds.png`.
- The selected-room override in `SetRoomCardThumbnail` was still replacing the correct Room 68 card with an unmapped bitmap. It now uses the same live palette. The bounded check exercises both background replacement and current-card updates, rather than only the initial thumbnail renderer.
- Both room cutscene lists append `(played from Room NN)` when the start differs from the viewed room. VGA's title is now **Opening**; EGA retains Room 01 · Opening. No extra replay/setup runs.
- VGA now uses the same Readable presenter and toggle as EGA. Headers respect the generic disassembly container metadata. A CD-specific glossary reads the CD's own object names/status contract and checks instruction patterns for reviewed helper names (8, 16–19, 28 and 42); it imports only shared engine variable roles, never EGA story/script IDs. Local scripts remain room-scoped and unnamed when no role is established. Inspected `ui-followup-02/vga-script17.png`.
- CD Room 01 local 211 writes its note limit to 3, so the shared browsing floor can now apply three minimum notes on automatic staff acquisition. Both editions preserve explicit higher levels. Note letters refresh before returning from an ordinary catalog visit, without waiting for entry camera/walk completion; actor refresh still waits for settled entry. Cutscenes/drafts retain display control.
- Game Status buttons are **Apply and close** / **Cancel**. Apply closes after success (or with no pending changes); Cancel discards. Existing stale-session/busy guards remain. The check toggles possession and verifies application versus cancellation and closed windows on both editions.

Follow-up build: solution build succeeded, 0 warnings/errors. Only `FullyQualifiedName~LoomStaffParityTests` was run: four passing cases (two editions for immediate letters/minimum/preserved higher levels and two for automatic staff with three notes).

UI: `ui-followup-02/report.txt` passed all checks after repairing the harness's WrapPanel cast and waiting for the asynchronous EGA sound list. Its VGA sound capture still selected the previous EGA popup; do not use that row or image as VGA evidence. The selector now requires the edition's resource prefix. `ui-followup-vga/report.txt` has 12 passing VGA checks, including the actual CD popup; this is the VGA sound evidence. No full suite, long replay harness, audio rendering or listening check. Temporary script-inspection sources were removed; decoded records remain in ignored `cd-scripts.txt` / `cd-entry-roles.txt`.

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.

Build:

```powershell
dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
```

Focused tests (six distinct cases across the repair runs, all pass; the new tests were later split into their own state/resource files):

```powershell
dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter 'FullyQualifiedName~LoomStaffParityTests|FullyQualifiedName~LoomCdScriptResourcesTests|FullyQualifiedName~StaffEditsPreserveOtherBits|FullyQualifiedName~BrowsingRaisesHeldStaff|FullyQualifiedName~TheCdStatusSchemaIsRead' --verbosity quiet
```

One new bounded WPF harness, `ParityCheck`, reused for failures and the final VGA-only check:

```powershell
dotnet build .verification/loom-parity/ParityCheck --no-restore
# Run with a 55-second process limit; arguments are <Games folder> <fresh output folder>.
Exe/ParityCheck/Debug/net10.0-windows/ParityCheck.exe '<absolute Games folder>' '<absolute output folder>'
# PARITY_EDITION='loom vga en' limits the run to CD after its script-source changes.
```

- `ui-02/report.txt`: 13 passing checks, both editions. EGA Room 02 entry has 8 resource links; VGA has 10 in the tested staff-held state (different scripts/data, not an expected equal count). Distaff transition/menu checks, concise doorway and Advanced verb labels, EGA thumbnail palette and CD script navigation.
- `ui-vga-final/report.txt`: 7 passing checks after reusing the hotspot finder and correcting CD source headers. `vga-menu69.png` is taken after advancing entry scripts and contains the skill menu without the staff or old note letters. `vga-script17.png` shows the real CD source header.
- Inspected `ui-02/ega-staff05.png`, `ui-02/ega-thumb68.png` and the final VGA skill-screen capture. Synthetic renders, not physical input or listening evidence.

The first new note-text assertion used an exact script label instead of the label plus instruction offset; corrected. The first UI check found the remaining VGA tree filter excluding scripts; fixed. No full suite, replay setup derivation wait, long playback harness or audio rendering was run.

App artifact: `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`. RTK was not found on this shell's PATH or in the checked install locations; plain PowerShell/git/dotnet were used. SDK/package-cache reads and local Git commits required sandbox escalation; all requested actions were approved by the automatic review.
