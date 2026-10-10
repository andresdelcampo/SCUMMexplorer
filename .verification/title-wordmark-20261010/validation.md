# Graphical title, 10 October 2026

The original 2172 × 724 transparent PNG is embedded as `Assets/scummexplorer-title-v1.png`. The top-left Image uses a CroppedBitmap rectangle `10,150,2152,435` to remove transparent margins at runtime, then fits inside 200 × 42 WPF pixels with HighQuality scaling. Its accessible name is SCUMMexplorer.

The Debug and Release app builds passed with zero warnings/errors. TitlePreview constructs the real App/MainWindow with a private profile, renders the header offscreen at window widths 1180 and 900, and captures welcome/full-control layouts without opening game data. The image resource loads and its rendered size is 200 × 40.43. At normal width the header remains 58 pixels high; at minimum width with every optional control shown, the controls wrap and the header grows to 100.92 pixels. Both captures were inspected and the lettering remains clear, with no clipping or overlap.

Run from `D:\Documents\Personal\Code Projects\SCUMMexplorer`:

```powershell
rtk proxy dotnet build .verification/title-wordmark-20261010/TitlePreview/TitlePreview.csproj -c Release --no-restore --verbosity minimal -m:1 -nr:false
rtk proxy dotnet Exe/TitlePreview/Release/net10.0-windows/TitlePreview.dll
```

Local captures: [normal width](out/header-1180-full.png), [minimum width](out/header-900-full.png). Generated captures/profile/logs remain local. The initial preview restore reported an unavailable NuGet audit endpoint; restoring with `--ignore-failed-sources -p:NuGetAudit=false` resolved it. The preview project required single-node MSBuild (`-m:1 -nr:false`) after the default project-reference build returned failure without diagnostics.
