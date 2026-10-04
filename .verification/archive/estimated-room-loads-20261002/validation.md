# Continue executed room loads without discarding estimates — 2 October 2026

Continues `5547ef9`. **Two tested cases improve:** the unchanged saved spell-input capture recipe completes script090, and the Room18 sphere Look action completes script065. This is a general session execution correction, with no room or cutscene exception. It does not establish how many other manual-input histories benefit.

## Authorization and policy

The user explicitly requested fixing/removing the overreaching uncertainty guard so triggered cutscenes can continue generally. This supersedes the earlier prohibition on accepting conditional room loads in the historical Room41 packet. All other constraints remain: preserve estimates and historical packets, raw4 recipe unchanged, no invented prerequisites/state, no wait/stop bypass, bounded diagnostics, protected files untouched/unstaged, no App launch/close/interaction or push.

The old guard treated an executed instruction on an estimated path as though it were only a decoded possibility. The new common predicate requires `RoomLoaded`, `Executed=true`, and `Reached` or `Conditional`. The existing indexed destination checks remain. Unknown operands/destinations, unexecuted branches, scheduler limits and source waits/stops still apply. A global script resumes after the load; room-owned scripts terminate on leaving the room as before.

No certainty is promoted. Exit/entry hooks and room scripts inherit an estimated load, action text states estimated execution, and the snapshot remains nonfaithful. Rendering preserves the values selected by estimated execution using `ArtworkStateIds()` as well as reached state writes; it does not add estimated writes to `StatesSetByCode`. Otherwise the newly entered Room43 could wrongly borrow a later open-cage overlay again. Actual entry source selects645=0; no image/state is forced.

## Measured outcomes

| Test | Before | After |
| --- | --- | --- |
| Unchanged `room41-raw4-click` | Room41, 15 starts/14 ends, 7659 frames | Room43, 15/15, 5886 frames; executed conditional090 end `+0730` |
| Room18 object370 Look (`0x36`, sphere) | 218 frames, 1 start/0 ends | 229 frames, 1/1; script065 end `+0302` |
| Other literal roots | Prior cage-fix baseline | All362 whole results exact |
| Four established memberships / two sequences | Prior cage-fix baseline | Whole JSON exact |

Raw4 is a saved slot4/Twisting input recipe with an actual204 Escape, not a walking path or the user's manual response18. Its entire old3800-event stream is an exact prefix of the new4153-event stream. The exact same input recipe now enters42 at4660,43 at4741,60 at4973 and43 at5224. Both the090 end and transition remain Conditional. The source recipe and historical output are unchanged; new results are separate.

The extra sphere case executes script065's estimated `loadRoom` at `+02E1`, re-entering18 before its end. The old guard blocked even this same-room reload. It is not currently a Look entry in Play Cutscene; no new menu item or literal-site promotion is claimed. Catalog coverage remains the stored168/235 until separately reviewed and regenerated.

## Separate response18 experiment and its limit

`response18.json` derives a separate case from the same source-input setup, replaces only the final cast with slot9 reverse keyboard notes, and removes the204 Escape so the conversation could run in full. This is **not a capture of the user's actual starting state**. One bounded run ends in41 with16/16 scopes and never dispatches633/4B. The captured pre-cast status explains why: only four notes are available; slot9 is unlearned and its sequence is6,6,6,5. No prerequisite or note count was edited to make it work. This result does not refute the user's correctly triggered conversation or test its Room42 continuation. Exact user input/setup remains to be established; do not substitute this failed setup or raw4 for it.

The user confirms **pressed note keys** for the manual18 attempt. Do not ask that again or suggest an unverified Apply/Play Draft control. The missing evidence is their starting state, not their input method. The user additionally reports the earlier cage artwork correction fixed the Elders' window and missing scythe. Record that as user evidence; this session did not interact with the UI to verify those observations.

## Verification

* Core662 pass, including five new cases for global/local estimated loads, unknown/missing destinations and an unresolved branch. Estimated entry effects, retained labels and source-closed Room43 composition are asserted. The protected unrelated object-look audit remains excluded as in prior runs.
* The initial red tests exposed the old transition stop. Additional fixture failures came from assuming absent globals rather than the session's initialized zero values; the final fixture explicitly removes its synthetic variable. Intermediate logs are retained.
* All363 literal roots rerun: only the sphere result changes, as explained above. Source hashes exact; four memberships and two sequences exact. Blockers reports0 sites.
* All76 room BMPs byte-exact to the cage-fixed baseline and zero remaining gaps. All15 direct cage-control files (including two captures) byte-exact. The unchanged direct captures remain1729/1723 frames; they do not prove a native-input setup.
* Normal main Release from `D:\Documents\Personal\Code Projects\SCUMMexplorer`, `rtk proxy dotnet build SCUMMexplorer.slnx -c Release`: zero warnings/errors. Artifact: `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`. A read-only process guard found no App before the build; none was launched or closed. No WPF/physical-input/listening check or push.
* All57 protected files hash-exact/unstaged. Original game data, catalog and prior verification records unchanged. Full prior observer/numeric/native/camera captures remain dated, not rerun or claimed exact under the new execution policy. Their independent provenance/call-phase/sequence-gap barriers are unchanged.

Evidence is under `Exe/EstimatedRoomLoads-20261002/`: `native-1`, `audits`, `room-audit`, `cage-controls`, `response18-1`, `core-final.trx`, and `build.txt`. [Summary](summary.json) is generated by [analyze.py](analyze.py); its first attempt stopped on the UTF-16 PowerShell build log, then passed with explicit encoding handling. No capture was changed to satisfy it.

## Reproduction

Run from the repository root with fresh output directories; all probes are Core-only and need no App interaction:

```powershell
rtk proxy dotnet run --project .verification/archive/room41-certainty-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom Exe/Room41RouteSession-20261002/raw4-only.json Exe/<fresh-native-output>
rtk proxy dotnet run --project .verification/archive/room41-certainty-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/archive/estimated-room-loads-20261002/response18.json Exe/<fresh-response18-output>
rtk proxy C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 .verification/archive/estimated-room-loads-20261002/analyze.py Exe/EstimatedRoomLoads-20261002
```

Next bounded task: reproduce the user's actual manual Sharpening-reverse input/setup without fabricated prerequisites, then check its source204-to090 continuation. Do not restart general observer expansion or re-diagnose the now-corrected certainty stop. Any physical app verification still requires authorization; leave the user's app untouched.
