# .verification: what is live and what is a record

**Live (rebuilt and run on `Games/loom ega en`):**
- The harness projects directly in this folder that take `<game folder> <output folder>` (`UiShots`, `SceneShots`, `StatusShots`, `WindowCheck`, `TextGalleryCheck`, `RoomAudit`, `DoorwayClickCheck`, `Blockers`, `CodeDump`, `ScriptDump` and the other checks kept in `docs/archive/HARNESS_REVIEW_2026-10-04.md`).
- The replay catalog pipeline: `replay-coverage-20261001/` (`ReplayAudit`, `discover_replays.py`, `export_catalog.py`), `room-replays-20261001/` (`MembershipAudit`, `established-starts.json`) and `english-catalog-20261004/` (the run that produced `loom-replays.json`).
- `english-compat-20261004/` (UiShots on the English data), the drivers `run-verify.cmd` and `harness-review-20261004/run-all.ps1`, and the golden files in `readable-scripts/` (English).

**Archive (`archive/`):** the dated evidence folders of the Spanish-edition work of 27 September to 4 October 2026 (92 folders: validations, traces, route probes). They describe what was checked then, on the Spanish data (now `Games/loom ega es`), so the offsets, bit numbers and object ids in them are Spanish ones. Docs and the changelog link into `archive/`. Their project files build (references were adjusted when they were moved), but their scripts that name `Games\loom` will not run as written; to repeat one, change the folder argument and expect English differences. Do not copy a number from the archive into code without checking it on the English data (`ScriptDump`). Heavy captures are packed in `Exe/_evidence-archive/`.

The remaining undated folders are older harnesses and their notes; the harness review lists which were kept and why.
