# Room34 watcher prerequisite chain: source barrier found — 3 October 2026

After `51aa4dd`. Two bounded cycles, **no coverage gain**, no production change. Audited **215/235 (91.49%)**, 20 remaining; fresh single-action **169/235 (71.91%)**, 66 gaps/49 owners/335 starts, unchanged and distinct.

## Cycle 1: Room36 box9 → 599 → 086 → 087 → Opening → exit609 → exit601 (`Exe/Room34Chain-20261003/cycle-1/input.json`)

Probe `Room34ChainProbe` (copy of the parked Room34 watcher probe, input mode only) first runs the verified Room36 watcher clicks box10→4→3→9 (all four 200 ends 00CA/006C/003D/009B), then the actual 599 click. This fixes the earlier failure: with box9 done, 599 is clickable and dispatches (Room36→37, 620 frames). The rest then completes with actual input: firewood click 109,102 → 086 end055C (frame 1486), Look 610 → 087 end09B9 (frame 3527; Room 38 settled), labelled staff/notes/learned setup, Opening 617/3A → Room38 local203 end00B2 (frame 3830), exit 609 → Room37 (frame 3895). Every stage balanced and settled.

It then **fails at exit 601 (Room37 → 36)**: the click dispatches object 601 verb 0x37 (cutscene +000B … endCutscene +004B, "Mejor no vuelvo allí vestido de esta manera."), no room load; 3000-frame ceiling at 6895 frames.

## Source gate (decoded, Core disassembler)

`Room 37 object 601 Verb 0x37`: `getActorCostume local0, 1; isNotEqual local0, 0x0067 → refusal body; else stopObjectCode`. Script 006 +015B starts verb 0x37 first; the refusal sets bit D64, which stops the walk, so verb 0x38 (`loadRoomWithEgo 599-side → room 36`) is never reached. The ego here has costume 1 (Look.Costume=1).

The only writer that gives actor 1 costume 0x67 is `Room 34 local 202` (+00DB `var7D = 0x67`, +00EF `actorOps 1 costume var7D`; +00BF sets var7D = 1, Room 34 object 557 verb 0x36 later sets 0x68). Script 017 only uses var77..79 (1/2/3, ego size); scripts 085/106 and Room 34 local 209 give 0x67 to other actors (3/4). So the handoff's "continuous exits 609→37, 601→36, 598→35, 587→34" route cannot work from Room 38/37: Room 34 can only be entered from 35 (and 33/46), and Room 36→35 needs 601 first. Treat that chain as source-blocked, not as an engine defect (not reviewed against ScummVM costume semantics beyond the decoded script).

## Cycle 2: start in catalog Room 34, Healing then box6→box8 (`cycle-2/input.json`)

Catalog arrival is an explicit exploration assumption. After status setup (staff, 6 notes, Healing slot 11 learned), the Healing key sequence 6,1,1,6 (forward 4D) never completes 209 (boundaries only Script 85 +0000/+05CE; 3251 frames). Trace: the click at 80,104 hit object **561**, whose verbs lack 0x4D ("object 561 has no verb 0x4D"), so the cast dispatched against 561, not 586. This is a probe targeting failure (586 is covered by 561 at the centre point), not a source or engine finding. Failed captures retained and excluded.

## Stop

Two cycles; stop this route. Next bounded experiment: from catalog Room 34 choose a click point that selects object 586 (or cast with the sentence object 586 as in the verified 561/44 sequences), require 209 end03C0 (opens box6), then box6→8 camera >464 watcher 200 +0035/end0056. Check the camera prerequisite in source first. This is a candidate; no recipe is proven. Room34 local207 +0079 remains separate (D9D/DC7). Room09 fix (match ForwardVerb OR ReverseVerb) still pending.

No status/class/story/actor injection beyond labelled supported staff/notes/learned edits. No App launch/close/interaction/rebuild or push. Dump scratch (`Dump/`) is a one-off disassembly helper. Core662/broad/main Release remain dated.

```
rtk proxy dotnet run --project .verification/room34-chain-20261003/Room34ChainProbe/Room34ChainProbe.csproj -c Release -- Games/loom Exe/<fresh-directory>
rtk proxy dotnet run --project .verification/room34-chain-20261003/Room34Cycle2Probe/Room34Cycle2Probe.csproj -c Release -- Games/loom Exe/<fresh-directory>
```
