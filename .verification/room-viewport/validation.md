# Room viewport and costume gallery validation — 27 September 2026

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.
All shell commands used the absolute RTK executable with approved escalation because RTK cannot launch in this sandbox.

The solution build passed with **0 warnings and 0 errors**. The existing Core suite passed **164 tests** during the initial presentation change; it was not repeated for the subsequent WPF-only controls change. The earlier scaled presentation passed 22 layout assertions; the final checks below supersede that result.

## Follow-up room controls

Final result: **39 assertions passed, exit code 0**. The retained [geometry report](followup-20260927-03/geometry-check.txt) records each assertion. The [harness source](PresentationCheck/Program.cs) runs against the real MainWindow and local game data.

- Rooms show up to 320 source pixels across, enlarged to fit. Panorama mode fits the complete room. Panning preserves source-coordinate mapping and overlay alignment.
- The room-ready status is collapsed, reclaiming its layout row. Music is centered and bottom-aligned, ending 8 pixels above the container bottom in the checked window.
- Pan taps move exactly 2 source pixels. A 75 ms timer increases the step over 1.75 seconds, capped at 64 source pixels for keys and 160 for buttons. Synthetic key and mouse handlers were held for 230 ms; release stops movement. Room boundaries, panorama changes and resource changes were checked. Focus/capture loss, deactivation and closing cancellation were also reviewed in code.
- The checked depth preference survives Room 04 to Room 03. The absent-mask branch was checked with an explicitly synthetic no-mask image, then actual Room 04 restored the enabled checked control. Room 69 itself has mask data; this test does not claim otherwise.
- Costume 01 contains 60 decoded poses at one enlarged scale. Script/sound selection and returning to a room still work.

## Reproduce

Use a fresh output directory for each harness run. It generates screenshots and a text report; generated runs remain local through .gitignore, except for the final report retained above.

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --verbosity quiet
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/room-viewport/PresentationCheck/PresentationCheck.csproj -- 'D:\Documents\Personal\Code Projects\SCUMMexplorer\Games\loom' 'D:\Documents\Personal\Code Projects\SCUMMexplorer\.verification\room-viewport\rerun-01'
```

The recorded final run used output directory .verification/room-viewport/followup-20260927-03. Its screenshots were visually reviewed and are kept locally, together with superseded runs. The harness now guards against repeated execution and report-write failures and always shuts down; the final run did not crash.

These are synthetic WPF input and layout checks. Physical computer-use checks covered the earlier presentation, then stopped at the user's Escape input. The final layout and held-input changes were not physically retested.

Application artifact: `D:\Documents\Personal\Code Projects\SCUMMexplorer\Exe\SCUMMexplorer.App\Debug\net10.0-windows\SCUMMexplorer.App.exe`.
