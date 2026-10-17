# ZakEnhanced S6 native play investigation — 17 October 2026

Fixture: `D:\Juegos\Lucas\zakE ega en`, SCUMM v2. This record distinguishes decoded game instructions and ScummVM source semantics from an original-game runtime comparison: the bundled ScummVM executable was not run. S5 original-picture, darkness and remaining object-gap uncertainties remain open.

## Reproduce

From this worktree:

```powershell
rtk dotnet run --project .verification/zakenhanced-play-20261017/PlayRoute/ZakEnhancedPlayRoute.csproj -p:RestoreIgnoreFailedSources=true -p:NuGetAudit=false -- "D:\Juegos\Lucas\zakE ega en" .verification/zakenhanced-play-20261017/baseline.txt wait,150 click,100,150 wait,2
rtk dotnet run --project .verification/zakenhanced-play-20261017/PlayRoute/ZakEnhancedPlayRoute.csproj --no-build -- "D:\Juegos\Lucas\zakE ega en" .verification/zakenhanced-play-20261017/source.txt source
```

The harness boots the game and sends only screen clicks or keys. `roomclick,x,y` converts room coordinates through the current camera and refuses points outside the view. `wait,n` advances normal frames. The source mode reads data only. Generated trace files are local evidence, not tracked assets.

## Initial baseline

At `f5d2e5c`, boot reaches Room 1 with input enabled and screen rows 16–143. The fourteen verbs are Push, Open, Walk to, Put on, Turn on, Pull, Close, Pick up, Take off, Turn off, Give, Read, What is and Use. They have native keys and positions, but the existing play layer supplies no old-engine font and refuses inputs because no later-engine `VAR_VERB_SCRIPT` is set. Shared engine owner is implementing native fixed script 4 input. This baseline does not pass S6.

## Source-derived route candidates (not yet route acceptance)

| Action | Decoded instruction |
|---|---|
| Open dresser drawer 92 | Room 1 verb 1 +0x002D sets intrinsic state; drawer rectangle (40,56,32,24), room coordinates |
| Pick up phone bill 15 | Room 1 verb 14 +0x0025 `pickupObject var[0x009]`; visible inside the opened drawer at (48,56,16,16) |
| Read held phone bill | Room 1 object 15 verb 12 +0x0028 starts Script 41 |
| Open bedroom door 84 | Room 1 verb 1 +0x0056 starts Script 10; +0x0058 sets the destination door's state |
| Walk through bedroom door | Room 1 object 84 verb 13 +0x0066 loads Room 2 via object 111 |
| Take butter knife 26 | Room 2 verb 14 +0x0026 picks it up; rectangle (400,48,24,16), requiring a genuine walk to bring it into view |
| Recover CashCard | Use plastic card 106 with a suitable held object invokes hidden object 93 at +0x006A; its +0x0013/+0x001E checks class 1 or 2, then +0x0029 picks up CashCard 18 |
| Leave apartment | Room 2 object 112 verb 13 +0x002B starts Script 29 for Zak; its alternative +0x003A loads Room 3 via stairs 134 |
| Native shop choice | Room 7 object 183 entry FD +0x002D/+0x003D creates Buy 17 and Sell 18. Merchandise Buy entries call Script 24 with their own item/price |
| Purchase response and skip | Script 24 +0x0023 rejects a purchase without CashCard; funded route +0x007B says the selected merchandise name, +0x0094 opens an override targeting +0x014E |

These candidates are inferred from this fixture, not forced story state. No Talk verb or later-engine dialogue menu is fabricated. Final route, tests and WPF coordinates are pending the shared prerequisites.
