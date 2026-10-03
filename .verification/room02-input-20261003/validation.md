# Room02 script045 box-change scenes via accepted input — 3 October 2026

After `a8e0430`/Room34 record. Audited union **219→223/235 (94.89%)**, 12 remaining; fresh single-action **169/235 (71.91%)** unchanged and distinct.

Source: script 045 polls the ego's walk box (+0000..+000A). Box 4 → +001D/end003E, box 5 → +0049/end006A, box 10 → +0075/end0092, box 11 → +009D/end00BA; each scene `putActor`s the ego and starts script 017 (costume size). Catalog arrival Room02 is an explicit exploration assumption; no status, class, story or actor edits.

Cycle 1 clicked box4/5/10/11 centroids once each: scenes 001D, 0049, 001D (box4 re-entered en route to 10), 0075 — the box-11 scene +009D never ran because box 10's scene interrupted the walk (retained, excluded: `Exe/Room02Input-20261003/cycle-1`). Cycle 2 repeats each target click until its own scene ends (box10 twice): boundaries 001D, 0049, 001D, 0075, 009D, all balanced, 535 frames, Room02 throughout. Executed Script 45 cutscene/putActor/walkActorTo events all Executed. These watcher sites have no direct object-verb control; no private-script comparison is fabricated.

New audited sites: script 045 +0x001D, +0x0049, +0x0075, +0x009D. No production/catalog change; protected 57 exact/unstaged; App untouched; no push. Core662/broad/main Release dated.

```
rtk proxy dotnet run --project .verification/room02-input-20261003/Room02InputProbe/Room02InputProbe.csproj -c Release -- Games/loom Exe/<fresh-directory>
rtk proxy python -X utf8 .verification/room02-input-20261003/check.py
```
