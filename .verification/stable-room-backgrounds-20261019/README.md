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

The parent granted an exclusive serial WPF lease for four bounded cases on product source `9dce396`; all four passed, cold and warm. A capture-only harness addition records the final selected window after each pass. Its build passed with zero warnings/errors (7.02 seconds).

| Fixture / rooms | Cold / warm result | Non-LOOM plain-card observations | Initialized cache differs from raw background |
|---|---|---|---|
| Maniac Enhanced `mmansionE ega es` / 1 | [PASS](mm-enhanced/result.txt) | 220 / 119 | 1 of 1 rooms |
| Zak Classic `zak ega en` / 1, 5 | [PASS](zak-classic/result.txt) | 1,325 / 1,147 | 2 of 2 rooms |
| Monkey Island CD `monkey1 vga en` / 33, 37 | [PASS](monkey-cd/result.txt) | 479 / 458 | 2 of 2 rooms |
| LOOM EGA `loom ega mt32 en` / 56, 58 | [PASS](loom-ega/result.txt) | Not applicable: native detail filling retained | 2 of 2 rooms |

Each case used the `batch` command above with its listed fixture, a separate output subdirectory and its listed room IDs. All **3,748** non-LOOM observations and the completed/selected cards matched plain source-background pixels. Full initialized caches remained distinct, readable and complete, and every applicable fresh-library native scene was prepared on cold and warm passes. LOOM EGA cards instead matched their completed cache, preserving the filled close-up details in both rooms.

Eight captures are retained: `cold-completed-selected.png` and `warm-completed-selected.png` within each case directory. All four warm captures were visually inspected: plain non-LOOM selector images, native details/actors in selected main views, filled LOOM EGA close-ups and `No Sound`. The before/after visible-window inventories were both empty; every harness closed itself. No OS input, other application interaction, game-data mutation or ScummVM execution. Private profile/cache output is not committed. Lease released after the fourth case.

These are bounded presentation checks on the delegated selector fix, separate from the parent's pending manual-navigation/intro changes. Parent integration owns requirement updates, combined acceptance and the full regression suite. No support level changes.
