# Stable plain room-selector backgrounds — 19 October 2026

Explicit all-game user clarification: only LOOM EGA needs its selector artwork filled with native details. Every other game keeps plain backgrounds after they appear, including through a warm cache load and room selection. Native first visits and complete initialized thumbnail caches remain necessary for smooth room opening.

The App change only gates `InitialState` publication after the existing dispatcher/game/generation/token guard. It returns the same current-load result, so neither cached-image nor newly-rendered-image publication can stop the native pass. Background publication, phase transitions, shared per-library native scenes, completion/failure handling and `SaveCompleted` remain unchanged. LOOM EGA is identified by `GameId.Loom` and `ScummVersion.V3`, including demos; LOOM CD uses plain selector backgrounds.

From the isolated worktree:

```powershell
rtk proxy dotnet build .verification/stable-room-backgrounds-20261019/StableRoomBackgroundsCheck.csproj -p:NuGetAudit=false --verbosity minimal
rtk proxy dotnet Exe/StableRoomBackgroundsCheck/Debug/net10.0-windows/StableRoomBackgroundsCheck.dll policy
```

The build passed with **zero warnings/errors** (11.49 seconds). The headless policy check passed **72 game/version/demo identity combinations**, constructing no App or Window. These identity-only inputs test presentation policy, not native game-state prerequisites.

The harness also provides a bounded real-window mode, adapted from the accepted [room-preview harness](../room-previews-20261019/WindowCheck/Program.cs). It retains the complete source library while limiting visible work to the requested rooms. Both cold and reopened warm passes require complete native preparation/cache, compare every sampled/final/selected selector image to the plain background outside LOOM EGA, and require a cache image that differs from the plain background so the display distinction is exercised. For LOOM EGA it requires the initialized images instead. Each invocation uses its own profile and cache; source data is read-only and sound is muted.

```powershell
rtk proxy dotnet Exe/StableRoomBackgroundsCheck/Debug/net10.0-windows/StableRoomBackgroundsCheck.dll batch 'D:/Juegos/Lucas/zak ega en' '.verification/stable-room-backgrounds-20261019/zak-classic' '1,5'
```

Actual-window mode is compiled but **not yet run**: the parent owns the serial WPF lease. No cold/warm/window result is claimed by the headless check. Parent integration owns requirement updates, broader acceptance and the full regression suite. No support level changes.
