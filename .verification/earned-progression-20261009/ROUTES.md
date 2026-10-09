# Remaining lesson routes — 9 October 2026

The English Roland reference now earns slots **3, 7 and 11** from their actual lesson routes. Seven unique new lesson checks cover slot 3 in English/Spanish EGA and CD/VGA, and slots 7/11 in English EGA and CD/VGA. A separate English Script 106 control confirms the corrected guard direction. All **37 recorded assertions in eight packets** pass; the offline reader additionally checks packed masks, actual helper arguments, source-copy hashes, packet hashes and balanced scopes. [Reduced results](lessons-routes-summary.json).

These are bounded source routes with explicit catalog starting points, seeded boot and synthetic Core input. They are not a sequential original-game playthrough, WPF event delivery, physical input, native pictures or listening. No production mismatch or engine change was demonstrated. No Game Status, note-floor, learned-mask, story-bit, costume, placement or certainty edits are used.

## Working routes and corrected guards

| Slot | Actual route | Source interpretation |
| --- | --- | --- |
| 3 | Real staff pickup → Room 04 owlet Look → wolves/owlet local 202 → four hole Looks → Script 25(3) | Local 202 fills the fourth hole and clears observation class 3. Local 200's first three occupied observations increment its counter; the fourth plays/counts the full draft. Local 201 reads **camera x, var 2**, not pointer x. Its playback gate is separate from the final lesson call. |
| 7 | Real staff pickup → Room 23 → ordinary ground click at an own-geometry corner below local 201's x threshold → local 202 → Script 25(7) | The entry's `equalZero` guard requires the Script 106 completion bit **clear** (D71/D72/012). Actual Script 106 sets it and suppresses the early monitors; it is not a prerequisite to earn this lesson. The old-v3 relational operands compare the following constant against the variable: local 201 fires below x=125. |
| 11 | Real staff pickup → Room 24's actual pasture exit → Room 25 → box 4 then box 3 through ordinary clicks → Script 69 introduction → lamb Look/local 204 → Script 25(11) | The pasture-completed bit DBA/DBB/05B must still be **clear**. Local 208 writes it on completion/departure. The fresh independent lamb request lacked the walk/introduction history; it did not demonstrate a missing lesson or a need to supply that bit. |

Each previously unlearned packed word changes only `0x2000`; loaded notes, reverse capability and used/last-note flags remain intact. The Script 106 control runs the actual own-source Room 66 response, observes its completed-bit assignment, then tries the Room 23 approach. This direct response is a **control of source effects**, not earned Transcend casting. It earns no lesson credit.

English/Spanish slot-3 packets retain six scopes each; CD slot 3 retains six. English/CD slots 7 and 11 retain their own source paths and balanced boundaries. CD Room 23 box 3 has different geometry: the initial EGA box-index-based requests stop at x=180 and correctly do not teach. The final helper derives an enabled corner below the monitor threshold from each edition's walk data. No shared engine or edition-specific workaround was added.

## Evidence reuse and preservation

The prior 33 learning packets, level-four input routes, nine five/six/eight-note helper controls, and the historical Room 23/25 input records were read before execution. Existing successful routes were not rerun as a broad audit. These seven lesson checks bring the independent unique total to **40**: 14 English EGA, 12 Spanish EGA and 14 CD/VGA newly learned slots, with boot/pickup-initialized slots 0/15 still excluded.

The failed `out-slot3-first/` root lookup, `out-slot3-pointer/` incomplete owl sequence, and `out-slot7-vga/` / `out-slot7-vga-corner/` wrong geometry controls remain ignored and preserved. They are harness/route limits, not production defects. Earlier guard descriptions in README are corrected by this source review, rather than promoted into state injection.

Compatibility is selective: Spanish slot 3 checks its own bit bank/text; CD checks all three source routes and its different geometry/format. Existing plain/language/v1.2 analysis remains applicable; no runtime change requests their broad replay pipelines. Italian remains initially unreviewed. Native comparisons and audio remain manual. The original four-file uncommitted actor inventory is preserved byte-for-byte and excluded from these commits.

## Reproduction

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`. Private build artifact: `.verification/earned-progression-20261009/out-routes-build/bin/debug/LoomEarnedRoutes.exe`; private `DOTNET_CLI_HOME` lives under that build directory. Core-only execution does not launch App/native UI or use the user's profile. All shell and child processes use RTK.

```powershell
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/earned-progression-20261009/run-routes.ps1 -Route slot3 -Edition ega-en -OutputName out-reproduce-slot3
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/earned-progression-20261009/run-routes.ps1 -Route slot7 -Edition vga -OutputName out-reproduce-slot7
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/earned-progression-20261009/run-routes.ps1 -Route slot11 -Edition ega-en -OutputName out-reproduce-slot11
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/earned-progression-20261009/summarize-routes.ps1
```

The first three commands are optional reproduction in fresh output folders; the final command revalidates preserved packets without repeating routes. The final helper builds cleanly with zero warnings/errors; no full solution/replay audit is needed for this verification-only work.
