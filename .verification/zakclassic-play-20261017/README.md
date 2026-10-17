# ZakClassic S6 native play investigation — 17 October 2026

Fixture `D:\Juegos\Lucas\zak ega en`, SCUMM v1. Independent worktree and execution against this edition; no Enhanced results are substituted for Classic evidence. The bundled ScummVM executable was not run. S5 visual acceptance and Classic costume-colour estimates remain open.

## Reproduce

```powershell
rtk dotnet run --project .verification/zakclassic-play-20261017/PlayRoute/ZakClassicPlayRoute.csproj -p:RestoreIgnoreFailedSources=true -p:NuGetAudit=false -- "D:\Juegos\Lucas\zak ega en" .verification/zakclassic-play-20261017/source.txt source
rtk dotnet run --project .verification/zakclassic-play-20261017/PlayRoute/ZakClassicPlayRoute.csproj --no-build -- "D:\Juegos\Lucas\zak ega en" .verification/zakclassic-play-20261017/drawer.txt wait,150 key,119 wait,2 roomclick,50,65 wait,2 click,10,145 wait,180 key,100 wait,2 roomclick,55,62 wait,2 click,10,145 wait,180
```

Only native screen clicks, verb keys and normal frame advancement are used. The source mode only decodes this fixture. No scene visits, direct sentences or state assignments occur.

## Verified opening sequence

The actual boot reaches Room 1, screen top16/bottom144, with native verbs at rows152/160/168. W selects Open. Clicking screen(50,81) selects drawer92 and composes the native sentence; clicking sentence area(10,145) executes it through Script4 and Script2, opening the drawer. D selects Pick up. Clicking screen(55,78) selects exposed phonebill15; clicking sentence area(10,145) executes its verb14, yielding owner15=1. Trace contains Script2 verb1/object92 and verb14/object15. The two-step sentence is the game's own behavior.

Two focused tests pass with the shared old-engine walk matrix and native interface renderer. They pin the visible sentence `Open dresser drawer`, rendered phone-bill inventory pixels, inventory hit target screen(180,177), the bedroom door's state and native load into Room2, and Esc from Script126's reached override followed by restored player control. Script126 begins with a delay600 and can interrupt a longer opening route; skip only when `CutsceneSkippable` is true, then wait for control.

An independently executed longer route reaches Room3 and Room5. Walking toward shop door161 stops at (88,116), matching the separately observed Enhanced boundary problem. The shared owner is investigating that route blocker. Full native choice, final WPF checks and S6 acceptance remain pending. ScummVM source comparison is separate from the prohibited original-runtime comparison.
