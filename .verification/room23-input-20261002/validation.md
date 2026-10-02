# Room23 watcher input — 2 October 2026

Continues Room25 commit `6c39f96`. **Six more previously unvalidated sites complete through actual walking input.** Both shepherd scenes and all four terrain transitions reach their source end points. No engine fix, spell setup, state edit or observer expansion was needed.

## Working route

A fresh seed0/default-preview session visits Room23, an explicitly assumed catalog arrival. After normal settling Bobbin is at396,134 in box0. Use the following room-coordinate clicks, waiting for each complete scene before continuing:

| Input derived from loaded geometry | Observed source execution | New site |
| --- | --- | --- |
| (121,121), box3 left endpoint | Watcher201 +000D chains202 when x<125;202 ends01BD | local202 +0000 |
| (293,129), box2 centroid | Watcher213 +0022 chains214 when x>242;214 ends018A | local214 +0000 |
| (216,94), box6 centroid | Watcher200 enters0020, ends004D; scene leaves ego in box8 | local200 +0020 |
| (348,116), box9 centroid | Watcher200 enters0084, ends00A8; scene leaves ego in box11 | local200 +0084 |
| (351,112), box10 centroid | Watcher200 enters00B3, ends00EE; scene leaves ego in box8 | local200 +00B3 |
| (218,95), box7 centroid | Watcher200 enters0058, ends0079; scene leaves ego in box5 | local200 +0058 |

The old-v3 comparisons evaluate the following operand against the variable (`b OP a`); thus201's `isGreater x,125` continues into202 for x<125, and213's `isLess x,242` continues into214 for x>242. These are source gates, not invented prerequisites. The terrain scripts perform their own placements, costumes and walks. The probe only sends clicks and advances ordinary frames.

The completed run takes1393frames, with six begins/six ends and139 recorded body events from200/202/214, all Executed and Conditional. It stays in23 and finishes at180,120,box5. Each stage has a3000-frame cap and requires the exact source end, expected room, cumulative balanced scopes, editable state, no moving ego/camera or message, and32 quiet frames. Failure prevents subsequent input. Original spell notes/reverse flags are exact; any learning occurs in loaded source, not status edits.

These six sites are watcher starts without direct object-verb triggers. No private script call, forced placement or artificial direct control was introduced, and no equivalence to such a control is claimed. The evidence is accepted public `Click` dispatch plus the actual watcher chains, cutscene starts, source ends and settling. This is Core input evidence, not WPF/physical mouse or native full-game certainty.

## Two bounded cycles

1. `cycle-1`: box3's centroid181,123 is walkable but does not cross x<125. It produces zero cutscene starts; the expected202 end remains absent at3041 total frames. The failed packet is retained unchanged. This disproves the centroid route, not the source watcher.
2. `cycle-2`: choose box3's decoded left endpoint121,121, which meets the already decoded gate. All six scenes complete. No further diagnostic run or production implementation is needed.

`prepare.py` preserves the first-cycle harness generation; the checked-in final Program.cs includes the endpoint correction. Do not run the generator over the existing directory. It refuses to overwrite it.

## Coverage and validation

Audited union **182→188/235 (80.00%), +6**, with47 sites remaining. Together with Room25 this session adds12 legitimate sites. Fresh single-action coverage remains **169/235 (71.91%)**,66 gaps/49 owners/335 catalog starts unchanged. The union is recomputed from completed literal previews, the two audited catalog sequences, Room34, Room25 and this Room23 input capture. No partial run contributes and no literal gap is removed.

Fresh Core-only probe build/run and `check.py` pass. The checker verifies input points from geometry, source gates/chain dispatch, all12 exact boundary strings,139 Conditional/Executed body events, source fingerprints, preserved failed packet, draft notes/reverse flags and all57 protected file hashes/unstaged status. Core/catalog unchanged. Historical captures untouched. Main Release/Core662/broad audits remain dated; documentation/evidence work does not justify touching the running App. No App launch/close/interaction, push, UI, audio or visual claim.

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.

```powershell
rtk proxy dotnet run --project .verification/room23-input-20261002/Room23InputProbe/Room23InputProbe.csproj -c Release -- Games/loom Exe/<fresh-directory>
rtk proxy python .verification/room23-input-20261002/check.py
```

Evidence: `Exe/Room23Input-20261002/cycle-2/input.json`; retained failure:`cycle-1/input.json`. Artifact:`Exe/Room23InputProbe/Release/net10.0/Room23InputProbe.dll`. [Summary/hashes](summary.json), [remaining sites](remaining-sites.json).

Next candidate by coverage value: Room10's four remaining sites. Loaded source identifies watcher200 box3/box7 transitions, local213 and object296/3B; inspect their incoming source conditions and prioritize input. For any spell test, use supported staff/notes/learned edits as explicit exploration choices, preserve source notes/reverse flags, and compare direct responses with accepted keyboard dispatch plus completion. No source-only candidate qualifies a site. Keep the two-cycle bound, all protected/history constraints and running App untouched.
