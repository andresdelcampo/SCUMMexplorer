# S5 ManiacEnhanced — 17 October 2026

## Scope and result

Source-backed first-visit preview is implemented for the reviewed Spanish enhanced fixture `D:\Juegos\Lucas\mmansionE ega es`. All 53 rooms open; all 53 have zero detected image gaps; none fail. 51 settle; Rooms 30 and 45 reach the 2400-frame bound. This is a bounded state preview, not proof that routes, later puzzles, or the original rendered picture match. Original-game visual acceptance remains open: ScummVM was not executed.

The reviewed boot-menu fingerprint selects Syd, Bernard and Start through genuine room clicks. This assumption appears in scene notes. Unknown menu fingerprints fail closed. No story variables are patched. Direct catalog visits retain the prepared opening state and explicitly say the route was not played.

## Retained evidence

- `room-audit.md`: final complete per-room run, state, actors, notes and outcomes.
- `room-audit.tsv`: final machine-readable 53-room summary.
- `source-comparisons.md`: decoded entry scripts and called-script state operations for the ten heaviest rooms plus opening Room 44 (11 distinct samples).
- `RoomAudit/`: reproducible harness based on the Indy3/Monkey floppy S5 models. BMP captures and transient output remain local.
- Parent record `.verification/maniaczak-objects-20261017/`: real WPF window checks, including Rooms 44, 1 and 18 in both Maniac editions.

## Source comparison

Instruction counts include entry, exit and unique object-verb blocks. Static branches show possible behavior; they do not establish that every branch ran. Active IDs below are the final audit's intrinsic-on image IDs, which may originate in the index or boot rather than entry writes.

| Room | Instructions | Entry/source comparison | Reached active images |
|---|---:|---|---|
| 18 | 317 | Entry +0000 starts Script 55; no direct entry state write | none |
| 1 | 224 | +0000 lights 11; +0004 stops sound 32; no direct state write | 56,57,62,63,64 |
| 7 | 200 | Script 55; +0002 tests intrinsic state 88, then sound 17/Script 81 branch | 88 |
| 16 | 143 | +0006 tests intrinsic-off 149 before box flags 2–4 | 144–150 |
| 30 | 128 | Actor 23/9 setup; object 176 branch; Scripts 127/129/125; Script 125 +0004 clears intrinsic 169 | 178; frame limit |
| 17 | 125 | Script 55; variable 167/94 branches and sound 25 | 192,30 |
| 3 | 124 | Script 55; Script 21-running test; intrinsic-off 228 and variable 173 branches | 76,75,78,79 |
| 24 | 113 | Script 55; intrinsic-off 233 selects actor 12 placement; intrinsic 253 sound branch | 118,115,116,117,256,253 |
| 4 | 105 | +0000 lights 11 | 214 |
| 43 | 102 | lights 11, resource 19 and actor 0 talk colour 15 | none |
| 44 | 15 | lights 11, stop sound 32; party already selected through native menu | none; three actors |

Runtime rules were checked against ScummVM `engines/scumm/object.cpp` (intrinsic mask 8, untouchable bit 2, reverse object lookup, directory drawing), `script_v2.cpp` (pseudo-room operands and loadRoomWithEgo signed byte walk coordinates scaled 8×2), `actor.cpp` (v1/v2 showActor stand/init/talk-stop sequence and preservation of limbs on init), and `base-costume.cpp` (v1 origin 0,0). Source was read on 2026-10-17; no fixed upstream commit is claimed.

## Fixes and regression checks

Old data now reaches the proper v1/v2 object reader and background decoder. Native state flags 1/2/4 alone cannot show an image; bit 8 controls visibility and bit 2 disables hotspots. Duplicate image directory entries remain drawable, while script lookups select the last matching entry. Pseudo-room hexadecimal operands are accepted. Old arrival walks are converted to pixel coordinates after object placement.

Initial audits falsely hit the frame limit because global Script 159 changed remote objects 547/549. Old-room stability now follows the current room's picture while retaining the full snapshot. Room 44 settles at 134 frames. Parent WPF inspection exposed a head-only actor; source-correct showActor initialization restored all three bodies. Enhanced Room 44 actor pixel counts are 1030, 1113 and 1025 for actors 1, 3 and 5. Pixel counts are supporting diagnostics, not a substitute for picture comparison.

Targeted suites passed throughout: 35 initial adapter/state tests; 39 pseudo/stability tests; 37 duplicate/mask tests; 45 actor lifecycle tests; final eight old first-visit rule tests including Zak Room 9 arrival at (272,120). Family tests pin Rooms 1, 2 and 44 plus genuine recipe notes. The final LOOM-filter regression run passed 836 tests, skipped 5 and failed 0 (841 total, 2m43s). The full configured suite is recorded by the parent integration checkpoint.

## Limits and estimates

- Rooms 30 and 45 remain bounded frame-limit previews; no completion is claimed.
- Dark rooms 5, 8, 9, 15, 22 and 23 are shown lit by the inspector, with the existing Advanced note. Darkness compositing is not verified here.
- No original-game screenshot comparison, complete route play, or pixel-perfect actor alignment claim is made.
- S4b's bounded timing/sound assumptions remain; this work does not establish S6 verb-based play or S7 acceptance.
- Classic costume colour estimates remain the earlier S2 leftover; this run does not promote them to verified colours.

## Reproduce

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer\.worktrees\maniac-enhanced-s5`.

```powershell
$env:AUDIT_NOTES="1"
rtk proxy dotnet run --project .verification/maniacenhanced-objects-20261017/RoomAudit/ManiacEnhancedRoomAudit.csproj -p:NuGetAudit=false -- "D:/Juegos/Lucas/mmansionE ega es" .verification/maniacenhanced-objects-20261017/final
$env:AUDIT_SOURCES="1"
rtk proxy dotnet run --project .verification/maniacenhanced-objects-20261017/RoomAudit/ManiacEnhancedRoomAudit.csproj -p:NuGetAudit=false -- "D:/Juegos/Lucas/mmansionE ega es" .verification/maniacenhanced-objects-20261017/output
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter "FullyQualifiedName~ScummOldFirstVisitRulesTests|FullyQualifiedName~ScummManiacEnhancedFirstVisitTests" --no-restore -p:NuGetAudit=false
```
