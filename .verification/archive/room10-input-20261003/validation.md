# Room10 walking and clam spell input — 3 October 2026

Continues `8722479`. **All four remaining Room10 sites complete through public game input.** No production correction, catalog change or observer expansion was needed. Audited coverage rises **188→192/235 (81.70%)**, with43 sites remaining. Separate fresh single-action coverage remains **169/235 (71.91%)**,66 gaps/49 owners/335 catalog starts.

## Completed input route

Fresh seed0/default-preview session, initial catalog arrival in Room10 (an explicit exploration assumption). Bobbin settles at260,79,box4. Both modes use actual walking and exit clicks. Only spell execution differs: the control activates296's verbs directly; the input run presses the loaded draft's note keys.

| Action | Source result |
| --- | --- |
| Click decoded box3 centroid182,53 | Watcher200 +0032 selects box3; cutscene0039 ends0102; ego216,112,box5 |
| Click decoded box7 centroid280,110 | Watcher200 +0106 selects box7; cutscene0110 ends01AB; ego280,90,box8 |
| Supported Game Status staff/three notes/learned slot0 | Explicit exploration setup, not a source-earned prerequisite; slot0 was already learned in the initial snapshot |
| Approach clam296 at62,95, target64,100; press Opening3,1,3,2 (`e q e w`) | Actual007 +002E dispatches296/3A; local212 starts0000/ends007C; source writes D5=308 and DD3=1 |
| Retarget clam; press reverse2,3,1,3 (`w e q e`) | Actual296/3B enters000C and ends005D: Bobbin refuses to spoil the gull's breakfast; clam remains D5=308,DD3=1 |
| Click exit285 at312,108 | Actual285/38 starts214 at0000, then loads5 at0003. Source214 writes DD3=0,DD2=1 and D5=309 |
| Click Room5 exit202 at20,72 | Actual202/38 loads10 at0000; no catalog revisit or forced placement |
| Retarget clam; press reverse2,3,1,3 again | Actual296/3B starts213 at0061;213 begins0000/ends002F and writes D5=307 (closed) |

Opening212 was already covered; it supplies the source prerequisite but adds no site. The two200 boundaries,296/3B +000C and213 +0000 are the four additions. The feeding refusal is a completed source scene, not a successful closing action. Only the final close changes D5 to307.

The input run takes1266frames and completes5/5 scopes; direct control1113frames, same ten boundary strings. Both finish in10 with ego62,95,box1. Each stage has a3000-frame cap and requires its exact source end (where applicable), expected room, cumulative balanced scopes, editable state, stationary ego/camera, no message, and32 quiet frames. Failure prevents later input. Exit stages require the actual destination rather than immediate Click return values.

All twelve note-key calls are accepted. Trace records007 +002E dispatches296/3A,296/3B,285/38,202/38,296/3B in that order. All86 recorded body events from200/211/212/213 and296/3A/3B are Executed/Conditional in the input run. The corresponding control events match every field except30 retained certainty fields (Reached in direct, Conditional in input); [exact differences](event-differences.json). These records are not an exhaustive instruction/writer trace. Snapshots confirm the selected state changes, with missing early DD2 deliberately kept missing rather than labelled known zero. No certainty is cleared or inferred from equality.

## Two bounded diagnostic cycles

1. `cycle-1`: the probe incorrectly expected213 on the first close. Both modes actually completed the feeding refusal296/3B, leaving the expected213 end absent. The bounded runs fail at3657/3740frames. Their eight completed boundary strings and full failed packets remain immutable. No coverage is credited from them.
2. `cycle-2`: correct that expected branch and use source-supported exit285→Room5→202→Room10 to run214. The second reverse cast now completes213. Both modes pass; stop diagnosis. No engine change or additional instrumentation is justified.

`prepare.py` preserves the first-cycle harness generator and refuses to overwrite its target directory. Checked-in `Room10InputProbe/Program.cs` contains the final cycle. Do not run the generator over the existing source.

## Verification and preservation

Fresh Core-only probe build/run and `check.py` pass. Checker verifies accepted clicks/keys, geometry, exact boundaries, selected source instructions, subsequent completion, paired body differences, source fingerprints, unchanged draft notes/reverse flags, and the union against all prior qualified literal/sequence/Room34/Room25/Room23 evidence. It checks all57 protected hashes and unstaged status, plus prior Room34/25/23 evidence hashes. Core and replay catalog have no diff. All existing unrelated working changes remain untouched and unstaged.

No App launch, close, interaction or push. No WPF, physical-input, visual, audio or native full-game claim. This evidence task does not change execution: main Release/Core662/broad regression results remain explicitly dated from the earlier checkpoint; no App rebuild was run. Session token-usage telemetry is unavailable, so work stops conservatively at this completed checkpoint.

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.

```powershell
rtk proxy dotnet run --project .verification/archive/room10-input-20261003/Room10InputProbe/Room10InputProbe.csproj -c Release -- Games/loom Exe/<fresh-directory>
rtk proxy python -X utf8 .verification/archive/room10-input-20261003/check.py
```

The checker audits the recorded `cycle-1`/`cycle-2` paths. Fresh reruns require a new directory and an explicit separate comparison; never overwrite historical evidence.

Artifact: `Exe/Room10InputProbe/Release/net10.0/Room10InputProbe.dll`. Full captures: `Exe/Room10Input-20261003/cycle-2/{direct,input}.json`, failed pair under `cycle-1/`. [Summary/hashes](summary.json), [remaining sites](remaining-sites.json).

Next bounded candidate by remaining-site count: Room36's four watcher200 sites001D/0048/0077/00A6. Inspect incoming source gates and use actual clicks; they are candidates, not newly validated routes. Keep completed Room10/23/25/34, sphere and Sharpening evidence immutable, supported labelled spell setup when required, the two-cycle bound, every estimate/barrier and the running App untouched.
