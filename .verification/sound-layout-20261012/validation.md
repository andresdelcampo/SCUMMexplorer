# Sound player presentation, 12 October 2026

Scope: main Advanced listening screen only. Preserve the existing treeview styles, hierarchy, selection, speech snippets and duration labels.

Follow-up: the user requested a waiting cursor for slow expansion. The preview verifies the cursor is set immediately for the real 7,236-row Indiana Jones 4 sound branch and released after layout, while the small LOOM sound branch does not add a busy scope. [Large branch](cursor-speech/validation.txt), [small branch](cursor-music/validation.txt). The tree's appearance and labels are retained.

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.

Build:

```powershell
rtk proxy dotnet build src/SCUMMexplorer.App/SCUMMexplorer.App.csproj -t:Rebuild --no-restore -m:1 -nr:false
rtk proxy dotnet build .verification/sound-layout-20261012/Preview/Preview.csproj --no-restore -m:1 -nr:false
```

The first incremental build encountered missing Core DLL/PDB outputs; the full rebuild restored them. Single-worker builds completed successfully. The preview's cached restore has NU1900 from unavailable NuGet vulnerability metadata; the App has no compiler warnings.

Preview artifact: `Exe/Preview/Debug/net10.0-windows/Preview.exe`. Application artifact: `Exe/Debug/SCUMMexplorer.exe`.

The preview uses a private profile under each evidence folder, renders the actual MainWindow offscreen and mutes its MediaPlayer. It loads original game resources and checks readiness, progress advancement, pause, stop/reset, selection, card width, visible transport controls and absence of scrolling for the selected resources at 900×600. Speech text is sourced from the existing script scan. No OS input or normal user profile changes are involved.

- [Indiana Jones 4 speech report](speech-final/validation.txt), [ready screen](speech-final/ready.png), [minimum window](speech-final/minimum.png).
- [LOOM AdLib report](music/validation.txt), [ready screen](music/ready.png), [minimum window](music/minimum.png), [unsupported audio](music/unavailable.png).

The LOOM run also checks that an unsupported resource disables playback and says it is unavailable, and that No Sound resets the presentation to muted. These are presentation/transport checks, not listening or original-game timing comparisons.

Commit verification: exported the staged App/Core sources to the ignored `Exe/.sound-commit-check/` folder and built that copy independently (zero warnings/errors). The focused `SpeechSoundsTests` and `V5SpeechTests` passed: 6 tests, no failures. The first sandboxed test run timed out connecting to its test host; retrying outside the sandbox passed. Other ongoing tree styling, device visibility, executable naming and screenshot edits are excluded from this commit. Generated PNG/TXT evidence remains local under the repository's existing ignore rules; the preview sources and this validation record are committed.

Reproduce a run:

```powershell
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/sound-layout-20261012/run-preview.ps1 -GameFolder 'Games/FUTURE/indy4 vga en' -OutputFolder 'D:\Documents\Personal\Code Projects\SCUMMexplorer\.verification\sound-layout-20261012\speech-final'
```
