# Usable source arrival for both Room20 responses — 2 October 2026

Both incomplete Room20 responses now have a tested settled arrival through actual source execution. Fresh catalog20 still lacks ego coordinates and retains its existing failure. The new recipe explicitly records catalog selections and direct responses; it does not establish native puzzle input or alter replay coverage.

From a fresh default session, catalog16 and direct350/0x47 starts its sleep reaction. After117 settling frames, Room16/local204[1] has written D8F=1. Catalog18 then direct369/look starts062, or direct371/look starts056. These source scripts enter20 and establish the ego through their own placement. Their arrival settles in20 after888/967 frames respectively. With D8F=1,20/local207 takes its source sleep guard rather than sending the ego straight back18.

Only after requiring the actual current room20, invoke390/0x48 or391/0x48. Both run20/local206, reach `getActorX` +002D with real source-placed ego coordinates, clear D8F +00A3 and end +00A9. The subsequent207/058 or059 sequence returns18. Each full route balances seven begins/seven ends; response stages settle570 frames after369 arrival,528 after371 arrival. All four combinations pass. No actor placement, puzzle bit, unfreeze, known-state or browsing-floor write was injected.

| Source arrival | Response | Arrival settling | Response settling | Result |
| --- | --- | --- | --- | --- |
| 18/369 look →062 | 390/48 | 888 | 570 | 18,7/7 |
| 18/369 look →062 | 391/48 | 888 | 570 | 18,7/7 |
| 18/371 look →056 | 390/48 | 967 | 528 | 18,7/7 |
| 18/371 look →056 | 391/48 | 967 | 528 | 18,7/7 |

[Compact state and hashes](room20-summary.json), [recipes](room20.json). Full packets: ignored `Exe/SourceProgressionSession-20261002/room20-1/`. This supersedes the earlier claim that neither source arrival can remain in20: without the executed sleep prerequisite, the earlier transient20→18 behavior still holds.

Reproduce from `D:\Documents\Personal\Code Projects\SCUMMexplorer`, prefix each shell command with required absolute RTK `proxy`:

```powershell
dotnet run --project .verification/source-progression-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/source-progression-20261002/room20.json Exe/SourceProgressionSession-20261002/room20-fresh
```

Core289/WPF62, doorways16/controls19/input18, all363 source roots, memberships and both sequences pass unchanged. All76 current PNGs are unchanged with zero gaps, preserving the scythe correction. [Fresh check summary](check-summary.json). No production or catalog edits were needed for these arrivals.
