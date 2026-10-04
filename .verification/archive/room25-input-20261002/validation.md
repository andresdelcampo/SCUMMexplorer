# Room25 walking and lamb input — 2 October 2026

Continues `89a2211`. **Six previously unvalidated sites now have completed game-input evidence.** No production correction was needed. This task tests accepted input and subsequent execution; direct playback is only a comparison.

## Working route

Start with the default seed0 preview boot and a catalog visit to Room25, both explicitly assumed exploration choices. Wait for normal editing availability, stationary ego/camera, no message and 32 quiet frames. No staff, note, learned-spell, flag, class or actor edits are needed. Source teaching during the conversations remains intact.

| Actual input | Source dispatch and completion | Newly qualified site |
| --- | --- | --- |
| Click (231,119), the decoded box4 centroid | Watcher200 starts at0058, writes D92=1, ends007E | Room25 local200 +0058 |
| Click (140,122), decoded box3 centroid | Watcher200 +0046 starts069; full introduction visits55/25/24/25, ends07ED | Global069 +0000 |
| Double-click the lamb at (200,124) | 007 +002E dispatches447/36; object +0053 starts204(0), ends03C5 | Room25 local204 +0000 |
| Double-click the lamb again | 007 +002E dispatches447/36; object +0061 starts205, ends00B0 | Room25 local205 +0000 |
| Double-click the lamb once more | 007 +002E dispatches447/36; object +006C starts206, ends010C | Room25 local206 +0000 |
| Click the exit at (36,56) | 006 +015B dispatches446/37; object +0014 starts208, ends0060 and loads24 at0066 | Room25 local208 +0000 |

Wait for each complete scene before the next action. Every stage has a 3000-frame limit and requires its exact source end, balanced cumulative scopes, expected room and 32 settled frames. Failure prevents subsequent input. No Escape, wait bypass or direct call occurs in the input branch. Walk points come from decoded box geometry; object points come from decoded bounds. Arrival ego is box3; actual walking reaches box4 and returns to box3. The last load is executed Conditional and remains labelled estimated.

The control uses the same initial two walking actions, followed by direct447/36 three times and direct446/37. It does not independently validate a direct start for either watcher. Both branches complete six scopes with identical12 boundary strings; all208 recorded events belonging to200/069/204/205/206/208 are exactly equal, including certainty, arguments and execution flags. All208 are Executed. Direct2634frames, input2650frames; the extra input time is not normalized away. Final room24 and D92/D93/DA6/DA7/DA8/DBB=1 in both. Original draft notes and reverse flags remain unchanged.

This compares the recorded scene-body stream, not every scheduler/native effect or every frame. Both final snapshots retain uncertainty. Core `Click` is the same public input API used by the App, but this is not WPF, physical mouse, visual or audio verification.

## Coverage and preservation

The audited union rises **176→182/235 (77.45%), +6**, with53 sites remaining. It is the explicit union of completed literal previews, the two previously audited sequences, Room34 input evidence and this completed Room25 input route. The separate fresh single-action measure remains **169/235 (71.91%)**,66 gaps/49 owners;335 catalog starts unchanged. No failed literal root is erased and no catalog recipe is promoted from the longer route.

`check.py` verifies exact source inventory/hash, packet source-file hashes, each input dispatch and subsequent object call, all stage outcomes,208 exact body events, unchanged notes/reverse flags, computed union and all57 protected hashes/unstaged status. Core and catalog unchanged. All historical captures remain in place; no previous evidence file was regenerated. Failed tooling attempts were a missing harness namespace and an initial checker hash against the older disassembly (six historical `left`/`height` label differences); neither required an engine change or extra route cycle. The checker uses the authoritative native-code-1 source now. One successful bounded route cycle, no execution blocker and no observer expansion.

Fresh Core-only probe build/run and checker pass. Core662, broad execution/render/observer audits and main Release remain dated baselines; this evidence/documentation task does not require rebuilding or interacting with the running App. No App launch, close or interaction; no push.

## Reproduce

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.

```powershell
rtk proxy dotnet run --project .verification/archive/room25-input-20261002/Room25InputProbe/Room25InputProbe.csproj -c Release -- Games/loom Exe/<fresh-directory>
rtk proxy python .verification/archive/room25-input-20261002/check.py
```

The checker addresses preserved `Exe/Room25Input-20261002/cycle-1/{direct,input}.json`; use a fresh directory for reproduction and review separately. Artifact: `Exe/Room25InputProbe/Release/net10.0/Room25InputProbe.dll`. [Summary and hashes](summary.json), [remaining-site ledger](remaining-sites.json).

Next candidate by practical coverage is Room23's six-site cluster. Inspect its source walk-box transitions and actual clicks before spell tests. Keep Room25, Room34, sphere and Sharpening complete; preserve the two-cycle diagnostic bound and every existing constraint.
