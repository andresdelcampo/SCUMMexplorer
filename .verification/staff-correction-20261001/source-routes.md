# Continued source routes and retained gaps (1 October 2026)

Superseding source-input research: [native progression validation](../source-progression-20261001/validation.md) establishes delayed632/633 access and native13/325 learning after052. Early aim misses below describe their sampled state, not permanent inaccessibility. It also corrects the old20 claim:369/371 **do enter20 transiently**, then207/058 return18 before settled response input. The original guarded-response conclusion remains unchanged.

Bounded investigation from the prior handoff. No recipe/catalog promotion or executor certainty change. Probes invoke only visits, direct source verbs, native clicks/double clicks, decoded native draft notes and frames. They use default `preserveStaffLevel:false`, no status edits, injected positions/classes/flags, forced loads or forced scene ends.

Authoritative disassembly: `Exe/VerificationSession-20261001/native-code-1/code.tsv`, SHA256 `0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569`. Full packets remain ignored in `Exe/StaffCorrectionSession-20261001/routes-1/` and `routes-3/`. [First routes/digests](routes-1-summary.json), [additional routes/digests](routes-3-summary.json) record the last actual snapshot for every stage. Native hit selection remains consistent with ScummVM [findObject](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/object.cpp), which excludes untouchable objects.

## Room41 native draft input

Room09/271use still acquires the staff through source. Original633-centre input misses because633 is untouchable. New tests distinguish alternative objects:

- 632 centre still leaves var067=0; slot4/9 reach013's aim-first route. Native double click632 does not establish its direct-look effects in this state.
- 634 centre **sets var067=634**, and008 +0x0023 runs634/39. Both633/634 source39 handlers set var068=635 (sword close-up), but634 has no explicit41/4B response. This proves a native aim target, not active633 or a successful cast.
- Slot4 then reaches010→011's high-note refusal at raw3. Slot9 also refuses high notes. No022→026/633response/204 success occurs. Native look632 then634 has the same outcome.
- Direct egg270/3A after source staff acquisition leaves the level3 in this setup. Separate direct13/325look learns slot4 through025 (+0x0031), retains raw3, and still cannot remove the high-note refusal. Learning's native accessibility remains unproven; do not turn either attempt into a validated progression prerequisite.

Next narrow dependencies: source level4/6 progression and the class/state phase exposing633. Local200 phase8 +0x02BE/+0x02C4 waits on D66/DB4; phase15 +0x05A6/+0x05AC repeats these gates. Local212 responds to ego box9/10 transitions through032. No gates or boxes are forced.

## Late096 actor certainty

Fresh44/653look completes095 in45; source45/656look holds staff,655use/092 returns43, then639use reaches096. Still four begins/three ends, final43. Actor4 reaches(110,93), Moving=0/IgnoreBoxes=true, but retains `actor 4's putActor at +0x003A follows a seeded pick`. The actor is **not still walking**: wait +0x0046 completed; load45 +0x0049 is only possible under inherited certainty. Literal placement and completed movement do not establish the branch that began it.

095 writes D7B/DA3/D97/D96 at +0x0720/+0x0725/+0x072A/+0x072F, then loads45 +0x078C. Re-establish the source route/certainty before changing execution. Fresh43 use still goes44/no096; staff without095 goes45/no096. No known-flag, unfreeze or actor injection.

## Four fresh bounded failures

Fresh all363 audit reproduces each result exactly:

| Start | Fresh evidence | Remaining dependency |
| --- | --- | --- |
|18/370look36|218 frames, rooms18/19,065 begins/no end; +0x0214 makes return18 +0x02E1 only possible after seeded pick|Certainty of065 return; no inserted load/end|
|20/390response48|78 frames,206 begins/no end; getActorX +0x002D cannot read ego position|Incoming route preserving actor placement; no literal arrival in fresh20|
|20/391response48|Same independent failure as390|Retain both owners/sites|
|38/610look36|3000 frames, rooms34/37/38/68,087 begins/no end|Source actor setup from086; established longer recipe remains distinct|

Fresh18/369look starts062 and18/371look starts056, both20 arrivals. These packets settle in18 with five balanced scopes. Their final-stage snapshots did not record transient20; the later per-frame probe confirms that entry and207/058's return18. Guarded probes record prerequisiteNotReached20 and **do not call390/391**. These attempts do not establish completed20 responses. Transitions use056/062/099; placement continues through dynamic/local scripts rather than the literal arrival index.

`routes-2` initially tried390 after a merely possible20 arrival and caught the wrong-room exception via shared failure policy; not a successful packet. `routes-3` records the prerequisite guard and runs all four combinations plus the unaffected wind-learning case. An early diagnostic accessed a nonexistent EstimatedVariables snapshot field; corrected to public fields, with no production consequence.

Catalog remains334 explorer starts,168/235 sites,50 outstanding owners,123 unresolved edges. No fresh gap/branch/site, replay membership or original-game input uncertainty is erased. [Current status/audit validation](validation.md) records the unchanged all-root/shared checks.

Reproduce with required RTK/bundled Python from the main checkout:

```powershell
rtk proxy python -X utf8 .verification/staff-correction-20261001/create_routes.py
rtk proxy dotnet run --project .verification/staff-correction-20261001/RouteProbe/RouteProbe.csproj -c Release -- Games/loom .verification/staff-correction-20261001/routes.json Exe/StaffCorrectionSession-20261001/routes-fresh
rtk proxy python -X utf8 .verification/staff-correction-20261001/create_more_routes.py
rtk proxy dotnet run --project .verification/staff-correction-20261001/RouteProbe/RouteProbe.csproj -c Release -- Games/loom .verification/staff-correction-20261001/more-routes.json Exe/StaffCorrectionSession-20261001/routes-more-fresh
```
