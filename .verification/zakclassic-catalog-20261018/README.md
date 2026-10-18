# ZakClassic inventory catalog coverage — 18 October 2026

| Field | Result |
|---|---|
| Family / fixture | ZakClassic; `D:/Juegos/Lucas/zak ega en` |
| Scope | Remaining admin inventory catalog, especially letter 139; exclusive shared Core source-flow writer |
| Core commits | `b09d6bd`, `59173d0`, `0c54de8`; parent owns registry and matrices |
| Catalog | **76 named items**: 72 code-bearing native indexed Pickupable objects plus four independently traced unflagged items (31, 53, 68, 71) |
| Native contract | 80 global slots; source room retained; explicit grants/transfer/Undo, capacity and stale guards unchanged |
| Comparison | Native game data and [ScummVM script_v2.cpp](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/script_v2.cpp), read 18 October 2026; no ScummVM execution |

## What changed

The earlier 82-entry Zak catalog omitted bird nest 53 and incorrectly admitted seven shop display objects 183–189. The generic source analyzer now carries the caller's variable values and ActiveObject1 through literal script and native special-handler calls, follows known comparison/state branches, and keeps both finite pickup identities at unknown branch joins. It uses native unsigned RHS-versus-LHS relational semantics, the raw destination / indirect-read distinction for words 14–16, backing-word invalidation for bit writes, helper/range/indirect write invalidation, and the stop-only meaning of doSentence FE. No item, room, script or instruction offset is special-cased in production.

The first implementation's join intersection erased one of two possible item IDs. Independent review supplied a failing two-target regression; disjoint states fix it. Review also caught signed/reversed comparison and alias/bit-write discrepancies, which now have synthetic regressions matched to the existing native executor tests. These failed intermediate models are not acceptance evidence.

## Complete source census and classifications

**1,478 contexts**: global resources, room entry/exit and every native object-verb alias. **1,475 complete**, 61 pickup occurrences, one positive Pickupable writer and no negative writer. No start/chain script target is indirect. The three incomplete global resources **150, 151, 153** have no literal start/chain caller anywhere in that complete caller corpus, are not native fixed boot/input/sentence/entry hooks (1/4/2/5), and contain no decoded pickup/flag write. Their malformed targets remain reported; their apparent table/text purpose is not asserted. They do not conceal an invoked inventory path.

| Previously unresolved site or header | Own-copy source classification |
|---|---|
| Script 119 +0053 pickup ActiveObject1 | Parameterized pickup/transfer template. Ordinary indexed items and native special callers establish its targets; Global 20's transfer path applies to an already-held item. |
| Script 24 +0242 pickup Var2D | Shop acquisition: native item purchase contexts; the seven display headers do not reach this pickup as their own inventory identity. |
| Script 24 +0267 pickup Var42 | Native bounded toolkit loop 35–37, already indexed Pickupable items. |
| Script 55 +0052/+0054 | Only flag writer and pickup: all seven actual callers are Object 71's verbs 1/3/4/5/11/12/14; identity 71 is preserved. |
| Room 2 / handler 117 FD +0022 | Caller item 25 (crayons); 117 is a sensor/resident handler, not the sentence item. |
| Room 13 / handler 276 FD +003D | Caller item 54 (squirrel nest). |
| Room 13 / handler 274 FD +0013 | Caller 53 Use → handler 275 FD → handler 274 FD, preserving 53 across both calls. New catalog item, retained Room 13. |
| Room 17 / handler 358 FD +0019 | Caller item 59 (flagpole). |
| Room 41 / handler 607 FD +0013 | Caller item 78 (glowing object). |
| Headers 298/307/632 | Resident calls to Script 119 retain actual sentence items 56/55/80; the handler headers themselves are not pickup identities. |
| Header 139 | **Scene letter/container, not a missing inventory item.** Room 3 normal Read 12 and Pickup 14 start +001F with literal pickup **27** (fan club card), hide letter ActiveObject1 and reveal mailbox state 138. Verb 2 hides the letter and reveals 28. Its FD handler coordinates Give; it does not pick up 139. Native tests execute both normal verbs, acquire card 27 in retained Room 3 and never add 139. |
| Shop headers 183–188 | Native Buy prefixes set Var2E to actual items 33/56/81/55/26/21. Script 24's immutable Unpickupable branch changes the actual item's owner; the display header is never copied into inventory. |
| Shop header 189 | Native Lotto Buy prefix rewrites ActiveObject1 to item 42 before Script 24. Actual ticket 42 is picked up; header 189 is not. |

The known Script 55 disjoint-state bound remains **explicit** (Classic +02D6, DAT counter VarE6, with unknown ActiveObject1 or actual caller 71). It is after the sole flag/pickup pair. A separate structural regression traverses **all later calls and both sides of their branches**: G55, G3, G19, G44, O7/183/FD, O30/492/TurnOn+FD, O33/520/Use and O33/521/FD. None has another pickup or Pickupable writer. Later owner transfers of DAT item 72 cannot introduce an unknown identity: 72 has its own Room 30 Pickup verb and is already indexed/cataloged. Thus this is a bounded recording/playback counter analysis, not unresolved inventory coverage. No diagnostic is deleted or presented as an unbounded proof.

## Commands and validation

Cwd `.worktrees/zakclassic-catalog`, all commands through RTK. Probe arguments `catalog` print the source roster/notes and timings; `census` prints context/flag counts and decode issues. Default mode dumps decoded instructions to local ignored logs.

```powershell
rtk proxy dotnet run --project .verification/zakclassic-catalog-20261018/Probe/Probe.csproj -p:NuGetAudit=false --verbosity quiet -- "zak ega en" catalog
rtk proxy dotnet run --project .verification/zakclassic-catalog-20261018/Probe/Probe.csproj --no-build -- "zak ega en" census
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~ScummZakClassicInventoryCatalogTests --verbosity minimal
```

Core/test builds have zero warnings/errors. Initial four-family transaction regression: 18 passed, zero failed (57s); conservative-write followup: 18 passed, zero failed (54s). Final source model: **40 passed** (20 analyzer +20 native v2 executor cases), zero failed (113ms). Final LOOM EGA/CD status guards: **20 passed**, zero failed (25s). Classic's first full native/source set: **15 passed**, zero failed (41s); after 72+4 roster/fixed-hook/all-retained-entrypoint assertions, **15 passed**, zero failed (52s). The separately added DAT closure proof plus complete census pass **2/2**, zero failed (8s); one overlaps the earlier 15, establishing all **16** final Classic cases. Parent's integrated full suite is the final single-run result. These counts overlap; they are not added into a unique-test total.

The Classic tests exercise both native letter verbs, the actual nest/tool relay, each of seven native shop branches, both retained card/nest Use/Read verbs away from their source rooms, every catalog grant and native verb entrypoint with exact Undo, the complete native-index roster plus explicit additions, and all script/decode/caller classifications. Grants and VisitRoom preparation are explicit exploration/admin actions, not earned puzzle progress.

Probe setup initially used one too many parent segments in ProjectReference and failed compilation; corrected before use. A census-output block was initially placed before enumeration and printed zero contexts; corrected before recording the 1,478/1,475 result. No game bytes, pictures, saves, ROMs or local output logs are committed.

## Remaining limitations and parent ownership

This establishes the admin item roster for the supplied Classic fixture. It does not claim a complete game walkthrough, original-engine visual/timing/listening parity, or broader unavailable editions. Parent owns window checks, support-level decisions, shared documentation/matrix generation, final master merge/build/full-suite protocol and the clean checkpoint. No other-family feature code changed; LOOM regression stayed green. No ScummVM process, Games junction, user profile mutation, force, stash or worktree removal.

## Measured named roster

Names are the edition's own static object headers; duplicate names retain separate native identities. Source-driven later renames remain game state, not invented catalog labels.

| Object | Retained room | Index state | Native header name |
|---:|---:|---:|---|
| 14 | 1 | 1 | Sushi in fish bowl |
| 15 | 1 | 1 | phone bill |
| 16 | 1 | 1 | kazoo |
| 17 | 1 | 1 | torn wallpaper |
| 18 | 1 | 3 | CashCard |
| 19 | 1 | 3 | receiver |
| 20 | 2 | 1 | power cord |
| 21 | 2 | 1 | remote control |
| 22 | 2 | 1 | egg |
| 23 | 2 | 3 | bread crumbs |
| 24 | 2 | 1 | small key |
| 25 | 2 | 1 | box of crayons |
| 26 | 2 | 1 | butter knife |
| 27 | 3 | 1 | fan club card |
| 29 | 3 | 1 | stale bread |
| 30 | 4 | 1 | application |
| 31 | 5 | 0 | bobby pin sign |
| 32 | 6 | 1 | CashCard |
| 33 | 6 | 1 | crystal shard |
| 34 | 7 | 5 | tool kit |
| 35 | 7 | 1 | monkey wrench |
| 36 | 7 | 1 | duct tape |
| 37 | 7 | 1 | rope |
| 38 | 7 | 1 | wire cutters |
| 39 | 7 | 1 | wet suit |
| 40 | 7 | 1 | nose glasses |
| 41 | 7 | 1 | guitar |
| 42 | 7 | 1 | Lotto ticket |
| 43 | 7 | 1 | hat |
| 44 | 7 | 1 | golf club |
| 45 | 8 | 3 | book |
| 46 | 8 | 3 | whiskey |
| 47 | 11 | 1 | toilet paper |
| 48 | 11 | 1 | cushion |
| 49 | 11 | 11 | lighter |
| 50 | 11 | 3 | oxygen tank |
| 51 | 11 | 3 | peanuts |
| 52 | 12 | 1 | tree branch |
| 53 | 13 | 0 | abandoned bird nest |
| 54 | 13 | 1 | squirrel nest |
| 55 | 14 | 1 | blue crystal |
| 56 | 14 | 1 | crystal shard |
| 57 | 14 | 3 | ankh |
| 58 | 15 | 1 | white crystal |
| 59 | 17 | 1 | flagpole |
| 60 | 18 | 1 | jail key |
| 61 | 27 | 3 | token |
| 62 | 27 | 3 | token |
| 63 | 27 | 3 | token |
| 64 | 28 | 1 | burnt fuse |
| 65 | 26 | 3 | helmet |
| 66 | 26 | 3 | helmet |
| 67 | 28 | 9 | vinyl tape |
| 68 | 28 | 0 | broom alien |
| 69 | 28 | 1 | flashlight |
| 70 | 28 | 1 | ladder |
| 71 | 30 | 0 | boom box |
| 72 | 30 | 1 | Digital Audio Tape |
| 73 | 30 | 1 | fuse |
| 74 | 30 | 1 | CashCards |
| 75 | 30 | 3 | CashCard |
| 76 | 35 | 1 | golden key |
| 77 | 40 | 1 | parachute |
| 78 | 41 | 3 | glowing object |
| 79 | 43 | 1 | scroll |
| 80 | 43 | 1 | candelabra |
| 81 | 44 | 3 | yellow crystal |
| 82 | 46 | 3 | ticket |
| 83 | 46 | 3 | ticket |
| 429 | 25 | 11 | yellow crystal |
| 431 | 25 | 11 | blue crystal |
| 471 | 28 | 3 | fuse |
| 535 | 33 | 3 | ladder |
| 536 | 33 | 3 | ladder |
| 537 | 33 | 3 | ladder |
| 544 | 35 | 1 | large key |
